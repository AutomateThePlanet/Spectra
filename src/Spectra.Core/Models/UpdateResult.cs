namespace Spectra.Core.Models;

/// <summary>
/// Result of a test update operation.
/// </summary>
public sealed record UpdateResult
{
    /// <summary>
    /// Count of tests that are up to date.
    /// </summary>
    public required int UpToDate { get; init; }

    /// <summary>
    /// Count of tests that were updated.
    /// </summary>
    public required int Updated { get; init; }

    /// <summary>
    /// Count of tests marked as orphaned.
    /// </summary>
    public required int Orphaned { get; init; }

    /// <summary>
    /// Count of tests flagged as redundant.
    /// </summary>
    public required int Redundant { get; init; }

    /// <summary>
    /// Paths to modified test files.
    /// </summary>
    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];

    /// <summary>
    /// Details of orphaned tests.
    /// </summary>
    public IReadOnlyList<ClassifiedTest> OrphanedTests { get; init; } = [];

    /// <summary>
    /// Details of redundant tests.
    /// </summary>
    public IReadOnlyList<ClassifiedTest> RedundantTests { get; init; } = [];
}
