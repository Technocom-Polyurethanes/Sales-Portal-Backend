namespace sales_portal.core.DTOs;

public class DocumentLinePackagingDto
{
    public Guid PackageTypeId { get; set; }
    public string PackageTypeName { get; set; } = string.Empty;
    public string PackageTypeNameAr { get; set; } = string.Empty;
    public int Packages { get; set; }
    public decimal WeightPerPackageKg { get; set; }
    public decimal RowWeightKg { get; set; }
}
