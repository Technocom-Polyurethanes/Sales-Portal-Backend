using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using sales_portal.api.Authorization;
using sales_portal.api.Diagnostics;
using sales_portal.api.Live;
using sales_portal.api.Middleware;
using sales_portal.api.Notifications;
using sales_portal.core.Audit;
using sales_portal.core.DTOs;
using sales_portal.core.Live;
using sales_portal.core.Notifications;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

/// <summary>Rich diagnostics for admins. /health stays anonymous and deliberately minimal.</summary>
[Route("api/system")]
[ApiController]
[Authorize(Policy = Policies.ManageUsers)]
public class SystemController(
    AppDbContext db,
    ILiveListenerStatus live,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<SystemController> logger,
    LatencyHistory latencyHistory,
    IAuditStatus auditStatus,
    QueuedNotificationPublisher notificationPublisher,
    IChangeBroadcaster changeBroadcaster,
    INotificationBroadcaster notificationBroadcaster) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusDto>> Status(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var databaseUp = await CanConnectAsync(cancellationToken);
        timer.Stop();
        latencyHistory.Add(timer.ElapsedMilliseconds);

        var database = new SystemDatabaseStatusDto
        {
            Status = databaseUp ? "Up" : "Down",
            LatencyMs = timer.ElapsedMilliseconds,
            LatencyHistory = latencyHistory.Snapshot()
        };
        await FillDatabaseMetricsAsync(database, cancellationToken);

        var listeners = new SystemLiveStatusDto
        {
            State = live.State,
            Disabled = live.Disabled,
            PollingFallback = live.State == "disconnected",
            Changes = ListenerState("changes", live),
            Notifications = ListenerState("notifications", live),
            ChangesLastEventAt = live.ChangesLastEventAt,
            NotificationsLastEventAt = live.NotificationsLastEventAt,
            ReconnectCount = live.ReconnectCount,
            LastError = live.LastError
        };

        var audit = new SystemAuditStatusDto
        {
            RowsTotal = await CountAsync(
                "audit.rows", () => db.AuditLogs.CountAsync(cancellationToken), cancellationToken),
            QueueDepth = auditStatus.QueueDepth,
            Dropped = auditStatus.Dropped,
            LastWriteAt = auditStatus.LastWriteAt,
            RetentionDays = RetentionDays()
        };
        var security = await ReadSecurityAsync(cancellationToken);
        var data = await ReadDataAsync(security, cancellationToken);

        var overall = !databaseUp
            ? "Unhealthy"
            : live.Disabled || listeners.State != "connected" ? "Degraded" : "Healthy";

        var apiVersion = ApiVersion();
        return Ok(new SystemStatusDto
        {
            Status = overall,
            CheckedAt = DateTimeOffset.UtcNow,
            Version = apiVersion,
            ApiVersion = apiVersion,
            Commit = Commit(),
            StartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            Environment = environment.EnvironmentName,
            UptimeSeconds = Environment.TickCount64 / 1000,
            Database = database,
            Live = listeners,
            Realtime = new SystemRealtimeStatusDto
            {
                ConnectedClients =
                    ((changeBroadcaster as ChangeBroadcaster)?.SubscriberCount ?? 0) +
                    ((notificationBroadcaster as NotificationBroadcaster)?.SubscriberCount ?? 0),
                NotificationQueueDepth = notificationPublisher.QueueDepth,
                Dropped = notificationPublisher.Dropped
            },
            Audit = audit,
            Jobs = Jobs(audit, auditStatus),
            Security = security,
            Data = data
        });
    }

    private async Task FillDatabaseMetricsAsync(
        SystemDatabaseStatusDto database,
        CancellationToken cancellationToken)
    {
        database.ServerVersion = await SystemStatusProbe.ValueAsync(
            async () => (await db.Database.SqlQueryRaw<string>("SHOW server_version")
                .ToListAsync(cancellationToken)).FirstOrDefault(),
            LogFailure,
            cancellationToken);
        database.SizeBytes = await SystemStatusProbe.ValueAsync(
            async () => (await db.Database.SqlQueryRaw<long>("SELECT pg_database_size(current_database())")
                .ToListAsync(cancellationToken)).FirstOrDefault(),
            LogFailure,
            cancellationToken);
        database.ConnectionsUsed = await SystemStatusProbe.ValueAsync(
            async () => (await db.Database.SqlQueryRaw<int>("SELECT count(*)::int FROM pg_stat_activity")
                .ToListAsync(cancellationToken)).FirstOrDefault(),
            LogFailure,
            cancellationToken);
        var maxConnections = await SystemStatusProbe.ValueAsync(
            async () => (await db.Database.SqlQueryRaw<string>("SHOW max_connections")
                .ToListAsync(cancellationToken)).FirstOrDefault(),
            LogFailure,
            cancellationToken);
        database.ConnectionsMax = int.TryParse(maxConnections, out var max) ? max : null;
        database.PendingMigrations = await SystemStatusProbe.ValueAsync(
            async () => (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count(),
            LogFailure,
            cancellationToken);
        database.LastMigration = await SystemStatusProbe.ValueAsync(
            async () => (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault(),
            LogFailure,
            cancellationToken);
    }

    private async Task<SystemSecurityStatusDto> ReadSecurityAsync(CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var failed = await SystemStatusProbe.RunAsync(
            "security.failedSignIns24h",
            () => db.AuditLogs.CountAsync(
                log => log.Action == AuditActions.LoginFailed && log.OccurredAt >= since,
                cancellationToken),
            LogFailure,
            cancellationToken);
        var lastFailed = await SystemStatusProbe.ValueAsync(
            () => db.AuditLogs
                .Where(log => log.Action == AuditActions.LoginFailed)
                .OrderByDescending(log => log.OccurredAt)
                .Select(log => (DateTimeOffset?)log.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken),
            LogFailure,
            cancellationToken);

        return new SystemSecurityStatusDto
        {
            FailedSignIns24h = failed.Ok ? failed.Count : null,
            LastFailedSignInAt = lastFailed,
            ActiveUsers = await CountAsync(
                "security.activeUsers", () => db.Users.CountAsync(user => user.IsActive, cancellationToken), cancellationToken),
            InactiveUsers = await CountAsync(
                "security.inactiveUsers", () => db.Users.CountAsync(user => !user.IsActive, cancellationToken), cancellationToken)
        };
    }

    private async Task<SystemDataStatusDto> ReadDataAsync(
        SystemSecurityStatusDto security,
        CancellationToken cancellationToken)
    {
        return new SystemDataStatusDto
        {
            Offers = await CountAsync("data.offers", () => db.Offers.CountAsync(cancellationToken), cancellationToken),
            Customers = await CountAsync("data.customers", () => db.Customers.CountAsync(cancellationToken), cancellationToken),
            Materials = await CountAsync("data.materials", () => db.Materials.CountAsync(cancellationToken), cancellationToken),
            ActiveUsers = security.ActiveUsers
        };
    }

    private static List<SystemJobDto> Jobs(SystemAuditStatusDto audit, IAuditStatus status) =>
    [
        new SystemJobDto
        {
            Name = "audit-writer",
            LastRunAt = status.LastWriteAt,
            Result = status.LastWriteAt is null ? "never" : "success"
        },
        new SystemJobDto
        {
            Name = "audit-retention",
            LastRunAt = status.LastRetentionRunAt,
            Result = status.LastRetentionRunAt is null ? "never" : "success",
            NextRunAt = DateTimeOffset.UtcNow.AddDays(1)
        },
        new SystemJobDto
        {
            Name = "notification-publisher",
            Result = "running"
        }
    ];

    private async Task<int?> CountAsync(
        string name,
        Func<Task<int>> count,
        CancellationToken cancellationToken)
    {
        var probe = await SystemStatusProbe.RunAsync(name, count, LogFailure, cancellationToken);
        return probe.Ok ? probe.Count : null;
    }

    private void LogFailure(Exception exception) =>
        logger.LogWarning(exception, "A system status probe failed.");

    private string? Commit()
    {
        var commit = FirstNonEmpty(
            configuration["App:Commit"],
            Environment.GetEnvironmentVariable("BUILD_COMMIT"),
            Environment.GetEnvironmentVariable("SOURCE_COMMIT"));
        return string.IsNullOrWhiteSpace(commit) ? null : commit;
    }

    private static string ApiVersion()
    {
        var version = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        version = FirstNonEmpty(version, typeof(SystemController).Assembly.GetName().Version?.ToString());
        return version.Split('+')[0];
    }

    private int RetentionDays() =>
        int.TryParse(configuration["Audit:RetentionDays"], out var days) && days > 0 ? days : 365;

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            logger.LogWarning(exception, "System status database probe failed.");
            return false;
        }
    }

    private static string ListenerState(string listener, ILiveListenerStatus live) =>
        live.Disabled ? "disabled" : live.IsConnected(listener) ? "connected" : "disconnected";

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
