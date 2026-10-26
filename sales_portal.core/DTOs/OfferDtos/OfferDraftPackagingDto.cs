namespace sales_portal.core.DTOs;

public class OfferDraftPackagingDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public Guid PackageTypeId { get; set; }
    public decimal WeightPerPackageKg { get; set; }
    public int Packages { get; set; }
}
