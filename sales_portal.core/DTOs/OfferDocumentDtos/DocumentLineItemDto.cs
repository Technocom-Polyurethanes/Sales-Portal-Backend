namespace sales_portal.core.DTOs;

public class DocumentLineItemDto
{
    public Guid OfferLineId { get; set; }
    public int SortOrder { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal QuantityKg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal Amount { get; set; }
    public bool ShowSpecifications { get; set; }
    public string? CustomSpecifications { get; set; }
    public string PackagingText { get; set; } = string.Empty;
    public string PackagingTextAr { get; set; } = string.Empty;
    public List<DocumentLinePackagingDto> Packaging { get; set; } = new();
}
