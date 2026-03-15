using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// A manual test without automation link.
/// </summary>
public sealed class UnlinkedTest
{
    /// <summary>Test ID.</summary>
    [JsonPropertyName("test_id")]
    public required string TestId { get; init; }

    /// <summary>Suite name.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Test title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Test priority.</summary>
    [JsonPropertyName("priority")]
    public required string Priority { get; init; }
}
