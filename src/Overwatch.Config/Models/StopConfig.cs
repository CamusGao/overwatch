using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>Configuration for how to stop a service.</summary>
[YamlSerializable]
public sealed class StopConfig
{
    public PlatformCommand? Command { get; set; }

    public TimeSpan? Timeout { get; set; }

    public int? Retries { get; set; }
}
