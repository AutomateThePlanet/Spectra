using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class SuiteListResult : CommandResult
{
    [JsonPropertyName("suites")]
    public IReadOnlyList<SuiteListEntry> Suites { get; init; } = Array.Empty<SuiteListEntry>();
}

public sealed class SuiteListEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("test_count")]
    public int TestCount { get; init; }

    [JsonPropertyName("directory")]
    public required string Directory { get; init; }

    [JsonPropertyName("automated_count")]
    public int AutomatedCount { get; init; }
}

public sealed class SuiteRenameResult : CommandResult
{
    [JsonPropertyName("dry_run")]
    public bool DryRun { get; init; }

    [JsonPropertyName("old_name")]
    public required string OldName { get; init; }

    [JsonPropertyName("new_name")]
    public required string NewName { get; init; }

    [JsonPropertyName("directory_renamed")]
    public bool DirectoryRenamed { get; init; }

    [JsonPropertyName("index_updated")]
    public bool IndexUpdated { get; init; }

    [JsonPropertyName("selections_updated")]
    public IReadOnlyList<string> SelectionsUpdated { get; init; } = Array.Empty<string>();

    [JsonPropertyName("config_block_renamed")]
    public bool ConfigBlockRenamed { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

public sealed class SuiteDeleteResult : CommandResult
{
    [JsonPropertyName("dry_run")]
    public bool DryRun { get; init; }

    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("tests_removed")]
    public int TestsRemoved { get; init; }

    [JsonPropertyName("stranded_automation_count")]
    public int StrandedAutomationCount { get; init; }

    [JsonPropertyName("stranded_automation_files")]
    public IReadOnlyList<string> StrandedAutomationFiles { get; init; } = Array.Empty<string>();

    [JsonPropertyName("external_dependency_cleanup")]
    public IReadOnlyList<ExternalDependencyCleanup> ExternalDependencyCleanup { get; init; } = Array.Empty<ExternalDependencyCleanup>();

    [JsonPropertyName("selections_updated")]
    public IReadOnlyList<string> SelectionsUpdated { get; init; } = Array.Empty<string>();

    [JsonPropertyName("config_block_removed")]
    public bool ConfigBlockRemoved { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

public sealed class ExternalDependencyCleanup
{
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("removed_deps")]
    public IReadOnlyList<string> RemovedDeps { get; init; } = Array.Empty<string>();
}
