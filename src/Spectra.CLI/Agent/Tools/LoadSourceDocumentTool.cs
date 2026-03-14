using Spectra.CLI.Source;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that loads the content of a specific source document.
/// </summary>
public sealed class LoadSourceDocumentTool
{
    private readonly SourceDocumentReader _reader;

    public LoadSourceDocumentTool(SourceDocumentReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "load_source_document";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Loads the full content of a source document by its path. " +
        "Use the document map to find available document paths first. " +
        "Optionally specify a section heading to load only that section.";

    /// <summary>
    /// Executes the tool and returns the document content.
    /// </summary>
    public async Task<LoadSourceDocumentResult> ExecuteAsync(
        string documentPath,
        string? sectionHeading = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        try
        {
            if (!string.IsNullOrEmpty(sectionHeading))
            {
                var section = await _reader.ReadSectionAsync(documentPath, sectionHeading, ct);
                if (!section.Found)
                {
                    return new LoadSourceDocumentResult
                    {
                        Success = false,
                        Error = $"Section '{sectionHeading}' not found in document"
                    };
                }

                return new LoadSourceDocumentResult
                {
                    Success = true,
                    Path = documentPath,
                    Content = section.Content,
                    Section = sectionHeading,
                    WasTruncated = false
                };
            }

            var content = await _reader.ReadAsync(documentPath, ct);

            return new LoadSourceDocumentResult
            {
                Success = true,
                Path = documentPath,
                Content = content.Content,
                WasTruncated = content.IsTruncated
            };
        }
        catch (FileNotFoundException)
        {
            return new LoadSourceDocumentResult
            {
                Success = false,
                Error = $"Document not found: {documentPath}"
            };
        }
        catch (Exception ex)
        {
            return new LoadSourceDocumentResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the load_source_document tool.
/// </summary>
public sealed record LoadSourceDocumentResult
{
    public required bool Success { get; init; }
    public string? Path { get; init; }
    public string? Content { get; init; }
    public string? Section { get; init; }
    public bool WasTruncated { get; init; }
    public string? Error { get; init; }
}
