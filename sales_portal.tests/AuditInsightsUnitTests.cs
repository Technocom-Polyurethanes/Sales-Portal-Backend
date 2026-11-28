using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using sales_portal.api.Audit;
using sales_portal.core.Audit;
using sales_portal.core.DTOs;
using sales_portal.domain.Enums;
using Xunit;

namespace SalesPortal.Tests;

public class AuditInsightsUnitTests
{
    [Fact]
    public async Task Anonymous_login_success_takes_the_actor_from_the_response()
    {
        var response = new LoginResponseDto
        {
            User = new AuthenticatedUserDto
            {
                Id = Guid.NewGuid(),
                Username = "amgad",
                FullName = "Amgad Anwar",
                Role = UserRole.Admin
            }
        };

        var entry = await RunFilterAsync(AuditActions.Login, new ObjectResult(response));

        Assert.Equal(response.User.Id, entry.ActorUserId);
        Assert.Equal("amgad", entry.ActorUsername);
        Assert.Equal("Amgad Anwar", entry.ActorFullName);
        Assert.Equal("Admin", entry.ActorRole);
    }

    [Fact]
    public async Task Failed_login_keeps_the_sanitized_attempted_username_and_no_actor_id()
    {
        var attempted = new string('a', 120).Insert(10, "\r\n");

        var entry = await RunFilterAsync(
            AuditActions.Login,
            new UnauthorizedResult(),
            new LoginRequestDto { Username = attempted, Password = "hunter2" });

        Assert.Null(entry.ActorUserId);
        Assert.Equal(100, entry.ActorUsername.Length);
        Assert.DoesNotContain("\r", entry.ActorUsername, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", entry.Details.Values, StringComparer.Ordinal);
        Assert.Equal("auth.login_failed", entry.Action);
    }

    [Fact]
    public void Every_action_code_has_an_explicit_default_description()
    {
        foreach (var code in AuditActions.All)
        {
            var description = AuditActions.DescriptionFor(code);
            Assert.False(string.IsNullOrWhiteSpace(description), $"{code} has no default description.");
        }

        Assert.Equal("Signed in", AuditActions.DescriptionFor(AuditActions.Login));
        Assert.Equal("Failed sign-in attempt", AuditActions.DescriptionFor(AuditActions.LoginFailed));
        Assert.Equal("Changed own password", AuditActions.DescriptionFor(AuditActions.PasswordChanged));
        Assert.Equal("Opened the proforma document", AuditActions.DescriptionFor(AuditActions.OfferDocumentOpened));
    }

    [Fact]
    public void Request_facts_capture_the_raw_user_agent_and_duration()
    {
        var entry = AuditEntryFactory.Create(
            new DefaultHttpContext(),
            AuditActions.OfferCreated,
            "POST",
            "/api/offers",
            new InMemoryActor(),
            new AuditContext(),
            new OkResult(),
            null,
            facts: new AuditRequestFacts(
                UserAgent: "Mozilla/5.0 Chrome/131.0.0.0 Windows NT 10.0",
                DurationMs: 42));

        Assert.Equal("Mozilla/5.0 Chrome/131.0.0.0 Windows NT 10.0", entry.UserAgent);
        Assert.Equal(42, entry.DurationMs);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64) Chrome/131.0.0.0 Safari/537.36", "Chrome 131 on Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0) Edg/120.0.0.0", "Edge 120 on Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0) CriOS/131.0.0.0 Mobile", "Chrome 131 on iOS")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) Firefox/132.0", "Firefox 132 on macOS")]
    [InlineData("curl/8.0", "Unknown device")]
    public void Device_label_is_derived_without_a_package(string userAgent, string expected)
    {
        Assert.Equal(expected, AuditDeviceLabel.For(userAgent));
    }

    [Fact]
    public void Long_user_agents_are_bounded_before_they_reach_storage()
    {
        var facts = new AuditRequestFacts(UserAgent: new string('x', 400));

        var entry = AuditEntryFactory.Create(
            new DefaultHttpContext(),
            AuditActions.Login,
            "POST",
            "/api/auth/login",
            new InMemoryActor(),
            new AuditContext(),
            new OkResult(),
            null,
            facts: facts);

        Assert.Equal(256, entry.UserAgent!.Length);
    }

    [Fact]
    public async Task Audit_enqueue_failure_does_not_mask_the_original_request_failure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditWriter>(new ThrowingWriter());
        var method = typeof(AuditFixture).GetMethod(nameof(AuditFixture.Action))!;
        var descriptor = new ControllerActionDescriptor { MethodInfo = method };
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
        var executing = new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new AuditFixture());
        var original = new InvalidOperationException("original failure");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AuditActionFilter().OnActionExecutionAsync(
                executing, () => Task.FromException<ActionExecutedContext>(original)));

        Assert.Same(original, thrown);
    }

    private static async Task<AuditEntry> RunFilterAsync(
        string code,
        IActionResult result,
        LoginRequestDto? request = null)
    {
        var writer = new CapturingWriter();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditWriter>(writer);

        var method = typeof(AuditFixture).GetMethod(nameof(AuditFixture.Action))!;
        var descriptor = new ControllerActionDescriptor { MethodInfo = method };
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Headers.UserAgent = "Mozilla/5.0 Chrome/131.0.0.0 Windows NT 10.0";
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");

        var actionContext = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
        var arguments = new Dictionary<string, object?>();
        if (request is not null) arguments["request"] = request;
        var executing = new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), arguments, new AuditFixture());
        var executed = new ActionExecutedContext(
            actionContext, new List<IFilterMetadata>(), new AuditFixture()) { Result = result };

        await new AuditActionFilter().OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.NotNull(writer.Entry);
        return writer.Entry!;
    }

    private sealed class AuditFixture
    {
        [Audit(AuditActions.Login)]
        public void Action()
        {
        }
    }

    private sealed class ThrowingWriter : IAuditWriter
    {
        public void Enqueue(AuditEntry entry) => throw new InvalidOperationException("writer failure");
    }

    private sealed class CapturingWriter : IAuditWriter
    {
        public AuditEntry? Entry { get; private set; }

        public void Enqueue(AuditEntry entry) => Entry = entry;
    }

    private sealed class InMemoryActor : sales_portal.core.Notifications.ICurrentActor
    {
        public Guid? UserId => Guid.NewGuid();
        public string? DisplayName => "tester";
    }
}
