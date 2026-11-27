using Microsoft.EntityFrameworkCore;
using sales_portal.core.Audit;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Audit;

/// <summary>Deletes old audit rows in small batches, once a day. Zero days disables deletion.</summary>
public sealed class AuditRetentionService(
    IConfiguration configuration,
    IServiceScopeFactory scopes,
    IAuditStatus status,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

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
        status.RecordRetentionRun();

        try
        {
            if (!int.TryParse(configuration["Audit:RetentionDays"], out var days)) days = 365;
            if (days <= 0) return;

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            var total = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM audit_logs WHERE id IN (SELECT id FROM audit_logs WHERE occurred_at < {cutoff} LIMIT 1000)",
                    cancellationToken);
                if (deleted == 0) break;
                total += deleted;
            }

            if (total > 0) logger.LogInformation("Deleted {Count} audit rows older than {Days} days.", total, days);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Audit retention cleanup failed; retrying on the next tick.");
        }
    }
}
