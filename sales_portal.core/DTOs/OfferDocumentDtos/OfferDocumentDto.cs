using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferDocumentDto
{
    public DocumentHeaderDto Header { get; set; } = new();
    public DocumentBillToDto BillTo { get; set; } = new();
    public DocumentMetadataDto Metadata { get; set; } = new();
    public List<DocumentLineItemDto> Items { get; set; } = new();
    public DocumentTotalsDto Totals { get; set; } = new();
    public List<DocumentDeliveryBatchRowDto> DeliverySchedule { get; set; } = new();
    public DocumentDeliveryTotalsRowDto DeliveryTotals { get; set; } = new();
    public List<DocumentBankAccountDto> BankAccounts { get; set; } = new();
    public string? Notes { get; set; }
    public DocumentLanguage Language { get; set; }
    public string Currency { get; set; } = "EGP";
    public string FormCode { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}
