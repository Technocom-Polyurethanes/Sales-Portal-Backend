using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class CatalogService : ICatalogService
{
    private readonly IRepository<PaymentTerm> _paymentTerms;
    private readonly IRepository<PackageType> _packageTypes;
    private readonly IRepository<BankAccount> _bankAccounts;
    private readonly IRepository<CompanySettings> _companySettings;
    private readonly IRepository<WorkspaceSettings> _workspaceSettings;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<User> _users;
    private readonly IRepository<Material> _materials;

    public CatalogService(
        IRepository<PaymentTerm> paymentTerms,
        IRepository<PackageType> packageTypes,
        IRepository<BankAccount> bankAccounts,
        IRepository<CompanySettings> companySettings,
        IRepository<WorkspaceSettings> workspaceSettings,
        IRepository<Customer> customers,
        IRepository<User> users,
        IRepository<Material> materials)
    {
        _paymentTerms = paymentTerms;
        _packageTypes = packageTypes;
        _bankAccounts = bankAccounts;
        _companySettings = companySettings;
        _workspaceSettings = workspaceSettings;
        _customers = customers;
        _users = users;
        _materials = materials;
    }

    public async Task<CatalogBootstrapDto> GetBootstrapAsync(CancellationToken cancellationToken)
    {
        var paymentTerms = await _paymentTerms.GetAll()
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

        var packageTypes = await _packageTypes.GetAll()
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

        var bankAccounts = await _bankAccounts.GetAll()
            .AsNoTracking()
            .Where(account => account.IsActive)
            .OrderBy(account => account.SortOrder)
            .Select(account => new BankAccountDto
            {
                Id = account.Id,
                Bank = account.Bank,
                BankAr = account.BankAr,
                Branch = account.Branch,
                BranchAr = account.BranchAr,
                AccountHolderName = account.AccountHolderName,
                AccountHolderNameAr = account.AccountHolderNameAr,
                AccountNumber = account.AccountNumber,
                Swift = account.Swift,
                Iban = account.Iban,
                Currency = account.Currency,
                SortOrder = account.SortOrder,
                IsActive = account.IsActive
            })
            .ToListAsync(cancellationToken);

        var company = await _companySettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var workspace = await _workspaceSettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return new CatalogBootstrapDto
        {
            PaymentTerms = paymentTerms,
            PackageTypes = packageTypes,
            BankAccounts = bankAccounts,
            CompanySettings = company == null ? null : MapCompany(company),
            WorkspaceSettings = workspace == null ? null : MapWorkspace(workspace)
        };
    }

    public async Task<OfferCatalog> GetOfferCatalogAsync(
        Guid? customerId,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var customer = customerId.HasValue
            ? await _customers.GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId.Value, cancellationToken)
            : null;

        var createdBy = createdByUserId.HasValue
            ? await _users.GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == createdByUserId.Value, cancellationToken)
            : null;

        var materials = await _materials.GetAll()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var packageTypes = await _packageTypes.GetAll()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var paymentTerms = await _paymentTerms.GetAll()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var bankAccounts = await _bankAccounts.GetAll()
            .AsNoTracking()
            .Where(account => account.IsActive)
            .OrderBy(account => account.SortOrder)
            .ToListAsync(cancellationToken);

        var companySettings = await _companySettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var workspaceSettings = await _workspaceSettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return new OfferCatalog
        {
            Customer = customer,
            CreatedBy = createdBy,
            Materials = materials,
            PackageTypes = packageTypes,
            PaymentTerms = paymentTerms,
            BankAccounts = bankAccounts,
            CompanySettings = companySettings,
            WorkspaceSettings = workspaceSettings
        };
    }

    private static CompanySettingsDto MapCompany(CompanySettings company)
    {
        return new CompanySettingsDto
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
        };
    }

    private static WorkspaceSettingsDto MapWorkspace(WorkspaceSettings workspace)
    {
        return new WorkspaceSettingsDto
        {
            Id = workspace.Id,
            RemindBeforeExpiry = workspace.RemindBeforeExpiry,
            DaysBeforeExpiry = workspace.DaysBeforeExpiry,
            DefaultCurrency = workspace.DefaultCurrency,
            DefaultTaxPercent = workspace.DefaultTaxPercent,
            DefaultPaymentTermId = workspace.DefaultPaymentTermId,
            DefaultDocumentLanguage = workspace.DefaultDocumentLanguage,
            ErpNextUrl = workspace.ErpNextUrl
        };
    }
}
