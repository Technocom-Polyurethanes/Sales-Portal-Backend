using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class SettingsService : ISettingsService
{
    private readonly IRepository<CompanySettings> _companySettings;
    private readonly IRepository<WorkspaceSettings> _workspaceSettings;
    private readonly IRepository<BankAccount> _bankAccounts;

    public SettingsService(
        IRepository<CompanySettings> companySettings,
        IRepository<WorkspaceSettings> workspaceSettings,
        IRepository<BankAccount> bankAccounts)
    {
        _companySettings = companySettings;
        _workspaceSettings = workspaceSettings;
        _bankAccounts = bankAccounts;
    }

    public async Task<SettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var company = await _companySettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var workspace = await _workspaceSettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var bankAccounts = await LoadBankAccountsAsync(cancellationToken);

        return new SettingsDto
        {
            Company = company == null ? new CompanySettingsDto() : MapCompany(company),
            Workspace = workspace == null
                ? new WorkspaceSettingsDto { BankAccounts = bankAccounts }
                : MapWorkspace(workspace, bankAccounts)
        };
    }

    public async Task<SettingsDto> UpdateAsync(UpdateSettingsDto dto, CancellationToken cancellationToken)
    {
        if (dto.Company != null)
        {
            var company = await _companySettings.GetAll().FirstOrDefaultAsync(cancellationToken);
            if (company == null)
            {
                company = new CompanySettings();
                ApplyCompany(company, dto.Company);
                await _companySettings.AddAsync(company);
            }
            else
            {
                ApplyCompany(company, dto.Company);
                _companySettings.Update(company);
            }
        }

        if (dto.Workspace != null)
        {
            var workspace = await _workspaceSettings.GetAll().FirstOrDefaultAsync(cancellationToken);
            if (workspace == null)
            {
                workspace = new WorkspaceSettings();
                ApplyWorkspace(workspace, dto.Workspace);
                await _workspaceSettings.AddAsync(workspace);
            }
            else
            {
                ApplyWorkspace(workspace, dto.Workspace);
                _workspaceSettings.Update(workspace);
            }

            if (dto.Workspace.BankAccounts is { } bankAccounts)
            {
                await ApplyBankAccountsAsync(bankAccounts, cancellationToken);
            }
        }

        await _companySettings.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
    }

    private async Task<List<BankAccountDto>> LoadBankAccountsAsync(CancellationToken cancellationToken)
    {
        return await _bankAccounts.GetAll()
            .AsNoTracking()
            .OrderBy(account => account.SortOrder)
            .ThenBy(account => account.Bank)
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
    }

    private async Task ApplyBankAccountsAsync(
        IReadOnlyList<BankAccountDto> bankAccounts,
        CancellationToken cancellationToken)
    {
        ValidateBankAccounts(bankAccounts);

        var existing = await _bankAccounts.GetAll().ToListAsync(cancellationToken);
        var incomingIds = bankAccounts
            .Where(account => account.Id != Guid.Empty)
            .Select(account => account.Id)
            .ToHashSet();

        foreach (var removed in existing.Where(account => !incomingIds.Contains(account.Id)))
        {
            _bankAccounts.Remove(removed);
        }

        for (var index = 0; index < bankAccounts.Count; index++)
        {
            var source = bankAccounts[index];
            var target = source.Id == Guid.Empty
                ? null
                : existing.FirstOrDefault(account => account.Id == source.Id);

            if (target == null)
            {
                target = new BankAccount
                {
                    Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id
                };
                await _bankAccounts.AddAsync(target);
                existing.Add(target);
            }
            else
            {
                _bankAccounts.Update(target);
            }

            ApplyBankAccount(target, source, index);
        }
    }

    private static void ValidateBankAccounts(IReadOnlyList<BankAccountDto> bankAccounts)
    {
        var errors = new Dictionary<string, string[]>();

        for (var index = 0; index < bankAccounts.Count; index++)
        {
            var account = bankAccounts[index];
            var prefix = $"workspace.bankAccounts[{index}]";

            if (string.IsNullOrWhiteSpace(account.Bank))
            {
                errors[$"{prefix}.bank"] = ["Bank is required."];
            }

            if (string.IsNullOrWhiteSpace(account.Branch))
            {
                errors[$"{prefix}.branch"] = ["Branch is required."];
            }

            if (string.IsNullOrWhiteSpace(account.AccountNumber))
            {
                errors[$"{prefix}.accountNumber"] = ["Account number is required."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("One or more bank accounts are invalid.", errors);
        }
    }

    private static void ApplyBankAccount(BankAccount target, BankAccountDto source, int index)
    {
        target.Bank = source.Bank.Trim();
        target.BankAr = source.BankAr;
        target.Branch = source.Branch.Trim();
        target.BranchAr = source.BranchAr;
        target.AccountHolderName = source.AccountHolderName?.Trim() ?? string.Empty;
        target.AccountHolderNameAr = source.AccountHolderNameAr;
        target.AccountNumber = source.AccountNumber.Trim();
        target.Swift = source.Swift?.Trim() ?? string.Empty;
        target.Iban = source.Iban?.Trim() ?? string.Empty;
        target.Currency = string.IsNullOrWhiteSpace(source.Currency) ? "EGP" : source.Currency.Trim();
        target.SortOrder = source.SortOrder > 0 ? source.SortOrder : index + 1;
        target.IsActive = source.IsActive;
    }

    private static void ApplyCompany(CompanySettings target, CompanySettingsDto source)
    {
        target.Name = source.Name;
        target.NameAr = source.NameAr;
        target.Tagline = source.Tagline;
        target.TaglineAr = source.TaglineAr;
        target.FormCode = source.FormCode;
        target.ReferencePrefix = source.ReferencePrefix;
        target.Address = source.Address;
        target.Phone = source.Phone;
        target.Email = source.Email;
        target.LogoUrl = source.LogoUrl;
        target.LogoWidthMm = source.LogoWidthMm;
        target.LogoHeightMm = source.LogoHeightMm;
    }

    private static void ApplyWorkspace(WorkspaceSettings target, WorkspaceSettingsDto source)
    {
        target.RemindBeforeExpiry = source.RemindBeforeExpiry;
        target.DaysBeforeExpiry = source.DaysBeforeExpiry;
        target.DefaultCurrency = source.DefaultCurrency;
        target.DefaultTaxPercent = source.DefaultTaxPercent;
        target.DefaultPaymentTermId = source.DefaultPaymentTermId;
        target.DefaultDocumentLanguage = source.DefaultDocumentLanguage;
        target.ErpNextUrl = source.ErpNextUrl;
    }

    private static CompanySettingsDto MapCompany(CompanySettings company) => new()
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

    private static WorkspaceSettingsDto MapWorkspace(
        WorkspaceSettings workspace,
        List<BankAccountDto> bankAccounts) => new()
    {
        Id = workspace.Id,
        RemindBeforeExpiry = workspace.RemindBeforeExpiry,
        DaysBeforeExpiry = workspace.DaysBeforeExpiry,
        DefaultCurrency = workspace.DefaultCurrency,
        DefaultTaxPercent = workspace.DefaultTaxPercent,
        DefaultPaymentTermId = workspace.DefaultPaymentTermId,
        DefaultDocumentLanguage = workspace.DefaultDocumentLanguage,
        ErpNextUrl = workspace.ErpNextUrl,
        BankAccounts = bankAccounts
    };
}
