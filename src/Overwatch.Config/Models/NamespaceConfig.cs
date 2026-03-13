using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>Root model for a namespace configuration file.</summary>
[YamlSerializable]
public sealed class NamespaceConfig
{
    /// <summary>Named services in this namespace. Key is service name.</summary>
    public Dictionary<string, ServiceConfig> Services { get; set; } = [];
}
