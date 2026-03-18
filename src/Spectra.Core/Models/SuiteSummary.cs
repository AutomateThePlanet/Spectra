namespace Spectra.Core.Models;

/// <summary>
/// Summary information for suite selection display.
/// </summary>
public sealed record SuiteSummary
{
    /// <summary>
    /// Suite name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Suite directory path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Number of tests in suite.
    /// </summary>
    public required int TestCount { get; init; }

    /// <summary>
    /// Most recent test modification time.
    /// </summary>
    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>
    /// Estimated documentation coverage percentage.
    /// </summary>
    public int? CoveragePercent { get; init; }
}
