using System.Text.Json.Serialization;
using Overwatch.Daemon;

namespace Overwatch.Cli;

/// <summary>JSON context for types returned from daemon responses that need deserialization in CLI.</summary>
[JsonSerializable(typeof(ReloadSummary))]
public partial class DaemonJsonContext : JsonSerializerContext
{
}
