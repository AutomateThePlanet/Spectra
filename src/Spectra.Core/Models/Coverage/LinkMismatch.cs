using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// An inconsistent bidirectional link.
/// </summary>
public sealed class LinkMismatch
{
    /// <summary>Test ID involved in the mismatch.</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>The automated_by value in the test (if any).</summary>
    [JsonPropertyName("test_automated_by")]
    public string? TestAutomatedBy { get; init; }

    /// <summary>The automation file that references this test (if any).</summary>
    [JsonPropertyName("automation_file")]
    public string? AutomationFile { get; init; }

    /// <summary>Description of the mismatch issue.</summary>
    [JsonPropertyName("issue")]
    public required string Issue { get; init; }
}
