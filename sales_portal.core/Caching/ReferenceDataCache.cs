using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using sales_portal.core.Live;

namespace sales_portal.core.Caching;

public sealed class ReferenceDataCache : IReferenceDataCache
{
    // Writes invalidate their regions straight away; the lifetime only bounds staleness when the
    // database is changed from outside this process.
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IChangeBroadcaster _changes;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _regions = new();

    public ReferenceDataCache(IMemoryCache cache, IChangeBroadcaster? changes = null)
    {
        _cache = cache;
        _changes = changes ?? NoChangeBroadcaster.Instance;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        IReadOnlyCollection<string> regions,
        Func<Task<T>> factory)
    {
        var cacheKey = $"reference-data:{key}";

        // Lazy makes concurrent first requests share one database load instead of each running it.
        var lazy = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            foreach (var region in regions)
            {
                entry.AddExpirationToken(new CancellationChangeToken(RegionSource(region).Token));
            }

            return new Lazy<Task<T>>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        })!;

        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            // A failed load must not be served again.
            _cache.Remove(cacheKey);
            throw;
        }
    }

    public void Invalidate(params string[] regions)
    {
        foreach (var region in regions)
        {
            if (_regions.TryRemove(region, out var source))
            {
                source.Cancel();
            }
        }

        // Every write invalidates the regions it touched once it is saved, so this is also the
        // moment to tell connected browsers. The entries above are already gone, which means a
        // browser that reloads straight away reads the new data and not a cached copy of the old.
        _changes.Publish(ChangeTopics.ForRegions(regions));
    }

    private CancellationTokenSource RegionSource(string region)
        => _regions.GetOrAdd(region, _ => new CancellationTokenSource());
}

/// <summary>Runs every load; used when no cache is registered (for example in unit tests).</summary>
public sealed class NoReferenceDataCache : IReferenceDataCache
{
    public static readonly NoReferenceDataCache Instance = new();

    public Task<T> GetOrCreateAsync<T>(
        string key,
        IReadOnlyCollection<string> regions,
        Func<Task<T>> factory) => factory();

    public void Invalidate(params string[] regions)
    {
    }
}
