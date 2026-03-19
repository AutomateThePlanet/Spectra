using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Command to generate test cases from documentation.
/// </summary>
public sealed class GenerateCommand : Command
{
    public GenerateCommand() : base("generate", "Generate test cases from documentation using AI")
    {
        // Suite argument is now optional - when not provided, enter interactive mode
        var suiteArgument = new Argument<string?>(
            "suite",
            () => null,
            "Target suite name for generated tests (optional - omit for interactive mode)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var countOption = new Option<int?>(
            ["--count", "-n"],
            "Number of tests to generate (default: 5)");

        var focusOption = new Option<string?>(
            ["--focus", "-f"],
            "Focus area description for test generation");

        var noInteractionOption = new Option<bool>(
            "--no-interaction",
            "Disable interactive prompts (for CI/automation)");

        var skipCriticOption = new Option<bool>(
            "--skip-critic",
            "Skip grounding verification (faster, but no verification metadata)");

        AddArgument(suiteArgument);
        AddOption(countOption);
        AddOption(focusOption);
        AddOption(noInteractionOption);
        AddOption(skipCriticOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForArgument(suiteArgument);
            var count = context.ParseResult.GetValueForOption(countOption);
            var focus = context.ParseResult.GetValueForOption(focusOption);
            var noInteraction = context.ParseResult.GetValueForOption(noInteractionOption);
            var skipCritic = context.ParseResult.GetValueForOption(skipCriticOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);
            var noReview = context.ParseResult.GetValueForOption(GlobalOptions.NoReviewOption);

            var handler = new GenerateHandler(verbosity, dryRun, noReview, noInteraction, skipCritic);
            context.ExitCode = await handler.ExecuteAsync(suite, count, focus, context.GetCancellationToken());
        });
    }
}
