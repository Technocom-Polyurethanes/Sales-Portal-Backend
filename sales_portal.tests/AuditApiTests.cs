using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public class AuditApiTests : IClassFixture<SalesPortalApiFactory>
{
    private readonly SalesPortalApiFactory _factory;

    public AuditApiTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Accountant")]
    public async Task Non_admins_cannot_read_the_audit_log(string role)
    {
        using var client = _factory.CreateAuthenticatedClient(role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs/filters")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_filtered_rows_and_read_filter_options()
    {
        var actorId = _factory.SeedUser(sales_portal.domain.Enums.UserRole.Admin);
        SeedLog(actorId);
        using var client = _factory.CreateAuthenticatedClient(actorId, "Admin");

        var list = await client.GetFromJsonAsync<AuditListResponse>(
            "/api/audit-logs?category=offers&action=offer.created&page=1&pageSize=20");
        var filters = await client.GetFromJsonAsync<AuditFiltersResponse>("/api/audit-logs/filters");

        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
        Assert.Single(list.Items);
        Assert.Equal("offer.created", list.Items[0].Action);
        Assert.Equal("Loc/0042-2026", list.Items[0].EntityLabel);
        Assert.Equal("Draft", list.Items[0].Details["from"]);
        Assert.Contains(filters!.Actions, action => action == "offer.created");
        Assert.Contains(filters.Categories, category => category == "offers");
    }

    private void SeedLog(Guid actorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            ActorUsername = "kero",
            ActorFullName = "Kero A.",
            ActorRole = "Admin",
            Action = "offer.created",
            Category = "offers",
            EntityType = "offer",
            EntityId = Guid.NewGuid(),
            EntityLabel = "Loc/0042-2026",
            Description = "Created offer",
            Outcome = "success",
            HttpMethod = "POST",
            Path = "/api/offers",
            StatusCode = 201,
            Details = "{\"from\":\"Draft\"}"
        });
        db.SaveChanges();
    }

    private sealed record AuditListResponse(List<AuditListItem> Items, int TotalCount);

    private sealed record AuditListItem(string Action, string? EntityLabel, Dictionary<string, string> Details);

    private sealed record AuditFiltersResponse(string[] Actions, string[] Categories);
}
