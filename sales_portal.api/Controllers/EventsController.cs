using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.Live;

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

    public EventsController(IChangeBroadcaster changes)
    {
        _changes = changes;
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

        // Subscribe first: a change made while the first bytes are being sent is then not missed.
        using var subscription = _changes.Subscribe();

        try
        {
            // Sends the headers right away, so the browser knows the stream is open.
            await WriteAsync("retry: 5000\nevent: ready\ndata: {}\n\n", cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var remaining = endsAt - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var wait = remaining < HeartbeatInterval ? remaining : HeartbeatInterval;
                var topics = await subscription.WaitAsync(wait, cancellationToken);

                if (topics.Count == 0)
                {
                    await WriteAsync(": ping\n\n", cancellationToken);
                    continue;
                }

                var data = JsonSerializer.Serialize(new { topics });
                await WriteAsync($"event: change\ndata: {data}\n\n", cancellationToken);
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

    private async Task WriteAsync(string text, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
