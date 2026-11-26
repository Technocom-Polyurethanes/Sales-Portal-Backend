using sales_portal.core.Notifications;

namespace sales_portal.core.IService;

public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize);

/// <summary>Every query is scoped to one signed-in user; another user's row does not exist.</summary>
public interface INotificationService
{
    Task<NotificationPageDto> GetPageAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>False when the id is missing or belongs to another user.</summary>
    Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
}
