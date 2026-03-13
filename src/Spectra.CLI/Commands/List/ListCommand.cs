using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.List;

/// <summary>
/// Command to list all tests or tests in a specific suite.
/// </summary>
public sealed class ListCommand : Command
{
    public ListCommand() : base("list", "List all tests or tests in a specific suite")
    {
        var suiteOption = new Option<string?>(
            ["--suite", "-s"],
            "Filter to a specific suite");

        var allOption = new Option<bool>(
            ["--all", "-a"],
            "Show all test details (not just summary)");

        AddOption(suiteOption);
        AddOption(allOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var showAll = context.ParseResult.GetValueForOption(allOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var handler = new ListHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(
                suite,
                showAll,
                context.GetCancellationToken());
        });
    }
}
