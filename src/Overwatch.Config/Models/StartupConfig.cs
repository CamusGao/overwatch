namespace Overwatch.Config.Models;

/// <summary>How the service process starts.</summary>
public enum StartupType
{
    /// <summary>Process runs in the foreground; the daemon holds the PID.</summary>
    Simple,
    /// <summary>Process forks itself to background; the daemon does not track the PID.</summary>
    Forking,
}

/// <summary>Configuration for how to start a service.</summary>
public sealed class StartupConfig
{
    public PlatformCommand Command { get; set; } = new();

    public StartupType Type { get; set; } = StartupType.Simple;

    /// <summary>
    /// For forking mode: timeout waiting for the process to complete startup.
    /// For simple mode: ignored.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
