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
