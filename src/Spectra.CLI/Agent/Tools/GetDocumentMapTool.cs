using Spectra.CLI.Index;
using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that retrieves the document map for the AI agent.
/// </summary>
public sealed class GetDocumentMapTool
{
    private readonly DocumentMapBuilder _mapBuilder;
    private readonly SourceConfig? _sourceConfig;

    public GetDocumentMapTool(DocumentMapBuilder mapBuilder, SourceConfig? sourceConfig = null)
    {
        _mapBuilder = mapBuilder ?? throw new ArgumentNullException(nameof(mapBuilder));
        _sourceConfig = sourceConfig;
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "get_document_map";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Retrieves the map of available source documents with their structure (headings, preview text). " +
        "Use this to understand what documentation is available before requesting specific documents.";

    /// <summary>
    /// Executes the tool and returns the document map.
    /// </summary>
    public async Task<GetDocumentMapResult> ExecuteAsync(
        string basePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        try
        {
            // Spec 040 Phase 4: prefer the v2 manifest layout. Reconstructs the
            // legacy DocumentIndex shape from manifest + per-suite files for
            // callers that consume the enriched view (DocumentTools).
            if (_sourceConfig is not null)
            {
                var manifestPath = LegacyIndexMigrator.ResolveManifestPath(basePath, _sourceConfig);
                var indexDir = LegacyIndexMigrator.ResolveIndexDir(basePath, _sourceConfig);
                if (File.Exists(manifestPath))
                {
                    var manifest = await new DocIndexManifestReader().ReadAsync(manifestPath, ct);
                    if (manifest is not null)
                    {
                        var index = await BuildLegacyIndexFromManifestAsync(
                            manifest, indexDir, ct);
                        var map = DocumentIndexService.ToDocumentMap(index);
                        return new GetDocumentMapResult
                        {
                            Success = true,
                            DocumentMap = map,
                            DocumentIndex = index,
                            Summary = DocumentMapBuilder.GetSummary(map),
                        };
                    }
                }
            }

            // Fall back to building from scratch via filesystem walk.
            var fallbackMap = await _mapBuilder.BuildAsync(basePath, ct);
            return new GetDocumentMapResult
            {
                Success = true,
                DocumentMap = fallbackMap,
                Summary = DocumentMapBuilder.GetSummary(fallbackMap),
            };
        }
        catch (Exception ex)
        {
            return new GetDocumentMapResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Reconstructs a legacy <see cref="DocumentIndex"/> by reading every
    /// suite's index file under <paramref name="indexDir"/>. Used to feed the
    /// enriched view in <c>DocumentTools</c> without regressing the AI agent's
    /// observability when the legacy single-file index is gone.
    /// </summary>
    private static async Task<DocumentIndex> BuildLegacyIndexFromManifestAsync(
        DocIndexManifest manifest,
        string indexDir,
        CancellationToken ct)
    {
        var entries = new List<DocumentIndexEntry>();
        var suiteReader = new SuiteIndexFileReader();
        foreach (var group in manifest.Groups)
        {
            var suiteFilePath = Path.Combine(indexDir, group.IndexFile);
            var suiteFile = await suiteReader.ReadAsync(suiteFilePath, group.Id, ct);
            if (suiteFile is null) continue;
            entries.AddRange(suiteFile.Entries);
        }

        return new DocumentIndex
        {
            GeneratedAt = manifest.GeneratedAt,
            TotalWordCount = entries.Sum(e => e.WordCount),
            TotalEstimatedTokens = manifest.TotalTokensEstimated,
            Entries = entries,
        };
    }
}

/// <summary>
/// Result of the get_document_map tool.
/// </summary>
public sealed record GetDocumentMapResult
{
    public required bool Success { get; init; }
    public DocumentMap? DocumentMap { get; init; }
    public DocumentIndex? DocumentIndex { get; init; }
    public string? Summary { get; init; }
    public string? Error { get; init; }
}
