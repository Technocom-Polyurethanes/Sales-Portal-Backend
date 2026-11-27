using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using sales_portal.api.Notifications;
using sales_portal.core.Notifications;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

public class NotificationUnitTests
{
    [Theory]
    [InlineData(NotificationTypes.OfferCreated, true, true, false)]
    [InlineData(NotificationTypes.OfferRevised, true, true, false)]
    [InlineData(NotificationTypes.OfferStatusChanged, true, true, true)]
    [InlineData(NotificationTypes.OfferDeleted, true, true, true)]
    [InlineData(NotificationTypes.CustomerCreated, true, true, true)]
    [InlineData(NotificationTypes.CustomerDeleted, true, true, true)]
    [InlineData(NotificationTypes.MaterialCreated, true, true, true)]
    [InlineData(NotificationTypes.MaterialDeleted, true, true, true)]
    [InlineData(NotificationTypes.BankCreated, true, true, true)]
    [InlineData(NotificationTypes.BankDeleted, true, true, true)]
    [InlineData(NotificationTypes.ImportCommitted, true, true, true)]
    [InlineData(NotificationTypes.UserCreated, true, false, false)]
    [InlineData(NotificationTypes.UserDeactivated, true, false, false)]
    [InlineData(NotificationTypes.UserRoleChanged, true, false, false)]
    public void Audience_matrix_matches_the_contract(string type, bool admin, bool sales, bool accountant)
    {
        Assert.Equal(admin, NotificationAudience.Receives(type, UserRole.Admin));
        Assert.Equal(sales, NotificationAudience.Receives(type, UserRole.Sales));
        Assert.Equal(accountant, NotificationAudience.Receives(type, UserRole.Accountant));
    }

    [Fact]
    public void Audience_excludes_actor_inactive_and_unauthorized_users()
    {
        var actor = User("actor", UserRole.Admin);
        var admin = User("admin", UserRole.Admin);
        var sales = User("sales", UserRole.Sales);
        var accountant = User("accountant", UserRole.Accountant);
        var inactive = User("inactive", UserRole.Admin, false);

        var recipients = NotificationAudience.Recipients(
            [actor, admin, sales, accountant, inactive],
            NotificationTypes.OfferCreated,
            actor.Id);

        Assert.Equal([admin.Id, sales.Id], recipients.Select(user => user.Id));
    }

    [Fact]
    public async Task Broadcaster_routes_only_to_the_named_recipient()
    {
        var broadcaster = new NotificationBroadcaster();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        using var subscription = broadcaster.Subscribe(owner);
        using var otherSubscription = broadcaster.Subscribe(other);
        var dto = Dto(Guid.NewGuid());

        broadcaster.Publish(new NotificationEventPayload(
            dto,
            [new NotificationDelivery(owner, dto.Id)]));

        using var received = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Equal(dto.Id, (await subscription.WaitAsync(received.Token))!.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => otherSubscription.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Broadcaster_drops_a_duplicate_notification_id()
    {
        var broadcaster = new NotificationBroadcaster();
        var owner = Guid.NewGuid();
        using var subscription = broadcaster.Subscribe(owner);
        var payload = new NotificationEventPayload(
            Dto(Guid.NewGuid()),
            [new NotificationDelivery(owner, Guid.NewGuid())]);

        broadcaster.Publish(payload);
        using var received = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.NotNull(await subscription.WaitAsync(received.Token));

        broadcaster.Publish(payload);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => subscription.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Slow_reader_keeps_at_most_100_items()
    {
        var broadcaster = new NotificationBroadcaster();
        var owner = Guid.NewGuid();
        using var subscription = broadcaster.Subscribe(owner);

        for (var i = 0; i < 200; i++)
        {
            broadcaster.Publish(new NotificationEventPayload(
                Dto(Guid.NewGuid()),
                [new NotificationDelivery(owner, Guid.NewGuid())]));
        }

        using var received = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 100; i++)
        {
            Assert.NotNull(await subscription.WaitAsync(received.Token));
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => subscription.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Broadcaster_closes_a_user_stream_on_revocation()
    {
        var broadcaster = new NotificationBroadcaster();
        var owner = Guid.NewGuid();
        using var subscription = broadcaster.Subscribe(owner);

        broadcaster.CloseUserSubscriptions(owner);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Null(await subscription.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Closing_a_user_does_not_orphan_a_replacement_subscription()
    {
        var broadcaster = new NotificationBroadcaster();
        var owner = Guid.NewGuid();
        var closed = broadcaster.Subscribe(owner);
        broadcaster.CloseUserSubscriptions(owner);
        using var replacement = broadcaster.Subscribe(owner);
        closed.Dispose();

        var dto = Dto(Guid.NewGuid());
        broadcaster.Publish(new NotificationEventPayload(
            dto,
            [new NotificationDelivery(owner, dto.Id)]));

        using var received = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Equal(dto.Id, (await replacement.WaitAsync(received.Token))!.Id);
    }

    [Fact]
    public async Task Revocation_discards_notifications_already_buffered_for_a_stream()
    {
        var broadcaster = new NotificationBroadcaster();
        var owner = Guid.NewGuid();
        using var subscription = broadcaster.Subscribe(owner);
        broadcaster.Publish(new NotificationEventPayload(
            Dto(Guid.NewGuid()),
            [new NotificationDelivery(owner, Guid.NewGuid())]));

        broadcaster.CloseUserSubscriptions(owner);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Null(await subscription.WaitAsync(timeout.Token));
    }

    [Fact]
    public async Task Publisher_stores_one_row_per_eligible_recipient_and_announces_it()
    {
        var databaseName = $"notifications-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        using var provider = services.BuildServiceProvider();

        var actor = User("actor", UserRole.Admin);
        var admin = User("admin", UserRole.Admin);
        var sales = User("sales", UserRole.Sales);
        var accountant = User("accountant", UserRole.Accountant);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(actor, admin, sales, accountant);
            await db.SaveChangesAsync();
        }

        var broadcaster = new NotificationBroadcaster();
        using var subscription = broadcaster.Subscribe(admin.Id);
        var publisher = new NotificationPublisher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            broadcaster,
            new ConfigurationBuilder().Build(),
            NullLogger<NotificationPublisher>.Instance);

        await publisher.PublishAsync(new NotificationEvent(
            NotificationTypes.OfferCreated,
            actor.Id,
            "Actor",
            "offer",
            Guid.NewGuid(),
            "Loc/0001-2026/AA"));

        using var check = provider.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await context.Notifications.ToListAsync();
        Assert.Equal(2, rows.Count);

        using var received = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var delivered = await subscription.WaitAsync(received.Token);
        Assert.NotNull(delivered);
        Assert.Equal(NotificationTypes.OfferCreated, delivered.Type);

        Assert.Equal(new[] { admin.Id, sales.Id }.Order(), rows.Select(row => row.RecipientUserId).Order());
        Assert.DoesNotContain(rows, row => row.RecipientUserId == actor.Id);
        Assert.DoesNotContain(rows, row => row.RecipientUserId == accountant.Id);
    }

    [Fact]
    public async Task Publisher_never_throws_when_storage_is_unavailable()
    {
        var publisher = new NotificationPublisher(
            new ThrowingScopeFactory(),
            new NotificationBroadcaster(),
            new ConfigurationBuilder().Build(),
            NullLogger<NotificationPublisher>.Instance);

        await publisher.PublishAsync(new NotificationEvent(
            NotificationTypes.OfferCreated,
            Guid.NewGuid(),
            "Actor",
            "offer",
            Guid.NewGuid(),
            "Loc/0001-2026/AA"));
    }

    [Fact]
    public async Task Queued_publisher_hands_the_event_to_the_worker()
    {
        var publisher = new QueuedNotificationPublisher(NullLogger<QueuedNotificationPublisher>.Instance);
        var notification = new NotificationEvent(
            NotificationTypes.OfferCreated,
            Guid.NewGuid(),
            "Actor",
            "offer",
            Guid.NewGuid(),
            "Loc/0001-2026/AA");

        await publisher.PublishAsync(notification);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        NotificationEvent? queued = null;
        await foreach (var item in publisher.ReadAllAsync(cancellation.Token))
        {
            queued = item;
            break;
        }

        Assert.Same(notification, queued);
    }

    private static User User(string username, UserRole role, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        FullName = username,
        Initials = "TU",
        PasswordHash = "not-used",
        Role = role,
        IsActive = isActive
    };

    private static NotificationDto Dto(Guid id) => new()
    {
        Id = id,
        Type = NotificationTypes.OfferCreated,
        ActorName = "Actor",
        EntityType = "offer",
        EntityLabel = "Loc/0001-2026/AA",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("Database is down.");
    }
}
