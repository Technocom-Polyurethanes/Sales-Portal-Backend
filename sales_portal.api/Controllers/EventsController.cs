using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.Live;
using sales_portal.core.Notifications;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

/// <summary>
/// A long-lived response (Server-Sent Events) that tells a signed-in browser when data changed, so
/// its screens can load it again without a manual refresh. It carries only topic names, never data.
/// </summary>
[Route("api/events")]
[ApiController]
[Authorize]
public class EventsController : ControllerBase
{
    /// <summary>Proxies and browsers drop a silent connection; a comment line every so often keeps it open.</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// A connection is checked once, when it opens. Ending it periodically makes the browser sign in
    /// again, so a deactivated user or an expired token cannot stay connected indefinitely.
    /// </summary>
    private static readonly TimeSpan MaxConnectionLifetime = TimeSpan.FromMinutes(30);

    private readonly IChangeBroadcaster _changes;
    private readonly INotificationBroadcaster _notifications;
    private readonly AppDbContext _users;

    public EventsController(
        IChangeBroadcaster changes,
        INotificationBroadcaster notifications,
        AppDbContext users)
    {
        _changes = changes;
        _notifications = notifications;
        _users = users;
    }

    [HttpGet]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store, no-transform";
        // Reverse proxies such as nginx hold a response back until it is "complete" unless told not to.
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var endsAt = DateTimeOffset.UtcNow + MaxConnectionLifetime;
        if (long.TryParse(User.FindFirstValue("exp"), out var expiresAtUnix))
        {
            var tokenEndsAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
            if (tokenEndsAt < endsAt)
            {
                endsAt = tokenEndsAt;
            }
        }

        if (CurrentUserId() is not Guid userId)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var account = await _users.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.IsActive, user.Role })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null || !account.IsActive)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var roleAtConnect = account.Role;

        // Subscribe first: a change made while the first bytes are being sent is then not missed.
        using var subscription = _changes.Subscribe();
        using var notificationSubscription = _notifications.Subscribe(userId);

        try
        {
            // Sends the headers right away, so the browser knows the stream is open.
            await WriteAsync("retry: 5000\nevent: ready\ndata: {}\n\n", cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Recheck on each heartbeat so a deactivation or role change on another API
                // instance closes this stream too, not just the one that served the write.
                var stillEligible = await _users.Users.AsNoTracking().AnyAsync(
                    user => user.Id == userId && user.IsActive && user.Role == roleAtConnect,
                    cancellationToken);
                if (!stillEligible)
                {
                    break;
                }

                var remaining = endsAt - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var wait = remaining < HeartbeatInterval ? remaining : HeartbeatInterval;
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                waitCts.CancelAfter(wait);

                var changeTask = subscription.WaitAsync(wait, waitCts.Token);
                var notificationTask = notificationSubscription.WaitAsync(waitCts.Token);
                await Task.WhenAny(changeTask, notificationTask);
                waitCts.Cancel();

                IReadOnlyCollection<string> topics = Array.Empty<string>();
                NotificationDto? notification = null;
                try
                {
                    topics = await changeTask;
                }
                catch (OperationCanceledException)
                    when (waitCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Nothing changed before the heartbeat or the other event won the wait.
                }

                try
                {
                    notification = await notificationTask;
                }
                catch (OperationCanceledException)
                    when (waitCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Nothing arrived before the heartbeat or the other event won the wait.
                }

                // The server closed this user's stream after deactivation or a role change.
                if (notification is null && notificationTask.IsCompletedSuccessfully)
                {
                    break;
                }

                if (topics.Count > 0)
                {
                    var data = JsonSerializer.Serialize(new { topics });
                    await WriteAsync($"event: change\ndata: {data}\n\n", cancellationToken);
                }

                if (notification is not null)
                {
                    var data = JsonSerializer.Serialize(notification, NotificationJson.Options);
                    await WriteAsync($"event: notification\ndata: {data}\n\n", cancellationToken);
                }

                if (topics.Count == 0 && notification is null)
                {
                    await WriteAsync(": ping\n\n", cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The browser went away (closed the tab, navigated, lost the connection).
        }
        catch (IOException)
        {
            // Same, reported by the transport as a failed write.
        }
    }

    private Guid? CurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out var id) ? id : null;
    }

    private async Task WriteAsync(string text, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
