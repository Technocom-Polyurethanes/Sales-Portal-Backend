using System.Threading.Channels;
using sales_portal.core.Audit;

namespace sales_portal.api.Audit;

/// <summary>Bounded queue: a slow or broken database never slows the request that caused a row.</summary>
public sealed class QueuedAuditWriter(ILogger<QueuedAuditWriter> logger) : IAuditWriter, IAuditStatus
{
    private const int Capacity = 5000;

    private readonly Channel<AuditEntry> _queue = Channel.CreateBounded<AuditEntry>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly object _sync = new();
    private int _dropped;
    private DateTimeOffset? _lastWriteAt;
    private DateTimeOffset? _lastRetentionRunAt;

    public int QueueDepth => _queue.Reader.Count;
    public int Dropped => Volatile.Read(ref _dropped);

    public DateTimeOffset? LastWriteAt
    {
        get { lock (_sync) return _lastWriteAt; }
    }

    public DateTimeOffset? LastRetentionRunAt
    {
        get { lock (_sync) return _lastRetentionRunAt; }
    }

    public void Enqueue(AuditEntry entry)
    {
        if (!_queue.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _dropped);
            logger.LogWarning("Audit queue is full; dropping {Action}.", entry.Action);
        }
    }

    public void RecordWrite()
    {
        lock (_sync) _lastWriteAt = DateTimeOffset.UtcNow;
    }

    public void RecordRetentionRun()
    {
        lock (_sync) _lastRetentionRunAt = DateTimeOffset.UtcNow;
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        _queue.Reader.WaitToReadAsync(cancellationToken);

    public bool TryRead(out AuditEntry entry) => _queue.Reader.TryRead(out entry!);

    public IAsyncEnumerable<AuditEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}
