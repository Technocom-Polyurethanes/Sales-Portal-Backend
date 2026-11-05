using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace sales_portal.core.Caching;

public sealed class ReferenceDataCache : IReferenceDataCache
{
    // Writes invalidate their regions straight away; the lifetime only bounds staleness when the
    // database is changed from outside this process.
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _regions = new();

    public ReferenceDataCache(IMemoryCache cache)
    {
        _cache = cache;
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
