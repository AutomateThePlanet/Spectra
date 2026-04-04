using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ListResult : CommandResult
{
    [JsonPropertyName("suites")]
    public required IReadOnlyList<SuiteEntry> Suites { get; init; }
}

public sealed class SuiteEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("test_count")]
    public int TestCount { get; init; }

    [JsonPropertyName("last_modified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModified { get; init; }
}
