using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customers;

    public CustomerService(IRepository<Customer> customers)
    {
        _customers = customers;
    }

    public async Task<PaginatedResultDto<CustomerLookupItemDto>> LookupAsync(
        LookupQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var customers = BuildSearchQuery(query.Search);
        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerLookupItemDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                NameAr = c.NameAr,
                ContactPerson = c.ContactPerson,
                City = c.City,
                TaxId = c.TaxId,
                DefaultCurrency = c.DefaultCurrency,
                Email = c.Email,
                Phone = c.Phone
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResultDto<CustomerLookupItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<CustomerListItemDto>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        return await BuildListQuery(search)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedResultDto<CustomerListItemDto>> GetPagedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await BuildListQuery(search)
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResultDto<CustomerListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PaginatedResultDto<OfferListItemDto>> GetOffersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var offersQuery = _customers.GetAll()
            .Where(c => c.Id == id)
            .SelectMany(c => c.Offers)
            .AsNoTracking();

        var totalCount = await offersQuery.CountAsync(cancellationToken);
        var items = await offersQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(o => new OfferListItemDto
            {
                Id = o.Id,
                Reference = o.Reference,
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
                UpdatedAt = o.UpdatedAt
            })
            .ToListAsync(cancellationToken);

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
            Email = dto.Email,
            Phone = dto.Phone,
            City = dto.City,
            Address = dto.Address.Trim(),
            DefaultPaymentTermId = dto.DefaultPaymentTermId,
            DefaultCurrency = string.IsNullOrWhiteSpace(dto.DefaultCurrency) ? "EGP" : dto.DefaultCurrency
        };

        await _customers.AddAsync(customer);
        await _customers.SaveChangesAsync(cancellationToken);

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
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.City = dto.City;
        customer.Address = dto.Address.Trim();
        customer.DefaultPaymentTermId = dto.DefaultPaymentTermId;
        customer.DefaultCurrency = string.IsNullOrWhiteSpace(dto.DefaultCurrency) ? "EGP" : dto.DefaultCurrency;

        await _customers.SaveChangesAsync(cancellationToken);

        return MapDetail(customer);
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
                Email = c.Email,
                Phone = c.Phone,
                City = c.City,
                Address = c.Address,
                DefaultPaymentTermId = c.DefaultPaymentTermId,
                DefaultPaymentTermLabel = c.DefaultPaymentTerm != null ? c.DefaultPaymentTerm.Label : null,
                DefaultCurrency = c.DefaultCurrency,
                OffersCount = c.Offers.Count,
                TotalValue = c.Offers.Sum(o => (decimal?)o.Total) ?? 0m,
                LastOfferDate = c.Offers
                    .OrderByDescending(o => o.IssueDate)
                    .Select(o => (DateOnly?)o.IssueDate)
                    .FirstOrDefault()
            });
    }

    private static CustomerDetailDto MapDetail(Customer customer)
    {
        var offers = customer.Offers
            .OrderByDescending(o => o.IssueDate)
            .ThenByDescending(o => o.Reference)
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
            Email = customer.Email,
            Phone = customer.Phone,
            City = customer.City,
            Address = customer.Address,
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
