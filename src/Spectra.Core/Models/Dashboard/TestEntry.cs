using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Denormalized test information for dashboard display.
/// </summary>
public sealed class TestEntry
{
    /// <summary>Test ID (e.g., "TC-101").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Parent suite name.</summary>
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    /// <summary>Test title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Source file path relative to tests directory.</summary>
    [JsonPropertyName("file")]
    public required string File { get; init; }

    /// <summary>Test priority (high/medium/low).</summary>
    [JsonPropertyName("priority")]
    public required string Priority { get; init; }

    /// <summary>Test tags for filtering.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Component under test.</summary>
    [JsonPropertyName("component")]
    public string? Component { get; init; }

    /// <summary>Referenced documentation files (source_refs).</summary>
    [JsonPropertyName("source_refs")]
    public IReadOnlyList<string> SourceRefs { get; init; } = [];

    /// <summary>Automation file path if linked.</summary>
    [JsonPropertyName("automated_by")]
    public string? AutomatedBy { get; init; }

    /// <summary>Whether any automation link exists (from automated_by or attribute scan).</summary>
    [JsonPropertyName("has_automation")]
    public bool HasAutomation { get; init; }
}
