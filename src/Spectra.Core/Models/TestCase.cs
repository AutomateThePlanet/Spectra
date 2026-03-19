using Spectra.Core.Models.Grounding;

namespace Spectra.Core.Models;

/// <summary>
/// Represents a parsed test case from a Markdown file.
/// </summary>
public sealed class TestCase
{
    // Identity
    /// <summary>
    /// Unique test identifier (e.g., "TC-102").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Relative path from tests/ directory.
    /// </summary>
    public required string FilePath { get; init; }

    // Metadata (from frontmatter)
    /// <summary>
    /// Test priority level.
    /// </summary>
    public required Priority Priority { get; init; }

    /// <summary>
    /// Tags for categorization and filtering.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Component being tested.
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// Test preconditions (from frontmatter, not body).
    /// </summary>
    public string? Preconditions { get; init; }

    /// <summary>
    /// Required environment variables or setup.
    /// </summary>
    public IReadOnlyList<string> Environment { get; init; } = [];

    /// <summary>
    /// Estimated test duration.
    /// </summary>
    public TimeSpan? EstimatedDuration { get; init; }

    /// <summary>
    /// Test ID this test depends on.
    /// </summary>
    public string? DependsOn { get; init; }

    /// <summary>
    /// Source documentation references.
    /// </summary>
    public IReadOnlyList<string> SourceRefs { get; init; } = [];

    /// <summary>
    /// Related work items (issues, PRs, etc.).
    /// </summary>
    public IReadOnlyList<string> RelatedWorkItems { get; init; } = [];

    /// <summary>
    /// Custom metadata fields.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Custom { get; init; }

    /// <summary>
    /// Test status (e.g., "orphaned" when documentation is removed).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Reason the test was marked orphaned.
    /// </summary>
    public string? OrphanedReason { get; init; }

    /// <summary>
    /// Date the test was marked orphaned.
    /// </summary>
    public DateTimeOffset? OrphanedDate { get; init; }

    // Content (from Markdown body)
    /// <summary>
    /// Test title (from first H1 heading).
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Test steps.
    /// </summary>
    public IReadOnlyList<string> Steps { get; init; } = [];

    /// <summary>
    /// Expected result.
    /// </summary>
    public required string ExpectedResult { get; init; }

    /// <summary>
    /// Test data.
    /// </summary>
    public string? TestData { get; init; }

    /// <summary>
    /// Grounding verification metadata (if verified).
    /// </summary>
    public GroundingMetadata? Grounding { get; init; }
}
