using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sales_portal.api.Authorization;
using sales_portal.api.Audit;
using sales_portal.core.Audit;
using sales_portal.core.Exceptions;
using sales_portal.core.Notifications;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

[Route("api/audit-logs")]
[ApiController]
[Authorize(Policy = Policies.ManageUsers)]
public class AuditLogsController(
    AppDbContext db,
    IAuditWriter writer,
    ICurrentActor actor) : ControllerBase
{
    private const int ExportCap = 10_000;

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

        var query = ApplyFilters(from, to, actorId, category, action, entityType, outcome, search);
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
                log.UserAgent,
                log.DurationMs,
                Details = log.Details
            })
            .ToListAsync(cancellationToken);

        var existing = await ExistingRoutesAsync(
            items.Select(item => (item.EntityType, item.EntityId)), cancellationToken);

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
                userAgentLabel = AuditDeviceLabel.For(item.UserAgent),
                durationMs = item.DurationMs,
                entityRoute = RouteFor(item.EntityType, item.EntityId, existing),
                Details = ParseDetails(item.Details)
            }),
            page,
            pageSize,
            totalCount
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? actorId,
        [FromQuery] string? category,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? outcome,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(from, to, actorId, category, action, entityType, outcome, search)
            .AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var byAction = await query
            .GroupBy(log => log.Action)
            .Select(group => new { key = group.Key, count = group.Count() })
            .OrderByDescending(row => row.count).ThenBy(row => row.key).Take(20)
            .ToListAsync(cancellationToken);
        var byCategory = await query
            .GroupBy(log => log.Category)
            .Select(group => new { key = group.Key, count = group.Count() })
            .OrderByDescending(row => row.count).ThenBy(row => row.key).Take(20)
            .ToListAsync(cancellationToken);
        var byOutcome = await query
            .GroupBy(log => log.Outcome)
            .Select(group => new { key = group.Key, count = group.Count() })
            .OrderByDescending(row => row.count).ThenBy(row => row.key)
            .ToListAsync(cancellationToken);
        var byActor = await query
            .GroupBy(log => log.ActorUsername)
            .Select(group => new { key = group.Key, count = group.Count() })
            .OrderByDescending(row => row.count).ThenBy(row => row.key).Take(20)
            .ToListAsync(cancellationToken);

        var days = await query
            .GroupBy(log => new { log.OccurredAt.Year, log.OccurredAt.Month, log.OccurredAt.Day })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                count = group.Count()
            })
            .OrderBy(row => row.Year).ThenBy(row => row.Month).ThenBy(row => row.Day)
            .ToListAsync(cancellationToken);
        var byDay = days.Select(day => new
        {
            key = $"{day.Year:D4}-{day.Month:D2}-{day.Day:D2}",
            day.count
        }).ToList();

        return Ok(new { totalCount, byAction, byCategory, byOutcome, byActor, byDay });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? actorId,
        [FromQuery] string? category,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? outcome,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(from, to, actorId, category, action, entityType, outcome, search)
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id);
        var matched = await query.CountAsync(cancellationToken);
        var rowCount = Math.Min(matched, ExportCap);

        RecordExport(rowCount);
        var fileName = $"audit-log-{DateTimeOffset.UtcNow:yyyyMMdd-HHmm}.csv";
        return new AuditCsvResult(
            query.Take(ExportCap).AsAsyncEnumerable(),
            fileName,
            HttpContext.RequestAborted);
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

    private IQueryable<AuditLog> ApplyFilters(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? actorId,
        string? category,
        string? action,
        string? entityType,
        string? outcome,
        string? search)
    {
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

        return query;
    }

    private void RecordExport(int rowCount)
    {
        var audit = new AuditContext();
        audit.SetDescription($"Exported {rowCount} audit rows");
        audit.AddDetail("rowCount", rowCount.ToString(CultureInfo.InvariantCulture));
        audit.AddDetail("cap", ExportCap.ToString(CultureInfo.InvariantCulture));

        writer.Enqueue(AuditEntryFactory.Create(
            HttpContext,
            AuditActions.Exported,
            HttpContext.Request.Method,
            HttpContext.Request.Path.Value ?? string.Empty,
            actor,
            audit,
            new OkResult(),
            null,
            facts: new AuditRequestFacts(HttpContext.Request.Headers.UserAgent.ToString())));
    }

    private async Task<HashSet<(string Type, Guid Id)>> ExistingRoutesAsync(
        IEnumerable<(string? Type, Guid? Id)> entities,
        CancellationToken cancellationToken)
    {
        var idsByType = entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Type) && entity.Id.HasValue)
            .GroupBy(
                entity => entity.Type!.ToLowerInvariant(),
                entity => entity.Id!.Value,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

        var existing = new HashSet<(string, Guid)>();
        foreach (var (type, ids) in idsByType)
        {
            List<Guid> found = type switch
            {
                "offer" => await db.Offers.AsNoTracking().Where(o => ids.Contains(o.Id))
                    .Select(o => o.Id).ToListAsync(cancellationToken),
                "customer" => await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id))
                    .Select(c => c.Id).ToListAsync(cancellationToken),
                "material" => await db.Materials.AsNoTracking().Where(m => ids.Contains(m.Id))
                    .Select(m => m.Id).ToListAsync(cancellationToken),
                "bank" => await db.BankAccounts.AsNoTracking().Where(b => ids.Contains(b.Id))
                    .Select(b => b.Id).ToListAsync(cancellationToken),
                "user" => await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                    .Select(u => u.Id).ToListAsync(cancellationToken),
                _ => []
            };

            foreach (var id in found) existing.Add((type, id));
        }

        return existing;
    }

    private static string? RouteFor(
        string? type,
        Guid? id,
        HashSet<(string Type, Guid Id)> existing)
    {
        if (string.IsNullOrWhiteSpace(type) || !id.HasValue) return null;
        var key = type.ToLowerInvariant();
        if (!existing.Contains((key, id.Value))) return null;

        return key switch
        {
            "offer" => $"/offers/{id.Value}",
            "customer" => $"/customers/{id.Value}",
            "material" => $"/materials/{id.Value}",
            "bank" => $"/banks/{id.Value}",
            "user" => $"/users/{id.Value}",
            _ => null
        };
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
