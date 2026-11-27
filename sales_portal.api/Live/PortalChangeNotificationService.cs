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
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

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
            _status.Disable();
            _logger.LogWarning("Postgres live updates are disabled: no notification connection string is configured.");
            return;
        }

        _status.Update(ListenerName, false);
        // LISTEN is unsupported by transaction poolers, so poll only while it is down.
        _ = RunPollAsync(
            token => ReadCountersAsync(connectionString, token),
            () => _status.IsConnected(ListenerName),
            Announce,
            PollInterval,
            exception => _logger.LogWarning(
                "Postgres change poll failed ({ErrorType}).", exception.GetType().Name),
            stoppingToken);
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

    public static string[] ChangedTables(
        IReadOnlyDictionary<string, (long Ins, long Upd, long Del)>? previous,
        IReadOnlyDictionary<string, (long Ins, long Upd, long Del)> current) =>
        previous is null
            ? []
            : current
                .Where(entry => !previous.TryGetValue(entry.Key, out var old) || old != entry.Value)
                .Select(entry => entry.Key)
                .ToArray();

    private static async Task<IReadOnlyDictionary<string, (long Ins, long Upd, long Del)>> ReadCountersAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = PostgresNotificationListener.CreateConnection(connectionString, "SalesPortalChangePoll");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "SELECT relname, n_tup_ins, n_tup_upd, n_tup_del FROM pg_stat_all_tables WHERE schemaname = 'public' AND relname::text = ANY(@tables)",
            connection);
        command.Parameters.AddWithValue("tables", ChangeTopics.MappedTables.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var current = new Dictionary<string, (long, long, long)>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            current[reader.GetString(0)] = (reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
        }

        return current;
    }

    public static async Task RunPollAsync(
        Func<CancellationToken, Task<IReadOnlyDictionary<string, (long Ins, long Upd, long Del)>>> readCounters,
        Func<bool> isConnected,
        Action<IReadOnlyCollection<string>> announce,
        TimeSpan interval,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, (long Ins, long Upd, long Del)>? previous = null;
        var failed = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (isConnected())
            {
                // LISTEN covers this window; a stale baseline would replay its notices on reconnect.
                previous = null;
            }
            else
            {
                try
                {
                    var current = await readCounters(cancellationToken).ConfigureAwait(false);
                    var changed = ChangedTables(previous, current);
                    if (changed.Length > 0) announce(changed);

                    previous = current;
                    failed = false;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (!failed) onError(exception);
                    failed = true;
                }
            }

            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
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
