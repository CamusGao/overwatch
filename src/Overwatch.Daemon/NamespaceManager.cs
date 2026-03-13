using System.Security.Cryptography;
using Overwatch.Config.Models;
using Overwatch.Config.Parsing;
using Overwatch.Config.Validation;
using Overwatch.Ipc.Messages;
using Overwatch.Platform;

namespace Overwatch.Daemon;

/// <summary>
/// Manages all loaded namespaces. Handles load/unload/reload and IPC command dispatch.
/// </summary>
public sealed class NamespaceManager : IAsyncDisposable
{
    private readonly IPlatformService _platform;
    private readonly string _logDir;
    private readonly Dictionary<string, Namespace> _namespaces = [];
    private readonly Dictionary<string, string> _contentHashes = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public NamespaceManager(IPlatformService platform, string logDir)
    {
        _platform = platform;
        _logDir = logDir;
    }

    /// <summary>Loads and starts a namespace from a YAML file.</summary>
    public async Task<(bool success, string? error)> LoadAsync(string filePath, CancellationToken ct = default)
    {
        var nsName = Path.GetFileNameWithoutExtension(filePath);

        NamespaceConfig config;
        try
        {
            config = ConfigParser.ParseFile(filePath);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse '{filePath}': {ex.Message}");
        }

        var errors = ConfigValidator.Validate(config);
        if (errors.Count > 0)
        {
            var msg = string.Join("; ", errors.Select(e => e.ToString()));
            return (false, $"Validation failed for namespace '{nsName}': {msg}");
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_namespaces.TryGetValue(nsName, out var existing))
                await existing.DisposeAsync();

            var ns = new Namespace(nsName, filePath, config, _platform, _logDir);
            _namespaces[nsName] = ns;
            _contentHashes[nsName] = ComputeFileHash(filePath);
            await ns.StartAllAsync(ct);
            return (true, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Stops and unloads a namespace.</summary>
    public async Task<(bool success, string? error)> UnloadAsync(string nsName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_namespaces.TryGetValue(nsName, out var ns))
                return (false, $"Namespace '{nsName}' not found.");

            await ns.DisposeAsync();
            _namespaces.Remove(nsName);
            _contentHashes.Remove(nsName);
            return (true, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IpcResponse> HandleStartAsync(string nsName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_namespaces.TryGetValue(nsName, out var ns))
                return IpcResponse.Failure($"Namespace '{nsName}' not found.");
            await ns.StartAllAsync(ct);
            return IpcResponse.Success();
        }
        catch (Exception ex)
        {
            return IpcResponse.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IpcResponse> HandleStopAsync(string nsName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_namespaces.TryGetValue(nsName, out var ns))
                return IpcResponse.Failure($"Namespace '{nsName}' not found.");
            await ns.StopAllAsync(ct);
            return IpcResponse.Success();
        }
        catch (Exception ex)
        {
            return IpcResponse.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IpcResponse> HandleRestartAsync(string nsName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_namespaces.TryGetValue(nsName, out var ns))
                return IpcResponse.Failure($"Namespace '{nsName}' not found.");
            await ns.StopAllAsync(ct);
            await ns.StartAllAsync(ct);
            return IpcResponse.Success();
        }
        catch (Exception ex)
        {
            return IpcResponse.Failure(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IpcResponse> HandlePsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var entries = _namespaces.Values
                .SelectMany(ns => ns.GetStatusEntries())
                .ToList();
            return IpcResponse.Success(new PsData { Services = entries });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Returns all currently loaded namespace names.</summary>
    public IReadOnlyCollection<string> GetNamespaceNames()
    {
        return _namespaces.Keys.ToList();
    }

    /// <summary>Returns the file path for a loaded namespace, or null.</summary>
    public string? GetFilePath(string nsName)
    {
        return _namespaces.TryGetValue(nsName, out var ns) ? ns.FilePath : null;
    }

    /// <summary>Returns the stored content hash for a loaded namespace, or null.</summary>
    public string? GetContentHash(string nsName)
    {
        return _contentHashes.TryGetValue(nsName, out var hash) ? hash : null;
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var ns in _namespaces.Values)
            await ns.DisposeAsync();
        _namespaces.Clear();
        _contentHashes.Clear();
        _lock.Dispose();
    }
}
