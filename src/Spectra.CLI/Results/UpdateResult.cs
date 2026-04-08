using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Result for the test update command.
/// </summary>
public sealed class UpdateResult : CommandResult
{
    [JsonPropertyName("suite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suite { get; init; }

    [JsonPropertyName("testsUpdated")]
    public int TestsUpdated { get; init; }

    [JsonPropertyName("testsRemoved")]
    public int TestsRemoved { get; init; }

    [JsonPropertyName("testsUnchanged")]
    public int TestsUnchanged { get; init; }

    [JsonPropertyName("classification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UpdateClassificationCounts? Classification { get; init; }

    [JsonPropertyName("filesModified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? FilesModified { get; init; }

    [JsonPropertyName("filesDeleted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? FilesDeleted { get; init; }
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
