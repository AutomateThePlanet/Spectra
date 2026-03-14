using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Represents a test in the execution queue.
/// </summary>
public sealed record QueuedTest
{
    /// <summary>Test case ID (e.g., TC-101).</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>Opaque handle for this test execution.</summary>
    [JsonPropertyName("test_handle")]
    public required string TestHandle { get; init; }

    /// <summary>Test title for display.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Test priority for ordering.</summary>
    [JsonPropertyName("priority")]
    public required Priority Priority { get; init; }

    /// <summary>ID of test this depends on, if any.</summary>
    [JsonPropertyName("depends_on")]
    public string? DependsOn { get; init; }

    /// <summary>Current execution status.</summary>
    [JsonPropertyName("status")]
    public TestStatus Status { get; init; } = TestStatus.Pending;
}
