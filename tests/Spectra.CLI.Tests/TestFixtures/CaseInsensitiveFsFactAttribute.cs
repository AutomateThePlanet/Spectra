namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 102 F2: a fact that needs a case-insensitive file system (Windows, macOS), without git. Skipped on Linux, where
/// the comparer-seam rows in <c>CriticSourceLoaderReviewTests</c> cover the case-insensitive rule.
/// </summary>
public sealed class CaseInsensitiveFsFactAttribute : FactAttribute
{
    public CaseInsensitiveFsFactAttribute()
    {
        if (OperatingSystem.IsLinux()) Skip = "the file system is case-sensitive (Linux); covered by the comparer-seam rows";
    }
}
