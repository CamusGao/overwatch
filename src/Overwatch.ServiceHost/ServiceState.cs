namespace Overwatch.ServiceHost;

/// <summary>Service lifecycle states.</summary>
public enum ServiceState
{
    Stopped,
    Starting,
    Running,
    Unhealthy,
    Failed,
    Stopping,
}
