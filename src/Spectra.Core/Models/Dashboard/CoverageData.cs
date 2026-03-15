using System.Text.Json.Serialization;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// Root structure for coverage visualization data.
/// </summary>
public sealed class CoverageData
{
    /// <summary>All nodes in the coverage graph (documents, tests, automation).</summary>
    [JsonPropertyName("nodes")]
    public IReadOnlyList<CoverageNode> Nodes { get; init; } = [];

    /// <summary>Relationships between nodes.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<CoverageLink> Links { get; init; } = [];
}
