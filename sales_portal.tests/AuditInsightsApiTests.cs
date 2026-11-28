using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using sales_portal.api.Audit;
using sales_portal.core.Audit;
using sales_portal.core.DTOs;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public class AuditInsightsApiTests
{
    [Theory]
    [InlineData("Sales")]
    [InlineData("Accountant")]
    public async Task Non_admins_cannot_read_summary_or_export(string role)
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient(role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs/export")).StatusCode);
    }

    [Theory]
    [InlineData("/api/audit-logs/summary")]
    [InlineData("/api/audit-logs/export")]
    public async Task Anonymous_requests_are_unauthorized(string path)
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task List_adds_device_duration_and_entity_route()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var customerId = SeedCustomer(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.Add(Log(
                action: AuditActions.CustomerCreated,
                category: "customers",
                entityType: "customer",
                entityId: customerId,
                entityLabel: "Audit Customer",
                description: "Created customer",
                userAgent: "Mozilla/5.0 Chrome/131.0.0.0 Windows NT 10.0",
                durationMs: 42));
            db.SaveChanges();
        }

        using var body = JsonDocument.Parse(await client.GetStringAsync("/api/audit-logs"));
        var item = body.RootElement.GetProperty("items")[0];

        Assert.Equal("Chrome 131 on Windows", item.GetProperty("userAgentLabel").GetString());
        Assert.Equal(42, item.GetProperty("durationMs").GetInt32());
        Assert.Equal($"/customers/{customerId}", item.GetProperty("entityRoute").GetString());
        Assert.Empty(factory.Writer.Entries);
    }

    [Fact]
    public async Task Summary_groups_rows_and_respects_the_category_filter()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var actorId = factory.SeedUser(UserRole.Admin);
        var otherId = factory.SeedUser(UserRole.Admin);
        SeedSummaryLogs(factory, actorId, otherId);

        using var body = JsonDocument.Parse(
            await client.GetStringAsync("/api/audit-logs/summary?category=offers"));

        var root = body.RootElement;
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("byAction")[0].GetProperty("count").GetInt32());
        Assert.Equal("offer.created", root.GetProperty("byAction")[0].GetProperty("key").GetString());
        Assert.Equal(1, CountWhere(root.GetProperty("byOutcome"), "success"));
        Assert.Equal(1, CountWhere(root.GetProperty("byOutcome"), "failure"));
        Assert.Equal(2, root.GetProperty("byActor")[0].GetProperty("count").GetInt32());
        Assert.Equal(1, CountWhere(root.GetProperty("byDay"), "2026-01-01"));
        Assert.Equal(1, CountWhere(root.GetProperty("byDay"), "2026-01-02"));
        Assert.Empty(factory.Writer.Entries);
    }

    [Fact]
    public async Task Export_writes_a_bom_neutralises_formulas_and_audits_the_export()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        SeedExportLogs(factory);

        var response = await client.GetAsync("/api/audit-logs/export");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Contains("attachment; filename=\"audit-log-", response.Content.Headers.ContentDisposition?.ToString());
        Assert.Contains("occurred_at,actor,action,category,outcome,entity,description,path,status_code,duration_ms,user_agent", csv);
        Assert.Contains("\"'=cmd, \"\"quoted\"\"\nsecond\"", csv);
        Assert.Contains("'+SUM(1)", csv);
        Assert.Contains("'@reference", csv);
        Assert.DoesNotContain(",=cmd", csv);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);

        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal(AuditActions.Exported, entry.Action);
        Assert.Equal("2", entry.Details["rowCount"]);
    }

    [Fact]
    public async Task Export_caps_at_ten_thousand_rows()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.AddRange(Enumerable.Range(0, 10_001).Select(index => Log(
                action: AuditActions.OfferCreated,
                category: "offers",
                description: $"Row {index}",
                occurredAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index))));
            db.SaveChanges();
        }

        var csv = await client.GetStringAsync("/api/audit-logs/export");

        Assert.Equal(10_001, csv.Split('\n').Length - 1);
        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal("10000", entry.Details["rowCount"]);
    }

    private static int CountWhere(JsonElement groups, string key) =>
        groups.EnumerateArray()
            .Where(group => group.GetProperty("key").GetString() == key)
            .Select(group => group.GetProperty("count").GetInt32())
            .FirstOrDefault();

    private static Guid SeedCustomer(CapturingApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer
        {
            CustomerCode = "C-AUDIT",
            Name = "Audit Customer",
            Address = "Cairo"
        };
        db.Customers.Add(customer);
        db.SaveChanges();
        return customer.Id;
    }

    private static void SeedSummaryLogs(CapturingApiFactory factory, Guid actorId, Guid otherId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var day = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        db.AuditLogs.AddRange(
            Log(action: AuditActions.OfferCreated, category: "offers", actorUsername: actorId.ToString(), description: "offer.created", outcome: "success", occurredAt: day),
            Log(action: AuditActions.OfferCreated, category: "offers", actorUsername: actorId.ToString(), description: "offer.created", outcome: "failure", occurredAt: day.AddDays(1)),
            Log(action: AuditActions.CustomerCreated, category: "customers", actorUsername: otherId.ToString(), description: "customer.created", outcome: "success", occurredAt: day.AddDays(1)));
        db.SaveChanges();
    }

    private static void SeedExportLogs(CapturingApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogs.AddRange(
            Log(
                action: AuditActions.OfferCreated,
                category: "offers",
                actorUsername: "+SUM(1)",
                entityLabel: "@reference",
                description: "=cmd, \"quoted\"\nsecond",
                outcome: "success",
                occurredAt: new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
                durationMs: 12,
                userAgent: "Mozilla/5.0 Chrome/131.0.0.0 Windows NT 10.0"),
            Log(
                action: AuditActions.CustomerCreated,
                category: "customers",
                actorUsername: "kero",
                description: "customer.created",
                outcome: "success",
                occurredAt: new DateTimeOffset(2026, 2, 2, 10, 0, 0, TimeSpan.Zero)));
        db.SaveChanges();
    }

    private static AuditLog Log(
        string action = AuditActions.OfferCreated,
        string category = "offers",
        string actorUsername = "actor",
        string actorFullName = "Actor",
        string entityLabel = "Entity",
        string description = "Changed",
        string outcome = "success",
        string? userAgent = null,
        int? durationMs = null,
        DateTimeOffset? occurredAt = null,
        Guid? actorId = null,
        Guid? entityId = null,
        string? entityType = null) => new()
    {
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
        ActorUserId = actorId,
        ActorUsername = actorUsername,
        ActorFullName = actorFullName,
        ActorRole = "Admin",
        Action = action,
        Category = category,
        EntityType = entityType,
        EntityId = entityId,
        EntityLabel = entityLabel,
        Description = description,
        Outcome = outcome,
        HttpMethod = "POST",
        Path = "/api/offers",
        StatusCode = 201,
        UserAgent = userAgent,
        DurationMs = durationMs
    };

    private sealed class CapturingApiFactory : SalesPortalApiFactory
    {
        public CapturingWriter Writer { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(Writer);
            });
        }
    }

    private sealed class CapturingWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Enqueue(AuditEntry entry) => Entries.Add(entry);
    }
}
