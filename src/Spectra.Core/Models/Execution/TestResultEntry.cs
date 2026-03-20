using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Individual test result entry for reports.
/// </summary>
public sealed record TestResultEntry
{
    /// <summary>Test case ID.</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>Test title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Final status.</summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TestStatus Status { get; init; }

    /// <summary>Attempt number.</summary>
    [JsonPropertyName("attempt")]
    public required int Attempt { get; init; }

    /// <summary>Execution duration.</summary>
    [JsonPropertyName("duration_ms")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DurationMs { get; init; }

    /// <summary>Tester notes.</summary>
    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; init; }

    /// <summary>Test ID that caused this test to be blocked.</summary>
    [JsonPropertyName("blocked_by")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BlockedBy { get; init; }

    /// <summary>Test preconditions.</summary>
    [JsonPropertyName("preconditions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Preconditions { get; init; }

    /// <summary>Test steps.</summary>
    [JsonPropertyName("steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Steps { get; init; }

    /// <summary>Expected result.</summary>
    [JsonPropertyName("expected_result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedResult { get; init; }

    /// <summary>Test data.</summary>
    [JsonPropertyName("test_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TestData { get; init; }

    /// <summary>Screenshot paths for this test.</summary>
    [JsonPropertyName("screenshot_paths")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ScreenshotPaths { get; init; }
}
