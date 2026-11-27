namespace sales_portal.core.Audit;

public interface IAuditWriter
{
    void Enqueue(AuditEntry entry);
}

/// <summary>Counters the admin status page reads. Writes never block or fail a request.</summary>
public interface IAuditStatus
{
    int QueueDepth { get; }
    int Dropped { get; }
    DateTimeOffset? LastWriteAt { get; }
    DateTimeOffset? LastRetentionRunAt { get; }

    void RecordWrite();

    void RecordRetentionRun();
}

public sealed class NoAuditStatus : IAuditStatus
{
    public static readonly NoAuditStatus Instance = new();

    private NoAuditStatus() { }

    public int QueueDepth => 0;
    public int Dropped => 0;
    public DateTimeOffset? LastWriteAt => null;
    public DateTimeOffset? LastRetentionRunAt => null;
    public void RecordWrite() { }
    public void RecordRetentionRun() { }
}
