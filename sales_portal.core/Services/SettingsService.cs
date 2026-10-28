using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class SettingsService : ISettingsService
{
    private readonly IRepository<CompanySettings> _companySettings;
    private readonly IRepository<WorkspaceSettings> _workspaceSettings;

    public SettingsService(
        IRepository<CompanySettings> companySettings,
        IRepository<WorkspaceSettings> workspaceSettings)
    {
        _companySettings = companySettings;
        _workspaceSettings = workspaceSettings;
    }

    public async Task<SettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var company = await _companySettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var workspace = await _workspaceSettings.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return new SettingsDto
        {
            Company = company == null ? new CompanySettingsDto() : MapCompany(company),
            Workspace = workspace == null ? new WorkspaceSettingsDto() : MapWorkspace(workspace)
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
        }

        await _companySettings.SaveChangesAsync(cancellationToken);

        return await GetAsync(cancellationToken);
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

    private static WorkspaceSettingsDto MapWorkspace(WorkspaceSettings workspace) => new()
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
