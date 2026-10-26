using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class CustomerOfferHistoryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateOnly IssueDate { get; set; }
    public OfferStatus Status { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EGP";
}
