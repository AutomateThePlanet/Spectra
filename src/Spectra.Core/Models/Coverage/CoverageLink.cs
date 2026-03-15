using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// A relationship between coverage nodes.
/// </summary>
public sealed class CoverageLink
{
    /// <summary>Source node ID.</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>Target node ID.</summary>
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    /// <summary>Relationship type (document_to_test, test_to_automation, automation_to_test).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Link health status (valid, broken, mismatch, orphaned).</summary>
    [JsonPropertyName("status")]
    public LinkStatus Status { get; init; } = LinkStatus.Valid;
}

/// <summary>
/// Link types for coverage relationships.
/// </summary>
public static class LinkType
{
    /// <summary>source_refs relationship from documentation to test.</summary>
    public const string DocumentToTest = "document_to_test";

    /// <summary>automated_by relationship from test to automation.</summary>
    public const string TestToAutomation = "test_to_automation";

    /// <summary>Attribute reference from automation back to test.</summary>
    public const string AutomationToTest = "automation_to_test";
}
