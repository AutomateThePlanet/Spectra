#pragma warning disable CS0618 // Obsolete type usage — legacy model
namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Result of requirements extraction and merge operation.
/// </summary>
public sealed record ExtractionResult
{
    /// <summary>Requirements extracted from documentation (before dedup).</summary>
    public IReadOnlyList<RequirementDefinition> Extracted { get; init; } = [];

    /// <summary>Requirements that will be added (after dedup).</summary>
    public IReadOnlyList<RequirementDefinition> Merged { get; init; } = [];

    /// <summary>Duplicates detected and skipped.</summary>
    public IReadOnlyList<DuplicateMatch> Duplicates { get; init; } = [];

    /// <summary>Number of requirements skipped as duplicates.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Total requirements in the file after merge.</summary>
    public int TotalInFile { get; init; }

    /// <summary>Number of source documents processed.</summary>
    public int SourceDocCount { get; init; }
}
