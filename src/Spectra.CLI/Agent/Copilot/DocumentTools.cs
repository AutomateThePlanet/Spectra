using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Source;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// AIFunction definitions for document access tools.
/// Wraps existing ToolRegistry tools for use in the Copilot SDK agent loop.
/// </summary>
public sealed class DocumentTools
{
    private readonly ToolRegistry _registry;
    private readonly string _basePath;

    public DocumentTools(ToolRegistry registry, string basePath)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
    }

    /// <summary>
    /// Creates AIFunctions for document access.
    /// </summary>
    public IEnumerable<AIFunction> CreateFunctions()
    {
        yield return AIFunctionFactory.Create(
            ListDocumentationFiles,
            nameof(ListDocumentationFiles),
            "Lists all available documentation files with their titles, sections, and preview text. " +
            "Use this first to understand what documentation is available before reading specific documents.");

        yield return AIFunctionFactory.Create(
            ReadDocument,
            nameof(ReadDocument),
            "Reads the full content of a specific documentation file. " +
            "Use the document path from ListDocumentationFiles. " +
            "Optionally specify a section heading to read only that section.");

        yield return AIFunctionFactory.Create(
            SearchDocuments,
            nameof(SearchDocuments),
            "Searches all documentation files for a keyword or phrase. " +
            "Returns matching documents with context around the matches. " +
            "Use this to find relevant documentation without reading everything.");
    }

    /// <summary>
    /// Lists all available documentation files with structure information.
    /// </summary>
    [Description("Lists all available documentation files with their titles, sections, and preview text.")]
    public async Task<string> ListDocumentationFiles(
        CancellationToken ct = default)
    {
        var result = await _registry.GetDocumentMap.ExecuteAsync(_basePath, ct);

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error ?? "Failed to get document map"
            });
        }

        // Return a simplified view for the AI
        var documents = result.DocumentMap?.Documents ?? [];
        var summary = new
        {
            success = true,
            total_documents = documents.Count,
            documents = documents.Select(d => new
            {
                path = d.Path,
                title = d.Title,
                sections = d.Headings,
                preview = d.Preview?.Length > 200 ? d.Preview[..200] + "..." : d.Preview,
                size_kb = d.SizeKb
            })
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads the full content of a specific document.
    /// </summary>
    [Description("Reads the full content of a specific documentation file.")]
    public async Task<string> ReadDocument(
        [Description("Path to the document (from ListDocumentationFiles)")] string documentPath,
        [Description("Optional section heading to read only that section")] string? sectionHeading = null,
        CancellationToken ct = default)
    {
        var result = await _registry.LoadSourceDocument.ExecuteAsync(documentPath, sectionHeading, ct);

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error ?? "Failed to load document"
            });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            path = result.Path,
            section = result.Section,
            content = result.Content,
            truncated = result.WasTruncated
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Searches documentation for a keyword.
    /// </summary>
    [Description("Searches all documentation files for a keyword or phrase.")]
    public async Task<string> SearchDocuments(
        [Description("Keyword or phrase to search for")] string keyword,
        [Description("Maximum number of results (default: 10)")] int maxResults = 10,
        CancellationToken ct = default)
    {
        var result = await _registry.SearchSourceDocs.ExecuteAsync(_basePath, keyword, maxResults, ct);

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error ?? "Search failed"
            });
        }

        // Use the correct property name: Results instead of Matches
        return JsonSerializer.Serialize(new
        {
            success = true,
            keyword,
            total_matches = result.Results?.Count ?? 0,
            matches = result.Results?.Select(m => new
            {
                path = m.FilePath,
                match_count = m.MatchCount,
                excerpts = m.Excerpts,
                relevance_score = m.Score
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Factory method to create DocumentTools with required dependencies.
    /// </summary>
    public static DocumentTools Create(string basePath, SpectraConfig config)
    {
        var registry = new ToolRegistry(basePath, config);
        return new DocumentTools(registry, basePath);
    }
}
