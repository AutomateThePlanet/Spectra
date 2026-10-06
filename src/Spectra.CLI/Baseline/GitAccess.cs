using Spectra.Core.Git;

namespace Spectra.CLI.Baseline;

/// <summary>
/// Spec 086 (analysis U2): builds every <see cref="GitReader"/> the CLI uses. The test hook lives here, not in Core:
/// Core's reader takes its runner through its constructor.
/// </summary>
public static class GitAccess
{
    /// <summary>Tests only: replaces the git process runner (a recording or faking runner).</summary>
    internal static IGitProcessRunner? RunnerOverride { get; set; }

    public static GitReader CreateReader() => new(RunnerOverride ?? new ProcessGitRunner());

    public static OwnerResolver CreateOwnerResolver() => new(CreateReader());
}
