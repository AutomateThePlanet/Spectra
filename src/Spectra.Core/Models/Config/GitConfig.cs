using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for git operations.
/// </summary>
public sealed class GitConfig
{
    [JsonPropertyName("auto_branch")]
    public bool AutoBranch { get; init; } = true;

    [JsonPropertyName("branch_prefix")]
    public string BranchPrefix { get; init; } = "spectra/";

    [JsonPropertyName("auto_commit")]
    public bool AutoCommit { get; init; } = true;

    [JsonPropertyName("auto_pr")]
    public bool AutoPr { get; init; } = false;
}
