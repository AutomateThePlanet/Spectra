using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Subcommand to build or refresh the documentation index.
/// </summary>
public sealed class DocsIndexCommand : Command
{
    public DocsIndexCommand()
        : base("index", "Build or refresh the documentation index (docs/_index/_manifest.yaml + per-suite files)")
    {
        var forceOption = new Option<bool>(
            ["--force", "-f"],
            "Force a full rebuild instead of incremental update");

        var skipCriteriaOption = new Option<bool>(
            ["--skip-criteria"],
            "Skip automatic acceptance criteria extraction after indexing");

        var noMigrateOption = new Option<bool>(
            ["--no-migrate"],
            "Refuse to migrate a legacy docs/_index.md (errors out if one is present)");

        var includeArchivedOption = new Option<bool>(
            ["--include-archived"],
            "Include suites flagged skip_analysis: true in any auto-extracted criteria step");

        var suitesOption = new Option<string?>(
            ["--suites"],
            "Comma-separated list of suite IDs to re-index (other suites' files left untouched)");

        AddOption(forceOption);
        AddOption(skipCriteriaOption);
        AddOption(noMigrateOption);
        AddOption(includeArchivedOption);
        AddOption(suitesOption);

        this.SetHandler(async (context) =>
        {
            var force = context.ParseResult.GetValueForOption(forceOption);
            var skipCriteria = context.ParseResult.GetValueForOption(skipCriteriaOption);
            var noMigrate = context.ParseResult.GetValueForOption(noMigrateOption);
            var includeArchived = context.ParseResult.GetValueForOption(includeArchivedOption);
            var suitesArg = context.ParseResult.GetValueForOption(suitesOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            IReadOnlyList<string>? suiteFilter = null;
            if (!string.IsNullOrWhiteSpace(suitesArg))
            {
                suiteFilter = suitesArg
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            var handler = new DocsIndexHandler(
                verbosity,
                dryRun,
                outputFormat,
                noInteraction,
                skipCriteria,
                noMigrate,
                includeArchived,
                suiteFilter);
            context.ExitCode = await handler.ExecuteAsync(force, context.GetCancellationToken());
        });
    }
}
