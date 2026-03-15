using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// An automation file referencing non-existent tests.
/// </summary>
public sealed class OrphanedAutomation
{
    /// <summary>Automation file path.</summary>
    [JsonPropertyName("file")]
    public required string File { get; init; }

    /// <summary>Test IDs referenced that don't exist.</summary>
    [JsonPropertyName("referenced_ids")]
    public IReadOnlyList<string> ReferencedIds { get; init; } = [];

    /// <summary>Line numbers where references were found.</summary>
    [JsonPropertyName("line_numbers")]
    public IReadOnlyList<int> LineNumbers { get; init; } = [];
}
