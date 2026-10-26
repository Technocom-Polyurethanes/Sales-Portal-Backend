using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class OfferQueryParamsDto
{
    public string? Search { get; set; }
    public OfferStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
