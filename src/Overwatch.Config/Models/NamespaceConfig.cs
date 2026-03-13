namespace Overwatch.Config.Models;

/// <summary>Root model for a namespace configuration file.</summary>
public sealed class NamespaceConfig
{
    /// <summary>Named services in this namespace. Key is service name.</summary>
    public Dictionary<string, ServiceConfig> Services { get; set; } = [];
}
