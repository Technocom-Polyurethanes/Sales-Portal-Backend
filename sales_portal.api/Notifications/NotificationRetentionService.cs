using Microsoft.EntityFrameworkCore;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Notifications;

/// <summary>Deletes old inbox rows in small batches so a daily run never holds a long lock.</summary>
public sealed class NotificationRetentionService : BackgroundService
{
    private const string DeleteBatchSql = """
        DELETE FROM notifications
        WHERE id IN (
            SELECT id FROM notifications
            WHERE created_at < NOW() - INTERVAL '60 days'
            LIMIT 1000
            FOR UPDATE SKIP LOCKED
        )
        """;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationRetentionService> _logger;

    public NotificationRetentionService(
        IServiceScopeFactory scopes,
        ILogger<NotificationRetentionService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await CleanupSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task CleanupSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var total = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var deleted = await db.Database.ExecuteSqlRawAsync(DeleteBatchSql, cancellationToken);
                if (deleted == 0) break;
                total += deleted;
            }

            if (total > 0)
            {
                _logger.LogInformation("Deleted {Count} notifications older than 60 days.", total);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Notification retention cleanup failed; retrying on the next tick.");
        }
    }
}
