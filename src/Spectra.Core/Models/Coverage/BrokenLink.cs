using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// A reference to a non-existent file.
/// </summary>
public sealed class BrokenLink
{
    /// <summary>Test ID with the broken link.</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>The automated_by value that doesn't exist.</summary>
    [JsonPropertyName("automated_by")]
    public required string AutomatedBy { get; init; }

    /// <summary>Reason for the broken link (e.g., "File not found").</summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
