using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Delete;

/// <summary>
/// Spec 040: <c>spectra delete &lt;test-id&gt;...</c> — safely delete one
/// or more test cases with automation and dependency guards.
/// </summary>
public sealed class DeleteCommand : Command
{
    public DeleteCommand() : base("delete", "Delete test cases (with automation and dependency safety checks)")
    {
        var idsArgument = new Argument<string[]>(
            "test-ids",
            description: "One or more test IDs to delete (e.g. TC-142)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var suiteOption = new Option<string?>(
            "--suite",
            "Delete every test in the named suite (alias for `spectra suite delete <name>`)");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Preview the operation without modifying any files");

        var forceOption = new Option<bool>(
            "--force",
            "Skip the interactive confirmation; override the automation guard");

        var noAutomationCheckOption = new Option<bool>(
            "--no-automation-check",
            "Override the automation guard without forcing past confirmation");

        AddArgument(idsArgument);
        AddOption(suiteOption);
        AddOption(dryRunOption);
        AddOption(forceOption);
        AddOption(noAutomationCheckOption);

        this.SetHandler(async context =>
        {
            var ids = context.ParseResult.GetValueForArgument(idsArgument);
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var noAutomationCheck = context.ParseResult.GetValueForOption(noAutomationCheckOption);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new DeleteHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(
                ids ?? Array.Empty<string>(),
                suite,
                dryRun,
                force,
                noAutomationCheck,
                noInteraction,
                context.GetCancellationToken());
        });
    }
}
