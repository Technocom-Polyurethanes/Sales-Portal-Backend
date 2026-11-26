namespace sales_portal.api.Notifications;

/// <summary>Writes queued notifications after the request that caused them has finished.</summary>
public sealed class NotificationPublishWorker : BackgroundService
{
    private readonly QueuedNotificationPublisher _queue;
    private readonly NotificationPublisher _publisher;
    private readonly ILogger<NotificationPublishWorker> _logger;

    public NotificationPublishWorker(
        QueuedNotificationPublisher queue,
        NotificationPublisher publisher,
        ILogger<NotificationPublishWorker> logger)
    {
        _queue = queue;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _publisher.PublishAsync(notification, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // The publisher logs its own failures; this keeps one bad event from stopping the queue.
                _logger.LogWarning(exception, "Could not publish a queued notification.");
            }
        }
    }
}
