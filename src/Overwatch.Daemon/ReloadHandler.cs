using Overwatch.Config.Parsing;
using Overwatch.Config.Validation;
using Overwatch.Ipc.Messages;

namespace Overwatch.Daemon;

/// <summary>
/// Handles the reload command: diffs existing namespaces against new files on disk.
/// </summary>
public sealed class ReloadHandler
{
    private readonly NamespaceManager _manager;
    private readonly string _configDir;

    public ReloadHandler(NamespaceManager manager, string configDir)
    {
        _manager = manager;
        _configDir = configDir;
    }

    /// <summary>
    /// Scans config-dir, loads new namespaces, unloads deleted ones,
    /// and reports changed existing namespaces (without restarting them).
    /// </summary>
    public async Task<IpcResponse> HandleAsync(CancellationToken ct = default)
    {
        var yamlFiles = Directory.Exists(_configDir)
            ? Directory.GetFiles(_configDir, "*.yaml").ToDictionary(
                f => Path.GetFileNameWithoutExtension(f),
                f => f)
            : new Dictionary<string, string>();

        var currentNames = _manager.GetNamespaceNames().ToHashSet();
        var newNames = yamlFiles.Keys.ToHashSet();

        var added = newNames.Except(currentNames).ToList();
        var removed = currentNames.Except(newNames).ToList();
        var existing = newNames.Intersect(currentNames).ToList();

        var messages = new List<string>();
        var errors = new List<string>();

        // Load new namespaces
        foreach (var nsName in added)
        {
            var (success, error) = await _manager.LoadAsync(yamlFiles[nsName], ct);
            if (success)
                messages.Add($"Loaded new namespace '{nsName}'.");
            else
                errors.Add(error ?? $"Failed to load '{nsName}'.");
        }

        // Unload deleted namespaces
        foreach (var nsName in removed)
        {
            var (success, error) = await _manager.UnloadAsync(nsName, ct);
            if (success)
                messages.Add($"Unloaded namespace '{nsName}'.");
            else
                errors.Add(error ?? $"Failed to unload '{nsName}'.");
        }

        // Report changed existing namespaces (do not restart)
        foreach (var nsName in existing)
        {
            var filePath = yamlFiles[nsName];
            try
            {
                var newConfig = ConfigParser.ParseFile(filePath);
                var validationErrors = ConfigValidator.Validate(newConfig);
                if (validationErrors.Count > 0)
                {
                    errors.Add($"'{nsName}' config has validation errors; skipped.");
                    continue;
                }

                // Detect if config changed by comparing content hash
                var storedHash = _manager.GetContentHash(nsName);
                var currentHash = ComputeFileHash(filePath);
                if (storedHash is null || storedHash != currentHash)
                {
                    messages.Add($"Namespace '{nsName}' config changed. Run 'overwatch restart {nsName}' to apply.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error reading '{nsName}': {ex.Message}");
            }
        }

        var summary = new ReloadSummary
        {
            Messages = messages,
            Errors = errors,
        };

        return IpcResponse.Success(summary);
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed class ReloadSummary
{
    public List<string> Messages { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
