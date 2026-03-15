namespace Spectra.Core.Models.Profile;

/// <summary>
/// Specialized domains that affect test generation.
/// </summary>
public enum DomainType
{
    /// <summary>
    /// Credit card, transactions, PCI considerations.
    /// </summary>
    Payments,

    /// <summary>
    /// Login, MFA, session management.
    /// </summary>
    Authentication,

    /// <summary>
    /// Personal data handling, consent, deletion.
    /// </summary>
    PiiGdpr,

    /// <summary>
    /// HIPAA, PHI considerations.
    /// </summary>
    Healthcare,

    /// <summary>
    /// Audit trails, regulations.
    /// </summary>
    Financial,

    /// <summary>
    /// WCAG, screen reader considerations.
    /// </summary>
    Accessibility
}
