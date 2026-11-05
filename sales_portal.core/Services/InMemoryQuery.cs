using sales_portal.core.DTOs;

namespace sales_portal.core.Services;

/// <summary>Search and paging over lists that are already cached in memory.</summary>
internal static class InMemoryQuery
{
    /// <summary>Case-insensitive "contains" over the given fields, like the SQL search it replaces.</summary>
    public static IReadOnlyList<T> Search<T>(
        IReadOnlyList<T> source,
        string? search,
        params Func<T, string?>[] fields)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return source;
        }

        var term = search.Trim();
        return source
            .Where(item => fields.Any(field =>
                field(item)?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
            .ToList();
    }

    public static PaginatedResultDto<T> Page<T>(IReadOnlyList<T> source, int page, int pageSize)
        => new()
        {
            Items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = source.Count
        };
}
