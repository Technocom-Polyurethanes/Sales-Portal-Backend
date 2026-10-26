namespace sales_portal.core.DTOs;

public class CalculatedDeliveryAllocationDto
{
    public Guid Id { get; set; }
    public Guid OfferLineId { get; set; }
    public Guid OfferLinePackagingId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public decimal WeightPerPackageKg { get; set; }
    public int Packages { get; set; }
    public decimal TotalWeightKg { get; set; }
}
