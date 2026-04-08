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
    /// If the file doesn't exist but a legacy _requirements.yaml does, renames it to .bak.
    /// </summary>
    public async Task<CriteriaIndex> ReadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            TryRenameLegacyRequirementsFile(filePath);
            return new CriteriaIndex();
        }

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

    /// <summary>
    /// If _criteria_index.yaml doesn't exist but _requirements.yaml does in the same directory,
    /// rename the legacy file to .bak so fresh extraction can create the new format.
    /// </summary>
    private static void TryRenameLegacyRequirementsFile(string criteriaFilePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(criteriaFilePath);
            if (string.IsNullOrEmpty(dir))
                return;

            var legacyPath = Path.Combine(dir, "_requirements.yaml");
            if (!File.Exists(legacyPath))
                return;

            var backupPath = legacyPath + ".bak";
            File.Move(legacyPath, backupPath, overwrite: false);
            Console.Error.WriteLine($"Renamed {Path.GetFileName(legacyPath)} → {Path.GetFileName(backupPath)} (legacy format)");
        }
        catch
        {
            // Non-critical — if rename fails (e.g., .bak already exists), continue silently
        }
    }
}
