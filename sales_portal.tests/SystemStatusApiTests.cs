using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
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
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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

        public void Disable()
        {
        }

        public void Update(string listener, bool value)
        {
        }
    }
}
