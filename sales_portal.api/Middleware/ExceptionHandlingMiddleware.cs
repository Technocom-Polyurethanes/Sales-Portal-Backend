using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.Exceptions;

namespace sales_portal.api.Middleware;

/// <summary>
/// Translates application exceptions into RFC 7807 ProblemDetails responses.
/// The legacy <c>message</c> field is kept as an extension so existing clients that
/// read <c>response.message</c> keep working.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OfferValidationException exception)
        {
            // Offer validation carries a flat list of user-facing messages plus warnings
            // that the offer builder renders; keep its documented response shape.
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
            {
                message = exception.Message,
                errors = exception.Errors,
                warnings = exception.Warnings
            });
        }
        catch (ValidationException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                exception.Message,
                exception.FieldErrors);
        }
        catch (NotFoundException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "Not Found",
                exception.Message);
        }
        catch (ConflictException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message);
        }
        catch (UnauthorizedException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Path}.", context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "An unexpected error occurred.");
        }
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string? detail,
        IDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is { Count: > 0 }
            ? new ValidationProblemDetails(errors)
            : new ProblemDetails();

        problem.Status = statusCode;
        problem.Title = title;
        problem.Detail = detail;
        problem.Type = $"https://httpstatuses.io/{statusCode}";

        // Backwards-compatible alias for clients that read a top-level message.
        if (!string.IsNullOrWhiteSpace(detail))
        {
            problem.Extensions["message"] = detail;
        }

        return WriteJsonAsync(context, statusCode, problem, "application/problem+json");
    }

    private static async Task WriteJsonAsync(
        HttpContext context,
        int statusCode,
        object payload,
        string contentType = "application/json")
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = contentType;
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions));
    }
}
