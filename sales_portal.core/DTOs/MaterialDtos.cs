namespace sales_portal.core.DTOs;

public class MaterialLookupDto
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

public class MaterialDto : MaterialLookupDto
{
    public string? ErpNextItemCode { get; set; }
}

public class MaterialUpsertDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string? ErpNextItemCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? ReferencePricePerKg { get; set; }
    public string Uom { get; set; } = "kg";
    public Guid? DefaultPackageTypeId { get; set; }
    public decimal DefaultWeightKg { get; set; } = 210m;
    public bool IsActive { get; set; } = true;
}
