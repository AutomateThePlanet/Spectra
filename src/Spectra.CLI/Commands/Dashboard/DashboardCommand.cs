using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Dashboard;

/// <summary>
/// Command to generate a static dashboard from test data.
/// </summary>
public sealed class DashboardCommand : Command
{
    public DashboardCommand() : base("dashboard", "Generate a static dashboard from test indexes and execution reports")
    {
        var outputOption = new Option<string>(
            ["--output", "-o"],
            () => "./site",
            "Output directory for the generated dashboard");

        var titleOption = new Option<string?>(
            ["--title", "-t"],
            "Dashboard title (default: repository name)");

        var templateOption = new Option<string?>(
            ["--template"],
            "Path to custom dashboard template directory");

        var previewOption = new Option<bool>(
            ["--preview"],
            "Generate dashboard with sample data for branding verification");

        AddOption(outputOption);
        AddOption(titleOption);
        AddOption(templateOption);
        AddOption(previewOption);

        this.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var title = context.ParseResult.GetValueForOption(titleOption);
            var template = context.ParseResult.GetValueForOption(templateOption);
            var preview = context.ParseResult.GetValueForOption(previewOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            var handler = new DashboardHandler(verbosity, dryRun);
            context.ExitCode = await handler.ExecuteAsync(output, title, template, preview, context.GetCancellationToken());
        });
    }
}
