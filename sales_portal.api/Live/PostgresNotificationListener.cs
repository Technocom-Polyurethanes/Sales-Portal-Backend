using Npgsql;

namespace sales_portal.api.Live;

/// <summary>
/// Shared LISTEN loop: keeps a dedicated connection alive with TCP keepalive and a periodic
/// SELECT 1, so a half-open socket fails into the caller's reconnect loop instead of hanging.
/// </summary>
public static class PostgresNotificationListener
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(250);

    public static string? ResolveConnectionString(IConfiguration configuration) =>
        new[]
        {
            Environment.GetEnvironmentVariable("PORTAL_NOTIFICATIONS_CONNECTION_STRING"),
            configuration.GetConnectionString("PortalNotifications"),
            configuration.GetConnectionString("Default"),
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
        }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public static NpgsqlConnection CreateConnection(string connectionString, string applicationName)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = applicationName
        };
        settings["Keepalive"] = 15;
        settings["Tcp Keepalive"] = true;
        return new NpgsqlConnection(settings.ConnectionString);
    }

    public static async Task ListenAsync(
        NpgsqlConnection connection,
        string channel,
        Action<IReadOnlyCollection<string>> onNotifications,
        CancellationToken cancellationToken)
    {
        var pending = new HashSet<string>(StringComparer.Ordinal);
        var gate = new object();
        var nextProbe = DateTime.UtcNow + ProbeInterval;
        DateTime? windowEnd = null;

        void Collect(object? _, NpgsqlNotificationEventArgs notification)
        {
            if (!string.Equals(notification.Channel, channel, StringComparison.Ordinal))
            {
                return;
            }

            lock (gate)
            {
                pending.Add(notification.Payload);
                windowEnd ??= DateTime.UtcNow + CoalesceWindow;
            }
        }

        connection.Notification += Collect;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime waitUntil;
                lock (gate)
                {
                    waitUntil = windowEnd < nextProbe ? windowEnd.Value : nextProbe;
                }

                var timeout = waitUntil - DateTime.UtcNow;
                if (timeout <= TimeSpan.Zero)
                {
                    timeout = TimeSpan.FromMilliseconds(1);
                }

                if (timeout > TimeSpan.Zero)
                {
                    await connection.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                }

                var now = DateTime.UtcNow;
                if (now >= nextProbe)
                {
                    await using var probe = new NpgsqlCommand("SELECT 1", connection);
                    await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    nextProbe = now + ProbeInterval;
                }

                string[] payloads;
                lock (gate)
                {
                    if (windowEnd is null || now < windowEnd)
                    {
                        continue;
                    }

                    if (pending.Count == 0)
                    {
                        windowEnd = null;
                        continue;
                    }

                    payloads = pending.ToArray();
                    pending.Clear();
                    windowEnd = null;
                }

                onNotifications(payloads);
            }
        }
        finally
        {
            connection.Notification -= Collect;
        }
    }
}
