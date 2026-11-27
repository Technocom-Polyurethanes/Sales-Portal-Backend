using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using sales_portal.core.Audit;
using sales_portal.core.Notifications;

namespace sales_portal.api.Audit;

/// <summary>Enqueues an audit row after any action marked with [Audit]. Never affects the response.</summary>
public sealed class AuditActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attribute = (context.ActionDescriptor as ControllerActionDescriptor)?
            .MethodInfo.GetCustomAttribute<AuditAttribute>(inherit: true);
        if (attribute is null)
        {
            await next();
            return;
        }

        ActionExecutedContext? executed = null;
        Exception? failure = null;
        try
        {
            executed = await next();
        }
        catch (Exception exception)
        {
            failure = exception;
            Enqueue(context, attribute.Code, executed?.Result, failure);
            throw;
        }

        try
        {
            Enqueue(context, attribute.Code, executed?.Result, null);
        }
        catch (Exception)
        {
            // Auditing is best effort: a broken writer must not turn a successful write into a 500.
        }
    }

    private static void Enqueue(
        ActionExecutingContext context,
        string code,
        IActionResult? result,
        Exception? failure)
    {
        var services = context.HttpContext.RequestServices;
        var writer = services.GetService(typeof(IAuditWriter)) as IAuditWriter;
        if (writer is null) return;

        var actor = services.GetService(typeof(ICurrentActor)) as ICurrentActor
            ?? new AnonymousActor();
        var audit = services.GetService(typeof(IAuditContext)) as IAuditContext
            ?? NoAuditContext.Instance;
        var http = context.HttpContext;

        writer.Enqueue(AuditEntryFactory.Create(
            http,
            code,
            http.Request.Method,
            http.Request.Path.Value ?? string.Empty,
            actor,
            audit,
            result,
            failure,
            context.RouteData.Values));
    }

    private sealed class AnonymousActor : ICurrentActor
    {
        public Guid? UserId => null;
        public string? DisplayName => null;
    }
}
