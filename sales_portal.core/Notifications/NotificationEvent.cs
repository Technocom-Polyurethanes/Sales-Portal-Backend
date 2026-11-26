using System.Text.Json;

namespace sales_portal.core.Notifications;

/// <summary>One wire format for pg_notify, SSE and the REST inbox.</summary>
public static class NotificationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>What happened, for whom it is eligible, and who did it.</summary>
public sealed record NotificationEvent(
    string Type,
    Guid ActorUserId,
    string ActorName,
    string EntityType,
    Guid? EntityId,
    string EntityLabel,
    IReadOnlyDictionary<string, object?>? Meta = null);

/// <summary>One stored row addressed to one user.</summary>
public sealed record NotificationDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string EntityLabel { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Meta { get; init; } =
        new Dictionary<string, object?>();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
}

/// <summary>The pg_notify payload: one row id per recipient, never any restricted entity data.</summary>
public sealed record NotificationDelivery(Guid UserId, Guid NotificationId);

public sealed record NotificationEventPayload(
    NotificationDto Notification,
    IReadOnlyList<NotificationDelivery> Recipients);
