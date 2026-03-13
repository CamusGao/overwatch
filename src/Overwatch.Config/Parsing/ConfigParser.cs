using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Diagnostics.CodeAnalysis;
using Overwatch.Config.Models;

namespace Overwatch.Config.Parsing;

/// <summary>
/// Parses YAML configuration files into <see cref="NamespaceConfig"/> instances.
/// </summary>
public static class ConfigParser
{
    private static readonly IDeserializer Deserializer = BuildDeserializer();

    // YamlDotNet's DeserializerBuilder is annotated with [RequiresDynamicCode] because it can use
    // Reflection.Emit for performance. In AOT builds it falls back to regular reflection, which is
    // safe here because all model types are statically referenced and will never be trimmed.
    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "YamlDotNet falls back to regular reflection in AOT. Model types are preserved via static references.")]
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
