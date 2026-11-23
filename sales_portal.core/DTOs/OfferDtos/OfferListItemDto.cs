using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferListItemDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public OfferType OfferType { get; set; }
    public int SerialNumber { get; set; }
    public int Version { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerNameAr { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Currency { get; set; } = "EGP";
    public OfferStatus Status { get; set; }
    public decimal Total { get; set; }
    public string? CreatedByName { get; set; }
    /// <summary>When this version was created. Never changes once the version exists.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the first version (v1) of this offer was created; the same for every version of the chain.</summary>
    public DateTimeOffset RootCreatedAt { get; set; }
    public int VersionsCount { get; set; }
    public string? LastModifiedByName { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public Guid? ParentOfferId { get; set; }
    public Guid RootOfferId { get; set; }
}
