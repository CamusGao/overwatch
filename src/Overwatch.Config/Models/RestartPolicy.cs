using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Overwatch.Config.Models;

/// <summary>
/// Restart policy for a service. Supports both string shorthand and on-exit/on-unhealthy object form.
/// String shorthand applies the same rule to both triggers.
/// </summary>
public sealed class RestartPolicy : IYamlConvertible
{
    /// <summary>Restart rule when the process exits (normally or abnormally).</summary>
    public RestartRule OnExit { get; set; } = RestartRule.None;

    /// <summary>Restart rule when health check continuously fails.</summary>
    public RestartRule OnUnhealthy { get; set; } = RestartRule.None;

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            var rule = RestartRule.Parse(scalar.Value);
            OnExit = rule;
            OnUnhealthy = rule;
        }
        else
        {
            parser.Consume<MappingStart>();
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                var value = parser.Consume<Scalar>().Value;
                if (key == "on-exit") OnExit = RestartRule.Parse(value);
                else if (key == "on-unhealthy") OnUnhealthy = RestartRule.Parse(value);
            }
        }
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        emitter.Emit(new MappingStart());
        emitter.Emit(new Scalar("on-exit"));
        emitter.Emit(new Scalar(FormatRule(OnExit)));
        emitter.Emit(new Scalar("on-unhealthy"));
        emitter.Emit(new Scalar(FormatRule(OnUnhealthy)));
        emitter.Emit(new MappingEnd());
    }

    private static string FormatRule(RestartRule rule) => rule.Mode switch
    {
        RestartMode.No => "no",
        RestartMode.Always => "always",
        RestartMode.OnFailure when rule.MaxRetries.HasValue => $"on-failure:{rule.MaxRetries}",
        RestartMode.OnFailure => "on-failure",
        _ => "no",
    };
}
