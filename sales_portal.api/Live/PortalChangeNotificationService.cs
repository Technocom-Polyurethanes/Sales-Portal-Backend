using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using sales_portal.core.Caching;
using sales_portal.core.Live;

namespace sales_portal.api.Live;

/// <summary>
/// Relays Postgres NOTIFY payloads to the browsers connected to this API instance.
/// </summary>
public sealed class PortalChangeNotificationService : BackgroundService
{
    private const string NotificationChannel = "portal_changes";
    private const string ApplicationName = "SalesPortalLiveListener";
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IConfiguration _configuration;
    private readonly IReferenceDataCache _cache;
    private readonly IChangeBroadcaster _changes;
    private readonly ILogger<PortalChangeNotificationService> _logger;

    public PortalChangeNotificationService(
        IConfiguration configuration,
        IReferenceDataCache cache,
        IChangeBroadcaster changes,
        ILogger<PortalChangeNotificationService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _changes = changes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = ResolveConnectionString(_configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Postgres live updates are disabled: no notification connection string is configured.");
            return;
        }

        var retryDelay = InitialRetryDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan? wait = null;
            try
            {
                var settings = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    ApplicationName = ApplicationName
                };

                await using var connection = new NpgsqlConnection(settings.ConnectionString);
                await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
                await using var command = new NpgsqlCommand($"LISTEN {NotificationChannel}", connection);
                await command.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);

                _logger.LogInformation("Listening for Postgres changes on {Channel}.", NotificationChannel);
                retryDelay = InitialRetryDelay;

                // Changes made while disconnected are lost, so announce every table once on each listen.
                Announce(ChangeTopics.MappedTables);
                await WaitForNotificationsAsync(connection, stoppingToken).ConfigureAwait(false);
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
                    "Postgres live listener failed ({ErrorType}); retrying in {RetryDelay}.",
                    exception.GetType().Name,
                    wait.Value);
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

    private async Task WaitForNotificationsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var pending = new HashSet<string>(StringComparer.Ordinal);
        var gate = new object();

        void Collect(object? _, NpgsqlNotificationEventArgs notification)
        {
            if (!string.Equals(notification.Channel, NotificationChannel, StringComparison.Ordinal))
            {
                return;
            }

            lock (gate)
            {
                pending.Add(notification.Payload);
            }
        }

        connection.Notification += Collect;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await connection.WaitAsync(cancellationToken).ConfigureAwait(false);

                var windowEnd = DateTime.UtcNow + CoalesceWindow;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var remaining = windowEnd - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    await connection.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
                }

                string[] tables;
                lock (gate)
                {
                    tables = pending.ToArray();
                    pending.Clear();
                }

                if (tables.Length == 0)
                {
                    continue;
                }

                _logger.LogDebug("Coalesced {TableCount} changed tables into one announcement.", tables.Length);
                Announce(tables);
            }
        }
        finally
        {
            connection.Notification -= Collect;
        }
    }

    private void Announce(IEnumerable<string> tables)
    {
        var regions = new HashSet<string>(StringComparer.Ordinal);
        var topics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            if (ChangeTopics.TryResolveTable(table, out var tableRegions, out var tableTopics))
            {
                regions.UnionWith(tableRegions);
                topics.UnionWith(tableTopics);
            }
        }

        if (topics.Count == 0)
        {
            return;
        }

        // Invalidating first drops every cached copy before a browser can reload; it also announces
        // the region topics itself, so only the remaining topics are published here.
        _cache.Invalidate(regions.ToArray());
        _changes.Publish(topics.Except(ChangeTopics.ForRegions(regions), StringComparer.Ordinal).ToArray());
    }

    private static string? ResolveConnectionString(IConfiguration configuration) =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("PORTAL_NOTIFICATIONS_CONNECTION_STRING"),
            configuration.GetConnectionString("PortalNotifications"),
            configuration.GetConnectionString("Default"),
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"));

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static TimeSpan Jitter(TimeSpan delay) =>
        TimeSpan.FromMilliseconds(Math.Min(
            delay.TotalMilliseconds * (0.8 + Random.Shared.NextDouble() * 0.4),
            MaxRetryDelay.TotalMilliseconds));
}
