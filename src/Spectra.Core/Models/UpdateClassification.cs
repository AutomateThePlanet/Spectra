namespace Spectra.Core.Models;

/// <summary>
/// Classification of a test's update status relative to source documentation.
/// </summary>
public enum UpdateClassification
{
    /// <summary>
    /// Test is current and matches documentation.
    /// </summary>
    UpToDate,

    /// <summary>
    /// Test exists but documentation has changed.
    /// </summary>
    Outdated,

    /// <summary>
    /// Test references documentation that no longer exists.
    /// </summary>
    Orphaned,

    /// <summary>
    /// Test duplicates another test's coverage.
    /// </summary>
    Redundant,

    /// <summary>
    /// Test status could not be determined.
    /// </summary>
    Unknown
}
