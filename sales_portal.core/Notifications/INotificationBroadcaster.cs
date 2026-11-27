using System.Collections.Concurrent;
using System.Threading.Channels;

namespace sales_portal.core.Notifications;

/// <summary>Routes stored notifications to the open streams of their recipient only.</summary>
public interface INotificationBroadcaster
{
    INotificationSubscription Subscribe(Guid userId);

    void Publish(NotificationEventPayload payload);

    /// <summary>Ends every stream of a user whose account was deactivated or changed role.</summary>
    void CloseUserSubscriptions(Guid userId);
}

/// <summary>One open SSE stream, addressed to one user.</summary>
public interface INotificationSubscription : IDisposable
{
    /// <summary>Waits for the next notification; null means the stream was closed by the server.</summary>
    Task<NotificationDto?> WaitAsync(CancellationToken cancellationToken);
}

public sealed class NotificationBroadcaster : INotificationBroadcaster
{
    private const int DedupeLimit = 100;
    private const int QueueLimit = 100;

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Subscription, byte>> _users = new();

    public int SubscriberCount => _users.Values.Sum(subscriptions => subscriptions.Count);

    public INotificationSubscription Subscribe(Guid userId)
    {
        var subscription = new Subscription(this, userId);
        _users.GetOrAdd(userId, _ => new()).TryAdd(subscription, 0);
        return subscription;
    }

    public void Publish(NotificationEventPayload payload)
    {
        foreach (var delivery in payload.Recipients)
        {
            if (!_users.TryGetValue(delivery.UserId, out var subscriptions))
            {
                continue;
            }

            var notification = payload.Notification with { Id = delivery.NotificationId };
            foreach (var subscription in subscriptions.Keys)
            {
                subscription.Add(notification);
            }
        }
    }

    public void CloseUserSubscriptions(Guid userId)
    {
        // Keep the bucket: replacing it can orphan a subscription that is being disposed or added.
        if (_users.TryGetValue(userId, out var subscriptions))
        {
            foreach (var subscription in subscriptions.Keys)
            {
                subscription.Close();
            }
        }
    }

    private sealed class Subscription : INotificationSubscription
    {
        private readonly NotificationBroadcaster _owner;
        private readonly Guid _userId;
        private readonly object _gate = new();
        private volatile bool _closed;
        private readonly Queue<Guid> _recent = new();
        private readonly HashSet<Guid> _seen = new();
        private readonly Channel<NotificationDto> _channel =
            Channel.CreateBounded<NotificationDto>(new BoundedChannelOptions(QueueLimit)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        public Subscription(NotificationBroadcaster owner, Guid userId)
        {
            _owner = owner;
            _userId = userId;
        }

        public void Add(NotificationDto notification)
        {
            lock (_gate)
            {
                // A revoked stream must not buffer anything else, even if a notice arrives later.
                if (_closed) return;

                // Dual delivery (pg_notify plus the in-process fallback) must never toast twice.
                if (!_seen.Add(notification.Id))
                {
                    return;
                }

                _recent.Enqueue(notification.Id);
                if (_recent.Count > DedupeLimit)
                {
                    _seen.Remove(_recent.Dequeue());
                }
            }

            _channel.Writer.TryWrite(notification);
        }

        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
            }
            _channel.Writer.TryComplete();
        }

        public async Task<NotificationDto?> WaitAsync(CancellationToken cancellationToken)
        {
            try
            {
                var notification = await _channel.Reader.ReadAsync(cancellationToken)
                    .ConfigureAwait(false);
                // Completing the channel still drains buffered items; a revoked stream gets none.
                return _closed ? null : notification;
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            Close();
            if (_owner._users.TryGetValue(_userId, out var subscriptions))
            {
                subscriptions.TryRemove(this, out _);
            }
        }
    }
}

/// <summary>No-op broadcaster for tests and hosts without an API.</summary>
public sealed class NoNotificationBroadcaster : INotificationBroadcaster
{
    public static readonly NoNotificationBroadcaster Instance = new();

    public INotificationSubscription Subscribe(Guid userId) => new IdleSubscription();

    public void Publish(NotificationEventPayload payload)
    {
    }

    public void CloseUserSubscriptions(Guid userId)
    {
    }

    private sealed class IdleSubscription : INotificationSubscription
    {
        public Task<NotificationDto?> WaitAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                .ContinueWith<NotificationDto?>(_ => null, cancellationToken);

        public void Dispose()
        {
        }
    }
}
