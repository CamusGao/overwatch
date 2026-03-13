using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Overwatch.Config.Parsing;

namespace Overwatch.Config.Models;

/// <summary>Configuration for a single managed service.</summary>
[YamlSerializable]
public sealed class ServiceConfig : IYamlConvertible
{
    /// <summary>Whether the service should be started automatically. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>System user to run the service as. Inherits daemon user if null.</summary>
    public string? User { get; set; }

    /// <summary>Names of other services in the same namespace that must start first.</summary>
    [YamlMember(Alias = "depends-on")]
    public List<string>? DependsOn { get; set; }

    /// <summary>Working directory for the service process.</summary>
    [YamlMember(Alias = "working-dir")]
    public string? WorkingDir { get; set; }

    /// <summary>Restart policy. Null means do not restart.</summary>
    public RestartPolicy? Restart { get; set; }

    /// <summary>Additional environment variables injected into the service process.</summary>
    public Dictionary<string, string>? Environments { get; set; }

    public LogsConfig? Logs { get; set; }

    [YamlMember(Alias = "health-check")]
    public HealthCheckConfig? HealthCheck { get; set; }

    public StartupConfig Startup { get; set; } = new();

    public StopConfig? Stop { get; set; }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "enabled":
                    Enabled = bool.Parse(parser.Consume<Scalar>().Value);
                    break;
                case "user":
                    User = parser.Consume<Scalar>().Value;
                    break;
                case "depends-on":
                    DependsOn = [];
                    parser.Consume<SequenceStart>();
                    while (!parser.TryConsume<SequenceEnd>(out _))
                        DependsOn.Add(parser.Consume<Scalar>().Value);
                    break;
                case "working-dir":
                    WorkingDir = parser.Consume<Scalar>().Value;
                    break;
                case "restart":
                    Restart = new RestartPolicy();
                    ((IYamlConvertible)Restart).Read(parser, typeof(RestartPolicy), nestedObjectDeserializer);
                    break;
                case "environments":
                    Environments = [];
                    parser.Consume<MappingStart>();
                    while (!parser.TryConsume<MappingEnd>(out _))
                    {
                        var envKey = parser.Consume<Scalar>().Value;
                        var envVal = parser.Consume<Scalar>().Value;
                        Environments[envKey] = envVal;
                    }
                    break;
                case "logs":
                    Logs = new LogsConfig();
                    ((IYamlConvertible)Logs).Read(parser, typeof(LogsConfig), nestedObjectDeserializer);
                    break;
                case "health-check":
                    HealthCheck = new HealthCheckConfig();
                    ((IYamlConvertible)HealthCheck).Read(parser, typeof(HealthCheckConfig), nestedObjectDeserializer);
                    break;
                case "startup":
                    ((IYamlConvertible)Startup).Read(parser, typeof(StartupConfig), nestedObjectDeserializer);
                    break;
                case "stop":
                    Stop = new StopConfig();
                    ((IYamlConvertible)Stop).Read(parser, typeof(StopConfig), nestedObjectDeserializer);
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
        emitter.Emit(new Scalar("enabled")); emitter.Emit(new Scalar(Enabled ? "true" : "false"));
        if (User is not null) { emitter.Emit(new Scalar("user")); emitter.Emit(new Scalar(User)); }
        if (DependsOn is not null)
        {
            emitter.Emit(new Scalar("depends-on"));
            emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, true, SequenceStyle.Flow));
            foreach (var dep in DependsOn) emitter.Emit(new Scalar(dep));
            emitter.Emit(new SequenceEnd());
        }
        if (WorkingDir is not null) { emitter.Emit(new Scalar("working-dir")); emitter.Emit(new Scalar(WorkingDir)); }
        if (Restart is not null) { emitter.Emit(new Scalar("restart")); ((IYamlConvertible)Restart).Write(emitter, nestedObjectSerializer); }
        if (Environments is not null)
        {
            emitter.Emit(new Scalar("environments"));
            emitter.Emit(new MappingStart());
            foreach (var (k, v) in Environments) { emitter.Emit(new Scalar(k)); emitter.Emit(new Scalar(v)); }
            emitter.Emit(new MappingEnd());
        }
        if (Logs is not null) { emitter.Emit(new Scalar("logs")); ((IYamlConvertible)Logs).Write(emitter, nestedObjectSerializer); }
        if (HealthCheck is not null) { emitter.Emit(new Scalar("health-check")); ((IYamlConvertible)HealthCheck).Write(emitter, nestedObjectSerializer); }
        emitter.Emit(new Scalar("startup")); ((IYamlConvertible)Startup).Write(emitter, nestedObjectSerializer);
        if (Stop is not null) { emitter.Emit(new Scalar("stop")); ((IYamlConvertible)Stop).Write(emitter, nestedObjectSerializer); }
        emitter.Emit(new MappingEnd());
    }
}
