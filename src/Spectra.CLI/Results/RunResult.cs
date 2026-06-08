using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Spec 065: JSON/human result for the <c>spectra run</c> command group. One flexible DTO serves
/// every subcommand; only populated fields are written (<see cref="JsonIgnoreCondition.WhenWritingNull"/>).
/// Mirrors the data the equivalent MCP tools return so the two surfaces are recognizably the same.
/// </summary>
public sealed class RunResult : CommandResult
{
    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("run_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunId { get; init; }

    [JsonPropertyName("suite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suite { get; init; }

    [JsonPropertyName("run_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunStatus { get; init; }

    [JsonPropertyName("progress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Progress { get; init; }

    [JsonPropertyName("test_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TestCount { get; init; }

    [JsonPropertyName("counts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? Counts { get; init; }

    [JsonPropertyName("current_test")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunTestRef? CurrentTest { get; init; }

    [JsonPropertyName("next_test")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunTestRef? NextTest { get; init; }

    [JsonPropertyName("recorded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunRecorded? Recorded { get; init; }

    [JsonPropertyName("blocked_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? BlockedTests { get; init; }

    [JsonPropertyName("reports")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunReports? Reports { get; init; }

    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunReportSummary? Summary { get; init; }

    [JsonPropertyName("runs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RunListItem>? Runs { get; init; }

    [JsonPropertyName("suites")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RunSuiteItem>? Suites { get; init; }

    [JsonPropertyName("selections")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Selections { get; init; }

    /// <summary>Full test-case detail block (for <c>run show</c>).</summary>
    [JsonPropertyName("test")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Test { get; init; }
}

public sealed class RunTestRef
{
    [JsonPropertyName("test_handle")] public required string TestHandle { get; init; }
    [JsonPropertyName("test_id")] public required string TestId { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
}

public sealed class RunRecorded
{
    [JsonPropertyName("test_id")] public required string TestId { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
}

public sealed class RunReports
{
    [JsonPropertyName("directory")] public string Directory { get; init; } = ".execution/reports/";
    [JsonPropertyName("json")] public string? Json { get; init; }
    [JsonPropertyName("markdown")] public string? Markdown { get; init; }
    [JsonPropertyName("html")] public string? Html { get; init; }
}

public sealed class RunReportSummary
{
    [JsonPropertyName("total")] public int Total { get; init; }
    [JsonPropertyName("passed")] public int Passed { get; init; }
    [JsonPropertyName("failed")] public int Failed { get; init; }
    [JsonPropertyName("skipped")] public int Skipped { get; init; }
    [JsonPropertyName("blocked")] public int Blocked { get; init; }
}

public sealed class RunListItem
{
    [JsonPropertyName("run_id")] public required string RunId { get; init; }
    [JsonPropertyName("suite")] public string? Suite { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("started_at")] public string? StartedAt { get; init; }
    [JsonPropertyName("started_by")] public string? StartedBy { get; init; }
}

public sealed class RunSuiteItem
{
    [JsonPropertyName("suite")] public required string Suite { get; init; }
    [JsonPropertyName("test_count")] public int TestCount { get; init; }
}
