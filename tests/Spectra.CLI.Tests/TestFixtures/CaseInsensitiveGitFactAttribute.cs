namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 087: a fact that needs a real git and a case-insensitive file system (Windows, macOS). Skipped on Linux, where
/// the comparer-seam tests in Spectra.Core.Tests cover the case-insensitive rule.
/// </summary>
public sealed class CaseInsensitiveGitFactAttribute : FactAttribute
{
    public CaseInsensitiveGitFactAttribute()
    {
        if (!GitAvailability.IsAvailable) Skip = GitAvailability.SkipReason;
        else if (OperatingSystem.IsLinux()) Skip = "the file system is case-sensitive (Linux); covered by the comparer-seam tests";
    }
}
