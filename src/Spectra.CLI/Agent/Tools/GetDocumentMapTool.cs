using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

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
            // Prefer reading from the document index if available
            if (_sourceConfig is not null)
            {
                var indexPath = DocumentIndexService.ResolveIndexPath(basePath, _sourceConfig);
                if (File.Exists(indexPath))
                {
                    var reader = new DocumentIndexReader();
                    var index = await reader.ReadFullAsync(indexPath, ct);
                    if (index is not null)
                    {
                        var map = DocumentIndexService.ToDocumentMap(index);
                        return new GetDocumentMapResult
                        {
                            Success = true,
                            DocumentMap = map,
                            DocumentIndex = index,
                            Summary = DocumentMapBuilder.GetSummary(map)
                        };
                    }
                }
            }

            // Fall back to building from scratch
            var fallbackMap = await _mapBuilder.BuildAsync(basePath, ct);

            return new GetDocumentMapResult
            {
                Success = true,
                DocumentMap = fallbackMap,
                Summary = DocumentMapBuilder.GetSummary(fallbackMap)
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
