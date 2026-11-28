using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.Audit;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Audit;

/// <summary>Writes queued rows in small batches, retrying a failed batch before it is discarded.</summary>
public sealed class AuditWriteWorker(
    QueuedAuditWriter queue,
    IServiceScopeFactory scopes,
    ILogger<AuditWriteWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(5);

    private readonly List<AuditEntry> _pending = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await queue.WaitToReadAsync(stoppingToken).ConfigureAwait(false)) break;
                await WriteBatchAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The queue is flushed in the finally block.
        }
        finally
        {
            await DrainAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Increments and selects the entries that may be queued again after a failed write.</summary>
    public static IReadOnlyList<AuditEntry> PrepareRetries(IEnumerable<AuditEntry> batch, int maxRetries)
    {
        var retried = new List<AuditEntry>();
        foreach (var entry in batch)
        {
            if (entry.RetryCount >= maxRetries) continue;
            entry.RetryCount++;
            retried.Add(entry);
        }

        return retried;
    }

    private async Task WriteBatchAsync(CancellationToken stoppingToken)
    {
        var batch = TakeBatch();
        if (batch.Count == 0) return;
        _pending.AddRange(batch);

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            linked.CancelAfter(WriteTimeout);
            await PersistAsync(batch, linked.Token).ConfigureAwait(false);
            queue.RecordWrite();
            RemovePending(batch);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The in-flight batch stays in _pending for the shutdown drain.
        }
        catch (Exception exception)
        {
            var retried = PrepareRetries(batch, MaxRetries);
            RemovePending(batch);
            foreach (var entry in retried) queue.Enqueue(entry);

            logger.LogError(
                exception,
                "Could not write {Count} audit rows; {Retried} will be retried.",
                batch.Count,
                retried.Count);

            if (retried.Count > 0) await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task DrainAsync()
    {
        var batch = new List<AuditEntry>(_pending);
        while (queue.TryRead(out var entry)) batch.Add(entry);
        if (batch.Count == 0) return;

        try
        {
            using var timeout = new CancellationTokenSource(ShutdownDrainTimeout);
            await PersistAsync(batch, timeout.Token).ConfigureAwait(false);
            queue.RecordWrite();
            _pending.Clear();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not flush {Count} audit rows during shutdown.", batch.Count);
        }
    }

    private async Task PersistAsync(IEnumerable<AuditEntry> batch, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogs.AddRange(batch.Select(ToRow));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void RemovePending(IEnumerable<AuditEntry> batch)
    {
        foreach (var entry in batch) _pending.Remove(entry);
    }

    private List<AuditEntry> TakeBatch()
    {
        var batch = new List<AuditEntry>(BatchSize);
        while (batch.Count < BatchSize && queue.TryRead(out var entry)) batch.Add(entry);
        return batch;
    }

    private static AuditLog ToRow(AuditEntry entry) => new()
    {
        Id = entry.Id,
        OccurredAt = entry.OccurredAt,
        ActorUserId = entry.ActorUserId,
        ActorUsername = entry.ActorUsername,
        ActorFullName = entry.ActorFullName,
        ActorRole = entry.ActorRole,
        Action = entry.Action,
        Category = entry.Category,
        EntityType = entry.EntityType,
        EntityId = entry.EntityId,
        EntityLabel = entry.EntityLabel,
        Description = entry.Description,
        Outcome = entry.Outcome,
        HttpMethod = entry.HttpMethod,
        Path = entry.Path,
        StatusCode = entry.StatusCode,
        IpAddress = entry.IpAddress,
        UserAgent = entry.UserAgent,
        DurationMs = entry.DurationMs,
        Details = JsonSerializer.Serialize(entry.Details)
    };
}
