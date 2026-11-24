using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using sales_portal.core.Caching;
using sales_portal.core.DTOs;
using sales_portal.core.Live;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using Xunit;

namespace SalesPortal.Tests;

// Live updates: a write announces what it changed, and connected browsers hear about it.
public class LiveUpdateTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static string[] Sorted(IEnumerable<string> topics)
        => topics.OrderBy(topic => topic, StringComparer.Ordinal).ToArray();

    [Fact]
    public void Cache_regions_map_to_the_topics_a_browser_listens_for()
    {
        Assert.Equal(new[] { "customers" }, Sorted(ChangeTopics.ForRegions(new[] { CacheRegions.Customers })));
        Assert.Equal(new[] { "materials" }, Sorted(ChangeTopics.ForRegions(new[] { CacheRegions.Materials })));
        Assert.Equal(new[] { "offers" }, Sorted(ChangeTopics.ForRegions(new[] { CacheRegions.Offers })));
        // Bank accounts live in the catalog region, and the settings screens listen for both.
        Assert.Equal(new[] { "banks", "catalog" }, Sorted(ChangeTopics.ForRegions(new[] { CacheRegions.Catalog })));
        Assert.Empty(ChangeTopics.ForRegions(new[] { "unknown" }));
    }

    [Fact]
    public async Task A_subscriber_receives_what_was_published()
    {
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();

        hub.Publish(new[] { ChangeTopics.Offers });

        var topics = await subscription.WaitAsync(Patience, CancellationToken.None);
        Assert.Equal(new[] { "offers" }, Sorted(topics));
    }

    [Fact]
    public async Task A_waiting_subscriber_wakes_up_when_something_is_published()
    {
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();

        var waiting = subscription.WaitAsync(Patience, CancellationToken.None);
        Assert.False(waiting.IsCompleted);
        hub.Publish(new[] { ChangeTopics.Customers });

        Assert.Equal(new[] { "customers" }, Sorted(await waiting));
    }

    [Fact]
    public async Task Announcements_that_pile_up_are_delivered_as_one_batch()
    {
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();

        hub.Publish(new[] { ChangeTopics.Offers });
        hub.Publish(new[] { ChangeTopics.Offers, ChangeTopics.Customers });
        hub.Publish(new[] { ChangeTopics.Customers });

        var batch = await subscription.WaitAsync(Patience, CancellationToken.None);
        Assert.Equal(new[] { "customers", "offers" }, Sorted(batch));

        var next = await subscription.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.Empty(next);
    }

    [Fact]
    public async Task Waiting_with_nothing_published_returns_empty_so_the_caller_can_send_a_keep_alive()
    {
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();

        var topics = await subscription.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.Empty(topics);
    }

    [Fact]
    public async Task Every_subscriber_gets_its_own_copy_and_a_disposed_one_is_forgotten()
    {
        var hub = new ChangeBroadcaster();
        using var first = hub.Subscribe();
        var second = hub.Subscribe();
        Assert.Equal(2, hub.SubscriberCount);

        hub.Publish(new[] { ChangeTopics.Users });
        Assert.Equal(new[] { "users" }, Sorted(await first.WaitAsync(Patience, CancellationToken.None)));
        Assert.Equal(new[] { "users" }, Sorted(await second.WaitAsync(Patience, CancellationToken.None)));

        second.Dispose();
        Assert.Equal(1, hub.SubscriberCount);
    }

    [Fact]
    public async Task Waiting_stops_when_the_connection_is_cancelled()
    {
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => subscription.WaitAsync(Patience, cancellation.Token));
    }

    [Fact]
    public async Task A_subscriber_that_never_reads_does_not_pile_up_memory()
    {
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();

        for (var i = 0; i < 20_000; i++)
        {
            hub.Publish(new[] { i % 2 == 0 ? ChangeTopics.Offers : ChangeTopics.Customers });
        }

        var batch = await subscription.WaitAsync(Patience, CancellationToken.None);
        Assert.Equal(2, batch.Count);
    }

    [Fact]
    public async Task Invalidating_the_cache_announces_the_topics_after_the_old_copy_is_gone()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var loads = 0;
        var loadsSeenWhenAnnounced = -1;
        ReferenceDataCache? cache = null;

        Task<int> Load() => Task.FromResult(++loads);

        var recorder = new RecordingBroadcaster(_ =>
        {
            // A browser that reloads the moment it is told must not be served the old cached copy.
            var before = loads;
            cache!.GetOrCreateAsync("k", new[] { CacheRegions.Customers }, Load).GetAwaiter().GetResult();
            loadsSeenWhenAnnounced = loads - before;
        });
        cache = new ReferenceDataCache(memory, recorder);

        await cache.GetOrCreateAsync("k", new[] { CacheRegions.Customers }, Load);
        await cache.GetOrCreateAsync("k", new[] { CacheRegions.Customers }, Load);
        Assert.Equal(1, loads);

        cache.Invalidate(CacheRegions.Customers);

        Assert.Equal(1, loadsSeenWhenAnnounced);
        Assert.Equal(new[] { "customers" }, Sorted(recorder.Topics));
    }

    [Fact]
    public void The_cache_still_works_without_a_broadcaster()
    {
        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()));

        cache.Invalidate(CacheRegions.Customers);
    }

    [Fact]
    public async Task Saving_a_bank_account_announces_banks_and_catalog()
    {
        using var db = new TestDatabase();
        var hub = new ChangeBroadcaster();
        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()), hub);
        using var subscription = hub.Subscribe();
        var service = TestFactory.CreateBankAccountService(db.Context, cache);

        await service.CreateAsync(
            new UpsertBankAccountDto { Bank = "SAIB", Branch = "Shubra", AccountNumber = "30203004433" },
            CancellationToken.None);

        var topics = await subscription.WaitAsync(Patience, CancellationToken.None);
        Assert.Equal(new[] { "banks", "catalog" }, Sorted(topics));
    }

    [Fact]
    public async Task Creating_a_user_announces_users()
    {
        using var db = new TestDatabase();
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();
        var service = new UserService(new Repository<User>(db.Context), hub);

        await service.CreateAsync(
            new CreateUserDto
            {
                Username = "live-user",
                FullName = "Live User",
                Initials = "lu",
                Email = "live@example.com",
                Role = UserRole.Sales,
                Password = "correct-horse"
            },
            CancellationToken.None);

        var topics = await subscription.WaitAsync(Patience, CancellationToken.None);
        Assert.Equal(new[] { "users" }, Sorted(topics));
    }

    [Fact]
    public async Task A_failed_write_announces_nothing()
    {
        using var db = new TestDatabase();
        var hub = new ChangeBroadcaster();
        using var subscription = hub.Subscribe();
        var service = new UserService(new Repository<User>(db.Context), hub);

        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync(
            new CreateUserDto { Username = "x" },
            CancellationToken.None));

        var topics = await subscription.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        Assert.Empty(topics);
    }

    private sealed class RecordingBroadcaster : IChangeBroadcaster
    {
        private readonly Action<IReadOnlyCollection<string>> _onPublish;

        public RecordingBroadcaster(Action<IReadOnlyCollection<string>> onPublish)
        {
            _onPublish = onPublish;
        }

        public List<string> Topics { get; } = new();

        public void Publish(IReadOnlyCollection<string> topics)
        {
            Topics.AddRange(topics);
            _onPublish(topics);
        }

        public IChangeSubscription Subscribe() => throw new NotSupportedException();
    }
}

// The endpoint a browser connects to.
[Collection(ProcessEnvironment.Name)]
public class EventsEndpointTests : IClassFixture<SalesPortalApiFactory>
{
    private readonly SalesPortalApiFactory _factory;

    public EventsEndpointTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_stream_requires_a_signed_in_user()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Sales")]
    [InlineData("Accountant")]
    public async Task Every_role_may_listen(string role)
    {
        using var client = _factory.CreateAuthenticatedClient(role: role);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/events"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_connected_browser_is_told_when_something_changes()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/events"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token));

        var opening = await ReadEventAsync(reader, cancellation.Token);
        Assert.Contains("event: ready", opening);

        // The stream is subscribed by the time it said "ready", so this cannot be missed.
        _factory.Services.GetRequiredService<IChangeBroadcaster>()
            .Publish(new[] { ChangeTopics.Offers });

        var change = await ReadEventAsync(reader, cancellation.Token);
        Assert.Contains("event: change", change);
        Assert.Contains("data: {\"topics\":[\"offers\"]}", change);
    }

    /// <summary>Reads one event: every line up to and including its data line.</summary>
    private static async Task<string> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("The stream ended before the event arrived.");
            lines.Add(line);
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                return string.Join("\n", lines);
            }
        }
    }
}
