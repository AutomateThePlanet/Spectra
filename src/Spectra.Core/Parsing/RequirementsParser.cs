using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Parses _requirements.yaml files into RequirementDefinition objects.
/// </summary>
public sealed class RequirementsParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses requirements from a YAML file. Returns empty list if file doesn't exist or is malformed.
    /// </summary>
    public async Task<IReadOnlyList<RequirementDefinition>> ParseAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            return Parse(content);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Parses requirements from YAML content.
    /// </summary>
    public IReadOnlyList<RequirementDefinition> Parse(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return [];
        }

        try
        {
            var doc = Deserializer.Deserialize<RequirementsDocument>(yamlContent);
            return doc?.Requirements ?? [];
        }
        catch
        {
            return [];
        }
    }
}
