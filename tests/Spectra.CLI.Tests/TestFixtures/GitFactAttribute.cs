using System.Diagnostics;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>Spec 086: whether a real <c>git</c> can be run (checked once).</summary>
public static class GitAvailability
{
    public const string SkipReason = "git is not installed; this test needs a real git repository";

    private static readonly Lazy<bool> Available = new(() =>
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            });
            if (process is null) return false;
            process.StandardOutput.ReadToEnd();
            process.WaitForExit(30_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    });

    public static bool IsAvailable => Available.Value;
}

/// <summary>A fact that needs a real git; skipped with a clear reason when git is absent (xunit 2 has no dynamic skip).</summary>
public sealed class GitFactAttribute : FactAttribute
{
    public GitFactAttribute()
    {
        if (!GitAvailability.IsAvailable) Skip = GitAvailability.SkipReason;
    }
}

/// <summary>A theory that needs a real git; skipped with a clear reason when git is absent.</summary>
public sealed class GitTheoryAttribute : TheoryAttribute
{
    public GitTheoryAttribute()
    {
        if (!GitAvailability.IsAvailable) Skip = GitAvailability.SkipReason;
    }
}
