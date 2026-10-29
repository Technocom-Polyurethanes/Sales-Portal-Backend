using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

public class ApiContractTests : IClassFixture<SalesPortalApiFactory>
{
    private readonly SalesPortalApiFactory _factory;

    public ApiContractTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("draft")]
    [InlineData("ISSUED")]
    [InlineData("Accepted")]
    [InlineData("Rejected")]
    public async Task Status_filter_accepts_known_values_case_insensitively(string status)
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/offers?status={status}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_status_returns_a_readable_problem_details_response()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/offers?status=Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Banana", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("An unexpected error occurred.", body, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SalesPortalApiFactory : WebApplicationFactory<Program>
{
    private const string JwtSecret = "test-only-secret-change-me-0123456789abcdef";
    private const string JwtIssuer = "SalesPortalAPI";
    private const string JwtAudience = "SalesPortalClient";

    public SalesPortalApiFactory()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
        Environment.SetEnvironmentVariable("JWT_ISSUER", JwtIssuer);
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", JwtAudience);
        Environment.SetEnvironmentVariable(
            "DB_CONNECTION_STRING",
            "Server=localhost;Port=5433;Database=sales_portal_tests;User Id=test;Password=test;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"sales-portal-api-tests-{Guid.NewGuid()}"));
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken());
        return client;
    }

    private static string CreateToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
