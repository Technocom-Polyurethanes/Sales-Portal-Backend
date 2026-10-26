namespace sales_portal.core.DTOs;

public class DeliveryPackagingSummaryDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal TotalWeightKg { get; set; }
}
