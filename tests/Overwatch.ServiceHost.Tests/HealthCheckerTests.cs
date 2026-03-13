using Overwatch.Config.Models;
using Overwatch.ServiceHost;
using Overwatch.Platform;
using Xunit;

namespace Overwatch.ServiceHost.Tests;

public class HealthCheckerTests
{
    [Fact]
    public async Task HealthChecker_PassingCheck_NoUnhealthyEvent()
    {
        var config = new HealthCheckConfig
        {
            Test = OperatingSystem.IsWindows()
                ? ["cmd.exe", "/c", "exit 0"]
                : ["sh", "-c", "exit 0"],
            Interval = TimeSpan.FromMilliseconds(100),
            Timeout = TimeSpan.FromSeconds(5),
            Retries = 2,
            StartPeriod = TimeSpan.Zero,
        };

        var unhealthyFired = false;
        using var checker = new HealthChecker(config);
        checker.Unhealthy += (_, _) => unhealthyFired = true;
        checker.Start();

        // Wait enough time for several checks to run
        await Task.Delay(800);
        checker.Stop();

        Assert.False(unhealthyFired);
        // IsHealthy should be true after successful checks
        Assert.True(checker.IsHealthy == true || checker.IsHealthy == null, 
            $"Expected healthy or unchecked, got: {checker.IsHealthy}");
    }

    [Fact]
    public async Task HealthChecker_FailingCheck_FiresUnhealthyAfterRetries()
    {
        var config = new HealthCheckConfig
        {
            Test = OperatingSystem.IsWindows()
                ? ["cmd.exe", "/c", "exit 1"]
                : ["sh", "-c", "exit 1"],
            Interval = TimeSpan.FromMilliseconds(50),
            Timeout = TimeSpan.FromSeconds(2),
            Retries = 2,
            StartPeriod = TimeSpan.Zero,
        };

        var tcs = new TaskCompletionSource();
        using var checker = new HealthChecker(config);
        checker.Unhealthy += (_, _) => tcs.TrySetResult();
        checker.Start();

        // Wait until unhealthy fires (up to 10s to tolerate slow CI)
        var fired = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))) == tcs.Task;
        checker.Stop();

        Assert.True(fired, "Unhealthy event should have fired before timeout");
        Assert.False(checker.IsHealthy);
    }
}
