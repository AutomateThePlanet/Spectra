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

        AddOption(outputOption);
        AddOption(formatOption);
        AddOption(coverageOption);
        AddOption(autoLinkOption);

        this.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption);
            var coverage = context.ParseResult.GetValueForOption(coverageOption);
            var autoLink = context.ParseResult.GetValueForOption(autoLinkOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var handler = new AnalyzeHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(
                output,
                format,
                coverage,
                autoLink,
                context.GetCancellationToken());
        });
    }
}
