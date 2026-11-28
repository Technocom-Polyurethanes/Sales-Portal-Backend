using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using sales_portal.api.Audit;
using sales_portal.core.Audit;
using sales_portal.core.DTOs;
using sales_portal.domain.Enums;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public class AuditEntityLabelApiTests
{
    [Fact]
    public async Task Offer_deletion_names_the_offer_reference()
    {
        using var factory = new CapturingApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var basics = TestFactory.SeedBasics(db);
        var offer = TestFactory.AddOffer(db, basics, "Loc/0001-2026/YL/TU");
        using var client = factory.CreateAuthenticatedClient(basics.User.Id);

        var response = await client.DeleteAsync($"/api/offers/{offer.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal("offer", entry.EntityType);
        Assert.Equal(offer.Id, entry.EntityId);
        Assert.Equal("Loc/0001-2026/YL/TU", entry.EntityLabel);
    }

    [Fact]
    public async Task Customer_creation_names_the_customer()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/customers", new UpsertCustomerDto
        {
            CustomerCode = "C-AUDIT",
            Name = "Audit Customer",
            Address = "Cairo"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal("customer", entry.EntityType);
        Assert.NotNull(entry.EntityId);
        Assert.Equal("Audit Customer", entry.EntityLabel);
    }

    [Fact]
    public async Task Material_creation_names_the_material_code_and_name()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/materials", new UpsertMaterialDto
        {
            ItemCode = "MAT-AUDIT",
            Name = "Audit Material",
            Category = "Chemicals"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal("material", entry.EntityType);
        Assert.NotNull(entry.EntityId);
        Assert.Equal("MAT-AUDIT Audit Material", entry.EntityLabel);
    }

    [Fact]
    public async Task Bank_creation_names_the_bank()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/banks", new UpsertBankAccountDto
        {
            Bank = "Audit Bank",
            Branch = "Main",
            AccountHolderName = "Sales Portal",
            AccountNumber = "12345",
            Swift = "AUDITEGSX",
            Iban = "EG123"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal("bank", entry.EntityType);
        Assert.NotNull(entry.EntityId);
        Assert.Equal("Audit Bank", entry.EntityLabel);
    }

    [Fact]
    public async Task User_creation_names_the_username()
    {
        using var factory = new CapturingApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            Username = "audited.user",
            FullName = "Audited User",
            Initials = "AU",
            Role = UserRole.Sales,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = Assert.Single(factory.Writer.Entries);
        Assert.Equal("user", entry.EntityType);
        Assert.NotNull(entry.EntityId);
        Assert.Equal("audited.user", entry.EntityLabel);
    }

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
