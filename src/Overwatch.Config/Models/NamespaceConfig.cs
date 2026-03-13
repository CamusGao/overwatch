using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>Root model for a namespace configuration file.</summary>
[YamlSerializable]
public sealed class NamespaceConfig : IYamlConvertible
{
    /// <summary>Named services in this namespace. Key is service name.</summary>
    public Dictionary<string, ServiceConfig> Services { get; set; } = [];

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "services":
                    parser.Consume<MappingStart>();
                    while (!parser.TryConsume<MappingEnd>(out _))
                    {
                        var name = parser.Consume<Scalar>().Value;
                        var svc = new ServiceConfig();
                        ((IYamlConvertible)svc).Read(parser, typeof(ServiceConfig), nestedObjectDeserializer);
                        Services[name] = svc;
                    }
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
        emitter.Emit(new Scalar("services"));
        emitter.Emit(new MappingStart());
        foreach (var (name, svc) in Services)
        {
            emitter.Emit(new Scalar(name));
            ((IYamlConvertible)svc).Write(emitter, nestedObjectSerializer);
        }
        emitter.Emit(new MappingEnd());
        emitter.Emit(new MappingEnd());
    }
}
