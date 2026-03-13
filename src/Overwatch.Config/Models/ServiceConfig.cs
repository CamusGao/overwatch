using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>Configuration for a single managed service.</summary>
[YamlSerializable]
public sealed class ServiceConfig
{
    /// <summary>Whether the service should be started automatically. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>System user to run the service as. Inherits daemon user if null.</summary>
    public string? User { get; set; }

    /// <summary>Names of other services in the same namespace that must start first.</summary>
    [YamlMember(Alias = "depends-on")]
    public List<string>? DependsOn { get; set; }

    /// <summary>Working directory for the service process.</summary>
    [YamlMember(Alias = "working-dir")]
    public string? WorkingDir { get; set; }

    /// <summary>Restart policy. Null means do not restart.</summary>
    public RestartPolicy? Restart { get; set; }

    /// <summary>Additional environment variables injected into the service process.</summary>
    public Dictionary<string, string>? Environments { get; set; }

    public LogsConfig? Logs { get; set; }

    [YamlMember(Alias = "health-check")]
    public HealthCheckConfig? HealthCheck { get; set; }

    public StartupConfig Startup { get; set; } = new();

    public StopConfig? Stop { get; set; }
}
