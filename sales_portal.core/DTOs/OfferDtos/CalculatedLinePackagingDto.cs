namespace sales_portal.core.DTOs;

public class CalculatedLinePackagingDto
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
