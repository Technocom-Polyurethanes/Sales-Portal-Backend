namespace sales_portal.core.Notifications;

/// <summary>The signed-in user behind a write. Null outside an HTTP request.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }

    string? DisplayName { get; }
}
