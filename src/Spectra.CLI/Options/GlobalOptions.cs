using System.CommandLine;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Options;

/// <summary>
/// Global options available on all commands.
/// </summary>
public static class GlobalOptions
{
    /// <summary>
    /// Output verbosity level.
    /// </summary>
    public static readonly Option<VerbosityLevel> VerbosityOption = new(
        aliases: ["--verbosity", "-v"],
        getDefaultValue: () => VerbosityLevel.Normal,
        description: "Output verbosity: quiet, minimal, normal, detailed, diagnostic");

    /// <summary>
    /// Dry run mode - preview changes without executing.
    /// </summary>
    public static readonly Option<bool> DryRunOption = new(
        name: "--dry-run",
        getDefaultValue: () => false,
        description: "Preview changes without executing");

    /// <summary>
    /// Skip interactive review (CI mode).
    /// </summary>
    public static readonly Option<bool> NoReviewOption = new(
        name: "--no-review",
        getDefaultValue: () => false,
        description: "Skip interactive review (CI mode)");

    /// <summary>
    /// Adds global options to a root command.
    /// </summary>
    public static void AddTo(RootCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.AddGlobalOption(VerbosityOption);
        command.AddGlobalOption(DryRunOption);
        command.AddGlobalOption(NoReviewOption);
    }
}
