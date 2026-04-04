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
            "Extract testable requirements from documentation");

        AddOption(outputOption);
        AddOption(formatOption);
        AddOption(coverageOption);
        AddOption(autoLinkOption);
        AddOption(extractRequirementsOption);

        this.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption);
            var coverage = context.ParseResult.GetValueForOption(coverageOption);
            var autoLink = context.ParseResult.GetValueForOption(autoLinkOption);
            var extractReqs = context.ParseResult.GetValueForOption(extractRequirementsOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new AnalyzeHandler(verbosity, outputFormat);

            if (extractReqs)
            {
                context.ExitCode = await handler.RunExtractRequirementsAsync(
                    dryRun,
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
