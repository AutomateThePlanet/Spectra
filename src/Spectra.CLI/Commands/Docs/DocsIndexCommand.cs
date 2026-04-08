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

        var skipCriteriaOption = new Option<bool>(
            ["--skip-criteria"],
            "Skip automatic acceptance criteria extraction after indexing");

        AddOption(forceOption);
        AddOption(skipCriteriaOption);

        this.SetHandler(async (context) =>
        {
            var force = context.ParseResult.GetValueForOption(forceOption);
            var skipCriteria = context.ParseResult.GetValueForOption(skipCriteriaOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new DocsIndexHandler(verbosity, dryRun, outputFormat, noInteraction, skipCriteria);
            context.ExitCode = await handler.ExecuteAsync(force, context.GetCancellationToken());
        });
    }
}
