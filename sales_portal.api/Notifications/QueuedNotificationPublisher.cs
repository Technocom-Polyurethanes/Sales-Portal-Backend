using System.Threading.Channels;
using sales_portal.core.Notifications;

namespace sales_portal.api.Notifications;

/// <summary>
/// Takes publish requests off the request path. The worker drains the queue so no request leaves
/// an untracked task behind, and a bounded channel keeps an outage from growing memory forever.
/// </summary>
public sealed class QueuedNotificationPublisher : INotificationPublisher
{
    private const int Capacity = 1000;

    private readonly Channel<NotificationEvent> _queue = Channel.CreateBounded<NotificationEvent>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly ILogger<QueuedNotificationPublisher> _logger;
    private int _dropped;

    public QueuedNotificationPublisher(ILogger<QueuedNotificationPublisher> logger) => _logger = logger;

    public int QueueDepth => _queue.Reader.Count;
    public int Dropped => Volatile.Read(ref _dropped);

    public Task PublishAsync(
        NotificationEvent notification,
        CancellationToken cancellationToken = default)
    {
        if (!_queue.Writer.TryWrite(notification))
        {
            Interlocked.Increment(ref _dropped);
            _logger.LogWarning("Notification queue is full; dropping {Type}.", notification.Type);
        }

        return Task.CompletedTask;
    }

    public IAsyncEnumerable<NotificationEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}
