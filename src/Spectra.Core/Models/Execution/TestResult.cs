using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Represents the outcome of executing a single test.
/// </summary>
public sealed class TestResult
{
    /// <summary>FK to parent Run.</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Test case ID (e.g., TC-101).</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>Opaque reference for this test in this run.</summary>
    [JsonPropertyName("test_handle")]
    public required string TestHandle { get; init; }

    /// <summary>Execution result.</summary>
    [JsonPropertyName("status")]
    public required TestStatus Status { get; set; }

    /// <summary>Tester notes or observations.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>When test execution began.</summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>When test execution ended.</summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>Attempt number (1 for first, 2+ for retests).</summary>
    [JsonPropertyName("attempt")]
    public required int Attempt { get; init; }

    /// <summary>Test ID that caused this test to be blocked.</summary>
    [JsonPropertyName("blocked_by")]
    public string? BlockedBy { get; set; }

    /// <summary>Screenshot file paths attached to this test result.</summary>
    [JsonPropertyName("screenshot_paths")]
    public IReadOnlyList<string>? ScreenshotPaths { get; set; }
}
