using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.core.Services.Import;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class ImportService : IImportService
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private const int MaxRows = 5000;

    private readonly IRepository<ImportBatch> _batches;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Material> _materials;
    private readonly IRepository<PaymentTerm> _paymentTerms;
    private readonly IRepository<User> _users;

    public ImportService(
        IRepository<ImportBatch> batches,
        IRepository<Customer> customers,
        IRepository<Material> materials,
        IRepository<PaymentTerm> paymentTerms,
        IRepository<User> users)
    {
        _batches = batches;
        _customers = customers;
        _materials = materials;
        _paymentTerms = paymentTerms;
        _users = users;
    }

    public Task<ImportTemplateDto> GetTemplateAsync(
        string type,
        CancellationToken cancellationToken)
    {
        var kind = ImportKinds.Parse(type);
        return Task.FromResult(new ImportTemplateDto
        {
            Content = kind.CsvTemplate(),
            FileName = $"{kind.ToSlug()}-template.csv"
        });
    }

    public async Task<ImportPreviewDto> PreviewAsync(
        string type,
        Stream content,
        string fileName,
        long length,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var kind = ImportKinds.Parse(type);

        if (length > MaxFileBytes)
        {
            throw new ValidationException("The file is larger than 5 MB.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("The file has no name.");
        }

        List<ParsedImportRow> parsed;
        try
        {
            parsed = ImportFileReader.Read(content, fileName, kind);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ValidationException(
                "The file could not be read. Check that it is a valid .xlsx or .csv file.");
        }

        if (parsed.Count == 0)
        {
            throw new ValidationException("The file has no data rows.");
        }

        if (parsed.Count > MaxRows)
        {
            throw new ValidationException(
                $"The file has more than {MaxRows:N0} rows. Split it and import again.");
        }

        var lookups = new ImportLookups
        {
            Customers = await _customers.GetAll().AsNoTracking().ToListAsync(cancellationToken),
            Materials = await _materials.GetAll().AsNoTracking().ToListAsync(cancellationToken),
            PaymentTerms = await _paymentTerms.GetAll().AsNoTracking().ToListAsync(cancellationToken)
        };

        var rows = ImportRowBuilder.Build(kind, parsed, lookups);
        var createdBy = await FindUserNameAsync(userId, cancellationToken);

        var batch = new ImportBatch
        {
            Type = kind.ToSlug(),
            FileName = Path.GetFileName(fileName),
            Status = "Pending",
            CreatedByUserId = userId,
            CreatedByName = createdBy,
            RowsJson = JsonSerializer.Serialize(rows)
        };

        await _batches.AddAsync(batch);
        await _batches.SaveChangesAsync(cancellationToken);

        return new ImportPreviewDto
        {
            ImportId = batch.Id,
            Summary = Summarize(rows),
            Rows = rows.Select(MapRow).ToList()
        };
    }

    public async Task<ImportCommitResultDto> CommitAsync(
        Guid importId,
        CancellationToken cancellationToken)
    {
        var batch = await _batches.GetByIdAsync(importId)
            ?? throw new NotFoundException("Import not found.");

        if (batch.Status == "Committed")
        {
            return ResultOf(batch);
        }

        var kind = ImportKinds.Parse(batch.Type);
        var rows = JsonSerializer.Deserialize<List<StoredImportRow>>(batch.RowsJson) ?? [];

        var targetIds = rows
            .Where(row => row.Status is "new" or "update" && row.TargetId.HasValue)
            .Select(row => row.TargetId!.Value)
            .ToHashSet();

        var customers = await _customers.GetAll()
            .Where(customer => targetIds.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id, cancellationToken);
        var materials = await _materials.GetAll()
            .Where(material => targetIds.Contains(material.Id))
            .ToDictionaryAsync(material => material.Id, cancellationToken);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            if (row.Status == "error")
            {
                failed += 1;
                continue;
            }

            if (row.Status == "unchanged")
            {
                skipped += 1;
                continue;
            }

            var applied = kind switch
            {
                ImportKind.Customers => await ApplyCustomerAsync(row, customers),
                ImportKind.Materials => await ApplyMaterialAsync(row, materials),
                _ => ApplyPrice(row, materials)
            };

            if (!applied)
            {
                failed += 1;
            }
            else if (row.Status == "new")
            {
                created += 1;
            }
            else
            {
                updated += 1;
            }
        }

        batch.Status = "Committed";
        batch.CommittedAt = DateTimeOffset.UtcNow;
        batch.CreatedCount = created;
        batch.UpdatedCount = updated;
        batch.SkippedCount = skipped;
        batch.FailedCount = failed;

        try
        {
            await _batches.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "The import could not be saved because a code was changed by someone else. Preview the file again.");
        }

        return ResultOf(batch);
    }

    public async Task<PaginatedResultDto<ImportHistoryItemDto>> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _batches.GetAll()
            .AsNoTracking()
            .Where(batch => batch.CommittedAt != null);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(batch => batch.CommittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(batch => new ImportHistoryItemDto
            {
                Id = batch.Id,
                Type = batch.Type,
                FileName = batch.FileName,
                CreatedByName = batch.CreatedByName,
                CreatedAt = batch.CreatedAt,
                CommittedAt = batch.CommittedAt,
                Created = batch.CreatedCount,
                Updated = batch.UpdatedCount,
                Skipped = batch.SkippedCount,
                Failed = batch.FailedCount
            })
            .ToListAsync(cancellationToken);

        return new PaginatedResultDto<ImportHistoryItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<string?> FindUserNameAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId == null) return null;
        var user = await _users.GetByIdAsync(userId.Value);
        return user?.FullName;
    }

    private async Task<bool> ApplyCustomerAsync(
        StoredImportRow row,
        Dictionary<Guid, Customer> customers)
    {
        Customer customer;
        if (row.TargetId is { } id && customers.TryGetValue(id, out var existing))
        {
            customer = existing;
        }
        else if (row.TargetId == null)
        {
            customer = new Customer();
            await _customers.AddAsync(customer);
        }
        else
        {
            return false;
        }

        customer.CustomerCode = Value(row, "customerCode") ?? customer.CustomerCode;
        customer.Name = Value(row, "name") ?? customer.Name;
        customer.Address = Value(row, "address") ?? customer.Address;
        customer.ErpNextId = Value(row, "erpNextId") ?? customer.ErpNextId;
        customer.NameAr = Value(row, "nameAr") ?? customer.NameAr;
        customer.TaxId = Value(row, "taxId") ?? customer.TaxId;
        customer.ContactPerson = Value(row, "contactPerson") ?? customer.ContactPerson;
        customer.ContactPersonAr = Value(row, "contactPersonAr") ?? customer.ContactPersonAr;
        customer.Email = Value(row, "email") ?? customer.Email;
        customer.Phone = Value(row, "phone") ?? customer.Phone;
        customer.City = Value(row, "city") ?? customer.City;
        customer.CityAr = Value(row, "cityAr") ?? customer.CityAr;
        customer.AddressAr = Value(row, "addressAr") ?? customer.AddressAr;
        customer.DefaultCurrency = Value(row, "defaultCurrency") ?? customer.DefaultCurrency;
        customer.DefaultPaymentTermId = row.PaymentTermId ?? customer.DefaultPaymentTermId;

        return true;
    }

    private async Task<bool> ApplyMaterialAsync(
        StoredImportRow row,
        Dictionary<Guid, Material> materials)
    {
        Material material;
        if (row.TargetId is { } id && materials.TryGetValue(id, out var existing))
        {
            material = existing;
        }
        else if (row.TargetId == null)
        {
            material = new Material();
            await _materials.AddAsync(material);
        }
        else
        {
            return false;
        }

        material.ItemCode = Value(row, "itemCode") ?? material.ItemCode;
        material.Name = Value(row, "name") ?? material.Name;
        material.Category = Value(row, "category") ?? material.Category;
        material.ErpNextItemCode = Value(row, "erpNextItemCode") ?? material.ErpNextItemCode;
        material.Uom = Value(row, "uom") ?? (string.IsNullOrWhiteSpace(material.Uom) ? "kg" : material.Uom);

        if (ToDecimal(Value(row, "referencePricePerKg")) is { } price)
        {
            material.ReferencePricePerKg = price;
        }

        if (ToDecimal(Value(row, "defaultWeightKg")) is { } weight)
        {
            material.DefaultWeightKg = weight;
        }

        if (Value(row, "isActive") is { } active)
        {
            material.IsActive = string.Equals(active, "true", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool ApplyPrice(
        StoredImportRow row,
        Dictionary<Guid, Material> materials)
    {
        if (row.TargetId is not { } id || !materials.TryGetValue(id, out var material))
        {
            return false;
        }

        if (ToDecimal(Value(row, "referencePricePerKg")) is not { } price)
        {
            return false;
        }

        material.ReferencePricePerKg = price;
        return true;
    }

    private static string? Value(StoredImportRow row, string field)
        => row.Values.TryGetValue(field, out var value) ? value : null;

    private static decimal? ToDecimal(string? text)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static ImportSummaryDto Summarize(List<StoredImportRow> rows) => new()
    {
        Total = rows.Count,
        New = rows.Count(row => row.Status == "new"),
        Update = rows.Count(row => row.Status == "update"),
        Unchanged = rows.Count(row => row.Status == "unchanged"),
        Error = rows.Count(row => row.Status == "error")
    };

    private static ImportRowDto MapRow(StoredImportRow row) => new()
    {
        RowNumber = row.RowNumber,
        Status = row.Status,
        Errors = row.Errors,
        Values = row.Values,
        Existing = row.Existing,
        Changes = row.Changes
    };

    private static ImportCommitResultDto ResultOf(ImportBatch batch) => new()
    {
        ImportId = batch.Id,
        Created = batch.CreatedCount,
        Updated = batch.UpdatedCount,
        Skipped = batch.SkippedCount,
        Failed = batch.FailedCount
    };
}
