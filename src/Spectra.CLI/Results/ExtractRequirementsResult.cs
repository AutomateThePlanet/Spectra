using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ExtractRequirementsResult : CommandResult
{
    [JsonPropertyName("extracted_count")]
    public int ExtractedCount { get; init; }

    [JsonPropertyName("new_count")]
    public int NewCount { get; init; }

    [JsonPropertyName("duplicates_skipped")]
    public int DuplicatesSkipped { get; init; }

    [JsonPropertyName("total_in_file")]
    public int TotalInFile { get; init; }

    [JsonPropertyName("requirements_file")]
    public required string RequirementsFile { get; init; }

    [JsonPropertyName("requirements")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RequirementEntry>? Requirements { get; init; }
}

public sealed class RequirementEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    [JsonPropertyName("priority")]
    public required string Priority { get; init; }
}
