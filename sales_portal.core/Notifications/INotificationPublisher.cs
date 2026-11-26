namespace sales_portal.core.Notifications;

/// <summary>Stores notifications for the eligible recipients and announces them over SSE.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Announcing is deliberately outside the originating write: a slow or broken notification path
/// must never fail or delay the response. Failures are swallowed by the publisher.
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

        _ = Task.Run(async () =>
        {
            try
            {
                await publisher.PublishAsync(notification, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // The publisher logs the reason; the caller has already committed its write.
            }
        });
    }
}
