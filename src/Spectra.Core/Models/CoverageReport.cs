namespace Spectra.Core.Models;

/// <summary>
/// Report of test coverage analysis.
/// </summary>
public sealed record CoverageReport
{
    /// <summary>
    /// When the report was generated.
    /// </summary>
    public required DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Total number of source documents analyzed.
    /// </summary>
    public required int TotalDocuments { get; init; }

    /// <summary>
    /// Total number of tests in all suites.
    /// </summary>
    public required int TotalTests { get; init; }

    /// <summary>
    /// Number of documents with at least one test.
    /// </summary>
    public required int CoveredDocuments { get; init; }

    /// <summary>
    /// Number of documents without any tests.
    /// </summary>
    public required int UncoveredDocuments { get; init; }

    /// <summary>
    /// Overall coverage percentage (0-100).
    /// </summary>
    public double CoveragePercentage =>
        TotalDocuments > 0 ? (double)CoveredDocuments / TotalDocuments * 100 : 0;

    /// <summary>
    /// Per-suite coverage breakdown.
    /// </summary>
    public IReadOnlyList<SuiteCoverage> Suites { get; init; } = [];

    /// <summary>
    /// Per-document coverage details.
    /// </summary>
    public IReadOnlyList<DocumentCoverage> Documents { get; init; } = [];

    /// <summary>
    /// Identified gaps in coverage.
    /// </summary>
    public IReadOnlyList<CoverageGap> Gaps { get; init; } = [];
}

/// <summary>
/// Coverage for a single suite.
/// </summary>
public sealed record SuiteCoverage
{
    public required string Name { get; init; }
    public required int TestCount { get; init; }
    public required int DocumentsCovered { get; init; }
    public IReadOnlyList<string> CoveredDocuments { get; init; } = [];
}

/// <summary>
/// Coverage for a single document.
/// </summary>
public sealed record DocumentCoverage
{
    public required string Path { get; init; }
    public required bool IsCovered { get; init; }
    public int TestCount { get; init; }
    public IReadOnlyList<string> TestIds { get; init; } = [];
    public IReadOnlyList<string> UncoveredSections { get; init; } = [];
}

/// <summary>
/// An identified gap in test coverage.
/// </summary>
public sealed record CoverageGap
{
    public required string DocumentPath { get; init; }
    public string? Section { get; init; }
    public required string Reason { get; init; }
    public required GapSeverity Severity { get; init; }
    public string? Suggestion { get; init; }
}

/// <summary>
/// Severity of a coverage gap.
/// </summary>
public enum GapSeverity
{
    Low,
    Medium,
    High,
    Critical
}
