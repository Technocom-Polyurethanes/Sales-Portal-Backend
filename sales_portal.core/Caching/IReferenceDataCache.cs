namespace sales_portal.core.Caching;

/// <summary>Named groups of cached data. A write to a group drops every entry that depends on it.</summary>
public static class CacheRegions
{
    public const string Customers = "customers";
    public const string Materials = "materials";

    /// <summary>Payment terms, package types, bank accounts, company and workspace settings.</summary>
    public const string Catalog = "catalog";

    /// <summary>Offers, used by data that carries offer aggregates (customer totals and counts).</summary>
    public const string Offers = "offers";
}

/// <summary>
/// In-memory cache for reference data that is read on every offer (customers, materials, settings)
/// and rarely written. Entries belong to one or more regions and are dropped when any of them is
/// invalidated. Loaders run with <see cref="CancellationToken.None"/> because the result is shared
/// by every request waiting for it.
/// </summary>
public interface IReferenceDataCache
{
    Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> regions, Func<Task<T>> factory);

    void Invalidate(params string[] regions);
}
