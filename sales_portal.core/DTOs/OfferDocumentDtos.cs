using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferDocumentDto
{
    public OfferDocumentHeaderDto Header { get; set; } = new();
    public OfferDocumentCustomerDto BillTo { get; set; } = new();
    public OfferDocumentMetadataDto Metadata { get; set; } = new();
    public List<OfferDocumentLineDto> Items { get; set; } = new();
    public OfferDocumentTotalsDto Totals { get; set; } = new();
    public List<OfferDocumentDeliveryBatchDto> DeliverySchedule { get; set; } = new();
    public OfferDocumentDeliveryTotalsDto DeliveryTotals { get; set; } = new();
    public List<OfferDocumentBankAccountDto> BankAccounts { get; set; } = new();
    public string? Notes { get; set; }
    public DocumentLanguage Language { get; set; }
    public string Currency { get; set; } = "EGP";
    public string FormCode { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}

public class OfferDocumentHeaderDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string TaglineAr { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public decimal LogoWidthMm { get; set; }
    public decimal LogoHeightMm { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentTitleAr { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class OfferDocumentCustomerDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? ContactPerson { get; set; }
    public string? Attention { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class OfferDocumentMetadataDto
{
    public string Reference { get; set; } = string.Empty;
    public int SerialNumber { get; set; }
    public int Version { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string PaymentTerm { get; set; } = string.Empty;
    public string PaymentTermAr { get; set; } = string.Empty;
    public string SalesRepresentative { get; set; } = string.Empty;
    public string SalesRepresentativeInitials { get; set; } = string.Empty;
    public OfferStatus Status { get; set; }
    public string Currency { get; set; } = "EGP";
    public string FormCode { get; set; } = string.Empty;
}

public class OfferDocumentLineDto
{
    public Guid OfferLineId { get; set; }
    public int SortOrder { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal QuantityKg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal Amount { get; set; }
    public bool ShowSpecifications { get; set; }
    public string? CustomSpecifications { get; set; }
    public string PackagingText { get; set; } = string.Empty;
    public string PackagingTextAr { get; set; } = string.Empty;
    public List<OfferDocumentPackagingRowDto> Packaging { get; set; } = new();
}

public class OfferDocumentPackagingRowDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal WeightPerPackageKg { get; set; }
    public decimal RowWeightKg { get; set; }
}

public class OfferDocumentTotalsDto
{
    public decimal Subtotal { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal NetAfterDiscount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public bool OtherTaxEnabled { get; set; }
    public string OtherTaxName { get; set; } = string.Empty;
    public decimal OtherTaxPercent { get; set; }
    public decimal OtherTaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EGP";
}

public class OfferDocumentDeliveryBatchDto
{
    public int BatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public List<OfferDocumentDeliveryMaterialDto> Materials { get; set; } = new();
    public List<OfferDocumentPackagingSummaryDto> PackagingSummary { get; set; } = new();
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
}

public class OfferDocumentDeliveryMaterialDto
{
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public List<OfferDocumentScheduledPackagingDto> Packaging { get; set; } = new();
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
}

public class OfferDocumentScheduledPackagingDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal WeightPerPackageKg { get; set; }
    public decimal TotalWeightKg { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string DisplayTextAr { get; set; } = string.Empty;
}

public class OfferDocumentPackagingSummaryDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal TotalWeightKg { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string DisplayTextAr { get; set; } = string.Empty;
}

public class OfferDocumentDeliveryTotalsDto
{
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
    public List<OfferDocumentPackagingSummaryDto> PackagingSummary { get; set; } = new();
}

public class OfferDocumentBankAccountDto
{
    public string Bank { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Swift { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
}
