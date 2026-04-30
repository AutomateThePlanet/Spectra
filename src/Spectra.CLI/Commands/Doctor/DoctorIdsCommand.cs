using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Doctor;

/// <summary>
/// Spec 040: <c>spectra doctor ids [--fix]</c> — audit and repair test ID
/// uniqueness, index/disk consistency, and the high-water-mark.
/// </summary>
public static class DoctorIdsCommand
{
    public static Command Create()
    {
        var cmd = new Command("ids", "Diagnose and (with --fix) repair test ID issues across the workspace");

        var fixOption = new Option<bool>(
            "--fix",
            "Resolve duplicates: keep oldest occurrence, renumber later occurrences at HWM+1, update depends_on references");

        cmd.AddOption(fixOption);

        cmd.SetHandler(async context =>
        {
            var fix = context.ParseResult.GetValueForOption(fixOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);

            var handler = new DoctorIdsHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(fix, noInteraction, context.GetCancellationToken());
        });

        return cmd;
    }
}
