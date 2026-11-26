namespace sales_portal.core.Notifications;

/// <summary>Stores notifications for the eligible recipients and announces them over SSE.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Announcing is deliberately outside the originating write: the production publisher queues the
/// work and a hosted worker drains it, so no request waits on notification I/O or leaves a task
/// untracked. Failures are swallowed by the publisher.
/// </summary>
public static class NotificationPublishing
{
    public static void PublishInBackground(
        this INotificationPublisher? publisher,
        NotificationEvent? notification)
    {
        if (publisher is null || notification is null)
        {
            return;
        }

        try
        {
            _ = publisher.PublishAsync(notification);
        }
        catch
        {
            // The publisher logs the reason; the caller has already committed its write.
        }
    }
}
