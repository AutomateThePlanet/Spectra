using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Dashboard;

/// <summary>
/// A node in the coverage visualization graph.
/// </summary>
public sealed class CoverageNode
{
    /// <summary>Unique node identifier with type prefix (e.g., "doc:checkout-flow", "test:TC-101").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Node type: document, test, or automation.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Display name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>File path.</summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>Coverage status: covered, partial, or uncovered.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Child node IDs for hierarchical display.</summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<string>? Children { get; init; }
}

/// <summary>
/// Node types for coverage visualization.
/// </summary>
public static class NodeType
{
    public const string Document = "document";
    public const string Test = "test";
    public const string Automation = "automation";
}

/// <summary>
/// Coverage status values.
/// </summary>
public static class CoverageStatus
{
    /// <summary>Has both test and automation (green).</summary>
    public const string Covered = "covered";

    /// <summary>Has test but no automation (yellow).</summary>
    public const string Partial = "partial";

    /// <summary>No test coverage (red).</summary>
    public const string Uncovered = "uncovered";
}
