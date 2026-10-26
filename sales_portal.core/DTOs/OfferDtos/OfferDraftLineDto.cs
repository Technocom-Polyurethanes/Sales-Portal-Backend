namespace sales_portal.core.DTOs;

public class OfferDraftLineDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public Guid MaterialId { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool ShowSpecifications { get; set; }
    public string? CustomSpecifications { get; set; }
    public List<OfferDraftPackagingDto> Packaging { get; set; } = new();
}
