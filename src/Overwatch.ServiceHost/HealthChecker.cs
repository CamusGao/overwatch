using Overwatch.Config.Models;

namespace Overwatch.ServiceHost;

/// <summary>
/// Periodically executes a health check command and fires events on failure.
/// </summary>
public sealed class HealthChecker : IDisposable
{
    private readonly HealthCheckConfig _config;
    private CancellationTokenSource? _cts;
    private Task? _checkLoop;
    private int _consecutiveFailures;

    /// <summary>Fired when consecutive failure count reaches <see cref="HealthCheckConfig.Retries"/>.</summary>
    public event EventHandler? Unhealthy;

    /// <summary>Whether the last check passed (null if no check has run yet).</summary>
    public bool? IsHealthy { get; private set; }

    public HealthChecker(HealthCheckConfig config)
    {
        _config = config;
    }

    /// <summary>Starts the health check loop in the background.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _consecutiveFailures = 0;
        IsHealthy = null;
        _checkLoop = RunAsync(_cts.Token);
    }

    /// <summary>Stops the health check loop.</summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Wait for start-period before first check
        if (_config.StartPeriod > TimeSpan.Zero)
        {
            try { await Task.Delay(_config.StartPeriod, ct); }
            catch (OperationCanceledException) { return; }
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var healthy = await ExecuteCheckAsync(ct);
                if (healthy)
                {
                    _consecutiveFailures = 0;
                    IsHealthy = true;
                }
                else
                {
                    _consecutiveFailures++;
                    IsHealthy = false;
                    if (_consecutiveFailures >= _config.Retries)
                    {
                        Unhealthy?.Invoke(this, EventArgs.Empty);
                        _consecutiveFailures = 0;
                    }
                }

                await Task.Delay(_config.Interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Treat exception as a failure
                _consecutiveFailures++;
                IsHealthy = false;
                if (_consecutiveFailures >= _config.Retries)
                {
                    Unhealthy?.Invoke(this, EventArgs.Empty);
                    _consecutiveFailures = 0;
                }
            }
        }
    }

    private async Task<bool> ExecuteCheckAsync(CancellationToken ct)
    {
        if (_config.Test.Count == 0) return true;

        var exe = _config.Test[0];
        var args = _config.Test.Count > 1
            ? string.Join(' ', _config.Test.Skip(1).Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
            : string.Empty;

        var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_config.Timeout);

        try
        {
            await proc.WaitForExitAsync(cts.Token);
            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            proc.Kill(entireProcessTree: true);
            return false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
