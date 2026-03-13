using Overwatch.Config.Models;

namespace Overwatch.ServiceHost;

/// <summary>
/// Determines whether a service should be restarted based on its <see cref="RestartPolicy"/>.
/// Tracks consecutive restart count and enforces MaxRetries limits.
/// </summary>
public sealed class RestartController
{
    private readonly RestartPolicy? _policy;
    private int _exitRestarts;
    private int _unhealthyRestarts;

    public RestartController(RestartPolicy? policy)
    {
        _policy = policy;
    }

    /// <summary>Whether a restart should be attempted after a process exit.</summary>
    public bool ShouldRestartOnExit(bool wasFailure)
    {
        var rule = _policy?.OnExit ?? RestartRule.None;
        return ShouldRestart(rule, wasFailure, ref _exitRestarts);
    }

    /// <summary>Whether a restart should be attempted after health check failure.</summary>
    public bool ShouldRestartOnUnhealthy()
    {
        var rule = _policy?.OnUnhealthy ?? RestartRule.None;
        return ShouldRestart(rule, isFailure: true, ref _unhealthyRestarts);
    }

    /// <summary>Resets all counters (call after a successful restart that reaches Running state).</summary>
    public void Reset()
    {
        _exitRestarts = 0;
        _unhealthyRestarts = 0;
    }

    /// <summary>Total number of restarts attempted (both triggers combined).</summary>
    public int TotalRestarts => _exitRestarts + _unhealthyRestarts;

    private static bool ShouldRestart(RestartRule rule, bool isFailure, ref int counter)
    {
        return rule.Mode switch
        {
            RestartMode.No => false,
            RestartMode.Always => CheckLimit(rule, ref counter),
            RestartMode.OnFailure when isFailure => CheckLimit(rule, ref counter),
            RestartMode.OnFailure => false,
            _ => false,
        };
    }

    private static bool CheckLimit(RestartRule rule, ref int counter)
    {
        if (rule.MaxRetries.HasValue && counter >= rule.MaxRetries.Value)
            return false;
        counter++;
        return true;
    }
}
