using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>Configuration for log file management.</summary>
[YamlSerializable]
public sealed class LogsConfig : IYamlConvertible
{
    /// <summary>Directory where log files are stored. Defaults to platform log dir / service-name.</summary>
    public string? Path { get; set; }

    /// <summary>Maximum size of a single log file before rotation (e.g., "100MB").</summary>
    [YamlMember(Alias = "max-size")]
    public string MaxSize { get; set; } = "100MB";

    /// <summary>Maximum age of log files before deletion (e.g., "7d").</summary>
    [YamlMember(Alias = "max-age")]
    public string MaxAge { get; set; } = "7d";

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "path":
                    Path = parser.Consume<Scalar>().Value;
                    break;
                case "max-size":
                    MaxSize = parser.Consume<Scalar>().Value;
                    break;
                case "max-age":
                    MaxAge = parser.Consume<Scalar>().Value;
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
        if (Path is not null) { emitter.Emit(new Scalar("path")); emitter.Emit(new Scalar(Path)); }
        emitter.Emit(new Scalar("max-size")); emitter.Emit(new Scalar(MaxSize));
        emitter.Emit(new Scalar("max-age")); emitter.Emit(new Scalar(MaxAge));
        emitter.Emit(new MappingEnd());
    }
}
