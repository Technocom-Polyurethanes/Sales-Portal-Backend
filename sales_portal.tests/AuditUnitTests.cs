using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using sales_portal.api.Audit;
using sales_portal.api.Controllers;
using sales_portal.core.Audit;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.Notifications;
using Xunit;

namespace SalesPortal.Tests;

public class AuditUnitTests
{
    [Theory]
    [InlineData("offer.created", "offers")]
    [InlineData("customer.deleted", "customers")]
    [InlineData("material.updated", "materials")]
    [InlineData("bank.created", "banks")]
    [InlineData("catalog.payment_term_created", "catalog")]
    [InlineData("settings.updated", "settings")]
    [InlineData("user.created", "users")]
    [InlineData("import.committed", "imports")]
    [InlineData("auth.login", "auth")]
    public void Category_is_derived_from_the_action_code(string action, string category)
    {
        Assert.Equal(category, AuditActions.CategoryFor(action));
    }

    [Fact]
    public void Details_sanitizer_rejects_secret_keys_and_truncates_long_values()
    {
        var sanitized = AuditDetails.Sanitize(new Dictionary<string, string>
        {
            ["password"] = "hunter2",
            ["ApiToken"] = "abc",
            ["client_secret"] = "xyz",
            ["passwordHash"] = "hash",
            ["from"] = "Draft",
            ["note"] = new string('x', 250)
        });

        Assert.Equal("Draft", sanitized["from"]);
        Assert.Equal(200, sanitized["note"].Length);
        Assert.DoesNotContain("password", sanitized.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", sanitized.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", sanitized.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", sanitized.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Entry_uses_the_signed_in_actor_and_context_enrichment()
    {
        var actor = new FakeActor(Guid.NewGuid(), "Kero A.");
        var context = new AuditContext();
        context.SetEntity("offer", actor.UserId, "Loc/0001-2026/KA");
        context.SetDescription("Changed offer status from Draft to Sent");
        context.AddDetail("from", "Draft");
        context.AddDetail("to", "Sent");

        var http = CreateHttpContext(actor, role: "Admin");
        var entry = AuditEntryFactory.Create(
            http,
            "offer.status_changed",
            "PATCH",
            "/api/offers/{id}/status",
            actor,
            context,
            new OkResult(),
            null);

        Assert.Equal("offer.status_changed", entry.Action);
        Assert.Equal("offers", entry.Category);
        Assert.Equal(actor.UserId, entry.ActorUserId);
        Assert.Equal("kero", entry.ActorUsername);
        Assert.Equal("Kero A.", entry.ActorFullName);
        Assert.Equal("Admin", entry.ActorRole);
        Assert.Equal("Loc/0001-2026/KA", entry.EntityLabel);
        Assert.Equal("Changed offer status from Draft to Sent", entry.Description);
        Assert.Equal("success", entry.Outcome);
        Assert.Equal(200, entry.StatusCode);
        Assert.Equal("Draft", entry.Details["from"]);
    }

    [Fact]
    public void Entry_falls_back_to_route_values_when_a_service_did_not_enrich_the_context()
    {
        var actor = new FakeActor(Guid.NewGuid(), "Kero A.");
        var offerId = Guid.NewGuid();
        var entry = AuditEntryFactory.Create(
            CreateHttpContext(actor, "Admin"),
            "offer.status_changed",
            "PATCH",
            "/api/offers/{id}/status",
            actor,
            new AuditContext(),
            new OkResult(),
            null,
            routeValues: new RouteValueDictionary { ["id"] = offerId });

        Assert.Equal("offer", entry.EntityType);
        Assert.Equal(offerId, entry.EntityId);
        Assert.Equal(offerId.ToString(), entry.EntityLabel);
        Assert.Equal("Changed offer status", entry.Description);
        Assert.Null(entry.UserAgent);
    }

    [Fact]
    public void Entry_marks_a_validation_failure_without_throwing()
    {
        var actor = new FakeActor(Guid.NewGuid(), "Kero A.");
        var entry = AuditEntryFactory.Create(
            CreateHttpContext(actor, "Admin"),
            "customer.created",
            "POST",
            "/api/customers",
            actor,
            new AuditContext(),
            new BadRequestResult(),
            null);

        Assert.Equal("failure", entry.Outcome);
        Assert.Equal(400, entry.StatusCode);
        Assert.Equal("Created customer", entry.Description);
    }

    [Fact]
    public void Thrown_validation_errors_keep_the_status_the_client_receives()
    {
        var actor = new FakeActor(Guid.NewGuid(), "Kero A.");
        var entry = AuditEntryFactory.Create(
            CreateHttpContext(actor, "Admin"),
            "customer.created",
            "POST",
            "/api/customers",
            actor,
            new AuditContext(),
            null,
            new ValidationException("Name is required."));

        Assert.Equal("failure", entry.Outcome);
        Assert.Equal(400, entry.StatusCode);
    }

    [Fact]
    public void Anonymous_failed_login_never_stores_request_credentials()
    {
        var entry = AuditEntryFactory.Create(
            CreateHttpContext(null, role: null),
            "auth.login_failed",
            "POST",
            "/api/auth/login",
            new FakeActor(null, null),
            new AuditContext(),
            new UnauthorizedResult(),
            null);

        Assert.Null(entry.ActorUserId);
        Assert.Equal(string.Empty, entry.ActorUsername);
        Assert.Equal("failure", entry.Outcome);
        Assert.DoesNotContain("password", entry.Details.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", entry.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failed_writes_are_retried_only_up_to_the_bound()
    {
        var first = new AuditEntry();
        var exhausted = new AuditEntry { RetryCount = 3 };

        var retried = AuditWriteWorker.PrepareRetries([first, exhausted], 3);

        Assert.Single(retried);
        Assert.Same(first, retried[0]);
        Assert.Equal(1, first.RetryCount);
        Assert.Equal(3, exhausted.RetryCount);
    }

    [Fact]
    public void Overflowing_queue_drops_writes_without_throwing_and_counts_them()
    {
        var writer = new QueuedAuditWriter(new Microsoft.Extensions.Logging.Abstractions.NullLogger<QueuedAuditWriter>());
        var status = (IAuditStatus)writer;

        for (var i = 0; i < 6000; i++)
        {
            writer.Enqueue(new AuditEntry { Action = "settings.updated" });
        }

        Assert.Equal(1000, status.Dropped);
        Assert.Equal(5000, status.QueueDepth);
    }

    [Fact]
    public void Every_mutating_action_has_an_audit_attribute()
    {
        var assembly = typeof(AuditLogsController).Assembly;
        var controllers = assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal) && type.IsClass);

        var missing = new List<string>();
        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var verbs = method.GetCustomAttributes()
                    .OfType<HttpMethodAttribute>()
                    .SelectMany(attribute => attribute.HttpMethods)
                    .ToArray();
                if (verbs.Length == 0 || verbs.All(verb => verb == "GET")) continue;

                var audited = method.IsDefined(typeof(AuditAttribute), inherit: true);
                var exempt = method.IsDefined(typeof(NoAuditAttribute), inherit: true);
                if (!audited && !exempt) missing.Add($"{controller.Name}.{method.Name}");
            }
        }

        Assert.Empty(missing);
    }

    private static HttpContext CreateHttpContext(ICurrentActor? actor, string? role)
    {
        var http = new DefaultHttpContext();
        var identity = new ClaimsIdentity(actor?.UserId is null ? null : "Test");
        if (actor?.UserId is { } userId)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Name, "kero"));
            identity.AddClaim(new Claim("fullName", actor.DisplayName ?? string.Empty));
        }
        if (role is not null) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        http.User = new ClaimsPrincipal(identity);
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        var services = new ServiceCollection();
        if (actor is not null) services.AddSingleton(actor);
        http.RequestServices = services.BuildServiceProvider();
        return http;
    }

    private sealed class FakeActor(Guid? id, string? name) : ICurrentActor
    {
        public Guid? UserId => id;
        public string? DisplayName => name;
    }
}
