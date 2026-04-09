using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Result for the test update command.
/// </summary>
public sealed class UpdateResult : CommandResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("suite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suite { get; init; }

    [JsonPropertyName("totalTests")]
    public int TotalTests { get; init; }

    [JsonPropertyName("testsUpdated")]
    public int TestsUpdated { get; init; }

    [JsonPropertyName("testsRemoved")]
    public int TestsRemoved { get; init; }

    [JsonPropertyName("testsUnchanged")]
    public int TestsUnchanged { get; init; }

    [JsonPropertyName("classification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UpdateClassificationCounts? Classification { get; init; }

    [JsonPropertyName("testsFlagged")]
    public int TestsFlagged { get; init; }

    [JsonPropertyName("flaggedTests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FlaggedTestEntry>? FlaggedTests { get; init; }

    [JsonPropertyName("filesModified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? FilesModified { get; init; }

    [JsonPropertyName("filesDeleted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? FilesDeleted { get; init; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Duration { get; init; }
}

public sealed class UpdateClassificationCounts
{
    [JsonPropertyName("upToDate")]
    public int UpToDate { get; init; }

    [JsonPropertyName("outdated")]
    public int Outdated { get; init; }

    [JsonPropertyName("orphaned")]
    public int Orphaned { get; init; }

    [JsonPropertyName("redundant")]
    public int Redundant { get; init; }
}

public sealed class FlaggedTestEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
