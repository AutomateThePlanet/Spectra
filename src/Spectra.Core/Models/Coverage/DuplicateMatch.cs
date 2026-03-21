namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Represents a detected duplicate between a new requirement and an existing one.
/// </summary>
public sealed record DuplicateMatch
{
    /// <summary>Title of the newly extracted requirement.</summary>
    public required string NewTitle { get; init; }

    /// <summary>ID of the existing matching requirement.</summary>
    public required string ExistingId { get; init; }

    /// <summary>Title of the existing matching requirement.</summary>
    public required string ExistingTitle { get; init; }

    /// <summary>Source document of the new requirement.</summary>
    public required string Source { get; init; }
}
