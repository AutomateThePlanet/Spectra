namespace Spectra.Core.Git;

/// <summary>
/// Spec 087 (F4): how repository paths compare on this platform — exactly on Linux, ignoring case on Windows and macOS
/// (the file systems SPECTRA already treats as case-insensitive elsewhere).
/// </summary>
public static class GitPathComparer
{
    public static StringComparer Platform { get; } = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}
