namespace sales_portal.core.DTOs;

public class DocumentDeliveryPackagingSummaryDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal TotalWeightKg { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string DisplayTextAr { get; set; } = string.Empty;
}
