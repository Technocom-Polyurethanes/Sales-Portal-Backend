using System.Text;
using Microsoft.AspNetCore.Mvc;
using sales_portal.domain.Models;

namespace sales_portal.api.Audit;

/// <summary>Streams a bounded audit export without holding the whole result in memory.</summary>
public sealed class AuditCsvResult(IAsyncEnumerable<AuditLog> rows, string fileName, CancellationToken cancellationToken)
    : IActionResult
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";

        await response.Body.WriteAsync(Bom, cancellationToken);
        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false), 4096, leaveOpen: true);
        await writer.WriteLineAsync(
            "occurred_at,actor,action,category,outcome,entity,description,path,status_code,duration_ms,user_agent");

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(string.Join(',', new[]
            {
                Escape(row.OccurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                Escape(row.ActorUsername),
                Escape(row.Action),
                Escape(row.Category),
                Escape(row.Outcome),
                Escape(row.EntityLabel ?? row.EntityType),
                Escape(row.Description),
                Escape(row.Path),
                row.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.DurationMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                Escape(row.UserAgent)
            }));
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var text = value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
            ? $"'{value}"
            : value;
        return text.Contains(',') || text.Contains('"') || text.Contains('\r') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;
    }
}
