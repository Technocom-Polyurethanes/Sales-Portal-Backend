namespace sales_portal.domain.Models;

/// <summary>An append-only record of a mutating request and its outcome.</summary>
public class AuditLog
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
    public string Details { get; set; } = "{}";
}
