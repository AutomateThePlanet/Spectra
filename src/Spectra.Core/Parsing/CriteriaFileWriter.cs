using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Writes per-document .criteria.yaml files with atomic write.
/// </summary>
public sealed class CriteriaFileWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
        .Build();

    /// <summary>
    /// Writes criteria to a .criteria.yaml file atomically.
    /// </summary>
    public async Task WriteAsync(
        string filePath,
        IReadOnlyList<AcceptanceCriterion> criteria,
        string? sourceDoc = null,
        string? docHash = null,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var headerLines = new List<string>();
        if (sourceDoc is not null)
            headerLines.Add($"# Extracted from: {sourceDoc}");
        if (docHash is not null)
            headerLines.Add($"# Doc hash: {docHash}");
        headerLines.Add($"# Generated at: {DateTime.UtcNow:o}");

        var header = string.Join(Environment.NewLine, headerLines) + Environment.NewLine;

        var doc = new CriteriaDocument { Criteria = criteria.ToList() };
        var yaml = Serializer.Serialize(doc);

        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, header + yaml, ct);
        File.Move(tempPath, filePath, overwrite: true);
    }
}
