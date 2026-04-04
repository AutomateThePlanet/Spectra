using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Index;

/// <summary>
/// Command to generate or update test metadata indexes.
/// </summary>
public sealed class IndexCommand : Command
{
    public IndexCommand() : base("index", "Generate or update test metadata indexes")
    {
        var suiteOption = new Option<string?>(
            ["--suite", "-s"],
            "Index only the specified suite");

        var rebuildOption = new Option<bool>(
            ["--rebuild", "-r"],
            () => false,
            "Force rebuild all indexes");

        AddOption(suiteOption);
        AddOption(rebuildOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var rebuild = context.ParseResult.GetValueForOption(rebuildOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new IndexHandler(verbosity, dryRun, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(suite, rebuild, context.GetCancellationToken());
        });
    }
}
