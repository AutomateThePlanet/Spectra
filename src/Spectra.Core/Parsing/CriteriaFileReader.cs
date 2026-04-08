using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Reads per-document .criteria.yaml files.
/// </summary>
public sealed class CriteriaFileReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Reads criteria from a .criteria.yaml file. Returns empty list if file doesn't exist or is malformed.
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ReadAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return [];

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(content))
                return [];

            var doc = Deserializer.Deserialize<CriteriaDocument>(content);
            return doc?.Criteria ?? [];
        }
        catch
        {
            return [];
        }
    }
}
