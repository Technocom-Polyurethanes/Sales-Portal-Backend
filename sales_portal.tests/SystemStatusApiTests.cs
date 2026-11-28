using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using sales_portal.api.Live;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public sealed class SystemStatusApiTests : IDisposable
{
    private readonly SalesPortalApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("Sales")]
    [InlineData("Accountant")]
    public async Task Non_admins_cannot_read_system_status(string role)
    {
        using var client = _factory.CreateAuthenticatedClient(role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/system/status")).StatusCode);
    }

    [Theory]
    [InlineData("disconnected", false, false, "Degraded", true)]
    [InlineData("connected", false, true, "Healthy", false)]
    [InlineData("disabled", true, false, "Degraded", false)]
    public async Task Admin_gets_database_and_live_listener_details(
        string liveState,
        bool disabled,
        bool connected,
        string overall,
        bool pollingFallback)
    {
        using var factory = new StubbedApiFactory(
            new StubLiveListenerStatus(liveState, disabled, connected));
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/system/status");
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        var root = body.RootElement;
        var database = root.GetProperty("database");
        var live = root.GetProperty("live");
        var listenerState = disabled ? "disabled" : connected ? "connected" : "disconnected";

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(overall, root.GetProperty("status").GetString());
        Assert.Equal("Up", database.GetProperty("status").GetString());
        Assert.True(database.GetProperty("latencyMs").GetInt64() >= 0);
        Assert.Equal(liveState, live.GetProperty("state").GetString());
        Assert.Equal(disabled, live.GetProperty("disabled").GetBoolean());
        Assert.Equal(pollingFallback, live.GetProperty("pollingFallback").GetBoolean());
        Assert.Equal(listenerState, live.GetProperty("changes").GetString());
        Assert.Equal(listenerState, live.GetProperty("notifications").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("checkedAt").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("version").GetString()));
        Assert.True(root.GetProperty("uptimeSeconds").GetInt64() >= 0);

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("apiVersion").GetString()));
        Assert.Equal(root.GetProperty("apiVersion").GetString(), root.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("startedAt").GetString()));
        Assert.True(root.TryGetProperty("commit", out _));

        var history = database.GetProperty("latencyHistory");
        Assert.True(history.GetArrayLength() is > 0 and <= 20);
        foreach (var name in new[]
                 {
                     "serverVersion", "sizeBytes", "connectionsUsed", "connectionsMax",
                     "pendingMigrations", "lastMigration"
                 })
        {
            Assert.True(database.TryGetProperty(name, out _));
        }

        Assert.Equal("2026-01-01T00:00:00+00:00", live.GetProperty("changesLastEventAt").GetString());
        Assert.Equal("2026-01-02T00:00:00+00:00", live.GetProperty("notificationsLastEventAt").GetString());
        Assert.Equal(2, live.GetProperty("reconnectCount").GetInt32());
        Assert.Equal("SocketException", live.GetProperty("lastError").GetString());

        Assert.True(root.GetProperty("realtime").GetProperty("notificationQueueDepth").GetInt32() >= 0);
        Assert.True(root.GetProperty("audit").GetProperty("queueDepth").GetInt32() >= 0);
        Assert.True(root.GetProperty("audit").GetProperty("retentionDays").GetInt32() > 0);
        Assert.Contains(
            root.GetProperty("jobs").EnumerateArray(),
            job => job.GetProperty("name").GetString() == "audit-writer");
        Assert.True(root.GetProperty("security").GetProperty("activeUsers").GetInt32() >= 1);
        Assert.True(root.GetProperty("data").GetProperty("activeUsers").GetInt32() >= 1);

        Assert.DoesNotContain("test-only-secret-change-me-0123456789abcdef", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=test", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commit_comes_from_deployment_configuration()
    {
        using var factory = new ConfiguredApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var body = JsonDocument.Parse(await client.GetStringAsync("/api/system/status"));

        Assert.Equal("abc123", body.RootElement.GetProperty("commit").GetString());
    }

    private sealed class ConfiguredApiFactory : SalesPortalApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["App:Commit"] = "abc123" }));
        }
    }

    private sealed class StubbedApiFactory(ILiveListenerStatus status) : SalesPortalApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILiveListenerStatus>();
                services.AddSingleton(status);
            });
        }
    }

    private sealed class StubLiveListenerStatus(string state, bool disabled, bool connected)
        : ILiveListenerStatus
    {
        public string State { get; } = state;
        public bool Disabled { get; } = disabled;

        public bool IsConnected(string listener) => connected;

        public DateTimeOffset? ChangesLastEventAt { get; } =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset? NotificationsLastEventAt { get; } =
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

        public int ReconnectCount => 2;
        public string? LastError => "SocketException";

        public void Disable()
        {
        }

        public void Update(string listener, bool value)
        {
        }

        public void RecordEvent(string listener)
        {
        }

        public void RecordReconnect()
        {
        }

        public void RecordError(string error)
        {
        }
    }
}
