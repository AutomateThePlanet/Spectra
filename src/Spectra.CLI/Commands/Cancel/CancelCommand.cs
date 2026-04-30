using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Cancel;

/// <summary>
/// Spec 040: <c>spectra cancel [--force]</c> — cancel an in-progress
/// SPECTRA operation in the workspace.
/// </summary>
public sealed class CancelCommand : Command
{
    public CancelCommand() : base("cancel", "Cancel an in-progress SPECTRA operation in the workspace")
    {
        var forceOption = new Option<bool>(
            "--force",
            "Skip the cooperative grace window; kill the running process immediately");

        AddOption(forceOption);

        this.SetHandler(async context =>
        {
            var force = context.ParseResult.GetValueForOption(forceOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new CancelHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(force, context.GetCancellationToken());
        });
    }
}
