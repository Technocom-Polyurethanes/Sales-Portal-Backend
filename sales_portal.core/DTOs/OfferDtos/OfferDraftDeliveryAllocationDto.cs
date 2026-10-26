namespace sales_portal.core.DTOs;

public class OfferDraftDeliveryAllocationDto
{
    public Guid Id { get; set; }
    public Guid OfferLineId { get; set; }
    public Guid OfferLinePackagingId { get; set; }
    public Guid MaterialId { get; set; }
    public Guid PackageTypeId { get; set; }
    public int Packages { get; set; }
}
