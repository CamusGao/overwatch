using Overwatch.Config.Models;
using Overwatch.Platform;

namespace Overwatch.ServiceHost;

/// <summary>
/// Manages the full lifecycle of a single service:
/// Stopped → Starting → Running → Unhealthy → (restart or) Failed.
/// </summary>
public sealed class ServiceHost : IAsyncDisposable
{
    private readonly string _name;
    private readonly ServiceConfig _config;
    private readonly IPlatformService _platform;
    private readonly RestartController _restartController;
    private ProcessRunner? _processRunner;
    private HealthChecker? _healthChecker;
    private LogWriter? _logWriter;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private volatile ServiceState _state = ServiceState.Stopped;
    private DateTimeOffset? _startTime;
    private int _totalRestarts;

    public string Name => _name;
    public ServiceState State => _state;
    public int? Pid => _processRunner?.Pid;
    public int TotalRestarts => _totalRestarts;
    public DateTimeOffset? StartTime => _startTime;

    public TimeSpan? Uptime => _startTime.HasValue && _state == ServiceState.Running
        ? DateTimeOffset.UtcNow - _startTime.Value
        : null;

    public event EventHandler<ServiceState>? StateChanged;

    public ServiceHost(string name, ServiceConfig config, IPlatformService platform)
    {
        _name = name;
        _config = config;
        _platform = platform;
        _restartController = new RestartController(config.Restart);
    }

    /// <summary>Starts the service. No-op if already running.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (_state is ServiceState.Running or ServiceState.Starting) return;
            await DoStartAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>Stops the service gracefully.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (_state is ServiceState.Stopped or ServiceState.Failed) return;
            await DoStopAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>Restarts the service.</summary>
    public async Task RestartAsync(CancellationToken ct = default)
    {
        await StopAsync(ct);
        await StartAsync(ct);
    }

    private async Task DoStartAsync()
    {
        SetState(ServiceState.Starting);

        DisposeCurrentRuntime();

        _processRunner = new ProcessRunner(_config, _platform);
        _processRunner.ProcessExited += OnProcessExited;

        if (_config.Logs is not null)
        {
            _logWriter = new LogWriter(_config.Logs, _name);
            _logWriter.Open();
        }

        var started = _processRunner.Start(
            stdoutHandler: line => _logWriter?.WriteLine($"[out] {line}"),
            stderrHandler: line => _logWriter?.WriteLine($"[err] {line}")
        );

        if (!started)
        {
            SetState(ServiceState.Failed);
            return;
        }

        SetState(ServiceState.Running);
        _startTime = DateTimeOffset.UtcNow;

        if (_config.HealthCheck is not null)
        {
            _healthChecker = new HealthChecker(_config.HealthCheck);
            _healthChecker.Unhealthy += OnUnhealthy;
            _healthChecker.Start();
        }
    }

    private async Task DoStopAsync()
    {
        SetState(ServiceState.Stopping);
        _healthChecker?.Stop();

        if (_processRunner is not null)
        {
            await _processRunner.StopAsync();
        }

        DisposeCurrentRuntime();
        SetState(ServiceState.Stopped);
        _startTime = null;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _ = HandleProcessExitAsync();
    }

    private async Task HandleProcessExitAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_state is ServiceState.Stopping or ServiceState.Stopped or ServiceState.Failed) return;

            _healthChecker?.Stop();
            var wasFailure = true; // treat unexpected exit as failure

            if (_restartController.ShouldRestartOnExit(wasFailure))
            {
                _totalRestarts++;
                await DoStartAsync();
            }
            else
            {
                DisposeCurrentRuntime();
                SetState(ServiceState.Failed);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void OnUnhealthy(object? sender, EventArgs e)
    {
        _ = HandleUnhealthyAsync();
    }

    private async Task HandleUnhealthyAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_state is not ServiceState.Running) return;
            SetState(ServiceState.Unhealthy);

            if (_restartController.ShouldRestartOnUnhealthy())
            {
                _totalRestarts++;
                await DoStopAsync();
                await DoStartAsync();
            }
            else
            {
                await DoStopAsync();
                SetState(ServiceState.Failed);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void DisposeCurrentRuntime()
    {
        _healthChecker?.Dispose();
        _healthChecker = null;
        _processRunner?.Dispose();
        _processRunner = null;
        _logWriter?.Dispose();
        _logWriter = null;
    }

    private void SetState(ServiceState newState)
    {
        _state = newState;
        StateChanged?.Invoke(this, newState);
    }

    public async ValueTask DisposeAsync()
    {
        if (_state is not ServiceState.Stopped and not ServiceState.Failed)
        {
            try { await DoStopAsync(); } catch { /* best effort */ }
        }
        DisposeCurrentRuntime();
        _stateLock.Dispose();
    }
}
