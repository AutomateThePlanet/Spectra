using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 069 (FR-005): <c>spectra docs changed</c> — list documents whose content changed since the
/// last criteria extraction (model-free SHA-256 compare against <c>_criteria_index.yaml</c>). Drives
/// the skill's incremental-skip so unchanged documents never reach a model turn.
/// </summary>
public sealed class DocsChangedCommand : Command
{
    public DocsChangedCommand()
        : base("changed", "List documents changed since the last criteria extraction (deterministic, no model call)")
    {
        var includeUnchanged = new Option<bool>(
            ["--include-unchanged"],
            "Also list documents whose criteria are up to date");

        var includeArchived = new Option<bool>(
            ["--include-archived"],
            "Include suites flagged skip_analysis: true");

        AddOption(includeUnchanged);
        AddOption(includeArchived);

        this.SetHandler(async (context) =>
        {
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var handler = new DocsChangedHandler(
                outputFormat,
                context.ParseResult.GetValueForOption(includeUnchanged),
                context.ParseResult.GetValueForOption(includeArchived));
            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });
    }
}
