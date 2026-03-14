namespace Spectra.Core.Models;

/// <summary>
/// Represents a proposed update to a test case.
/// </summary>
public sealed record UpdateProposal
{
    /// <summary>
    /// The original test case.
    /// </summary>
    public required TestCase OriginalTest { get; init; }

    /// <summary>
    /// Classification of the test's update status.
    /// </summary>
    public required UpdateClassification Classification { get; init; }

    /// <summary>
    /// Proposed updated test case (null if no update needed or test should be deleted).
    /// </summary>
    public TestCase? ProposedTest { get; init; }

    /// <summary>
    /// Reason for the classification.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Source documents that were analyzed.
    /// </summary>
    public IReadOnlyList<string> AnalyzedSources { get; init; } = [];

    /// <summary>
    /// Confidence score (0-1) of the classification.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Detailed changes between original and proposed test.
    /// </summary>
    public IReadOnlyList<ProposedChange> Changes { get; init; } = [];

    /// <summary>
    /// Whether this proposal recommends deleting the test.
    /// </summary>
    public bool ShouldDelete => Classification == UpdateClassification.Orphaned ||
                                 Classification == UpdateClassification.Redundant;

    /// <summary>
    /// Whether this proposal recommends updating the test.
    /// </summary>
    public bool ShouldUpdate => Classification == UpdateClassification.Outdated &&
                                 ProposedTest is not null;

    /// <summary>
    /// Whether no action is needed.
    /// </summary>
    public bool NoActionNeeded => Classification == UpdateClassification.UpToDate;
}

/// <summary>
/// A specific change proposed for a test.
/// </summary>
public sealed record ProposedChange
{
    /// <summary>
    /// Type of change.
    /// </summary>
    public required ChangeType Type { get; init; }

    /// <summary>
    /// Field being changed (e.g., "title", "steps", "expected_result").
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// Original value.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// New proposed value.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// Reason for this change.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Type of change proposed.
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// Field value was modified.
    /// </summary>
    Modified,

    /// <summary>
    /// Field was added.
    /// </summary>
    Added,

    /// <summary>
    /// Field was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// Item was added to a list.
    /// </summary>
    ItemAdded,

    /// <summary>
    /// Item was removed from a list.
    /// </summary>
    ItemRemoved,

    /// <summary>
    /// Item was reordered in a list.
    /// </summary>
    Reordered
}
