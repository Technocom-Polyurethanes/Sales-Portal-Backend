using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using sales_portal.domain.Enums;
using Xunit;

namespace SalesPortal.Tests;

// The role matrix over HTTP. Authorization runs before model binding, so an empty body is enough
// to tell "forbidden" apart from "allowed but the request is incomplete".
[Collection(ProcessEnvironment.Name)]
public class RoleAuthorizationTests : IClassFixture<SalesPortalApiFactory>
{
    private static readonly object EmptyBody = new { };

    private readonly SalesPortalApiFactory _factory;

    public RoleAuthorizationTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    private static void AssertAllowed(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Accountant")]
    public async Task Users_api_is_admin_only(string role)
    {
        using var client = _factory.CreateAuthenticatedClient(role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", EmptyBody)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/reset-password", EmptyBody)).StatusCode);
    }

    [Fact]
    public async Task Users_api_requires_a_token_and_works_for_an_admin()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users")).StatusCode);

        using var admin = _factory.CreateAuthenticatedClient(role: "Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Accountant_reads_offers_but_cannot_write_them()
    {
        using var client = _factory.CreateAuthenticatedClient(role: "Accountant");
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/offers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/offers", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/offers/calculate", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/offers/{id}", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/offers/{id}/revise", EmptyBody)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PatchAsJsonAsync($"/api/offers/{id}/status", new { status = "Issued" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/offers/next-sequence")).StatusCode);
    }

    [Fact]
    public async Task Sales_can_write_offers_and_create_catalogue_records_but_not_edit_or_import()
    {
        using var client = _factory.CreateAuthenticatedClient(role: "Sales");
        var id = Guid.NewGuid();

        AssertAllowed(await client.PostAsJsonAsync("/api/offers/calculate", EmptyBody));
        AssertAllowed(await client.PostAsJsonAsync("/api/customers", EmptyBody));
        AssertAllowed(await client.PostAsJsonAsync("/api/materials", EmptyBody));
        AssertAllowed(await client.PostAsJsonAsync("/api/banks", EmptyBody));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/banks")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/customers/{id}", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/materials/{id}", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/banks/{id}", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/settings", EmptyBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/import/history")).StatusCode);
    }

    [Fact]
    public async Task Accountant_can_import_and_manage_the_catalogue_but_not_settings()
    {
        using var client = _factory.CreateAuthenticatedClient(role: "Accountant");
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/import/history")).StatusCode);
        AssertAllowed(await client.PostAsJsonAsync("/api/customers", EmptyBody));
        AssertAllowed(await client.PutAsJsonAsync($"/api/customers/{id}", EmptyBody));
        AssertAllowed(await client.PutAsJsonAsync($"/api/materials/{id}", EmptyBody));
        AssertAllowed(await client.PutAsJsonAsync($"/api/banks/{id}", EmptyBody));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/settings", EmptyBody)).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_users_token_stops_working_immediately()
    {
        var userId = _factory.SeedUser(UserRole.Admin, isActive: false);
        using var client = _factory.CreateClientWithToken(userId, "Admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/offers")).StatusCode);
    }

    [Fact]
    public async Task A_token_for_an_unknown_user_is_rejected()
    {
        using var client = _factory.CreateClientWithToken(Guid.NewGuid(), "Admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/offers")).StatusCode);
    }

    [Fact]
    public async Task The_role_comes_from_the_database_not_from_the_token()
    {
        var userId = _factory.SeedUser(UserRole.Sales);
        using var client = _factory.CreateClientWithToken(userId, "Admin");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task A_user_created_by_an_admin_can_sign_in_with_the_assigned_role()
    {
        using var admin = _factory.CreateAuthenticatedClient(role: "Admin");
        var username = $"acc-{Guid.NewGuid():N}"[..20];

        var create = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = "Hassan Accountant",
            initials = "ha",
            email = "hassan@example.com",
            role = "Accountant",
            password = "initial-pass-1"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Accountant", created.GetProperty("role").GetString());
        Assert.False(created.TryGetProperty("passwordHash", out _));

        using var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { username, password = "initial-pass-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Accountant", session.GetProperty("user").GetProperty("role").GetString());

        var reset = await admin.PostAsJsonAsync(
            $"/api/users/{created.GetProperty("id").GetGuid()}/reset-password",
            new { newPassword = "second-pass-22" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var oldLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new { username, password = "initial-pass-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new { username, password = "second-pass-22" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        var adminId = _factory.SeedUser(UserRole.Admin);
        _factory.SeedUser(UserRole.Admin);
        using var client = _factory.CreateAuthenticatedClient(adminId, "Admin");

        var response = await client.PutAsJsonAsync($"/api/users/{adminId}", new
        {
            fullName = "Test User",
            initials = "TU",
            role = "Admin",
            isActive = false
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
