using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Source;

/// <summary>
/// Filters a <see cref="DocumentMap"/> against a v2 documentation-index
/// manifest, dropping documents whose suite is flagged
/// <see cref="Spectra.Core.Models.Index.DocSuiteEntry.SkipAnalysis"/>
/// (Spec 040 §3.7). When the manifest is absent, returns the input unchanged.
/// </summary>
/// <remarks>
/// Applied wherever AI-facing flows consume <see cref="DocumentEntry"/>:
/// criteria extraction, generation context loading. Coverage flows MUST NOT
/// use this filter — coverage considers all documents regardless of analysis
/// status (FR-018).
/// </remarks>
public static class ManifestDocumentFilter
{
    /// <summary>
    /// Returns a new <see cref="DocumentMap"/> containing only documents whose
    /// path is NOT covered by a skip-analysis suite in the manifest. Pass
    /// <paramref name="includeArchived"/> = true to bypass the filter.
    /// </summary>
    /// <param name="map">Input map (typically from <see cref="DocumentMapBuilder.BuildAsync"/>).</param>
    /// <param name="manifestPath">Path to <c>_manifest.yaml</c>.</param>
    /// <param name="indexDir">Path to <c>docs/_index/</c>.</param>
    /// <param name="includeArchived">When true, returns the input unchanged.</param>
    public static async Task<DocumentMap> FilterAsync(
        DocumentMap map,
        string manifestPath,
        string indexDir,
        bool includeArchived,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexDir);

        if (includeArchived) return map;

        var manifest = await new DocIndexManifestReader().ReadAsync(manifestPath, ct);
        if (manifest is null) return map;

        // Collect every doc-path covered by a skip-analysis suite.
        var skipPaths = new HashSet<string>(StringComparer.Ordinal);
        var suiteReader = new SuiteIndexFileReader();
        foreach (var group in manifest.Groups.Where(g => g.SkipAnalysis))
        {
            var suiteFilePath = Path.Combine(indexDir, group.IndexFile);
            var suiteFile = await suiteReader.ReadAsync(suiteFilePath, group.Id, ct);
            if (suiteFile is null) continue;
            foreach (var entry in suiteFile.Entries)
            {
                skipPaths.Add(entry.Path);
            }
        }

        if (skipPaths.Count == 0) return map;

        var kept = map.Documents
            .Where(d => !skipPaths.Contains(d.Path))
            .ToList();

        return new DocumentMap
        {
            Documents = kept,
            TotalSizeKb = kept.Sum(e => e.SizeKb),
        };
    }
}
