using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Overwatch.Config.Parsing;

namespace Overwatch.Config.Models;

/// <summary>Configuration for periodic health checks.</summary>
[YamlSerializable]
public sealed class HealthCheckConfig : IYamlConvertible
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

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "test":
                    Test = [];
                    parser.Consume<SequenceStart>();
                    while (!parser.TryConsume<SequenceEnd>(out _))
                        Test.Add(parser.Consume<Scalar>().Value);
                    break;
                case "interval":
                    Interval = DurationConverter.ParseDuration(parser.Consume<Scalar>().Value);
                    break;
                case "timeout":
                    Timeout = DurationConverter.ParseDuration(parser.Consume<Scalar>().Value);
                    break;
                case "retries":
                    Retries = int.Parse(parser.Consume<Scalar>().Value);
                    break;
                case "start-period":
                    StartPeriod = DurationConverter.ParseDuration(parser.Consume<Scalar>().Value);
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
        emitter.Emit(new Scalar("test"));
        emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, true, SequenceStyle.Flow));
        foreach (var t in Test) emitter.Emit(new Scalar(t));
        emitter.Emit(new SequenceEnd());
        emitter.Emit(new Scalar("interval")); emitter.Emit(new Scalar(Interval.ToString()));
        emitter.Emit(new Scalar("timeout")); emitter.Emit(new Scalar(Timeout.ToString()));
        emitter.Emit(new Scalar("retries")); emitter.Emit(new Scalar(Retries.ToString()));
        emitter.Emit(new Scalar("start-period")); emitter.Emit(new Scalar(StartPeriod.ToString()));
        emitter.Emit(new MappingEnd());
    }
}
