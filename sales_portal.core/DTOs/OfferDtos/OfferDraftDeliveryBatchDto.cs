namespace sales_portal.core.DTOs;

public class OfferDraftDeliveryBatchDto
{
    public Guid Id { get; set; }
    public int BatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public List<OfferDraftDeliveryAllocationDto> Allocations { get; set; } = new();
}
