namespace Spectra.Core.Models.Profile;

/// <summary>
/// Default priority for generated test cases.
/// </summary>
public enum Priority
{
    /// <summary>
    /// P1 - Critical functionality.
    /// </summary>
    High,

    /// <summary>
    /// P2 - Standard functionality (default).
    /// </summary>
    Medium,

    /// <summary>
    /// P3 - Nice-to-have coverage.
    /// </summary>
    Low
}
