using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using sales_portal.core.Audit;
using sales_portal.core.Exceptions;
using sales_portal.core.Notifications;

namespace sales_portal.api.Audit;

/// <summary>Builds one row from the request facts without ever reading the body.</summary>
public static class AuditEntryFactory
{
    public static AuditEntry Create(
        HttpContext http,
        string code,
        string httpMethod,
        string path,
        ICurrentActor actor,
        IAuditContext audit,
        IActionResult? result,
        Exception? exception,
        IReadOnlyDictionary<string, object?>? routeValues = null,
        AuditRequestFacts? facts = null)
    {
        var statusCode = StatusFor(exception, result);
        var failed = exception is not null || statusCode >= StatusCodes.Status400BadRequest;
        var action = failed && code == AuditActions.Login ? AuditActions.LoginFailed : code;
        var responseActor = facts?.ResponseActor;
        var failedLogin = failed && action is AuditActions.LoginFailed;
        var entityType = audit.EntityType ?? (responseActor is null ? EntityTypeFor(code) : "user");
        var entityId = audit.EntityId ?? responseActor?.Id ?? EntityIdFromRoute(routeValues);
        var entityLabel = audit.EntityLabel ?? responseActor?.Username ?? entityId?.ToString();

        var user = http.User;
        var entry = new AuditEntry
        {
            ActorUserId = failedLogin ? null : responseActor?.Id ?? actor.UserId,
            ActorUsername = failedLogin
                ? AuditDetails.SanitizeAttemptedUsername(facts?.AttemptedUsername)
                : AuditDetails.SanitizeUsername(
                    responseActor?.Username ?? user.FindFirstValue(ClaimTypes.Name) ?? actor.DisplayName),
            ActorFullName = AuditDetails.SanitizeText(
                responseActor?.FullName ?? user.FindFirstValue("fullName"), 150),
            ActorRole = AuditDetails.SanitizeText(
                responseActor?.Role ?? user.FindFirstValue(ClaimTypes.Role), 50),
            Action = action,
            Category = AuditActions.CategoryFor(action),
            EntityType = entityType,
            EntityId = entityId,
            EntityLabel = entityLabel,
            Description = AuditDetails.SanitizeDescription(audit.Description) is { Length: > 0 } description
                ? description
                : AuditActions.DescriptionFor(action),
            Outcome = failed ? "failure" : "success",
            HttpMethod = httpMethod,
            Path = AuditDetails.SanitizePath(path),
            StatusCode = statusCode,
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = AuditDetails.SanitizeUserAgent(facts?.UserAgent),
            DurationMs = facts?.DurationMs,
            Details = AuditDetails.Sanitize(audit.Details)
        };

        return entry;
    }

    private static int StatusFor(Exception? exception, IActionResult? result)
    {
        if (exception is not null)
        {
            return exception switch
            {
                ValidationException or OfferValidationException => StatusCodes.Status400BadRequest,
                NotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                UnauthorizedException or UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };
        }

        return (result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;
    }

    private static string EntityTypeFor(string code) => code.Split('.')[0] switch
    {
        "auth" => "user",
        "catalog" => "payment_term",
        _ => code.Split('.')[0]
    };

    private static Guid? EntityIdFromRoute(IReadOnlyDictionary<string, object?>? routeValues)
    {
        if (routeValues is null) return null;
        foreach (var key in new[] { "id", "importId", "userId" })
        {
            if (routeValues.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var id))
            {
                return id;
            }
        }

        return null;
    }
}
