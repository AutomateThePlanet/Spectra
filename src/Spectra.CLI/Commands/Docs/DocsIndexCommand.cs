using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Subcommand to build or refresh the documentation index.
/// </summary>
public sealed class DocsIndexCommand : Command
{
    public DocsIndexCommand() : base("index", "Build or refresh the documentation index (docs/_index.md)")
    {
        var forceOption = new Option<bool>(
            ["--force", "-f"],
            "Force a full rebuild instead of incremental update");

        AddOption(forceOption);

        this.SetHandler(async (context) =>
        {
            var force = context.ParseResult.GetValueForOption(forceOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            var handler = new DocsIndexHandler(verbosity, dryRun);
            context.ExitCode = await handler.ExecuteAsync(force, context.GetCancellationToken());
        });
    }
}
