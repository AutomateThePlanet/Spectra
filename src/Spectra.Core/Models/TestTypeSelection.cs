namespace Spectra.Core.Models;

/// <summary>
/// Type of tests to generate in interactive mode.
/// </summary>
public enum TestTypeSelection
{
    /// <summary>
    /// Happy path + negative + boundary tests.
    /// </summary>
    FullCoverage,

    /// <summary>
    /// Error and failure scenarios only.
    /// </summary>
    NegativeOnly,

    /// <summary>
    /// User will describe a specific area.
    /// </summary>
    SpecificArea,

    /// <summary>
    /// Open-ended description.
    /// </summary>
    FreeDescription
}
