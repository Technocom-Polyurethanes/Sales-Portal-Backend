using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class DocumentMetadataDto
{
    public string Reference { get; set; } = string.Empty;
    public int SerialNumber { get; set; }
    public int Version { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string PaymentTerm { get; set; } = string.Empty;
    public string PaymentTermAr { get; set; } = string.Empty;
    public string SalesRepresentative { get; set; } = string.Empty;
    public string SalesRepresentativeInitials { get; set; } = string.Empty;
    public OfferStatus Status { get; set; }
    public string Currency { get; set; } = "EGP";
    public string FormCode { get; set; } = string.Empty;
}
