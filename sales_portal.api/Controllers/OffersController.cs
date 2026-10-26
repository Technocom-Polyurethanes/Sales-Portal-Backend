using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sales_portal.api.Services;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OffersController : ControllerBase
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = CreateSnapshotJsonOptions();

    private readonly AppDbContext _db;
    private readonly OfferCalculator _calculator;
    private readonly OfferCatalogProvider _catalogProvider;

    public OffersController(
        AppDbContext db,
        OfferCalculator calculator,
        OfferCatalogProvider catalogProvider)
    {
        _db = db;
        _calculator = calculator;
        _catalogProvider = catalogProvider;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<OfferCalculationResultDto>> Calculate(
        [FromBody] OfferCalculateRequestDto request,
        CancellationToken cancellationToken)
    {
        request.CreatedByUserId ??= CurrentUserId();

        var catalog = await _catalogProvider.LoadAsync(request, cancellationToken);
        var calculation = _calculator.Calculate(request, catalog);

        return Ok(calculation);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OfferListItemDto>>> GetOffers(
        [FromQuery] string? search,
        [FromQuery] OfferStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Offers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.Reference.ToLower().Contains(term) ||
                o.Customer.Name.ToLower().Contains(term) ||
                (o.Customer.NameAr != null && o.Customer.NameAr.ToLower().Contains(term)));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
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
                UpdatedAt = o.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<OfferListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OfferDetailsDto>> GetOffer(Guid id, CancellationToken cancellationToken)
    {
        var offer = await FullOfferQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return offer == null ? NotFound() : Ok(MapOfferDetails(offer));
    }

    [HttpPost]
    public async Task<ActionResult<OfferDetailsDto>> CreateOffer(
        [FromBody] OfferDraftDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return BadRequest(new { message = "Offer reference is required." });
        }

        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        request.Reference = request.Reference.Trim();
        request.Version = request.Version <= 0 ? 1 : request.Version;
        request.CreatedByUserId = userId;

        var catalog = await _catalogProvider.LoadAsync(request, cancellationToken);
        var calculation = _calculator.Calculate(request, catalog);
        if (calculation.ValidationErrors.Count > 0)
        {
            return BadRequest(new
            {
                message = "Offer validation failed.",
                errors = calculation.ValidationErrors,
                warnings = calculation.ValidationWarnings
            });
        }

        calculation.Draft.Status = OfferStatus.Draft;

        if (await _db.Offers.AnyAsync(
                o => o.Reference == calculation.Draft.Reference &&
                     o.Version == calculation.Draft.Version,
                cancellationToken))
        {
            return Conflict(new { message = "An offer with this reference and version already exists." });
        }

        var offer = new Offer();
        ApplyCalculatedOffer(offer, calculation, userId.Value);
        _db.Offers.Add(offer);
        MarkNewChildrenAsAdded(offer);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "An offer with this reference and version already exists." });
        }

        var details = await LoadOfferDetailsAsync(offer.Id, cancellationToken);
        return CreatedAtAction(nameof(GetOffer), new { id = offer.Id }, details);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OfferDetailsDto>> UpdateOffer(
        Guid id,
        [FromBody] OfferDraftDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return BadRequest(new { message = "Offer reference is required." });
        }

        var offer = await FullOfferQuery()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer == null)
        {
            return NotFound();
        }

        if (offer.Status != OfferStatus.Draft)
        {
            return BadRequest(new { message = "Only draft offers can be updated." });
        }

        request.Reference = request.Reference.Trim();
        request.Version = request.Version <= 0 ? offer.Version : request.Version;
        request.CreatedByUserId = offer.CreatedByUserId;

        var catalog = await _catalogProvider.LoadAsync(request, cancellationToken);
        var calculation = _calculator.Calculate(request, catalog);
        if (calculation.ValidationErrors.Count > 0)
        {
            return BadRequest(new
            {
                message = "Offer validation failed.",
                errors = calculation.ValidationErrors,
                warnings = calculation.ValidationWarnings
            });
        }

        calculation.Draft.Status = OfferStatus.Draft;

        if (await _db.Offers.AnyAsync(
                o => o.Id != id &&
                     o.Reference == calculation.Draft.Reference &&
                     o.Version == calculation.Draft.Version,
                cancellationToken))
        {
            return Conflict(new { message = "An offer with this reference and version already exists." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            RemoveOfferChildren(offer);
            await _db.SaveChangesAsync(cancellationToken);

            // Start with a clean tracker so the replacement graph is inserted rather than
            // being mistaken for modifications to the entities that were just removed.
            _db.ChangeTracker.Clear();

            var trackedOffer = await _db.Offers.FirstAsync(o => o.Id == id, cancellationToken);
            ApplyCalculatedOffer(trackedOffer, calculation, trackedOffer.CreatedByUserId);
            MarkNewChildrenAsAdded(trackedOffer);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "The offer could not be updated." });
        }

        return Ok(await LoadOfferDetailsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<ActionResult<OfferDetailsDto>> ReviseOffer(Guid id, CancellationToken cancellationToken)
    {
        var source = await FullOfferQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (source == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var nextVersion = await _db.Offers
            .Where(o => o.Reference == source.Reference)
            .MaxAsync(o => o.Version, cancellationToken) + 1;

        var draft = MapToDraft(source);
        draft.Id = null;
        draft.Version = nextVersion;
        draft.ParentOfferId = source.Id;
        draft.Status = OfferStatus.Draft;
        draft.CreatedByUserId = userId;

        var catalog = await _catalogProvider.LoadAsync(draft, cancellationToken);
        var calculation = _calculator.Calculate(draft, catalog);
        if (calculation.ValidationErrors.Count > 0)
        {
            return BadRequest(new
            {
                message = "Offer validation failed.",
                errors = calculation.ValidationErrors,
                warnings = calculation.ValidationWarnings
            });
        }

        calculation.Draft.Status = OfferStatus.Draft;

        var revisedOffer = new Offer();
        ApplyCalculatedOffer(revisedOffer, calculation, userId.Value);
        _db.Offers.Add(revisedOffer);
        MarkNewChildrenAsAdded(revisedOffer);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "A new revision could not be created." });
        }

        var details = await LoadOfferDetailsAsync(revisedOffer.Id, cancellationToken);
        return CreatedAtAction(nameof(GetOffer), new { id = revisedOffer.Id }, details);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OfferDetailsDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateOfferStatusDto request,
        CancellationToken cancellationToken)
    {
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (offer == null)
        {
            return NotFound();
        }

        if (!IsValidStatusTransition(offer.Status, request.Status))
        {
            return BadRequest(new
            {
                message = $"Cannot transition an offer from {offer.Status} to {request.Status}."
            });
        }

        offer.Status = request.Status;

        var document = ParseDocument(offer.SnapshotJson);
        if (document != null)
        {
            document.Metadata.Status = request.Status;
            offer.SnapshotJson = JsonSerializer.Serialize(document, SnapshotJsonOptions);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await LoadOfferDetailsAsync(id, cancellationToken));
    }

    private IQueryable<Offer> FullOfferQuery()
    {
        return _db.Offers
            .Include(o => o.Customer)
            .Include(o => o.CreatedBy)
            .Include(o => o.PaymentTerm)
            .Include(o => o.Lines)
            .ThenInclude(l => l.Packaging)
            .Include(o => o.DeliveryBatches)
            .ThenInclude(b => b.Allocations)
            .AsSplitQuery();
    }

    private async Task<OfferDetailsDto?> LoadOfferDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var offer = await FullOfferQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return offer == null ? null : MapOfferDetails(offer);
    }

    private static OfferDetailsDto MapOfferDetails(Offer offer)
    {
        return new OfferDetailsDto
        {
            Id = offer.Id,
            Reference = offer.Reference,
            SerialNumber = offer.SerialNumber,
            Version = offer.Version,
            ParentOfferId = offer.ParentOfferId,
            CustomerId = offer.CustomerId,
            Customer = offer.Customer == null
                ? null
                : new CustomerLookupDto
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
            Attention = offer.Attention,
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

    private static OfferLineDraftDto MapLine(OfferLine line)
    {
        return new OfferLineDraftDto
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
                .Select(packaging => new OfferLinePackagingDraftDto
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

    private static OfferDeliveryBatchDraftDto MapBatch(OfferDeliveryBatch batch)
    {
        return new OfferDeliveryBatchDraftDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            DeliveryDate = batch.DeliveryDate,
            Allocations = batch.Allocations
                .OrderBy(allocation => allocation.PackageTypeName)
                .Select(allocation => new OfferDeliveryAllocationDraftDto
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
        OfferCalculationResultDto calculation,
        Guid createdByUserId)
    {
        var draft = calculation.Draft;

        offer.Reference = draft.Reference.Trim();
        offer.SerialNumber = draft.SerialNumber;
        offer.Version = draft.Version <= 0 ? 1 : draft.Version;
        offer.ParentOfferId = draft.ParentOfferId;
        offer.CustomerId = draft.CustomerId;
        offer.CreatedByUserId = createdByUserId;
        offer.Attention = draft.Attention;
        offer.TaxId = draft.TaxId;
        offer.IssueDate = draft.IssueDate;
        offer.DueDate = calculation.Totals.DueDate;
        offer.PaymentTermId = draft.PaymentTermId;
        offer.Currency = draft.Currency;
        offer.DocumentLanguage = draft.DocumentLanguage;
        offer.Notes = draft.Notes;
        offer.FormCode = string.IsNullOrWhiteSpace(draft.FormCode) ? "TF-06-01A" : draft.FormCode;
        offer.Status = draft.Status;
        offer.Subtotal = calculation.Totals.Subtotal;
        offer.OrderDiscount = calculation.Totals.OrderDiscount;
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
        _db.OfferDeliveryAllocations.RemoveRange(allocations);

        var batches = offer.DeliveryBatches.ToList();
        _db.OfferDeliveryBatches.RemoveRange(batches);

        var packagingRows = offer.Lines.SelectMany(line => line.Packaging).ToList();
        _db.OfferLinePackagings.RemoveRange(packagingRows);

        var lines = offer.Lines.ToList();
        _db.OfferLines.RemoveRange(lines);
    }

    private void MarkNewChildrenAsAdded(Offer offer)
    {
        foreach (var line in offer.Lines)
        {
            _db.OfferLines.Add(line);
        }

        foreach (var batch in offer.DeliveryBatches)
        {
            _db.OfferDeliveryBatches.Add(batch);
        }
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

    private Guid? CurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out var id) ? id : null;
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
