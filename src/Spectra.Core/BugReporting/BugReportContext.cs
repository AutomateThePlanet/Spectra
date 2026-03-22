namespace Spectra.Core.BugReporting;

/// <summary>
/// Runtime data assembled when composing a bug report from test execution context.
/// </summary>
public sealed class BugReportContext
{
    public required string TestId { get; init; }
    public required string TestTitle { get; init; }
    public required string SuiteName { get; init; }
    public string Environment { get; init; } = "";
    public required string Severity { get; init; }
    public required string RunId { get; init; }
    public string FailedSteps { get; init; } = "";
    public string ExpectedResult { get; init; } = "";
    public IReadOnlyList<string> Attachments { get; init; } = [];
    public IReadOnlyList<string> SourceRefs { get; init; } = [];
    public IReadOnlyList<string> Requirements { get; init; } = [];
    public string? Component { get; init; }
    public IReadOnlyList<string> ExistingBugs { get; init; } = [];

    /// <summary>
    /// Generates a bug title from test title and failed steps.
    /// </summary>
    public string GenerateTitle()
    {
        var stepSummary = FailedSteps.Length > 50
            ? FailedSteps[..50] + "..."
            : FailedSteps;

        return string.IsNullOrWhiteSpace(stepSummary)
            ? $"Bug: {TestTitle}"
            : $"Bug: {TestTitle} - {stepSummary.Split('\n')[0].TrimStart(' ', '-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.')}";
    }

    /// <summary>
    /// Maps test priority to bug severity.
    /// </summary>
    public static string MapPriorityToSeverity(string? priority, string defaultSeverity = "medium")
    {
        return priority?.ToLowerInvariant() switch
        {
            "high" => "critical",
            "medium" => "major",
            "low" => "minor",
            _ => MapSeverityDefault(defaultSeverity)
        };
    }

    private static string MapSeverityDefault(string defaultSeverity)
    {
        return defaultSeverity.ToLowerInvariant() switch
        {
            "critical" or "major" or "medium" or "minor" => defaultSeverity.ToLowerInvariant(),
            _ => "medium"
        };
    }
}
