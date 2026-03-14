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

        AddOption(outputOption);
        AddOption(formatOption);

        this.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var handler = new AnalyzeHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(
                output,
                format,
                context.GetCancellationToken());
        });
    }
}
