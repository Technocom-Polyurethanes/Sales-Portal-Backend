using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferCalculationResultDto
{
    public OfferDraftDto Draft { get; set; } = new();
    public OfferCalculationTotalsDto Totals { get; set; } = new();
    public List<CalculatedOfferLineDto> Lines { get; set; } = new();
    public List<CalculatedDeliveryBatchDto> DeliveryBatches { get; set; } = new();
    public List<DeliveryPackagingCapDto> DeliveryCaps { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public OfferDocumentDto Document { get; set; } = new();
}

public class OfferCalculationTotalsDto
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
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Currency { get; set; } = "EGP";
    public DocumentLanguage DocumentLanguage { get; set; }
}

public class CalculatedOfferLineDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal RatePerKg { get; set; }
    public decimal QuantityKg { get; set; }
    public decimal Amount { get; set; }
    public bool ShowSpecifications { get; set; }
    public string? CustomSpecifications { get; set; }
    public string PackagingText { get; set; } = string.Empty;
    public string PackagingTextAr { get; set; } = string.Empty;
    public List<CalculatedPackagingDto> Packaging { get; set; } = new();
}

public class CalculatedPackagingDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public decimal WeightPerPackageKg { get; set; }
    public int Packages { get; set; }
    public decimal RowWeightKg { get; set; }
    public int ScheduledPackages { get; set; }
    public int RemainingPackages { get; set; }
}

public class CalculatedDeliveryBatchDto
{
    public Guid Id { get; set; }
    public int BatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public List<CalculatedDeliveryAllocationDto> Allocations { get; set; } = new();
    public List<CalculatedPackagingSummaryDto> PackagingSummary { get; set; } = new();
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
}

public class CalculatedDeliveryAllocationDto
{
    public Guid Id { get; set; }
    public Guid OfferLineId { get; set; }
    public Guid OfferLinePackagingId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public decimal WeightPerPackageKg { get; set; }
    public int Packages { get; set; }
    public decimal TotalWeightKg { get; set; }
}

public class CalculatedPackagingSummaryDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal TotalWeightKg { get; set; }
}

public class DeliveryPackagingCapDto
{
    public Guid OfferLineId { get; set; }
    public Guid OfferLinePackagingId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int OrderedPackages { get; set; }
    public int ScheduledPackages { get; set; }
    public int RemainingPackages { get; set; }
}
