namespace Spectra.Core.Models;

/// <summary>
/// A test case with its classification for update operations.
/// </summary>
public sealed record ClassifiedTest
{
    /// <summary>
    /// The test case.
    /// </summary>
    public required TestCase Test { get; init; }

    /// <summary>
    /// Classification result.
    /// </summary>
    public required UpdateClassification Classification { get; init; }

    /// <summary>
    /// Reason for this classification.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// For redundant tests: ID of the similar test.
    /// </summary>
    public string? RelatedTestId { get; init; }

    /// <summary>
    /// For outdated tests: the new content.
    /// </summary>
    public TestCase? UpdatedContent { get; init; }
}
