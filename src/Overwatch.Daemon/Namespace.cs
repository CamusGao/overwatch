using Overwatch.Config.Models;
using Overwatch.Config.Validation;
using Overwatch.Ipc.Messages;
using Overwatch.Platform;

namespace Overwatch.Daemon;

/// <summary>
/// Represents a loaded namespace: one YAML configuration file and its associated service hosts.
/// </summary>
public sealed class Namespace : IAsyncDisposable
{
    public string Name { get; }
    public string FilePath { get; }
    public NamespaceConfig Config { get; private set; }
    private readonly Dictionary<string, Overwatch.ServiceHost.ServiceHost> _services = [];
    private readonly IPlatformService _platform;
    private readonly string _logDir;

    public IReadOnlyDictionary<string, Overwatch.ServiceHost.ServiceHost> Services => _services;

    public Namespace(string name, string filePath, NamespaceConfig config, IPlatformService platform, string logDir)
    {
        Name = name;
        FilePath = filePath;
        Config = config;
        _platform = platform;
        _logDir = logDir;
    }

    /// <summary>Starts all enabled services in dependency order.</summary>
    public async Task StartAllAsync(CancellationToken ct = default)
    {
        var order = ConfigValidator.TopologicalSort(Config.Services);

        foreach (var svcName in order)
        {
            var cfg = Config.Services[svcName];
            if (!cfg.Enabled) continue;

            var host = GetOrCreateHost(svcName, cfg);
            await host.StartAsync(ct);
        }
    }

    /// <summary>Stops all services in reverse dependency order.</summary>
    public async Task StopAllAsync(CancellationToken ct = default)
    {
        var order = ConfigValidator.TopologicalSort(Config.Services);

        foreach (var svcName in order.Reverse())
        {
            if (_services.TryGetValue(svcName, out var host))
                await host.StopAsync(ct);
        }
    }

    /// <summary>Returns status entries for all services.</summary>
    public IEnumerable<ServiceStatusEntry> GetStatusEntries()
    {
        foreach (var (name, host) in _services)
        {
            yield return new ServiceStatusEntry
            {
                Ns = Name,
                Service = name,
                Status = host.State.ToString().ToLowerInvariant(),
                Pid = host.Pid,
                Health = host.State == Overwatch.ServiceHost.ServiceState.Running ? "healthy" : null,
                Restarts = host.TotalRestarts,
                Uptime = host.Uptime.HasValue ? FormatUptime(host.Uptime.Value) : null,
            };
        }
    }

    private Overwatch.ServiceHost.ServiceHost GetOrCreateHost(string name, ServiceConfig config)
    {
        if (!_services.TryGetValue(name, out var host))
        {
            // Log base dir: <logDir>/<namespace>/<service>/
            var serviceLogBase = Path.Combine(_logDir, Name);
            host = new Overwatch.ServiceHost.ServiceHost(name, config, _platform, serviceLogBase);
            _services[name] = host;
        }
        return host;
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d{ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h{ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m{ts.Seconds}s";
        return $"{(int)ts.TotalSeconds}s";
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();
        foreach (var host in _services.Values)
            await host.DisposeAsync();
        _services.Clear();
    }
}
