using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class InitResult : CommandResult
{
    [JsonPropertyName("created")]
    public required IReadOnlyList<string> Created { get; init; }
}
