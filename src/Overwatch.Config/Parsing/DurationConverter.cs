using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Overwatch.Config.Parsing;

/// <summary>
/// YamlDotNet type converter that parses duration strings like "20s", "5m", "1h", "7d" into TimeSpan.
/// </summary>
internal sealed class DurationConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(TimeSpan) || type == typeof(TimeSpan?);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            if (string.IsNullOrWhiteSpace(scalar.Value))
                return type == typeof(TimeSpan?) ? null : (object)TimeSpan.Zero;

            return ParseDuration(scalar.Value);
        }

        throw new YamlException($"Expected a scalar duration value.");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is TimeSpan ts)
            emitter.Emit(new Scalar(FormatDuration(ts)));
        else
            emitter.Emit(new Scalar("0s"));
    }

    public static TimeSpan ParseDuration(string value)
    {
        value = value.Trim();
        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value[..^2], out var ms))
            return TimeSpan.FromMilliseconds(ms);

        if (value.EndsWith('s') && double.TryParse(value[..^1], out var secs))
            return TimeSpan.FromSeconds(secs);

        if (value.EndsWith('m') && double.TryParse(value[..^1], out var mins))
            return TimeSpan.FromMinutes(mins);

        if (value.EndsWith('h') && double.TryParse(value[..^1], out var hours))
            return TimeSpan.FromHours(hours);

        if (value.EndsWith('d') && double.TryParse(value[..^1], out var days))
            return TimeSpan.FromDays(days);

        if (TimeSpan.TryParse(value, out var ts2))
            return ts2;

        throw new FormatException($"Cannot parse duration: '{value}'. Supported formats: 500ms, 20s, 5m, 1h, 7d.");
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalDays >= 1 && ts.TotalDays == Math.Floor(ts.TotalDays)) return $"{(int)ts.TotalDays}d";
        if (ts.TotalHours >= 1 && ts.TotalHours == Math.Floor(ts.TotalHours)) return $"{(int)ts.TotalHours}h";
        if (ts.TotalMinutes >= 1 && ts.TotalMinutes == Math.Floor(ts.TotalMinutes)) return $"{(int)ts.TotalMinutes}m";
        if (ts.TotalMilliseconds < 1000) return $"{(int)ts.TotalMilliseconds}ms";
        return $"{(int)ts.TotalSeconds}s";
    }
}
