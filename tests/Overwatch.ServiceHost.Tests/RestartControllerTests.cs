using Overwatch.Config.Models;
using Overwatch.ServiceHost;
using Xunit;

namespace Overwatch.ServiceHost.Tests;

public class RestartControllerTests
{
    [Fact]
    public void Policy_No_NeverRestarts()
    {
        var policy = new RestartPolicy();
        // OnExit defaults to None
        var ctrl = new RestartController(policy);

        Assert.False(ctrl.ShouldRestartOnExit(true));
        Assert.False(ctrl.ShouldRestartOnExit(false));
        Assert.False(ctrl.ShouldRestartOnUnhealthy());
    }

    [Fact]
    public void NullPolicy_NeverRestarts()
    {
        var ctrl = new RestartController(null);
        Assert.False(ctrl.ShouldRestartOnExit(true));
        Assert.False(ctrl.ShouldRestartOnUnhealthy());
    }

    [Fact]
    public void Policy_Always_AlwaysRestarts()
    {
        var policy = new RestartPolicy
        {
            OnExit = new RestartRule { Mode = RestartMode.Always },
            OnUnhealthy = new RestartRule { Mode = RestartMode.Always },
        };
        var ctrl = new RestartController(policy);

        Assert.True(ctrl.ShouldRestartOnExit(true));
        Assert.True(ctrl.ShouldRestartOnExit(false));
        Assert.True(ctrl.ShouldRestartOnUnhealthy());
    }

    [Fact]
    public void Policy_OnFailure_OnlyRestartsOnFailure()
    {
        var policy = new RestartPolicy
        {
            OnExit = new RestartRule { Mode = RestartMode.OnFailure },
        };
        var ctrl = new RestartController(policy);

        Assert.True(ctrl.ShouldRestartOnExit(wasFailure: true));
        Assert.False(ctrl.ShouldRestartOnExit(wasFailure: false));
    }

    [Fact]
    public void Policy_OnFailureN_StopsAfterN()
    {
        var policy = new RestartPolicy
        {
            OnExit = new RestartRule { Mode = RestartMode.OnFailure, MaxRetries = 3 },
        };
        var ctrl = new RestartController(policy);

        Assert.True(ctrl.ShouldRestartOnExit(true));  // 1
        Assert.True(ctrl.ShouldRestartOnExit(true));  // 2
        Assert.True(ctrl.ShouldRestartOnExit(true));  // 3
        Assert.False(ctrl.ShouldRestartOnExit(true)); // over limit
    }

    [Fact]
    public void TotalRestarts_CountsBothTriggers()
    {
        var policy = new RestartPolicy
        {
            OnExit = new RestartRule { Mode = RestartMode.Always },
            OnUnhealthy = new RestartRule { Mode = RestartMode.Always },
        };
        var ctrl = new RestartController(policy);

        ctrl.ShouldRestartOnExit(true);
        ctrl.ShouldRestartOnUnhealthy();

        Assert.Equal(2, ctrl.TotalRestarts);
    }

    [Fact]
    public void Reset_ClearsCounter()
    {
        var policy = new RestartPolicy
        {
            OnExit = new RestartRule { Mode = RestartMode.OnFailure, MaxRetries = 1 },
        };
        var ctrl = new RestartController(policy);

        Assert.True(ctrl.ShouldRestartOnExit(true));
        Assert.False(ctrl.ShouldRestartOnExit(true)); // at limit

        ctrl.Reset();
        Assert.True(ctrl.ShouldRestartOnExit(true)); // counter cleared
    }
}
