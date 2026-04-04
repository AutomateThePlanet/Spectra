using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ShowResult : CommandResult
{
    [JsonPropertyName("test")]
    public required TestDetail Test { get; init; }
}

public sealed class TestDetail
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }

    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("component")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Component { get; init; }

    [JsonPropertyName("tags")]
    public required IReadOnlyList<string> Tags { get; init; }

    [JsonPropertyName("source_refs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? SourceRefs { get; init; }

    [JsonPropertyName("steps")]
    public required IReadOnlyList<string> Steps { get; init; }

    [JsonPropertyName("expected_results")]
    public required IReadOnlyList<string> ExpectedResults { get; init; }
}
