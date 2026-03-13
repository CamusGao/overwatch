using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>
/// Represents a command that may differ per OS platform.
/// Supports both string shorthand and linux/windows object form.
/// </summary>
public sealed class PlatformCommand : IYamlConvertible
{
    public string? Linux { get; set; }
    public string? Windows { get; set; }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            Linux = scalar.Value;
            Windows = scalar.Value;
        }
        else
        {
            parser.Consume<MappingStart>();
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                var value = parser.Consume<Scalar>().Value;
                if (key == "linux") Linux = value;
                else if (key == "windows") Windows = value;
            }
        }
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        if (Linux == Windows && Linux is not null)
        {
            emitter.Emit(new Scalar(Linux));
        }
        else
        {
            emitter.Emit(new MappingStart());
            if (Linux is not null) { emitter.Emit(new Scalar("linux")); emitter.Emit(new Scalar(Linux)); }
            if (Windows is not null) { emitter.Emit(new Scalar("windows")); emitter.Emit(new Scalar(Windows)); }
            emitter.Emit(new MappingEnd());
        }
    }

    /// <summary>Returns the command for the current OS platform, or null if not set.</summary>
    public string? Resolve()
    {
        return OperatingSystem.IsWindows() ? Windows : Linux;
    }

    public override string? ToString() => Resolve();
}
