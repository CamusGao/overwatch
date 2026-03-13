using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Overwatch.Config.Parsing;

namespace Overwatch.Config.Models;

/// <summary>Configuration for how to stop a service.</summary>
[YamlSerializable]
public sealed class StopConfig : IYamlConvertible
{
    public PlatformCommand? Command { get; set; }

    public TimeSpan? Timeout { get; set; }

    public int? Retries { get; set; }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "command":
                    Command = new PlatformCommand();
                    ((IYamlConvertible)Command).Read(parser, typeof(PlatformCommand), nestedObjectDeserializer);
                    break;
                case "timeout":
                    var val = parser.Consume<Scalar>().Value;
                    if (!string.IsNullOrEmpty(val))
                        Timeout = DurationConverter.ParseDuration(val);
                    break;
                case "retries":
                    Retries = int.Parse(parser.Consume<Scalar>().Value);
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
        if (Command is not null) { emitter.Emit(new Scalar("command")); ((IYamlConvertible)Command).Write(emitter, nestedObjectSerializer); }
        if (Timeout.HasValue) { emitter.Emit(new Scalar("timeout")); emitter.Emit(new Scalar(Timeout.Value.ToString())); }
        if (Retries.HasValue) { emitter.Emit(new Scalar("retries")); emitter.Emit(new Scalar(Retries.Value.ToString())); }
        emitter.Emit(new MappingEnd());
    }
}
