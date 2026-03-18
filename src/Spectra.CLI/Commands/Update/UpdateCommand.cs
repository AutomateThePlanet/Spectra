using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Update;

/// <summary>
/// Command to update test cases based on documentation changes.
/// </summary>
public sealed class UpdateCommand : Command
{
    public UpdateCommand() : base("update", "Update test cases based on documentation changes")
    {
        var suiteArgument = new Argument<string?>(
            "suite",
            () => null,
            "Target suite name to update (omit for interactive mode)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var diffOption = new Option<bool>(
            ["--diff", "-d"],
            "Show diff of proposed changes without applying");

        var deleteOrphanedOption = new Option<bool>(
            "--delete-orphaned",
            "Automatically delete orphaned tests");

        var noInteractionOption = new Option<bool>(
            "--no-interaction",
            "Disable interactive prompts (requires --suite)");

        AddArgument(suiteArgument);
        AddOption(diffOption);
        AddOption(deleteOrphanedOption);
        AddOption(noInteractionOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForArgument(suiteArgument);
            var showDiff = context.ParseResult.GetValueForOption(diffOption);
            var deleteOrphaned = context.ParseResult.GetValueForOption(deleteOrphanedOption);
            var noInteraction = context.ParseResult.GetValueForOption(noInteractionOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);
            var noReview = context.ParseResult.GetValueForOption(GlobalOptions.NoReviewOption);

            var handler = new UpdateHandler(verbosity, dryRun, noReview, noInteraction);
            context.ExitCode = await handler.ExecuteAsync(
                suite,
                showDiff,
                deleteOrphaned,
                context.GetCancellationToken());
        });
    }
}
