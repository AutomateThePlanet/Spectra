namespace Spectra.Core.Models.Profile;

/// <summary>
/// Domain-specific generation preferences.
/// </summary>
public sealed class DomainOptions
{
    /// <summary>
    /// Gets or sets active domains requiring special handling.
    /// </summary>
    public IReadOnlyList<DomainType> Domains { get; init; } = [];

    /// <summary>
    /// Gets or sets the level of PII/GDPR consideration.
    /// </summary>
    public PiiSensitivity PiiSensitivity { get; init; } = PiiSensitivity.None;

    /// <summary>
    /// Gets or sets whether to add compliance reminders to relevant tests.
    /// </summary>
    public bool IncludeComplianceNotes { get; init; }
}
