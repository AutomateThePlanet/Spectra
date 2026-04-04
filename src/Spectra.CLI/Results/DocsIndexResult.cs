using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class DocsIndexResult : CommandResult
{
    [JsonPropertyName("documents_indexed")]
    public int DocumentsIndexed { get; init; }

    [JsonPropertyName("documents_updated")]
    public int DocumentsUpdated { get; init; }

    [JsonPropertyName("index_path")]
    public required string IndexPath { get; init; }
}
