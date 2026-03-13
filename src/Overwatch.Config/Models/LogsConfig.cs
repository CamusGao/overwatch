namespace Overwatch.Config.Models;

/// <summary>Configuration for log file management.</summary>
public sealed class LogsConfig
{
    /// <summary>Directory where log files are stored. Defaults to platform log dir / service-name.</summary>
    public string? Path { get; set; }

    /// <summary>Maximum size of a single log file before rotation (e.g., "100MB").</summary>
    public string MaxSize { get; set; } = "100MB";

    /// <summary>Maximum age of log files before deletion (e.g., "7d").</summary>
    public string MaxAge { get; set; } = "7d";
}
