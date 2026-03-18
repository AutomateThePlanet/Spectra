namespace Spectra.Core.Models;

/// <summary>
/// State of an interactive session flow.
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Showing suite picker.
    /// </summary>
    SuiteSelection,

    /// <summary>
    /// Showing test type options.
    /// </summary>
    TestTypeSelection,

    /// <summary>
    /// Accepting focus description.
    /// </summary>
    FocusInput,

    /// <summary>
    /// Analyzing coverage gaps.
    /// </summary>
    GapAnalysis,

    /// <summary>
    /// AI generating tests.
    /// </summary>
    Generating,

    /// <summary>
    /// Showing generation results.
    /// </summary>
    Results,

    /// <summary>
    /// Offering to generate for remaining gaps.
    /// </summary>
    GapSelection,

    /// <summary>
    /// Session finished.
    /// </summary>
    Complete
}
