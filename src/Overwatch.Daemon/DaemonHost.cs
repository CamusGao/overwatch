using Overwatch.Ipc.Messages;
using Overwatch.Ipc.Transport;
using Overwatch.Platform;

namespace Overwatch.Daemon;

/// <summary>
/// The daemon's main host. Starts the IPC server, loads all namespaces from config-dir,
/// and dispatches IPC commands to the NamespaceManager.
/// </summary>
public sealed class DaemonHost : IAsyncDisposable
{
    private readonly string _configDir;
    private readonly string _socketPath;
    private readonly IPlatformService _platform;
    private readonly NamespaceManager _namespaceManager;
    private readonly ReloadHandler _reloadHandler;
    private IpcServer? _ipcServer;

    public DaemonHost(string configDir, string socketPath, IPlatformService platform)
    {
        _configDir = configDir;
        _socketPath = socketPath;
        _platform = platform;
        _namespaceManager = new NamespaceManager(platform);
        _reloadHandler = new ReloadHandler(_namespaceManager, configDir);
    }

    /// <summary>Starts the daemon: launches IPC server and loads all config files.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _ipcServer = new IpcServer(_socketPath, HandleIpcRequestAsync);
        _ipcServer.Start();

        await LoadAllNamespacesAsync(ct);
    }

    private async Task LoadAllNamespacesAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_configDir))
        {
            Console.WriteLine($"[daemon] Config directory not found: {_configDir}");
            return;
        }

        var files = Directory.GetFiles(_configDir, "*.yaml");
        foreach (var file in files)
        {
            var (success, error) = await _namespaceManager.LoadAsync(file, ct);
            if (success)
                Console.WriteLine($"[daemon] Loaded namespace '{Path.GetFileNameWithoutExtension(file)}'.");
            else
                Console.WriteLine($"[daemon] Failed to load '{file}': {error}");
        }
    }

    private async Task<IpcResponse> HandleIpcRequestAsync(IpcRequest request)
    {
        return request.Cmd switch
        {
            IpcCommands.Start => request.Ns is not null
                ? await _namespaceManager.HandleStartAsync(request.Ns)
                : IpcResponse.Failure("'start' requires a namespace name."),

            IpcCommands.Stop => request.Ns is not null
                ? await _namespaceManager.HandleStopAsync(request.Ns)
                : IpcResponse.Failure("'stop' requires a namespace name."),

            IpcCommands.Restart => request.Ns is not null
                ? await _namespaceManager.HandleRestartAsync(request.Ns)
                : IpcResponse.Failure("'restart' requires a namespace name."),

            IpcCommands.Ps => await _namespaceManager.HandlePsAsync(),

            IpcCommands.Reload => await _reloadHandler.HandleAsync(),

            _ => IpcResponse.Failure($"Unknown command: '{request.Cmd}'."),
        };
    }

    /// <summary>Stops the daemon gracefully.</summary>
    public async Task StopAsync()
    {
        if (_ipcServer is not null)
            await _ipcServer.StopAsync();
        await _namespaceManager.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_ipcServer is not null)
            await _ipcServer.DisposeAsync();
    }
}
