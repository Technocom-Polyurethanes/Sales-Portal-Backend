using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using sales_portal.api.Authorization;
using sales_portal.api.Live;
using sales_portal.core.DTOs;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

/// <summary>Rich diagnostics for admins. /health stays anonymous and deliberately minimal.</summary>
[Route("api/system")]
[ApiController]
[Authorize(Policy = Policies.ManageUsers)]
public class SystemController(
    AppDbContext db,
    ILiveListenerStatus live,
    IWebHostEnvironment environment,
    ILogger<SystemController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusDto>> Status(CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var databaseUp = await CanConnectAsync(cancellationToken);
        var database = new SystemDatabaseStatusDto
        {
            Status = databaseUp ? "Up" : "Down",
            LatencyMs = timer.ElapsedMilliseconds
        };

        var listeners = new SystemLiveStatusDto
        {
            State = live.State,
            Disabled = live.Disabled,
            PollingFallback = live.State == "disconnected",
            Changes = ListenerState("changes", live),
            Notifications = ListenerState("notifications", live)
        };

        var overall = !databaseUp
            ? "Unhealthy"
            : live.Disabled || listeners.State != "connected" ? "Degraded" : "Healthy";

        return Ok(new SystemStatusDto
        {
            Status = overall,
            CheckedAt = DateTimeOffset.UtcNow,
            Version = typeof(SystemController).Assembly.GetName().Version?.ToString() ?? "unknown",
            Environment = environment.EnvironmentName,
            UptimeSeconds = Environment.TickCount64 / 1000,
            Database = database,
            Live = listeners
        });
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            logger.LogWarning(exception, "System status database probe failed.");
            return false;
        }
    }

    private static string ListenerState(string listener, ILiveListenerStatus live) =>
        live.Disabled ? "disabled" : live.IsConnected(listener) ? "connected" : "disconnected";
}
