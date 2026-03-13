using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Overwatch.Config.Models;

namespace Overwatch.Config.Parsing;

/// <summary>
/// Parses YAML configuration files into <see cref="NamespaceConfig"/> instances.
/// </summary>
public static class ConfigParser
{
    private static readonly IDeserializer Deserializer = BuildDeserializer();

    private static IDeserializer BuildDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithTypeConverter(new DurationConverter())
            .Build();
    }

    /// <summary>Parses the given YAML string into a <see cref="NamespaceConfig"/>.</summary>
    public static NamespaceConfig Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return new NamespaceConfig();

        return Deserializer.Deserialize<NamespaceConfig>(yaml) ?? new NamespaceConfig();
    }

    /// <summary>Parses the YAML file at the given path into a <see cref="NamespaceConfig"/>.</summary>
    public static NamespaceConfig ParseFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return Parse(yaml);
    }
}
