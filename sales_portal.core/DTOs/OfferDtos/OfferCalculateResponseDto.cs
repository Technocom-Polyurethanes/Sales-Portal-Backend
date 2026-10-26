namespace sales_portal.core.DTOs;

public class OfferCalculateResponseDto
{
    public OfferDraftDto Draft { get; set; } = new();
    public CalculatedTotalsDto Totals { get; set; } = new();
    public List<CalculatedOfferLineDto> Lines { get; set; } = new();
    public List<CalculatedDeliveryBatchDto> DeliveryBatches { get; set; } = new();
    public List<DeliveryCapDto> DeliveryCaps { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public OfferDocumentDto Document { get; set; } = new();
}
