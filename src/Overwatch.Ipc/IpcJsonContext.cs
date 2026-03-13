using System.Text.Json.Serialization;
using Overwatch.Ipc.Messages;

namespace Overwatch.Ipc;

/// <summary>
/// JSON serialization context for AOT compatibility.
/// Registers all IPC message types with System.Text.Json source generation.
/// </summary>
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSerializable(typeof(PsData))]
[JsonSerializable(typeof(ServiceStatusEntry))]
[JsonSerializable(typeof(List<ServiceStatusEntry>))]
public partial class IpcJsonContext : JsonSerializerContext
{
}
