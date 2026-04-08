using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Reads _criteria_index.yaml master index files.
/// </summary>
public sealed class CriteriaIndexReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Reads the criteria index from a YAML file. Returns empty index if file doesn't exist.
    /// </summary>
    public async Task<CriteriaIndex> ReadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return new CriteriaIndex();

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(content))
                return new CriteriaIndex();

            var index = Deserializer.Deserialize<CriteriaIndex>(content);
            return index ?? new CriteriaIndex();
        }
        catch
        {
            return new CriteriaIndex();
        }
    }
}
