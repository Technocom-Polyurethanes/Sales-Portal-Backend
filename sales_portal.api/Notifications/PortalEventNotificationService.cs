using System.Text.Json;
using Npgsql;
using sales_portal.api.Live;
using sales_portal.core.Notifications;

namespace sales_portal.api.Notifications;

/// <summary>Relays stored notification events from Postgres to the open streams of their recipients.</summary>
public sealed class PortalEventNotificationService : BackgroundService
{
    private const string ListenerName = "notifications";
    private const string NotificationChannel = "portal_events";
    private const string ApplicationName = "SalesPortalNotificationListener";
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IConfiguration _configuration;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly ILiveListenerStatus _status;
    private readonly ILogger<PortalEventNotificationService> _logger;

    public PortalEventNotificationService(
        IConfiguration configuration,
        INotificationBroadcaster broadcaster,
        ILiveListenerStatus status,
        ILogger<PortalEventNotificationService> logger)
    {
        _configuration = configuration;
        _broadcaster = broadcaster;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = PostgresNotificationListener.ResolveConnectionString(_configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Notification events are disabled: no notification connection string is configured.");
            return;
        }

        _status.Update(ListenerName, false);
        var retryDelay = InitialRetryDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan? wait = null;
            try
            {
                await using var connection = PostgresNotificationListener.CreateConnection(
                    connectionString,
                    ApplicationName);
                await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
                await using var command = new NpgsqlCommand($"LISTEN {NotificationChannel}", connection);
                await command.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);

                _status.Update(ListenerName, true);
                retryDelay = InitialRetryDelay;

                await PostgresNotificationListener.ListenAsync(
                    connection,
                    NotificationChannel,
                    Deliver,
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                wait = Jitter(retryDelay);
                retryDelay = TimeSpan.FromMilliseconds(
                    Math.Min(retryDelay.TotalMilliseconds * 2, MaxRetryDelay.TotalMilliseconds));

                _logger.LogWarning(
                    "Notification event listener failed ({ErrorType}); retrying in {RetryDelay}.",
                    exception.GetType().Name,
                    wait.Value);
            }
            finally
            {
                _status.Update(ListenerName, false);
            }

            if (wait is { } delay)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void Deliver(IReadOnlyCollection<string> payloads)
    {
        foreach (var payload in payloads)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<NotificationEventPayload>(
                    payload,
                    NotificationJson.Options);
                if (parsed is null || parsed.Notification.Id == Guid.Empty) continue;
                _broadcaster.Publish(parsed);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Ignoring a malformed notification event.");
            }
        }
    }

    private static TimeSpan Jitter(TimeSpan delay) =>
        TimeSpan.FromMilliseconds(Math.Min(
            delay.TotalMilliseconds * (0.8 + Random.Shared.NextDouble() * 0.4),
            MaxRetryDelay.TotalMilliseconds));
}
