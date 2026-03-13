using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Overwatch.Config.Models;

namespace Overwatch.Config.Parsing;

/// <summary>
/// Parses YAML configuration files into <see cref="NamespaceConfig"/> instances.
/// </summary>
public static class ConfigParser
{
    // Dummy ObjectDeserializer — all types handle their own reading via IYamlConvertible.
    private static readonly ObjectDeserializer NoOpDeserializer =
        type => throw new NotSupportedException($"Unexpected nested deserialize for {type}");

    /// <summary>Parses the given YAML string into a <see cref="NamespaceConfig"/>.</summary>
    public static NamespaceConfig Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return new NamespaceConfig();

        using var reader = new StringReader(yaml);
        var parser = new Parser(reader);
        parser.Consume<StreamStart>();
        if (!parser.TryConsume<DocumentStart>(out _))
            return new NamespaceConfig();
        if (parser.Current is DocumentEnd or StreamEnd or null)
            return new NamespaceConfig();

        var config = new NamespaceConfig();
        ((IYamlConvertible)config).Read(parser, typeof(NamespaceConfig), NoOpDeserializer);
        return config;
    }

    /// <summary>Parses the YAML file at the given path into a <see cref="NamespaceConfig"/>.</summary>
    public static NamespaceConfig ParseFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return Parse(yaml);
    }
}
