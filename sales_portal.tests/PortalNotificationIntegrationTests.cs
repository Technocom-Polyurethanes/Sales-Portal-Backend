using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using sales_portal.core.Caching;
using sales_portal.core.Live;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

/// <summary>
/// Real-Postgres proof for direct database writes. These tests need a connection string and are
/// skipped when the environment variable is not set.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public class PortalNotificationIntegrationTests
{
    private const string TestConnectionStringEnv = "SALES_PORTAL_TEST_DB_CONNECTION_STRING";

    private static string? TestConnectionString() =>
        Environment.GetEnvironmentVariable(TestConnectionStringEnv);

    [Fact]
    public async Task Direct_sql_write_emits_sse_and_invalidates_cache()
    {
        if (string.IsNullOrWhiteSpace(TestConnectionString())) return;

        using var factory = new PortalNotificationApiFactory(TestConnectionString()!);
        await factory.PrepareDatabaseAsync();

        await using (var context = factory.CreateDbContext())
        {
            context.Database.ExecuteSqlRaw(
                "INSERT INTO customers (id, customer_code, name, address, default_currency, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, NOW(), NOW())",
                Guid.NewGuid(), "CUST-DIRECT", "Before direct edit", "Address", "EGP");
        }

        var cache = (ReferenceDataCache)factory.Services.GetRequiredService<IReferenceDataCache>();
        var loads = 0;
        await cache.GetOrCreateAsync(
            "customers:integration",
            new[] { CacheRegions.Customers },
            async () =>
            {
                loads++;
                await using var context = factory.CreateDbContext();
                return await context.Customers
                    .Where(c => c.CustomerCode == "CUST-DIRECT")
                    .Select(c => c.Name)
                    .SingleAsync();
            });
        Assert.Equal(1, loads);

        using var client = factory.CreateAuthenticatedClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/events"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);
        response.EnsureSuccessStatusCode();

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token));
        await ReadEventAsync(reader, "ready", cancellation.Token);

        await using (var admin = new NpgsqlConnection(TestConnectionString()!))
        {
            await admin.OpenAsync(cancellation.Token);
            await using var command = admin.CreateCommand();
            command.CommandText = "UPDATE customers SET name = 'After direct edit' WHERE customer_code = 'CUST-DIRECT'";
            await command.ExecuteNonQueryAsync(cancellation.Token);
        }

        var change = await ReadEventAsync(reader, "change", cancellation.Token);
        using var json = JsonDocument.Parse(ExtractData(change));
        var topics = json.RootElement.GetProperty("topics").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("customers", topics);

        var refreshed = await cache.GetOrCreateAsync(
            "customers:integration",
            new[] { CacheRegions.Customers },
            async () =>
            {
                loads++;
                await using var context = factory.CreateDbContext();
                return await context.Customers
                    .Where(c => c.CustomerCode == "CUST-DIRECT")
                    .Select(c => c.Name)
                    .SingleAsync();
            });
        Assert.Equal("After direct edit", refreshed);
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task Listener_recovers_after_connection_is_terminated()
    {
        if (string.IsNullOrWhiteSpace(TestConnectionString())) return;

        using var factory = new PortalNotificationApiFactory(TestConnectionString()!);
        await factory.PrepareDatabaseAsync();

        await using (var context = factory.CreateDbContext())
        {
            context.Database.ExecuteSqlRaw(
                "INSERT INTO customers (id, customer_code, name, address, default_currency, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, NOW(), NOW())",
                Guid.NewGuid(), "CUST-RECONNECT", "Before", "Address", "EGP");
        }

        using var client = factory.CreateAuthenticatedClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/events"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);
        response.EnsureSuccessStatusCode();

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token));
        await ReadEventAsync(reader, "ready", cancellation.Token);

        await using (var admin = new NpgsqlConnection(TestConnectionString()!))
        {
            await admin.OpenAsync(cancellation.Token);
            await using (var terminate = admin.CreateCommand())
            {
                terminate.CommandText =
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name = 'SalesPortalLiveListener' AND pid <> pg_backend_pid()";
                await terminate.ExecuteNonQueryAsync(cancellation.Token);
            }
        }

        // After reconnect the listener re-announces all tables; wait for a change event.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellation.Token);
        await using (var admin = new NpgsqlConnection(TestConnectionString()!))
        {
            await admin.OpenAsync(cancellation.Token);
            await using (var command = admin.CreateCommand())
            {
                command.CommandText = "UPDATE customers SET name = 'After reconnect' WHERE customer_code = 'CUST-RECONNECT'";
                await command.ExecuteNonQueryAsync(cancellation.Token);
            }
        }

        var change = await ReadEventAsync(reader, "change", cancellation.Token);
        Assert.Contains("\"customers\"", change);
    }

    [Fact]
    public async Task Burst_of_direct_inserts_is_coalesced()
    {
        if (string.IsNullOrWhiteSpace(TestConnectionString())) return;

        using var factory = new PortalNotificationApiFactory(TestConnectionString()!);
        await factory.PrepareDatabaseAsync();

        using var client = factory.CreateAuthenticatedClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/events"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);
        response.EnsureSuccessStatusCode();

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token));
        await ReadEventAsync(reader, "ready", cancellation.Token);

        await using (var admin = new NpgsqlConnection(TestConnectionString()!))
        {
            await admin.OpenAsync(cancellation.Token);
            for (var i = 0; i < 500; i++)
            {
                await using var command = admin.CreateCommand();
                command.CommandText =
                    "INSERT INTO customers (id, customer_code, name, address, default_currency, created_at, updated_at) VALUES (@id, @code, @name, @address, @currency, NOW(), NOW())";
                command.Parameters.AddWithValue("id", Guid.NewGuid());
                command.Parameters.AddWithValue("code", $"CUST-BURST-{i}");
                command.Parameters.AddWithValue("name", $"Burst {i}");
                command.Parameters.AddWithValue("address", "Address");
                command.Parameters.AddWithValue("currency", "EGP");
                await command.ExecuteNonQueryAsync(cancellation.Token);
            }
        }

        var events = 0;
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            readCancellation.CancelAfter(remaining);
            try
            {
                var change = await ReadEventAsync(reader, "change", readCancellation.Token);
                if (change.Contains("\"customers\"")) events++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.InRange(events, 1, 20);
    }

    private static async Task<string> ReadEventAsync(StreamReader reader, string eventName, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("The stream ended before the event arrived.");
            lines.Add(line);
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var text = string.Join("\n", lines);
                if (text.Contains($"event: {eventName}", StringComparison.Ordinal)) return text;
                lines.Clear();
            }
        }
    }

    private static string ExtractData(string sseEvent)
    {
        var line = sseEvent.Split('\n').FirstOrDefault(l => l.StartsWith("data:", StringComparison.Ordinal));
        return line?["data:".Length..].Trim() ?? "{}";
    }

    private sealed class PortalNotificationApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public PortalNotificationApiFactory(string connectionString)
        {
            _connectionString = connectionString;
            Environment.SetEnvironmentVariable("JWT_SECRET", "test-only-secret-change-me-0123456789abcdef");
            Environment.SetEnvironmentVariable("JWT_ISSUER", "SalesPortalAPI");
            Environment.SetEnvironmentVariable("JWT_AUDIENCE", "SalesPortalClient");
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", connectionString);
            Environment.SetEnvironmentVariable("PORTAL_NOTIFICATIONS_CONNECTION_STRING", connectionString);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _connectionString,
                    ["ConnectionStrings:PortalNotifications"] = _connectionString,
                    ["Database:AutoMigrate"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Use real Postgres so the LISTEN trigger path is exercised.
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_connectionString, npgsql => npgsql.MigrationsAssembly("sales_portal.infrastructure"))
                        .UseSnakeCaseNamingConvention());

                services.RemoveAll<IHostedService>();
                services.AddHostedService<sales_portal.api.Live.PortalChangeNotificationService>();
            });
        }

        public AppDbContext CreateDbContext() =>
            Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

        public async Task PrepareDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        public HttpClient CreateAuthenticatedClient(Guid? userId = null, string role = "Admin")
        {
            var id = userId ?? Guid.NewGuid();
            EnsureUser(id, Enum.Parse<sales_portal.domain.Enums.UserRole>(role));
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", SalesPortalApiFactoryHelper.CreateToken(id, role));
            return client;
        }

        private void EnsureUser(Guid id, sales_portal.domain.Enums.UserRole role)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Users.Any(u => u.Id == id)) return;
            db.Users.Add(new sales_portal.domain.Models.User
            {
                Id = id,
                Username = $"test-{id:N}",
                FullName = "Test User",
                Initials = "TU",
                PasswordHash = "not-used",
                Role = role,
                IsActive = true
            });
            db.SaveChanges();
        }
    }

    private static class SalesPortalApiFactoryHelper
    {
        public static string CreateToken(Guid userId, string role)
        {
            var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-secret-change-me-0123456789abcdef")),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "SalesPortalAPI",
                audience: "SalesPortalClient",
                claims: new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)
                },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
