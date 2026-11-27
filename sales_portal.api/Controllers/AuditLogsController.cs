using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sales_portal.api.Authorization;
using sales_portal.core.Audit;
using sales_portal.core.Exceptions;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

[Route("api/audit-logs")]
[ApiController]
[Authorize(Policy = Policies.ManageUsers)]
public class AuditLogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? actorId,
        [FromQuery] string? category,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? outcome,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("Page must be at least 1.");
        if (pageSize < 1 || pageSize > 200) throw new ValidationException("Page size must be between 1 and 200.");

        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (from is not null) query = query.Where(log => log.OccurredAt >= from);
        if (to is not null) query = query.Where(log => log.OccurredAt <= to);
        if (actorId is not null) query = query.Where(log => log.ActorUserId == actorId);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(log => log.Category.ToLower() == category.ToLower());
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(log => log.Action.ToLower() == action.ToLower());
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(log => log.EntityType != null && log.EntityType.ToLower() == entityType.ToLower());
        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(log => log.Outcome.ToLower() == outcome.ToLower());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(log =>
                log.Description.ToLower().Contains(term) ||
                (log.EntityLabel != null && log.EntityLabel.ToLower().Contains(term)) ||
                log.ActorUsername.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new
            {
                log.Id,
                log.OccurredAt,
                log.ActorUserId,
                log.ActorUsername,
                log.ActorFullName,
                log.ActorRole,
                log.Action,
                log.Category,
                log.EntityType,
                log.EntityId,
                log.EntityLabel,
                log.Description,
                log.Outcome,
                log.HttpMethod,
                log.Path,
                log.StatusCode,
                log.IpAddress,
                Details = log.Details
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = items.Select(item => new
            {
                item.Id,
                item.OccurredAt,
                item.ActorUserId,
                item.ActorUsername,
                item.ActorFullName,
                item.ActorRole,
                item.Action,
                item.Category,
                item.EntityType,
                item.EntityId,
                item.EntityLabel,
                item.Description,
                item.Outcome,
                item.HttpMethod,
                item.Path,
                item.StatusCode,
                item.IpAddress,
                Details = ParseDetails(item.Details)
            }),
            page,
            pageSize,
            totalCount
        });
    }

    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken cancellationToken)
    {
        var actors = await db.Users
            .AsNoTracking()
            .OrderBy(user => user.Username)
            .Select(user => new { user.Id, user.Username, user.FullName })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            actors,
            categories = AuditActions.Categories,
            actions = AuditActions.All
        });
    }

    private static Dictionary<string, string> ParseDetails(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
