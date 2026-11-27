using sales_portal.core.Caching;

namespace sales_portal.core.Live;

/// <summary>
/// The kinds of data a connected browser can be told has changed. A topic carries no data, only
/// "this changed, ask again": what a user may see still goes through the normal, authorized API.
/// </summary>
public static class ChangeTopics
{
    public const string Users = "users";
    public const string Customers = "customers";
    public const string Materials = "materials";

    /// <summary>Bank accounts. Also announced together with <see cref="Catalog"/>.</summary>
    public const string Banks = "banks";

    /// <summary>Payment terms, package types, bank accounts, company and workspace settings.</summary>
    public const string Catalog = "catalog";

    /// <summary>Offers, their lines, packaging and delivery batches.</summary>
    public const string Offers = "offers";

    /// <summary>The log of committed imports.</summary>
    public const string Imports = "imports";

    /// <summary>The admin audit log.</summary>
    public const string Audit = "audit";

    /// <summary>The tables whose direct database writes are announced to browsers.</summary>
    public static IReadOnlyCollection<string> MappedTables { get; } =
    [
        "users",
        "customers",
        "package_types",
        "materials",
        "bank_accounts",
        "payment_terms",
        "company_settings",
        "workspace_settings",
        "offers",
        "offer_lines",
        "offer_line_packagings",
        "offer_delivery_batches",
        "offer_delivery_allocations",
        "import_batches",
        "audit_logs"
    ];

    private static readonly IReadOnlyDictionary<string, (string[] Regions, string[] Topics)> TableMap =
        new Dictionary<string, (string[] Regions, string[] Topics)>(StringComparer.Ordinal)
        {
            ["users"] = Map([CacheRegions.Offers], Users),
            ["customers"] = Map([CacheRegions.Customers, CacheRegions.Offers]),
            ["materials"] = Map([CacheRegions.Materials, CacheRegions.Catalog, CacheRegions.Offers]),
            ["bank_accounts"] = Map([CacheRegions.Catalog]),
            ["package_types"] = Map([CacheRegions.Catalog]),
            ["payment_terms"] = Map([CacheRegions.Catalog]),
            ["company_settings"] = Map([CacheRegions.Catalog]),
            ["workspace_settings"] = Map([CacheRegions.Catalog]),
            ["offers"] = Map([CacheRegions.Offers]),
            ["offer_lines"] = Map([CacheRegions.Offers]),
            ["offer_line_packagings"] = Map([CacheRegions.Offers]),
            ["offer_delivery_batches"] = Map([CacheRegions.Offers]),
            ["offer_delivery_allocations"] = Map([CacheRegions.Offers]),
            ["import_batches"] = Map([], Imports),
            ["audit_logs"] = Map([], Audit)
        };

    /// <summary>
    /// Resolves the regions and the complete topic set for a table. Unknown tables resolve to
    /// nothing, so a notification from an unrelated table is ignored.
    /// </summary>
    public static bool TryResolveTable(
        string table,
        out IReadOnlyCollection<string> regions,
        out IReadOnlyCollection<string> topics)
    {
        if (table is not null && TableMap.TryGetValue(table, out var mapping))
        {
            regions = mapping.Regions;
            topics = mapping.Topics;
            return true;
        }

        regions = Array.Empty<string>();
        topics = Array.Empty<string>();
        return false;
    }

    private static (string[] Regions, string[] Topics) Map(string[] regions, params string[] extras) =>
        (regions, ForRegions(regions).Union(extras, StringComparer.Ordinal).ToArray());

    /// <summary>
    /// The topics behind cache regions. Every write already invalidates the regions it touched
    /// after it was saved (and committed), so announcing from there means a browser is only told
    /// about data that is really stored, and never reads a cache that still holds the old copy.
    /// </summary>
    public static IReadOnlyCollection<string> ForRegions(IEnumerable<string> regions)
    {
        var topics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in regions)
        {
            switch (region)
            {
                case CacheRegions.Customers:
                    topics.Add(Customers);
                    break;
                case CacheRegions.Materials:
                    topics.Add(Materials);
                    break;
                case CacheRegions.Catalog:
                    topics.Add(Catalog);
                    topics.Add(Banks);
                    break;
                case CacheRegions.Offers:
                    topics.Add(Offers);
                    break;
            }
        }

        return topics;
    }
}
