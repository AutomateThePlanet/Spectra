using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Analyze;

/// <summary>
/// Command to analyze test coverage.
/// </summary>
public sealed class AnalyzeCommand : Command
{
    public AnalyzeCommand() : base("analyze", "Analyze test coverage across documentation")
    {
        var outputOption = new Option<string?>(
            ["--output", "-o"],
            "Output file path for the report");

        var formatOption = new Option<ReportFormat>(
            ["--format", "-f"],
            () => ReportFormat.Text,
            "Report format (text, json, markdown)");

        var coverageOption = new Option<bool>(
            ["--coverage", "-c"],
            "Analyze automation coverage (test-to-automation linking)");

        var autoLinkOption = new Option<bool>(
            ["--auto-link"],
            "Scan automation code and update test automated_by fields");

        var extractRequirementsOption = new Option<bool>(
            ["--extract-requirements"],
            "Extract testable requirements from documentation")
        { IsHidden = true };

        var extractCriteriaOption = new Option<bool>(
            ["--extract-criteria"],
            "Extract acceptance criteria from documentation");

        var forceOption = new Option<bool>(
            ["--force"],
            "Force re-extraction even if document hash is unchanged");

        var importCriteriaOption = new Option<string?>(
            ["--import-criteria"],
            "Path to a criteria file to import (YAML, CSV, or JSON)");

        var mergeOption = new Option<bool>(
            ["--merge"],
            "Merge imported criteria with existing (default behavior)");

        var replaceOption = new Option<bool>(
            ["--replace"],
            "Replace existing criteria instead of merging");

        var skipSplittingOption = new Option<bool>(
            ["--skip-splitting"],
            "Skip AI-powered splitting of compound criteria");

        var listCriteriaOption = new Option<bool>(
            ["--list-criteria"],
            "List all acceptance criteria with coverage status");

        var sourceTypeOption = new Option<string?>(
            ["--source-type"],
            "Filter criteria by source type (e.g. document, import)");

        var componentOption = new Option<string?>(
            ["--component"],
            "Filter criteria by component");

        var priorityOption = new Option<string?>(
            ["--priority"],
            "Filter criteria by priority (e.g. high, medium, low)");

        var includeArchivedOption = new Option<bool>(
            ["--include-archived"],
            "Include suites flagged skip_analysis: true (Old/, legacy/, archive/, release-notes/) when extracting criteria");

        AddOption(outputOption);
        AddOption(formatOption);
        AddOption(coverageOption);
        AddOption(autoLinkOption);
        AddOption(extractRequirementsOption);
        AddOption(extractCriteriaOption);
        AddOption(forceOption);
        AddOption(importCriteriaOption);
        AddOption(mergeOption);
        AddOption(replaceOption);
        AddOption(skipSplittingOption);
        AddOption(listCriteriaOption);
        AddOption(sourceTypeOption);
        AddOption(componentOption);
        AddOption(priorityOption);
        AddOption(includeArchivedOption);

        this.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption);
            var coverage = context.ParseResult.GetValueForOption(coverageOption);
            var autoLink = context.ParseResult.GetValueForOption(autoLinkOption);
            var extractReqs = context.ParseResult.GetValueForOption(extractRequirementsOption);
            var extractCriteria = context.ParseResult.GetValueForOption(extractCriteriaOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var importCriteria = context.ParseResult.GetValueForOption(importCriteriaOption);
            var replace = context.ParseResult.GetValueForOption(replaceOption);
            var skipSplitting = context.ParseResult.GetValueForOption(skipSplittingOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var listCriteria = context.ParseResult.GetValueForOption(listCriteriaOption);
            var sourceType = context.ParseResult.GetValueForOption(sourceTypeOption);
            var component = context.ParseResult.GetValueForOption(componentOption);
            var priority = context.ParseResult.GetValueForOption(priorityOption);
            var includeArchived = context.ParseResult.GetValueForOption(includeArchivedOption);

            var handler = new AnalyzeHandler(verbosity, outputFormat, includeArchived);

            if (listCriteria)
            {
                context.ExitCode = await handler.RunListCriteriaAsync(
                    sourceType,
                    component,
                    priority,
                    context.GetCancellationToken());
            }
            else if (importCriteria is not null)
            {
                context.ExitCode = await handler.RunImportCriteriaAsync(
                    importCriteria,
                    replace,
                    skipSplitting,
                    dryRun,
                    context.GetCancellationToken());
            }
            else if (extractReqs || extractCriteria)
            {
                context.ExitCode = await handler.RunExtractCriteriaAsync(
                    dryRun,
                    force,
                    context.GetCancellationToken());
            }
            else
            {
                context.ExitCode = await handler.ExecuteAsync(
                    output,
                    format,
                    coverage,
                    autoLink,
                    context.GetCancellationToken());
            }
        });
    }
}
