using sales_portal.api.Diagnostics;
using sales_portal.api.Middleware;
using Xunit;

namespace SalesPortal.Tests;

public class SystemStatusInsightsTests
{
    [Fact]
    public void Latency_history_keeps_only_the_latest_twenty_samples()
    {
        var history = new LatencyHistory();
        for (var sample = 1; sample <= 25; sample++) history.Add(sample);

        var snapshot = history.Snapshot();

        Assert.Equal(20, snapshot.Length);
        Assert.Equal(6, snapshot[0]);
        Assert.Equal(25, snapshot[^1]);
    }

    [Fact]
    public async Task One_failing_probe_does_not_stop_the_others()
    {
        var errors = new List<string>();
        var failing = await SystemStatusProbe.RunAsync(
            "size",
            () => throw new InvalidOperationException("password=hidden-secret"),
            error => errors.Add(error.GetType().Name),
            CancellationToken.None);
        var working = await SystemStatusProbe.RunAsync(
            "rows",
            () => Task.FromResult(42),
            error => errors.Add(error.GetType().Name),
            CancellationToken.None);

        Assert.False(failing.Ok);
        Assert.Equal("InvalidOperationException", failing.Error);
        Assert.Equal(0, failing.Count);
        Assert.DoesNotContain("hidden-secret", failing.Error ?? string.Empty, StringComparison.Ordinal);

        Assert.True(working.Ok);
        Assert.Equal(42, working.Count);
        Assert.Null(working.Error);
        Assert.Equal(["InvalidOperationException"], errors);
    }
}
