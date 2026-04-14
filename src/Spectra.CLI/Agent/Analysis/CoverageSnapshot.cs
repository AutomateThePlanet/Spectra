namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// Controls whether the coverage context includes full test title lists
/// or only summary statistics.
/// </summary>
public enum CoverageContextMode
{
    /// <summary>All titles, criteria, and source refs included.</summary>
    Full,

    /// <summary>Only statistics and uncovered items (for suites with >500 tests).</summary>
    Summary
}

/// <summary>
/// Aggregated coverage snapshot for a suite, built from _index.json,
/// .criteria.yaml files, and docs/_index.md. Used to make behavior
/// analysis coverage-aware so only genuine gaps are recommended.
/// </summary>
public sealed class CoverageSnapshot
{
    /// <summary>Total existing tests in the suite.</summary>
    public int ExistingTestCount { get; init; }

    /// <summary>Existing test titles for dedup context.</summary>
    public IReadOnlyList<string> ExistingTestTitles { get; init; } = [];

    /// <summary>Criteria IDs already covered by at least one test.</summary>
    public IReadOnlySet<string> CoveredCriteriaIds { get; init; } = new HashSet<string>();

    /// <summary>Criteria IDs with zero linked tests.</summary>
    public IReadOnlyList<UncoveredCriterion> UncoveredCriteria { get; init; } = [];

    /// <summary>Doc section refs that have linked tests.</summary>
    public IReadOnlySet<string> CoveredSourceRefs { get; init; } = new HashSet<string>();

    /// <summary>Doc section refs with no linked tests.</summary>
    public IReadOnlyList<string> UncoveredSourceRefs { get; init; } = [];

    /// <summary>Total acceptance criteria across all sources.</summary>
    public int TotalCriteriaCount { get; init; }

    /// <summary>Full or Summary based on suite size.</summary>
    public CoverageContextMode Mode => ExistingTestCount > 500
        ? CoverageContextMode.Summary
        : CoverageContextMode.Full;

    /// <summary>Whether this snapshot has any meaningful coverage data.</summary>
    public bool HasData => ExistingTestCount > 0 || TotalCriteriaCount > 0;
}

/// <summary>
/// A single acceptance criterion with zero linked tests.
/// </summary>
public sealed record UncoveredCriterion(
    string Id,
    string Text,
    string? Source,
    string Priority);
