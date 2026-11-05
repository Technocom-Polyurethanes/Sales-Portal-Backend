using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferQueryParamsDto
{
    public string? Search { get; set; }
    public OfferStatus? Status { get; set; }

    /// <summary>
    /// When true (the default) only the highest version of each reference is returned.
    /// </summary>
    public bool LatestOnly { get; set; } = true;

    /// <summary>
    /// id (default, the offer number: issue month, serial, version), updatedAt, createdAt,
    /// reference, total or issueDate.
    /// </summary>
    public string SortBy { get; set; } = "id";

    /// <summary>
    /// asc or desc. When omitted the offer number sorts ascending and every other column sorts
    /// descending.
    /// </summary>
    public string? SortDir { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
