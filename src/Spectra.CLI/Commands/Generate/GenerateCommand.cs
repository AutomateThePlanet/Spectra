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
        var suiteArgument = new Argument<string>(
            "suite",
            "Target suite name for generated tests");

        var countOption = new Option<int?>(
            ["--count", "-n"],
            "Number of tests to generate (default: 5)");

        AddArgument(suiteArgument);
        AddOption(countOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForArgument(suiteArgument);
            var count = context.ParseResult.GetValueForOption(countOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);
            var noReview = context.ParseResult.GetValueForOption(GlobalOptions.NoReviewOption);

            var handler = new GenerateHandler(verbosity, dryRun, noReview);
            context.ExitCode = await handler.ExecuteAsync(suite, count, context.GetCancellationToken());
        });
    }
}
