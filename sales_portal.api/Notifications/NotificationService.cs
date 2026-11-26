using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.IService;
using sales_portal.core.Notifications;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Notifications;

/// <summary>Reads and updates inbox rows; every query is scoped to the signed-in user.</summary>
public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationPageDto> GetPageAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // Keep (page - 1) * pageSize inside int: a huge page must be an empty page, not a 500.
        page = Math.Min(page, int.MaxValue / pageSize + 1);

        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await _db.Notifications
            .CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null, cancellationToken);
        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new NotificationPageDto(
            rows.Select(Map).ToList(),
            totalCount,
            unreadCount,
            page,
            pageSize);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Notifications.CountAsync(
            n => n.RecipientUserId == userId && n.ReadAt == null,
            cancellationToken);

    public async Task<bool> MarkReadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.RecipientUserId == userId && n.Id == id, cancellationToken);
        if (notification is null) return false;
        if (notification.ReadAt is not null) return true;

        notification.ReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && n.ReadAt == null)
            .ToListAsync(cancellationToken);
        if (unread.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var notification in unread)
        {
            notification.ReadAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    private static NotificationDto Map(Notification notification)
    {
        Dictionary<string, object?> meta;
        try
        {
            meta = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                notification.Meta,
                NotificationJson.Options) ?? new();
        }
        catch (JsonException)
        {
            meta = new();
        }

        return new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            ActorName = notification.ActorName,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            EntityLabel = notification.EntityLabel,
            Meta = meta,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
    }
}
