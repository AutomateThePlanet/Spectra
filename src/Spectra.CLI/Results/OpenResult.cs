using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class OpenResult : CommandResult
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("opened")]
    public bool Opened { get; init; }
}
