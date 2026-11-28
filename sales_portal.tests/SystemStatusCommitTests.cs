using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public sealed class SystemStatusCommitTests : IDisposable
{
    private readonly AssemblyCommitApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Commit_falls_back_to_the_revision_embedded_in_the_build()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var body = JsonDocument.Parse(await client.GetStringAsync("/api/system/status"));

        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var revision = informational.Split('+').Last();

        Assert.Equal(revision[..7], body.RootElement.GetProperty("commit").GetString());
    }

    private sealed class AssemblyCommitApiFactory : SalesPortalApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["App:Commit"] = "",
                    ["BUILD_COMMIT"] = "",
                    ["SOURCE_COMMIT"] = "",
                }));
        }
    }
}
