using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.Calculations;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Enums;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class OfferService : IOfferService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = CreateSnapshotJsonOptions();

    private readonly IRepository<Offer> _offers;
    private readonly IRepository<OfferLine> _offerLines;
    private readonly IRepository<OfferLinePackaging> _offerLinePackagings;
    private readonly IRepository<OfferDeliveryBatch> _deliveryBatches;
    private readonly IRepository<OfferDeliveryAllocation> _deliveryAllocations;
    private readonly ICatalogService _catalogService;
    private readonly IOfferCalculator _calculator;

    public OfferService(
        IRepository<Offer> offers,
        IRepository<OfferLine> offerLines,
        IRepository<OfferLinePackaging> offerLinePackagings,
        IRepository<OfferDeliveryBatch> deliveryBatches,
        IRepository<OfferDeliveryAllocation> deliveryAllocations,
        ICatalogService catalogService,
        IOfferCalculator calculator)
    {
        _offers = offers;
        _offerLines = offerLines;
        _offerLinePackagings = offerLinePackagings;
        _deliveryBatches = deliveryBatches;
        _deliveryAllocations = deliveryAllocations;
        _catalogService = catalogService;
        _calculator = calculator;
    }

    public async Task<OfferCalculateResponseDto> CalculateAsync(
        OfferDraftDto draft,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        draft.CreatedByUserId ??= currentUserId;

        var catalog = await LoadCatalogAsync(draft, cancellationToken);
        return _calculator.Calculate(draft, catalog);
    }

    public async Task<PaginatedResultDto<OfferListItemDto>> GetListAsync(
        OfferQueryParamsDto query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var offers = _offers.GetAll().AsNoTracking().AsQueryable();

        // LatestOnly is evaluated before search/status so the filters apply to the
        // highest-version row of each reference rather than to historical versions.
        if (query.LatestOnly)
        {
            offers = offers.Where(o =>
                !_offers.GetAll().Any(other =>
                    other.Reference == o.Reference && other.Version > o.Version));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            offers = offers.Where(o =>
                o.Reference.ToLower().Contains(term) ||
                o.Customer.Name.ToLower().Contains(term) ||
                (o.Customer.NameAr != null && o.Customer.NameAr.ToLower().Contains(term)));
        }

        if (query.Status.HasValue)
        {
            offers = offers.Where(o => o.Status == query.Status.Value);
        }

        var sortBy = (query.SortBy ?? string.Empty).Trim().ToLowerInvariant();
        var ascending = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<Offer> ordered = (sortBy, ascending) switch
        {
            ("createdat", true) => offers.OrderBy(o => o.CreatedAt),
            ("createdat", false) => offers.OrderByDescending(o => o.CreatedAt),
            ("reference", true) => offers.OrderBy(o => o.Reference),
            ("reference", false) => offers.OrderByDescending(o => o.Reference),
            ("total", true) => offers.OrderBy(o => o.Total),
            ("total", false) => offers.OrderByDescending(o => o.Total),
            ("issuedate", true) => offers.OrderBy(o => o.IssueDate),
            ("issuedate", false) => offers.OrderByDescending(o => o.IssueDate),
            (_, true) => offers.OrderBy(o => o.UpdatedAt),
            _ => offers.OrderByDescending(o => o.UpdatedAt)
        };

        var totalCount = await offers.CountAsync(cancellationToken);
        var items = await ordered
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
                UpdatedAt = o.UpdatedAt,
                VersionsCount = _offers.GetAll().Count(version => version.Reference == o.Reference),
                LastModifiedByName = o.UpdatedBy != null
                    ? o.UpdatedBy.FullName
                    : o.CreatedBy.FullName,
                LastModifiedAt = o.UpdatedAt,
                ParentOfferId = o.ParentOfferId
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResultDto<OfferListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<OfferDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var offer = await FullOfferQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer == null)
        {
            return null;
        }

        var detail = MapOfferDetails(offer);
        detail.VersionsCount = await _offers.GetAll()
            .CountAsync(o => o.Reference == offer.Reference, cancellationToken);
        return detail;
    }

    public async Task<OfferDocumentDto?> GetDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshotJson = await _offers.GetAll()
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => o.SnapshotJson)
            .FirstOrDefaultAsync(cancellationToken);

        return ParseDocument(snapshotJson);
    }

    public async Task<List<OfferListItemDto>> GetVersionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var reference = await _offers.GetAll()
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => o.Reference)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new NotFoundException("Offer was not found.");
        }

        return await _offers.GetAll()
            .AsNoTracking()
            .Where(o => o.Reference == reference)
            .OrderByDescending(o => o.Version)
            .ThenByDescending(o => o.CreatedAt)
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
                UpdatedAt = o.UpdatedAt,
                VersionsCount = _offers.GetAll().Count(version => version.Reference == o.Reference),
                LastModifiedByName = o.UpdatedBy != null
                    ? o.UpdatedBy.FullName
                    : o.CreatedBy.FullName,
                LastModifiedAt = o.UpdatedAt,
                ParentOfferId = o.ParentOfferId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<OfferNextSequenceDto> GetNextSequenceAsync(int year, int month, Guid? currentUserId, CancellationToken cancellationToken)
    {
        if (year <= 0 || month < 1 || month > 12)
        {
            throw new ValidationException("Year and month are required.");
        }

        var monthStart = new DateOnly(year, month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var maxSeq = await _offers.GetAll()
            .AsNoTracking()
            .Where(o => o.IssueDate >= monthStart && o.IssueDate < nextMonthStart)
            .MaxAsync(o => (int?)o.SerialNumber, cancellationToken) ?? 0;

        if (maxSeq <= 0)
        {
            var refs = await _offers.GetAll()
                .AsNoTracking()
                .Where(o => o.IssueDate >= monthStart && o.IssueDate < nextMonthStart)
                .Select(o => o.Reference)
                .ToListAsync(cancellationToken);

            foreach (var reference in refs)
            {
                var firstSegment = reference.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (int.TryParse(firstSegment, out var parsed) && parsed > maxSeq)
                {
                    maxSeq = parsed;
                }
            }
        }

        var suggestedSequence = maxSeq + 1;

        var company = await _catalogService.GetOfferCatalogAsync(null, currentUserId, cancellationToken);
        var prefix = string.IsNullOrWhiteSpace(company.CompanySettings?.ReferencePrefix)
            ? "YL"
            : company.CompanySettings.ReferencePrefix.Trim();

        string initials = "AA";
        var createdByInitials = company.CreatedBy?.Initials;
        if (!string.IsNullOrWhiteSpace(createdByInitials))
        {
            initials = createdByInitials.Trim();
        }

        var suggestedReference = ReferenceRules.Build(
            suggestedSequence,
            new DateOnly(year, month, 1),
            prefix,
            initials);

        return new OfferNextSequenceDto
        {
            SuggestedSequence = suggestedSequence,
            SuggestedReference = suggestedReference
        };
    }

    public async Task<OfferDetailDto> CreateAsync(
        OfferDraftDto draft,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.Reference))
        {
            throw new ValidationException("Offer reference is required.");
        }

        draft.Reference = draft.Reference.Trim();
        draft.Version = draft.Version <= 0 ? 1 : draft.Version;
        draft.CreatedByUserId = currentUserId;
        draft.Status = OfferStatus.Draft;

        var catalog = await LoadCatalogAsync(draft, cancellationToken);
        var calculation = _calculator.Calculate(draft, catalog);
        ThrowIfInvalid(calculation);

        if (await _offers.GetAll().AnyAsync(
                o => o.Reference == calculation.Draft.Reference &&
                     o.Version == calculation.Draft.Version,
                cancellationToken))
        {
            throw new ConflictException("An offer with this reference and version already exists.");
        }

        var offer = new Offer();
        ApplyCalculatedOffer(offer, calculation, currentUserId);
        await _offers.AddAsync(offer);
        await MarkNewChildrenAsAddedAsync(offer);

        await using var transaction = await _offers.BeginTransactionAsync(cancellationToken);
        try
        {
            await _offers.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("An offer with this reference and version already exists.");
        }

        return await GetByIdAsync(offer.Id, cancellationToken)
            ?? throw new NotFoundException("Offer was not found after creation.");
    }

    public async Task<OfferDetailDto> UpdateAsync(
        Guid id,
        OfferDraftDto draft,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.Reference))
        {
            throw new ValidationException("Offer reference is required.");
        }

        var offer = await FullOfferQuery()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer == null)
        {
            throw new NotFoundException("Offer was not found.");
        }

        if (offer.Status != OfferStatus.Draft)
        {
            throw new ValidationException("Only draft offers can be updated.");
        }

        draft.Reference = draft.Reference.Trim();
        draft.Version = draft.Version <= 0 ? offer.Version : draft.Version;
        draft.CreatedByUserId = offer.CreatedByUserId;
        draft.Status = OfferStatus.Draft;

        var catalog = await LoadCatalogAsync(draft, cancellationToken);
        var calculation = _calculator.Calculate(draft, catalog);
        ThrowIfInvalid(calculation);

        if (await _offers.GetAll().AnyAsync(
                o => o.Id != id &&
                     o.Reference == calculation.Draft.Reference &&
                     o.Version == calculation.Draft.Version,
                cancellationToken))
        {
            throw new ConflictException("An offer with this reference and version already exists.");
        }

        await using var transaction = await _offers.BeginTransactionAsync(cancellationToken);
        try
        {
            RemoveOfferChildren(offer);
            await _offers.SaveChangesAsync(cancellationToken);

            // Start with a clean tracker so the replacement graph is inserted rather than
            // being mistaken for modifications to the entities that were just removed.
            _offers.ClearTracking();

            var trackedOffer = await _offers.GetAll().FirstAsync(o => o.Id == id, cancellationToken);
            ApplyCalculatedOffer(trackedOffer, calculation, trackedOffer.CreatedByUserId, currentUserId);
            await MarkNewChildrenAsAddedAsync(trackedOffer);

            await _offers.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("The offer could not be updated.");
        }

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Offer was not found after update.");
    }

    public async Task<OfferDetailDto> ReviseAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var source = await FullOfferQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (source == null)
        {
            throw new NotFoundException("Offer was not found.");
        }

        var nextVersion = await _offers.GetAll()
            .Where(o => o.Reference == source.Reference)
            .MaxAsync(o => o.Version, cancellationToken) + 1;

        var draft = MapToDraft(source);
        draft.Id = null;
        draft.Version = nextVersion;
        draft.ParentOfferId = source.Id;
        draft.Status = OfferStatus.Draft;
        draft.CreatedByUserId = currentUserId;

        var catalog = await LoadCatalogAsync(draft, cancellationToken);
        var calculation = _calculator.Calculate(draft, catalog);
        ThrowIfInvalid(calculation);

        calculation.Draft.Status = OfferStatus.Draft;

        var revisedOffer = new Offer();
        ApplyCalculatedOffer(revisedOffer, calculation, currentUserId);
        await _offers.AddAsync(revisedOffer);
        await MarkNewChildrenAsAddedAsync(revisedOffer);

        await using var transaction = await _offers.BeginTransactionAsync(cancellationToken);
        try
        {
            await _offers.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("A new revision could not be created.");
        }

        return await GetByIdAsync(revisedOffer.Id, cancellationToken)
            ?? throw new NotFoundException("Offer was not found after revision.");
    }

    public async Task<OfferDetailDto> UpdateStatusAsync(
        Guid id,
        UpdateOfferStatusDto dto,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var offer = await _offers.GetByIdAsync(id);
        if (offer == null)
        {
            throw new NotFoundException("Offer was not found.");
        }

        if (!IsValidStatusTransition(offer.Status, dto.Status))
        {
            throw new ValidationException(
                $"Cannot transition an offer from {offer.Status} to {dto.Status}.");
        }

        offer.Status = dto.Status;
        offer.UpdatedByUserId = currentUserId;

        var document = ParseDocument(offer.SnapshotJson);
        if (document != null)
        {
            document.Metadata.Status = dto.Status;
            offer.SnapshotJson = JsonSerializer.Serialize(document, SnapshotJsonOptions);
        }

        _offers.Update(offer);

        try
        {
            await _offers.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The offer was modified by another user. Refresh and try again.");
        }

        return await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Offer was not found after status update.");
    }

    private async Task<OfferCatalog> LoadCatalogAsync(
        OfferDraftDto draft,
        CancellationToken cancellationToken)
    {
        var catalog = await _catalogService.GetOfferCatalogAsync(
            draft.CustomerId,
            draft.CreatedByUserId,
            cancellationToken);

        catalog.PaymentTerm = catalog.PaymentTerms.FirstOrDefault(pt => pt.Id == draft.PaymentTermId);
        return catalog;
    }

    private IQueryable<Offer> FullOfferQuery()
    {
        return _offers.GetAll()
            .Include(o => o.Customer)
            .Include(o => o.CreatedBy)
            .Include(o => o.UpdatedBy)
            .Include(o => o.PaymentTerm)
            .Include(o => o.Lines)
            .ThenInclude(l => l.Packaging)
            .Include(o => o.DeliveryBatches)
            .ThenInclude(b => b.Allocations)
            .AsSplitQuery();
    }

    private static OfferDetailDto MapOfferDetails(Offer offer)
    {
        return new OfferDetailDto
        {
            Id = offer.Id,
            Reference = offer.Reference,
            SerialNumber = offer.SerialNumber,
            Version = offer.Version,
            ParentOfferId = offer.ParentOfferId,
            CustomerId = offer.CustomerId,
            Customer = offer.Customer == null
                ? null
                : new CustomerLookupItemDto
                {
                    Id = offer.Customer.Id,
                    CustomerCode = offer.Customer.CustomerCode,
                    Name = offer.Customer.Name,
                    NameAr = offer.Customer.NameAr,
                    ContactPerson = offer.Customer.ContactPerson,
                    City = offer.Customer.City,
                    TaxId = offer.Customer.TaxId,
                    DefaultCurrency = offer.Customer.DefaultCurrency,
                    Email = offer.Customer.Email,
                    Phone = offer.Customer.Phone
                },
            CreatedByUserId = offer.CreatedByUserId,
            CreatedByName = offer.CreatedBy?.FullName,
            UpdatedByUserId = offer.UpdatedByUserId,
            UpdatedByName = offer.UpdatedBy?.FullName,
            Attention = offer.Attention,
            AttentionAr = offer.AttentionAr,
            TaxId = offer.TaxId,
            IssueDate = offer.IssueDate,
            DueDate = offer.DueDate,
            PaymentTermId = offer.PaymentTermId,
            PaymentTermLabel = offer.PaymentTerm?.Label,
            Currency = offer.Currency,
            DocumentLanguage = offer.DocumentLanguage,
            Notes = offer.Notes,
            FormCode = offer.FormCode,
            Status = offer.Status,
            Subtotal = offer.Subtotal,
            OrderDiscount = offer.OrderDiscount,
            OrderDiscountPercent = offer.OrderDiscountPercent,
            TaxPercent = offer.TaxPercent,
            TaxAmount = offer.TaxAmount,
            OtherTaxEnabled = offer.OtherTaxEnabled,
            OtherTaxName = offer.OtherTaxName,
            OtherTaxPercent = offer.OtherTaxPercent,
            OtherTaxAmount = offer.OtherTaxAmount,
            Total = offer.Total,
            CreatedAt = offer.CreatedAt,
            UpdatedAt = offer.UpdatedAt,
            Lines = offer.Lines
                .OrderBy(line => line.SortOrder)
                .Select(MapLine)
                .ToList(),
            DeliveryBatches = offer.DeliveryBatches
                .OrderBy(batch => batch.BatchNumber)
                .Select(MapBatch)
                .ToList(),
            Document = ParseDocument(offer.SnapshotJson)
        };
    }

    private static OfferDraftDto MapToDraft(Offer offer)
    {
        return new OfferDraftDto
        {
            Id = offer.Id,
            Reference = offer.Reference,
            SerialNumber = offer.SerialNumber,
            Version = offer.Version,
            ParentOfferId = offer.ParentOfferId,
            CustomerId = offer.CustomerId,
            CreatedByUserId = offer.CreatedByUserId,
            Attention = offer.Attention,
            AttentionAr = offer.AttentionAr,
            TaxId = offer.TaxId,
            IssueDate = offer.IssueDate,
            DueDate = offer.DueDate,
            PaymentTermId = offer.PaymentTermId,
            Currency = offer.Currency,
            DocumentLanguage = offer.DocumentLanguage,
            Notes = offer.Notes,
            FormCode = offer.FormCode,
            Status = offer.Status,
            OrderDiscount = offer.OrderDiscount,
            OrderDiscountPercent = 0m,
            TaxPercent = offer.TaxPercent,
            OtherTaxEnabled = offer.OtherTaxEnabled,
            OtherTaxName = offer.OtherTaxName,
            OtherTaxPercent = offer.OtherTaxPercent,
            Lines = offer.Lines
                .OrderBy(line => line.SortOrder)
                .Select(MapLine)
                .ToList(),
            DeliveryBatches = offer.DeliveryBatches
                .OrderBy(batch => batch.BatchNumber)
                .Select(MapBatch)
                .ToList()
        };
    }

    private static OfferDraftLineDto MapLine(OfferLine line)
    {
        return new OfferDraftLineDto
        {
            Id = line.Id,
            SortOrder = line.SortOrder,
            MaterialId = line.MaterialId,
            RatePerKg = line.RatePerKg,
            DiscountPercent = line.DiscountPercent,
            ShowSpecifications = line.ShowSpecifications,
            CustomSpecifications = line.CustomSpecifications,
            Packaging = line.Packaging
                .OrderBy(packaging => packaging.SortOrder)
                .Select(packaging => new OfferDraftPackagingDto
                {
                    Id = packaging.Id,
                    SortOrder = packaging.SortOrder,
                    PackageTypeId = packaging.PackageTypeId,
                    WeightPerPackageKg = packaging.WeightPerPackageKg,
                    Packages = packaging.Packages
                })
                .ToList()
        };
    }

    private static OfferDraftDeliveryBatchDto MapBatch(OfferDeliveryBatch batch)
    {
        return new OfferDraftDeliveryBatchDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            DeliveryDate = batch.DeliveryDate,
            Allocations = batch.Allocations
                .OrderBy(allocation => allocation.PackageTypeName)
                .Select(allocation => new OfferDraftDeliveryAllocationDto
                {
                    Id = allocation.Id,
                    OfferLineId = allocation.OfferLineId,
                    OfferLinePackagingId = allocation.OfferLinePackagingId,
                    MaterialId = allocation.MaterialId,
                    PackageTypeId = allocation.PackageTypeId,
                    Packages = allocation.Packages
                })
                .ToList()
        };
    }

    private static void ApplyCalculatedOffer(
        Offer offer,
        OfferCalculateResponseDto calculation,
        Guid createdByUserId,
        Guid? updatedByUserId = null)
    {
        var draft = calculation.Draft;

        offer.Reference = draft.Reference.Trim();
        offer.SerialNumber = draft.SerialNumber;
        offer.Version = draft.Version <= 0 ? 1 : draft.Version;
        offer.ParentOfferId = draft.ParentOfferId;
        offer.CustomerId = draft.CustomerId;
        offer.CreatedByUserId = createdByUserId;
        offer.UpdatedByUserId = updatedByUserId ?? createdByUserId;
        offer.Attention = draft.Attention;
        offer.AttentionAr = draft.AttentionAr;
        offer.TaxId = draft.TaxId;
        offer.IssueDate = draft.IssueDate;
        offer.DueDate = calculation.Totals.DueDate;
        offer.PaymentTermId = draft.PaymentTermId;
        offer.Currency = draft.Currency;
        offer.DocumentLanguage = draft.DocumentLanguage;
        offer.Notes = draft.Notes;
        offer.FormCode = calculation.Document.FormCode;
        offer.Status = draft.Status;
        offer.Subtotal = calculation.Totals.Subtotal;
        offer.OrderDiscount = calculation.Totals.OrderDiscount;
        offer.OrderDiscountPercent = calculation.Totals.OrderDiscountPercent;
        offer.TaxPercent = calculation.Totals.TaxPercent;
        offer.TaxAmount = calculation.Totals.TaxAmount;
        offer.OtherTaxEnabled = calculation.Totals.OtherTaxEnabled;
        offer.OtherTaxName = calculation.Totals.OtherTaxName;
        offer.OtherTaxPercent = calculation.Totals.OtherTaxPercent;
        offer.OtherTaxAmount = calculation.Totals.OtherTaxAmount;
        offer.Total = calculation.Totals.Total;
        offer.SnapshotJson = JsonSerializer.Serialize(calculation.Document, SnapshotJsonOptions);

        var lineIdMap = new Dictionary<Guid, Guid>();
        var packagingIdMap = new Dictionary<Guid, Guid>();

        foreach (var calculatedLine in calculation.Lines.OrderBy(line => line.SortOrder))
        {
            var line = new OfferLine
            {
                Id = Guid.NewGuid(),
                SortOrder = calculatedLine.SortOrder,
                MaterialId = calculatedLine.MaterialId,
                MaterialCode = calculatedLine.MaterialCode,
                MaterialName = calculatedLine.MaterialName,
                Category = calculatedLine.Category,
                RatePerKg = calculatedLine.RatePerKg,
                DiscountPercent = 0m,
                QuantityKg = calculatedLine.QuantityKg,
                Amount = calculatedLine.Amount,
                ShowSpecifications = calculatedLine.ShowSpecifications,
                CustomSpecifications = calculatedLine.CustomSpecifications
            };

            lineIdMap[calculatedLine.Id] = line.Id;

            foreach (var calculatedPackaging in calculatedLine.Packaging.OrderBy(packaging => packaging.SortOrder))
            {
                var packaging = new OfferLinePackaging
                {
                    Id = Guid.NewGuid(),
                    SortOrder = calculatedPackaging.SortOrder,
                    PackageTypeId = calculatedPackaging.PackageTypeId,
                    PackageTypeName = calculatedPackaging.PackageTypeName,
                    PackageTypeNameAr = calculatedPackaging.PackageTypeNameAr,
                    WeightPerPackageKg = calculatedPackaging.WeightPerPackageKg,
                    Packages = calculatedPackaging.Packages
                };

                packagingIdMap[calculatedPackaging.Id] = packaging.Id;
                line.Packaging.Add(packaging);
            }

            offer.Lines.Add(line);
        }

        foreach (var calculatedBatch in calculation.DeliveryBatches.OrderBy(batch => batch.BatchNumber))
        {
            var batch = new OfferDeliveryBatch
            {
                Id = Guid.NewGuid(),
                BatchNumber = calculatedBatch.BatchNumber,
                DeliveryDate = calculatedBatch.DeliveryDate
            };

            foreach (var calculatedAllocation in calculatedBatch.Allocations)
            {
                if (!lineIdMap.TryGetValue(calculatedAllocation.OfferLineId, out var lineId) ||
                    !packagingIdMap.TryGetValue(calculatedAllocation.OfferLinePackagingId, out var packagingId))
                {
                    continue;
                }

                batch.Allocations.Add(new OfferDeliveryAllocation
                {
                    Id = Guid.NewGuid(),
                    OfferLineId = lineId,
                    OfferLinePackagingId = packagingId,
                    MaterialId = calculatedAllocation.MaterialId,
                    PackageTypeId = calculatedAllocation.PackageTypeId,
                    PackageTypeName = calculatedAllocation.PackageTypeName,
                    PackageTypeNameAr = calculatedAllocation.PackageTypeNameAr,
                    WeightPerPackageKg = calculatedAllocation.WeightPerPackageKg,
                    Packages = calculatedAllocation.Packages,
                    TotalWeightKg = calculatedAllocation.TotalWeightKg
                });
            }

            offer.DeliveryBatches.Add(batch);
        }
    }

    private void RemoveOfferChildren(Offer offer)
    {
        var allocations = offer.DeliveryBatches.SelectMany(batch => batch.Allocations).ToList();
        _deliveryAllocations.RemoveRange(allocations);

        var batches = offer.DeliveryBatches.ToList();
        _deliveryBatches.RemoveRange(batches);

        var packagingRows = offer.Lines.SelectMany(line => line.Packaging).ToList();
        _offerLinePackagings.RemoveRange(packagingRows);

        var lines = offer.Lines.ToList();
        _offerLines.RemoveRange(lines);
    }

    private async Task MarkNewChildrenAsAddedAsync(Offer offer)
    {
        await _offerLines.AddRangeAsync(offer.Lines);
        await _deliveryBatches.AddRangeAsync(offer.DeliveryBatches);
    }

    private static bool IsValidStatusTransition(OfferStatus current, OfferStatus next)
    {
        if (current == next)
        {
            return true;
        }

        return (current, next) switch
        {
            (OfferStatus.Draft, OfferStatus.Issued) => true,
            (OfferStatus.Issued, OfferStatus.Accepted) => true,
            (OfferStatus.Issued, OfferStatus.Rejected) => true,
            _ => false
        };
    }

    private static void ThrowIfInvalid(OfferCalculateResponseDto calculation)
    {
        if (calculation.ValidationErrors.Count > 0)
        {
            throw new OfferValidationException(
                calculation.ValidationErrors,
                calculation.ValidationWarnings);
        }
    }

    private static OfferDocumentDto? ParseDocument(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson) || snapshotJson == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OfferDocumentDto>(snapshotJson, SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateSnapshotJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
