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

    /// <summary>updatedAt (default), createdAt, reference, total or issueDate.</summary>
    public string SortBy { get; set; } = "updatedAt";

    /// <summary>desc (default) or asc.</summary>
    public string SortDir { get; set; } = "desc";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
