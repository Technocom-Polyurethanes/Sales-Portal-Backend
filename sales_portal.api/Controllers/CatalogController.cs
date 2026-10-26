using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly AppDbContext _db;

    public CatalogController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("bootstrap")]
    public async Task<ActionResult<CatalogBootstrapDto>> Bootstrap(CancellationToken cancellationToken)
    {
        var paymentTerms = await _db.PaymentTerms
            .AsNoTracking()
            .Where(pt => pt.IsActive)
            .OrderBy(pt => pt.Days)
            .ThenBy(pt => pt.Label)
            .Select(pt => new PaymentTermDto
            {
                Id = pt.Id,
                Code = pt.Code,
                Label = pt.Label,
                LabelAr = pt.LabelAr,
                Days = pt.Days,
                IsActive = pt.IsActive
            })
            .ToListAsync(cancellationToken);

        var packageTypes = await _db.PackageTypes
            .AsNoTracking()
            .Where(pt => pt.IsActive)
            .OrderBy(pt => pt.Name)
            .Select(pt => new PackageTypeDto
            {
                Id = pt.Id,
                Name = pt.Name,
                NameAr = pt.NameAr,
                IsActive = pt.IsActive
            })
            .ToListAsync(cancellationToken);

        var bankAccounts = await _db.BankAccounts
            .AsNoTracking()
            .Where(account => account.IsActive)
            .OrderBy(account => account.SortOrder)
            .Select(account => new BankAccountDto
            {
                Id = account.Id,
                Bank = account.Bank,
                Branch = account.Branch,
                AccountHolderName = account.AccountHolderName,
                AccountNumber = account.AccountNumber,
                Swift = account.Swift,
                Iban = account.Iban,
                Currency = account.Currency,
                SortOrder = account.SortOrder,
                IsActive = account.IsActive
            })
            .ToListAsync(cancellationToken);

        var company = await _db.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var workspace = await _db.WorkspaceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new CatalogBootstrapDto
        {
            PaymentTerms = paymentTerms,
            PackageTypes = packageTypes,
            BankAccounts = bankAccounts,
            CompanySettings = company == null
                ? null
                : new CompanySettingsDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    NameAr = company.NameAr,
                    Tagline = company.Tagline,
                    TaglineAr = company.TaglineAr,
                    FormCode = company.FormCode,
                    ReferencePrefix = company.ReferencePrefix,
                    Address = company.Address,
                    Phone = company.Phone,
                    Email = company.Email,
                    LogoUrl = company.LogoUrl,
                    LogoWidthMm = company.LogoWidthMm,
                    LogoHeightMm = company.LogoHeightMm
                },
            WorkspaceSettings = workspace == null
                ? null
                : new WorkspaceSettingsDto
                {
                    Id = workspace.Id,
                    RemindBeforeExpiry = workspace.RemindBeforeExpiry,
                    DaysBeforeExpiry = workspace.DaysBeforeExpiry,
                    DefaultCurrency = workspace.DefaultCurrency,
                    DefaultTaxPercent = workspace.DefaultTaxPercent,
                    DefaultPaymentTermId = workspace.DefaultPaymentTermId,
                    DefaultDocumentLanguage = workspace.DefaultDocumentLanguage,
                    ErpNextUrl = workspace.ErpNextUrl
                }
        });
    }
}
