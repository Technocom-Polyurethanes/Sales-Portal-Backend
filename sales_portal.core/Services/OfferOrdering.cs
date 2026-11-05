using sales_portal.domain.Models;

namespace sales_portal.core.Services;

/// <summary>
/// Orders offers by the number printed on them (issue year and month, then serial number, then
/// version) and never by the internal GUID. The reference restarts its sequence every month, so
/// the month has to come before the serial number.
/// </summary>
public static class OfferOrdering
{
    public static IOrderedQueryable<Offer> OrderByDisplayedId(this IQueryable<Offer> offers)
        => offers
            .OrderBy(o => o.IssueDate.Year)
            .ThenBy(o => o.IssueDate.Month)
            .ThenBy(o => o.SerialNumber)
            .ThenBy(o => o.Version);

    public static IOrderedQueryable<Offer> OrderByDisplayedIdDescending(this IQueryable<Offer> offers)
        => offers
            .OrderByDescending(o => o.IssueDate.Year)
            .ThenByDescending(o => o.IssueDate.Month)
            .ThenByDescending(o => o.SerialNumber)
            .ThenByDescending(o => o.Version);

    public static IOrderedEnumerable<Offer> OrderByDisplayedIdInMemory(this IEnumerable<Offer> offers)
        => offers
            .OrderBy(o => o.IssueDate.Year)
            .ThenBy(o => o.IssueDate.Month)
            .ThenBy(o => o.SerialNumber)
            .ThenBy(o => o.Version);
}
