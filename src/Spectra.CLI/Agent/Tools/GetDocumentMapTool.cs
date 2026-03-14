using Spectra.CLI.Source;
using Spectra.Core.Models;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that retrieves the document map for the AI agent.
/// </summary>
public sealed class GetDocumentMapTool
{
    private readonly DocumentMapBuilder _mapBuilder;

    public GetDocumentMapTool(DocumentMapBuilder mapBuilder)
    {
        _mapBuilder = mapBuilder ?? throw new ArgumentNullException(nameof(mapBuilder));
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
            var map = await _mapBuilder.BuildAsync(basePath, ct);

            return new GetDocumentMapResult
            {
                Success = true,
                DocumentMap = map,
                Summary = DocumentMapBuilder.GetSummary(map)
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
    public string? Summary { get; init; }
    public string? Error { get; init; }
}
