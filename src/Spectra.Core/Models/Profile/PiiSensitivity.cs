namespace Spectra.Core.Models.Profile;

/// <summary>
/// Level of PII/GDPR handling in tests.
/// </summary>
public enum PiiSensitivity
{
    /// <summary>
    /// No PII considerations.
    /// </summary>
    None,

    /// <summary>
    /// Basic PII awareness.
    /// </summary>
    Standard,

    /// <summary>
    /// Full GDPR/privacy compliance focus.
    /// </summary>
    Strict
}
