using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Show;

/// <summary>
/// Command to show details of a specific test.
/// </summary>
public sealed class ShowCommand : Command
{
    public ShowCommand() : base("show", "Show details of a specific test")
    {
        var testIdArgument = new Argument<string>(
            "test-id",
            "The ID of the test to show (e.g., TC001)");

        var rawOption = new Option<bool>(
            ["--raw", "-r"],
            "Show raw markdown content instead of formatted output");

        AddArgument(testIdArgument);
        AddOption(rawOption);

        this.SetHandler(async (context) =>
        {
            var testId = context.ParseResult.GetValueForArgument(testIdArgument);
            var showRaw = context.ParseResult.GetValueForOption(rawOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var handler = new ShowHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(
                testId,
                showRaw,
                context.GetCancellationToken());
        });
    }
}
