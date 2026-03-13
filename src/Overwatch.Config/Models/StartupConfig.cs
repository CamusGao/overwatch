using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Overwatch.Config.Parsing;

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
[YamlSerializable]
public sealed class StartupConfig : IYamlConvertible
{
    public PlatformCommand Command { get; set; } = new();

    public StartupType Type { get; set; } = StartupType.Simple;

    /// <summary>
    /// For forking mode: timeout waiting for the process to complete startup.
    /// For simple mode: ignored.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "command":
                    ((IYamlConvertible)Command).Read(parser, typeof(PlatformCommand), nestedObjectDeserializer);
                    break;
                case "type":
                    Type = Enum.Parse<StartupType>(parser.Consume<Scalar>().Value, ignoreCase: true);
                    break;
                case "timeout":
                    var val = parser.Consume<Scalar>().Value;
                    if (!string.IsNullOrEmpty(val))
                        Timeout = DurationConverter.ParseDuration(val);
                    break;
                default:
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        emitter.Emit(new MappingStart());
        emitter.Emit(new Scalar("command")); ((IYamlConvertible)Command).Write(emitter, nestedObjectSerializer);
        emitter.Emit(new Scalar("type")); emitter.Emit(new Scalar(Type.ToString().ToLowerInvariant()));
        if (Timeout.HasValue) { emitter.Emit(new Scalar("timeout")); emitter.Emit(new Scalar(Timeout.Value.ToString())); }
        emitter.Emit(new MappingEnd());
    }
}
