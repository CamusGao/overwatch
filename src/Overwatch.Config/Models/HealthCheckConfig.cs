using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>Configuration for periodic health checks.</summary>
[YamlSerializable]
public sealed class HealthCheckConfig
{
    /// <summary>Health check command. First element is the executable, rest are arguments.</summary>
    public List<string> Test { get; set; } = [];

    public TimeSpan Interval { get; set; }

    public TimeSpan Timeout { get; set; }

    /// <summary>Number of consecutive failures before declaring unhealthy.</summary>
    public int Retries { get; set; }

    /// <summary>Delay after service start before health checks begin.</summary>
    [YamlMember(Alias = "start-period")]
    public TimeSpan StartPeriod { get; set; }
}
