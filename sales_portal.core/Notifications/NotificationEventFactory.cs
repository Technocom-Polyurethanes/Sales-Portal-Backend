namespace sales_portal.core.Notifications;

/// <summary>Builds an event from the current actor, or returns null for background work.</summary>
public static class NotificationEventFactory
{
    public static NotificationEvent? Create(
        ICurrentActor? actor,
        string type,
        string entityType,
        Guid? entityId,
        string entityLabel,
        IReadOnlyDictionary<string, object?>? meta = null)
    {
        if (actor?.UserId is not Guid userId) return null;

        return new NotificationEvent(
            type,
            userId,
            actor.DisplayName ?? string.Empty,
            entityType,
            entityId,
            entityLabel,
            meta);
    }

    public static NotificationEvent Create(
        Guid userId,
        string actorName,
        string type,
        string entityType,
        Guid? entityId,
        string entityLabel,
        IReadOnlyDictionary<string, object?>? meta = null) =>
        new(type, userId, actorName, entityType, entityId, entityLabel, meta);
}
