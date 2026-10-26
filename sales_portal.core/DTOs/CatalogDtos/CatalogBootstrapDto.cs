namespace sales_portal.core.DTOs;

public class CatalogBootstrapDto
{
    public List<PaymentTermDto> PaymentTerms { get; set; } = new();
    public List<PackageTypeDto> PackageTypes { get; set; } = new();
    public List<BankAccountDto> BankAccounts { get; set; } = new();
    public CompanySettingsDto? CompanySettings { get; set; }
    public WorkspaceSettingsDto? WorkspaceSettings { get; set; }
}
