using Microsoft.EntityFrameworkCore;
using sales_portal.core.Caching;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.core.Notifications;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class CustomerService : ICustomerService
{
    // The lookup list carries no offer data, so offers never invalidate it.
    private static readonly string[] LookupRegions = [CacheRegions.Customers];

    // The directory list shows offer counts and totals, so offer writes invalidate it too.
    private static readonly string[] DirectoryRegions = [CacheRegions.Customers, CacheRegions.Offers];

    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Offer> _offers;
    private readonly IReferenceDataCache _cache;
    private readonly ICurrentActor? _actor;
    private readonly INotificationPublisher? _publisher;

    public CustomerService(
        IRepository<Customer> customers,
        IRepository<Offer> offers,
        IReferenceDataCache? cache = null,
        ICurrentActor? actor = null,
        INotificationPublisher? publisher = null)
    {
        _customers = customers;
        _offers = offers;
        _cache = cache ?? NoReferenceDataCache.Instance;
        _actor = actor;
        _publisher = publisher;
    }

    public async Task<PaginatedResultDto<CustomerLookupItemDto>> LookupAsync(
        LookupQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var all = await LoadLookupListAsync();
        var matches = InMemoryQuery.Search(
            all,
            query.Search,
            c => c.CustomerCode,
            c => c.Name,
            c => c.NameAr);

        return InMemoryQuery.Page(matches, page, pageSize);
    }

    public async Task<List<CustomerListItemDto>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var all = await LoadDirectoryListAsync();
        return InMemoryQuery.Search(all, search, c => c.CustomerCode, c => c.Name, c => c.NameAr)
            .ToList();
    }

    public async Task<PaginatedResultDto<CustomerListItemDto>> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var all = await LoadDirectoryListAsync();
        var matches = InMemoryQuery.Search(
            all,
            search,
            c => c.CustomerCode,
            c => c.Name,
            c => c.NameAr);

        return InMemoryQuery.Page(matches, page, pageSize);
    }

    // Every customer, ordered by the customer code that is shown (never by the GUID).
    private Task<IReadOnlyList<CustomerLookupItemDto>> LoadLookupListAsync()
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<CustomerLookupItemDto>>(
            "customers:lookup",
            LookupRegions,
            async () =>
            {
                var rows = await _customers.GetAll()
                    .AsNoTracking()
                    .Select(c => new CustomerLookupItemDto
                    {
                        Id = c.Id,
                        CustomerCode = c.CustomerCode,
                        Name = c.Name,
                        NameAr = c.NameAr,
                        ContactPerson = c.ContactPerson,
                        ContactPersonAr = c.ContactPersonAr,
                        City = c.City,
                        CityAr = c.CityAr,
                        TaxId = c.TaxId,
                        DefaultCurrency = c.DefaultCurrency,
                        Email = c.Email,
                        Phone = c.Phone
                    })
                    .ToListAsync(CancellationToken.None);

                rows.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.CustomerCode, b.CustomerCode));
                return rows;
            });
    }

    private Task<IReadOnlyList<CustomerListItemDto>> LoadDirectoryListAsync()
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<CustomerListItemDto>>(
            "customers:directory",
            DirectoryRegions,
            async () =>
            {
                var rows = await BuildListQuery(null).ToListAsync(CancellationToken.None);

                // One flat query over the light offer columns (no snapshot JSON), grouped here,
                // instead of three correlated subqueries per customer row.
                var offerRows = await _customers.GetAll()
                    .AsNoTracking()
                    .SelectMany(c => c.Offers)
                    .Select(o => new OfferSummaryRow(o.CustomerId, o.RootOfferId, o.Version, o.Total, o.IssueDate))
                    .ToListAsync(CancellationToken.None);

                var summaries = offerRows
                    .GroupBy(o => o.CustomerId)
                    .ToDictionary(g => g.Key, g => SummarizeOffers(g));

                foreach (var row in rows)
                {
                    if (summaries.TryGetValue(row.Id, out var summary))
                    {
                        row.OffersCount = summary.OffersCount;
                        row.TotalValue = summary.TotalValue;
                        row.LastOfferDate = summary.LastOfferDate;
                    }
                }

                rows.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.CustomerCode, b.CustomerCode));
                return rows;
            });
    }

    private sealed record OfferSummaryRow(Guid CustomerId, Guid RootOfferId, int Version, decimal Total, DateOnly IssueDate);

    private sealed record CustomerOfferSummary(int OffersCount, decimal TotalValue, DateOnly? LastOfferDate);

    private static CustomerOfferSummary SummarizeOffers(IEnumerable<OfferSummaryRow> offers)
    {
        var rows = offers.ToList();
        // Only the highest version of each chain counts, as in the customer detail.
        var latest = rows
            .GroupBy(o => o.RootOfferId)
            .Select(chain => chain.OrderByDescending(o => o.Version).First())
            .ToList();

        return new CustomerOfferSummary(
            latest.Count,
            latest.Sum(o => o.Total),
            rows.Count == 0 ? null : rows.Max(o => o.IssueDate));
    }

    public async Task<PaginatedResultDto<OfferListItemDto>> GetOffersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var offersQuery = _customers.GetAll()
            .Where(c => c.Id == id)
            .SelectMany(c => c.Offers)
            .AsNoTracking();

        // Versions of one offer are listed once, as the latest version.
        var latestQuery = offersQuery
            .Where(o => !offersQuery.Any(other =>
                other.RootOfferId == o.RootOfferId && other.Version > o.Version));

        var totalCount = await latestQuery.CountAsync(cancellationToken);
        var items = await latestQuery
            .OrderByDisplayedId()
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(o => new OfferListItemDto
            {
                Id = o.Id,
                Reference = o.Reference,
                OfferType = o.OfferType,
                SerialNumber = o.SerialNumber,
                Version = o.Version,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer.Name,
                CustomerNameAr = o.Customer.NameAr,
                IssueDate = o.IssueDate,
                DueDate = o.DueDate,
                Currency = o.Currency,
                Status = o.Status,
                Total = o.Total,
                CreatedByName = o.CreatedBy.FullName,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                RootOfferId = o.RootOfferId,
                LastModifiedByName = o.UpdatedBy != null
                    ? o.UpdatedBy.FullName
                    : o.CreatedBy.FullName,
                LastModifiedAt = o.UpdatedAt,
                ParentOfferId = o.ParentOfferId
            })
            .ToListAsync(cancellationToken);

        // One grouped query for the page instead of a correlated count per row.
        var rootIds = items.Select(item => item.RootOfferId).Distinct().ToList();
        if (rootIds.Count > 0)
        {
            var chains = await offersQuery
                .Where(o => rootIds.Contains(o.RootOfferId))
                .GroupBy(o => o.RootOfferId)
                .Select(g => new
                {
                    RootOfferId = g.Key,
                    Count = g.Count(),
                    FirstCreatedAt = g.Min(o => o.CreatedAt)
                })
                .ToDictionaryAsync(x => x.RootOfferId, cancellationToken);

            foreach (var item in items)
            {
                if (chains.TryGetValue(item.RootOfferId, out var chain))
                {
                    item.VersionsCount = chain.Count;
                    item.RootCreatedAt = chain.FirstCreatedAt;
                }
                else
                {
                    item.RootCreatedAt = item.CreatedAt;
                }
            }
        }

        return new PaginatedResultDto<OfferListItemDto>
        {
            Items = items,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CustomerDetailDto?> GetByIdWithHistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAll()
            .AsNoTracking()
            .Include(c => c.DefaultPaymentTerm)
            .Include(c => c.Offers)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return customer == null ? null : MapDetail(customer);
    }

    public async Task<CustomerDetailDto> CreateAsync(
        UpsertCustomerDto dto,
        CancellationToken cancellationToken)
    {
        Validate(dto);

        var customerCode = dto.CustomerCode.Trim();
        if (await _customers.GetAll().AnyAsync(c => c.CustomerCode == customerCode, cancellationToken))
        {
            throw new ConflictException("A customer with this customer code already exists.");
        }

        var customer = new Customer
        {
            CustomerCode = customerCode,
            ErpNextId = dto.ErpNextId,
            Name = dto.Name.Trim(),
            NameAr = dto.NameAr,
            TaxId = dto.TaxId,
            ContactPerson = dto.ContactPerson,
            ContactPersonAr = dto.ContactPersonAr,
            Email = dto.Email,
            Phone = dto.Phone,
            City = dto.City,
            CityAr = dto.CityAr,
            Address = dto.Address.Trim(),
            AddressAr = dto.AddressAr,
            DefaultPaymentTermId = dto.DefaultPaymentTermId,
            DefaultCurrency = string.IsNullOrWhiteSpace(dto.DefaultCurrency) ? "EGP" : dto.DefaultCurrency
        };

        await _customers.AddAsync(customer);
        await _customers.SaveChangesAsync(cancellationToken);
        _cache.Invalidate(CacheRegions.Customers);
        _publisher.PublishInBackground(NotificationEventFactory.Create(
            _actor,
            NotificationTypes.CustomerCreated,
            "customer",
            customer.Id,
            customer.Name));

        return await GetByIdWithHistoryAsync(customer.Id, cancellationToken)
            ?? throw new NotFoundException("Customer was not found after creation.");
    }

    public async Task<CustomerDetailDto?> UpdateAsync(
        Guid id,
        UpsertCustomerDto dto,
        CancellationToken cancellationToken)
    {
        Validate(dto);

        var customer = await _customers.GetAll()
            .Include(c => c.DefaultPaymentTerm)
            .Include(c => c.Offers)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer == null)
        {
            return null;
        }

        var customerCode = dto.CustomerCode.Trim();
        if (await _customers.GetAll().AnyAsync(
                c => c.Id != id && c.CustomerCode == customerCode,
                cancellationToken))
        {
            throw new ConflictException("A customer with this customer code already exists.");
        }

        customer.CustomerCode = customerCode;
        customer.ErpNextId = dto.ErpNextId;
        customer.Name = dto.Name.Trim();
        customer.NameAr = dto.NameAr;
        customer.TaxId = dto.TaxId;
        customer.ContactPerson = dto.ContactPerson;
        customer.ContactPersonAr = dto.ContactPersonAr;
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.City = dto.City;
        customer.CityAr = dto.CityAr;
        customer.Address = dto.Address.Trim();
        customer.AddressAr = dto.AddressAr;
        customer.DefaultPaymentTermId = dto.DefaultPaymentTermId;
        customer.DefaultCurrency = string.IsNullOrWhiteSpace(dto.DefaultCurrency) ? "EGP" : dto.DefaultCurrency;

        await _customers.SaveChangesAsync(cancellationToken);
        _cache.Invalidate(CacheRegions.Customers);

        return MapDetail(customer);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAll()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer == null)
        {
            return false;
        }

        // Offers reference customers, so a physical delete has to remove their chains first.
        var roots = await _offers.GetAll()
            .Where(o => o.CustomerId == id)
            .Select(o => o.RootOfferId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var offers = await _offers.GetAll()
            .Where(o => roots.Contains(o.RootOfferId))
            .ToListAsync(cancellationToken);
        _offers.RemoveRange(offers);

        var label = customer.Name;
        _customers.Remove(customer);
        await _customers.SaveChangesAsync(cancellationToken);
        _cache.Invalidate(CacheRegions.Customers, CacheRegions.Offers);
        _publisher.PublishInBackground(NotificationEventFactory.Create(
            _actor,
            NotificationTypes.CustomerDeleted,
            "customer",
            null,
            label));
        return true;
    }

    private IQueryable<Customer> BuildSearchQuery(string? search)
    {
        var query = _customers.GetAll().AsNoTracking().AsQueryable();
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var term = search.Trim().ToLowerInvariant();
        return query.Where(c =>
            c.CustomerCode.ToLower().Contains(term) ||
            c.Name.ToLower().Contains(term) ||
            (c.NameAr != null && c.NameAr.ToLower().Contains(term)));
    }

    private IQueryable<CustomerListItemDto> BuildListQuery(string? search)
    {
        return BuildSearchQuery(search)
            .Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                ErpNextId = c.ErpNextId,
                Name = c.Name,
                NameAr = c.NameAr,
                TaxId = c.TaxId,
                ContactPerson = c.ContactPerson,
                ContactPersonAr = c.ContactPersonAr,
                Email = c.Email,
                Phone = c.Phone,
                City = c.City,
                CityAr = c.CityAr,
                Address = c.Address,
                AddressAr = c.AddressAr,
                DefaultPaymentTermId = c.DefaultPaymentTermId,
                DefaultPaymentTermLabel = c.DefaultPaymentTerm != null ? c.DefaultPaymentTerm.Label : null,
                DefaultCurrency = c.DefaultCurrency,
                // OffersCount, TotalValue and LastOfferDate are filled in by LoadDirectoryListAsync.
                OffersCount = 0,
                TotalValue = 0m,
                LastOfferDate = null
            });
    }

    private static CustomerDetailDto MapDetail(Customer customer)
    {
        var versionCounts = customer.Offers
            .GroupBy(o => o.RootOfferId)
            .ToDictionary(g => g.Key, g => g.Count());

        var offers = customer.Offers
            .LatestPerChain()
            .OrderByDisplayedIdInMemory()
            .ToList();

        return new CustomerDetailDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            ErpNextId = customer.ErpNextId,
            Name = customer.Name,
            NameAr = customer.NameAr,
            TaxId = customer.TaxId,
            ContactPerson = customer.ContactPerson,
            ContactPersonAr = customer.ContactPersonAr,
            Email = customer.Email,
            Phone = customer.Phone,
            City = customer.City,
            CityAr = customer.CityAr,
            Address = customer.Address,
            AddressAr = customer.AddressAr,
            DefaultPaymentTermId = customer.DefaultPaymentTermId,
            DefaultPaymentTermLabel = customer.DefaultPaymentTerm?.Label,
            DefaultCurrency = customer.DefaultCurrency,
            OffersCount = offers.Count,
            TotalValue = offers.Sum(o => o.Total),
            LastOfferDate = offers.Count == 0 ? null : offers.Max(o => o.IssueDate),
            Offers = offers.Select(o => new CustomerOfferHistoryDto
            {
                Id = o.Id,
                Reference = o.Reference,
                Version = o.Version,
                VersionsCount = versionCounts[o.RootOfferId],
                IssueDate = o.IssueDate,
                Status = o.Status,
                Total = o.Total,
                Currency = o.Currency
            }).ToList()
        };
    }

    private static void Validate(UpsertCustomerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerCode))
        {
            throw new ValidationException("Customer code is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ValidationException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            throw new ValidationException("Customer address is required.");
        }
    }
}
