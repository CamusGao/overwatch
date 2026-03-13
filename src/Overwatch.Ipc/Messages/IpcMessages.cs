using System.Text.Json.Serialization;

namespace Overwatch.Ipc.Messages;

/// <summary>IPC command types sent from CLI to Daemon.</summary>
public static class IpcCommands
{
    public const string Start = "start";
    public const string Stop = "stop";
    public const string Restart = "restart";
    public const string Ps = "ps";
    public const string Reload = "reload";
}

/// <summary>Request message sent from CLI to Daemon.</summary>
public sealed class IpcRequest
{
    /// <summary>Command: start, stop, restart, ps, reload.</summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = string.Empty;

    /// <summary>Namespace name (required for start/stop/restart, omitted for ps/reload).</summary>
    [JsonPropertyName("ns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ns { get; set; }
}

/// <summary>Response message sent from Daemon to CLI.</summary>
public sealed class IpcResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    public static IpcResponse Success(object? data = null) => new() { Ok = true, Data = data };
    public static IpcResponse Failure(string error) => new() { Ok = false, Error = error };
}

/// <summary>Service status row used in ps response.</summary>
public sealed class ServiceStatusEntry
{
    [JsonPropertyName("ns")]
    public string Ns { get; set; } = string.Empty;

    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("pid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Pid { get; set; }

    [JsonPropertyName("health")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Health { get; set; }

    [JsonPropertyName("restarts")]
    public int Restarts { get; set; }

    [JsonPropertyName("uptime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uptime { get; set; }
}

/// <summary>Data payload for the ps response.</summary>
public sealed class PsData
{
    [JsonPropertyName("services")]
    public List<ServiceStatusEntry> Services { get; set; } = [];
}
