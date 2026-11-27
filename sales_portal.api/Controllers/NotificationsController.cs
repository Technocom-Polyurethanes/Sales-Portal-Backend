using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.Audit;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

/// <summary>The signed-in user's notification inbox. Every row is scoped to that user.</summary>
[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationPageDto>> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId() is not Guid userId) return Unauthorized();

        return Ok(await _notifications.GetPageAsync(
            userId,
            page,
            pageSize,
            unreadOnly,
            cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not Guid userId) return Unauthorized();

        var unreadCount = await _notifications.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new { unreadCount });
    }

    [NoAudit("Personal read state, not a business change.")]
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not Guid userId) return Unauthorized();

        return await _notifications.MarkReadAsync(userId, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [NoAudit("Personal read state, not a business change.")]
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not Guid userId) return Unauthorized();

        await _notifications.MarkAllReadAsync(userId, cancellationToken);
        return NoContent();
    }

    private Guid? CurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out var id) ? id : null;
    }
}
