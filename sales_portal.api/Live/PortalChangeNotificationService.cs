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
    private const string ListenerName = "changes";
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IConfiguration _configuration;
    private readonly IReferenceDataCache _cache;
    private readonly IChangeBroadcaster _changes;
    private readonly ILiveListenerStatus _status;
    private readonly ILogger<PortalChangeNotificationService> _logger;

    public PortalChangeNotificationService(
        IConfiguration configuration,
        IReferenceDataCache cache,
        IChangeBroadcaster changes,
        ILiveListenerStatus status,
        ILogger<PortalChangeNotificationService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _changes = changes;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = PostgresNotificationListener.ResolveConnectionString(_configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Postgres live updates are disabled: no notification connection string is configured.");
            return;
        }

        _status.Update(ListenerName, false);
        var retryDelay = InitialRetryDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan? wait = null;
            try
            {
                await using var connection = PostgresNotificationListener.CreateConnection(connectionString, ApplicationName);
                await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
                await using var command = new NpgsqlCommand($"LISTEN {NotificationChannel}", connection);
                await command.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);

                _status.Update(ListenerName, true);
                _logger.LogInformation("Listening for Postgres changes on {Channel}.", NotificationChannel);
                retryDelay = InitialRetryDelay;

                // Changes made while disconnected are lost, so announce every table once on each listen.
                Announce(ChangeTopics.MappedTables);
                await PostgresNotificationListener.ListenAsync(
                    connection,
                    NotificationChannel,
                    tables => Announce(tables),
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
                    "Postgres live listener failed ({ErrorType}); retrying in {RetryDelay}.",
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

    private static TimeSpan Jitter(TimeSpan delay) =>
        TimeSpan.FromMilliseconds(Math.Min(
            delay.TotalMilliseconds * (0.8 + Random.Shared.NextDouble() * 0.4),
            MaxRetryDelay.TotalMilliseconds));
}
