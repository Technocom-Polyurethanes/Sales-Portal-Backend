namespace sales_portal.core.DTOs;

public class CustomerListItemDto : CustomerLookupItemDto
{
    public string? ErpNextId { get; set; }
    public string Address { get; set; } = string.Empty;
    public Guid? DefaultPaymentTermId { get; set; }
    public string? DefaultPaymentTermLabel { get; set; }
    public int OffersCount { get; set; }
    public decimal TotalValue { get; set; }
    public DateOnly? LastOfferDate { get; set; }
}
