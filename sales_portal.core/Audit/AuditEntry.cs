namespace sales_portal.core.Audit;

/// <summary>One queued row. Snapshots only; no request bodies, tokens or passwords.</summary>
public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ActorUserId { get; set; }
    public string ActorUsername { get; set; } = string.Empty;
    public string? ActorFullName { get; set; }
    public string? ActorRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityLabel { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Outcome { get; set; } = "success";
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int? DurationMs { get; set; }
    public Dictionary<string, string> Details { get; set; } = [];

    /// <summary>In-process retry counter; never persisted.</summary>
    public int RetryCount { get; set; }
}
