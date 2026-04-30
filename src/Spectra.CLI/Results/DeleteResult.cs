using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class DeleteResult : CommandResult
{
    [JsonPropertyName("dry_run")]
    public bool DryRun { get; init; }

    [JsonPropertyName("deleted")]
    public IReadOnlyList<DeletedTest> Deleted { get; init; } = Array.Empty<DeletedTest>();

    [JsonPropertyName("dependency_cleanup")]
    public IReadOnlyList<DependencyCleanup> DependencyCleanup { get; init; } = Array.Empty<DependencyCleanup>();

    [JsonPropertyName("skipped")]
    public IReadOnlyList<SkippedTest> Skipped { get; init; } = Array.Empty<SkippedTest>();

    [JsonPropertyName("errors")]
    public IReadOnlyList<DeleteError> Errors { get; init; } = Array.Empty<DeleteError>();
}

public sealed class DeletedTest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("automated_by")]
    public IReadOnlyList<string> AutomatedBy { get; init; } = Array.Empty<string>();

    [JsonPropertyName("stranded_automation")]
    public IReadOnlyList<string> StrandedAutomation { get; init; } = Array.Empty<string>();
}

public sealed class DependencyCleanup
{
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    [JsonPropertyName("removed_dep")]
    public required string RemovedDep { get; init; }
}

public sealed class SkippedTest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}

public sealed class DeleteError
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
