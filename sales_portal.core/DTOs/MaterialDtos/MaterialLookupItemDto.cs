namespace sales_portal.core.DTOs;

public class MaterialLookupItemDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? ReferencePricePerKg { get; set; }
    public string Uom { get; set; } = "kg";
    public Guid? DefaultPackageTypeId { get; set; }
    public string? DefaultPackageTypeName { get; set; }
    public string? DefaultPackageTypeNameAr { get; set; }
    public decimal DefaultWeightKg { get; set; }
    public bool IsActive { get; set; }
}
