using System.Diagnostics;
using Overwatch.Config.Models;
using Overwatch.Platform;

namespace Overwatch.ServiceHost;

/// <summary>
/// Manages starting and stopping a service process.
/// Handles simple (daemon holds PID) and forking (no PID management) modes.
/// </summary>
public sealed class ProcessRunner : IDisposable
{
    private readonly ServiceConfig _config;
    private readonly IPlatformService _platform;
    private Process? _process;

    public int? Pid => _process?.HasExited == false ? _process.Id : null;

    public event EventHandler? ProcessExited;

    public ProcessRunner(ServiceConfig config, IPlatformService platform)
    {
        _config = config;
        _platform = platform;
    }

    /// <summary>
    /// Starts the service process. Returns true if successfully started.
    /// For simple mode, the process is tracked. For forking, it is launched and detached.
    /// </summary>
    public bool Start(Action<string> stdoutHandler, Action<string> stderrHandler)
    {
        var command = _config.Startup.Command.Resolve();
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Startup command is not set.");

        var (exe, args) = ParseCommand(command);
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (_config.WorkingDir is not null)
            psi.WorkingDirectory = _config.WorkingDir;

        if (_config.Environments is { Count: > 0 })
        {
            foreach (var (key, value) in _config.Environments)
                psi.Environment[key] = value;
        }

        if (_config.User is not null)
            _platform.ApplyUser(psi, _config.User);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        if (_config.Startup.Type == StartupType.Simple)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutHandler(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrHandler(e.Data); };
            process.Exited += (_, _) => ProcessExited?.Invoke(this, EventArgs.Empty);

            if (!process.Start()) return false;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
        }
        else
        {
            // Forking mode: launch and don't track PID
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutHandler(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrHandler(e.Data); };
            if (!process.Start()) return false;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.Dispose();
            _process = null;
        }

        return true;
    }

    /// <summary>
    /// Stops the service. Uses stop.command if configured, otherwise kills the process (simple mode).
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        var stopCmd = _config.Stop?.Command?.Resolve();
        if (!string.IsNullOrWhiteSpace(stopCmd))
        {
            stopCmd = SubstitutePid(stopCmd);
            var (exe, args) = ParseCommand(stopCmd);
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var stopProc = Process.Start(psi);
            if (stopProc is not null)
            {
                var timeout = _config.Stop?.Timeout ?? TimeSpan.FromSeconds(10);
                await stopProc.WaitForExitAsync(ct).WaitAsync(timeout, ct).ConfigureAwait(false);
            }
        }
        else if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
        }

        if (_process is { HasExited: false })
        {
            try { await _process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct); }
            catch { /* ignore timeout */ }
        }
    }

    /// <summary>Substitutes ${_PID} with the actual PID in stop commands (simple mode only).</summary>
    public string SubstitutePid(string command)
    {
        if (_config.Startup.Type == StartupType.Simple && Pid.HasValue)
            return command.Replace("${_PID}", Pid.Value.ToString(), StringComparison.Ordinal);
        return command;
    }

    public void Dispose()
    {
        _process?.Dispose();
        _process = null;
    }

    private static (string exe, string args) ParseCommand(string command)
    {
        command = command.Trim();
        int spaceIdx = command.IndexOf(' ');
        if (spaceIdx < 0) return (command, string.Empty);
        return (command[..spaceIdx], command[(spaceIdx + 1)..]);
    }
}
