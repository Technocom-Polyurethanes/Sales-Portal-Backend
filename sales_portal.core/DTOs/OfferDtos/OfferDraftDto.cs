using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferDraftDto
{
    public Guid? Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int SerialNumber { get; set; }
    public int Version { get; set; } = 1;
    public Guid? ParentOfferId { get; set; }

    public Guid CustomerId { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public string? Attention { get; set; }
    public string? TaxId { get; set; }
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? DueDate { get; set; }
    public Guid PaymentTermId { get; set; }
    public string Currency { get; set; } = "EGP";
    public DocumentLanguage DocumentLanguage { get; set; } = DocumentLanguage.En;
    public string? Notes { get; set; }
    public string? FormCode { get; set; }
    public OfferStatus Status { get; set; } = OfferStatus.Draft;

    public decimal OrderDiscount { get; set; }
    public decimal OrderDiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public bool OtherTaxEnabled { get; set; }
    public string? OtherTaxName { get; set; }
    public decimal OtherTaxPercent { get; set; }

    public List<OfferDraftLineDto> Lines { get; set; } = new();
    public List<OfferDraftDeliveryBatchDto> DeliveryBatches { get; set; } = new();
}
