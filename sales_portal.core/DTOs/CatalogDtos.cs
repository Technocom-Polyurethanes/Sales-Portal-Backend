using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class CatalogBootstrapDto
{
    public List<PaymentTermDto> PaymentTerms { get; set; } = new();
    public List<PackageTypeDto> PackageTypes { get; set; } = new();
    public List<BankAccountDto> BankAccounts { get; set; } = new();
    public CompanySettingsDto? CompanySettings { get; set; }
    public WorkspaceSettingsDto? WorkspaceSettings { get; set; }
}

public class PaymentTermDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public int Days { get; set; }
    public bool IsActive { get; set; }
}

public class PackageTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class BankAccountDto
{
    public Guid Id { get; set; }
    public string Bank { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Swift { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CompanySettingsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string TaglineAr { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public string ReferencePrefix { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public decimal LogoWidthMm { get; set; }
    public decimal LogoHeightMm { get; set; }
}

public class WorkspaceSettingsDto
{
    public Guid Id { get; set; }
    public bool RemindBeforeExpiry { get; set; }
    public int DaysBeforeExpiry { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
    public decimal DefaultTaxPercent { get; set; }
    public Guid? DefaultPaymentTermId { get; set; }
    public DocumentLanguage DefaultDocumentLanguage { get; set; }
    public string? ErpNextUrl { get; set; }
}
