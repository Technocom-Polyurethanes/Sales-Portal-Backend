using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferDetailDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int SerialNumber { get; set; }
    public int Version { get; set; }
    public Guid? ParentOfferId { get; set; }
    public Guid CustomerId { get; set; }
    public CustomerLookupItemDto? Customer { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public int VersionsCount { get; set; }
    public string? Attention { get; set; }
    public string? TaxId { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid PaymentTermId { get; set; }
    public string? PaymentTermLabel { get; set; }
    public string Currency { get; set; } = "EGP";
    public DocumentLanguage DocumentLanguage { get; set; }
    public string? Notes { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public OfferStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal OrderDiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public bool OtherTaxEnabled { get; set; }
    public string OtherTaxName { get; set; } = string.Empty;
    public decimal OtherTaxPercent { get; set; }
    public decimal OtherTaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<OfferDraftLineDto> Lines { get; set; } = new();
    public List<OfferDraftDeliveryBatchDto> DeliveryBatches { get; set; } = new();
    public OfferDocumentDto? Document { get; set; }
}
