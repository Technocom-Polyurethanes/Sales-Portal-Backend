using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using sales_portal.api.Live;
using sales_portal.core.Notifications;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Notifications;

/// <summary>
/// Stores one inbox row per eligible user, then announces it. A broken notification path is logged
/// and swallowed so it can never fail the write that caused it.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    // PostgreSQL caps a NOTIFY payload at 8000 bytes; stay well below it.
    private const int MaxPayloadBytes = 7_000;
    private const int RecipientsPerNotice = 16;
    private const string Channel = "portal_events";

    private readonly IServiceScopeFactory _scopes;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly ILiveListenerStatus _listeners;
    private readonly string? _connectionString;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        IServiceScopeFactory scopes,
        INotificationBroadcaster broadcaster,
        ILiveListenerStatus listeners,
        IConfiguration configuration,
        ILogger<NotificationPublisher> logger)
    {
        _scopes = scopes;
        _broadcaster = broadcaster;
        _listeners = listeners;
        _connectionString = PostgresNotificationListener.ResolveConnectionString(configuration);
        _logger = logger;
    }

    public async Task PublishAsync(
        NotificationEvent notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metaJson = JsonSerializer.Serialize(
                notification.Meta ?? new Dictionary<string, object?>(),
                NotificationJson.Options);

            NotificationEventPayload payload;
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
                var recipients = NotificationAudience.Recipients(
                    users,
                    notification.Type,
                    notification.ActorUserId);
                if (recipients.Count == 0) return;

                var createdAt = DateTimeOffset.UtcNow;
                var rows = recipients.Select(user => new Notification
                {
                    RecipientUserId = user.Id,
                    Type = notification.Type,
                    ActorUserId = notification.ActorUserId,
                    ActorName = notification.ActorName,
                    EntityType = notification.EntityType,
                    EntityId = notification.EntityId,
                    EntityLabel = notification.EntityLabel,
                    Meta = metaJson,
                    CreatedAt = createdAt
                }).ToList();

                db.Notifications.AddRange(rows);
                await db.SaveChangesAsync(cancellationToken);

                var dto = new NotificationDto
                {
                    Id = rows[0].Id,
                    Type = notification.Type,
                    ActorName = notification.ActorName,
                    EntityType = notification.EntityType,
                    EntityId = notification.EntityId,
                    EntityLabel = notification.EntityLabel,
                    Meta = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        metaJson,
                        NotificationJson.Options) ?? new Dictionary<string, object?>(),
                    CreatedAt = createdAt
                };
                var deliveries = rows
                    .Select(row => new NotificationDelivery(row.RecipientUserId, row.Id))
                    .ToArray();
                payload = new NotificationEventPayload(dto, deliveries);
            }

            await AnnounceAsync(payload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not publish notification {Type}.", notification.Type);
        }
    }

    private async Task AnnounceAsync(
        NotificationEventPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || _listeners.State != "connected")
        {
            _broadcaster.Publish(payload);
            return;
        }

        var recipients = payload.Recipients;
        for (var offset = 0; offset < recipients.Count; offset += RecipientsPerNotice)
        {
            var batch = new NotificationEventPayload(
                payload.Notification,
                recipients.Skip(offset).Take(RecipientsPerNotice).ToArray());
            var json = JsonSerializer.Serialize(batch, NotificationJson.Options);
            if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            {
                _broadcaster.Publish(batch);
                continue;
            }

            await using var connection = PostgresNotificationListener.CreateConnection(
                _connectionString!,
                "SalesPortalNotifications");
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                $"SELECT pg_notify('{Channel}', @payload)",
                connection);
            command.Parameters.AddWithValue("payload", json);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
