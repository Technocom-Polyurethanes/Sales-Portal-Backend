using System.Text.Json;
using sales_portal.core.Exceptions;

namespace sales_portal.api.Middleware;

public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

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
            await WriteAsync(context, StatusCodes.Status400BadRequest, new
            {
                message = exception.Message,
                errors = exception.Errors,
                warnings = exception.Warnings
            });
        }
        catch (ValidationException exception)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, new
            {
                message = exception.Message
            });
        }
        catch (NotFoundException exception)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, new
            {
                message = exception.Message
            });
        }
        catch (ConflictException exception)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, new
            {
                message = exception.Message
            });
        }
        catch (UnauthorizedException exception)
        {
            await WriteAsync(context, StatusCodes.Status401Unauthorized, new
            {
                message = exception.Message
            });
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteAsync(context, StatusCodes.Status401Unauthorized, new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Path}.", context.Request.Path);

            await WriteAsync(context, StatusCodes.Status500InternalServerError, new
            {
                message = "An unexpected error occurred."
            });
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, object payload)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
