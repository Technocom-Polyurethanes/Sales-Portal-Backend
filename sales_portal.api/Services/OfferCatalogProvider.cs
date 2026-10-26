using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Services;

public class OfferCatalogProvider
{
    private readonly AppDbContext _db;

    public OfferCatalogProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OfferCatalog> LoadAsync(OfferDraftDto draft, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == draft.CustomerId, cancellationToken);

        var paymentTerm = await _db.PaymentTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(pt => pt.Id == draft.PaymentTermId, cancellationToken);

        var materialIds = draft.Lines
            .Select(line => line.MaterialId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var materials = await _db.Materials
            .AsNoTracking()
            .Where(material => materialIds.Contains(material.Id))
            .ToListAsync(cancellationToken);

        var packageTypeIds = draft.Lines
            .SelectMany(line => line.Packaging)
            .Select(packaging => packaging.PackageTypeId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var packageTypes = await _db.PackageTypes
            .AsNoTracking()
            .Where(packageType => packageTypeIds.Contains(packageType.Id))
            .ToListAsync(cancellationToken);

        var bankAccounts = await _db.BankAccounts
            .AsNoTracking()
            .Where(account => account.IsActive)
            .OrderBy(account => account.SortOrder)
            .ToListAsync(cancellationToken);

        var companySettings = await _db.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var workspaceSettings = await _db.WorkspaceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        User? createdBy = null;
        if (draft.CreatedByUserId.HasValue)
        {
            createdBy = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == draft.CreatedByUserId.Value, cancellationToken);
        }

        return new OfferCatalog
        {
            Customer = customer,
            PaymentTerm = paymentTerm,
            Materials = materials,
            PackageTypes = packageTypes,
            BankAccounts = bankAccounts,
            CompanySettings = companySettings,
            WorkspaceSettings = workspaceSettings,
            CreatedBy = createdBy
        };
    }
}
