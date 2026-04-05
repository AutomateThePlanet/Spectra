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

        var suiteOption = new Option<string?>(
            ["--suite", "-s"],
            "Target suite name for generated tests (alternative to positional argument)");

        var countOption = new Option<int?>(
            ["--count", "-n"],
            "Number of tests to generate (default: 5)");

        var focusOption = new Option<string?>(
            ["--focus", "-f"],
            "Focus area description for test generation");

        var skipCriticOption = new Option<bool>(
            "--skip-critic",
            "Skip grounding verification (faster, but no verification metadata)");

        var fromSuggestionsOption = new Option<string?>(
            "--from-suggestions",
            "Generate from previous session suggestions (optionally pass indices like 1,3)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var fromDescriptionOption = new Option<string?>(
            "--from-description",
            "Create a test from a plain-language behavior description");

        var contextOption = new Option<string?>(
            "--context",
            "Additional context for --from-description");

        var autoCompleteOption = new Option<bool>(
            "--auto-complete",
            "Run all phases without prompts (analyze, generate, suggestions, finalize)");

        var analyzeOnlyOption = new Option<bool>(
            "--analyze-only",
            "Only analyze documentation and output recommended test count (no generation)");

        AddArgument(suiteArgument);
        AddOption(suiteOption);
        AddOption(countOption);
        AddOption(focusOption);
        AddOption(skipCriticOption);
        AddOption(fromSuggestionsOption);
        AddOption(fromDescriptionOption);
        AddOption(contextOption);
        AddOption(autoCompleteOption);
        AddOption(analyzeOnlyOption);

        this.SetHandler(async (context) =>
        {
            // --suite option takes priority over positional argument
            var suite = context.ParseResult.GetValueForOption(suiteOption)
                        ?? context.ParseResult.GetValueForArgument(suiteArgument);
            var count = context.ParseResult.GetValueForOption(countOption);
            var focus = context.ParseResult.GetValueForOption(focusOption);
            var skipCritic = context.ParseResult.GetValueForOption(skipCriticOption);
            var fromSuggestions = context.ParseResult.GetValueForOption(fromSuggestionsOption);
            var fromDescription = context.ParseResult.GetValueForOption(fromDescriptionOption);
            var descContext = context.ParseResult.GetValueForOption(contextOption);
            var autoComplete = context.ParseResult.GetValueForOption(autoCompleteOption);
            var analyzeOnly = context.ParseResult.GetValueForOption(analyzeOnlyOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);
            var noReview = context.ParseResult.GetValueForOption(GlobalOptions.NoReviewOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);

            var handler = new GenerateHandler(verbosity, dryRun, noReview, noInteraction, skipCritic, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(
                suite, count, focus,
                fromSuggestions, fromDescription, descContext, autoComplete, analyzeOnly,
                context.GetCancellationToken());
        });
    }
}
