using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 084 (FR-016–FR-023): <c>spectra ai update-triage (--suite &lt;s&gt; | --all-suites) [--no-auto-ack]
/// [--dry-run]</c>. Reports, per test, what changed in the doc sections it references and the criteria it links
/// since its baseline, ranked into buckets and grouped. Model-free and deterministic; exit 0 whatever it finds.
/// </summary>
public sealed class UpdateTriageCommand : Command
{
    public UpdateTriageCommand()
        : base("update-triage", "Report which tests' doc sections and criteria changed since their baseline")
    {
        var suiteOption = new Option<string?>("--suite", "The suite to triage");
        var allSuitesOption = new Option<bool>("--all-suites", "Triage every suite");
        var noAutoAckOption = new Option<bool>("--no-auto-ack", "Report COSMETIC changes instead of acknowledging them");
        var dryRunOption = new Option<bool>("--dry-run", "Report, but write nothing (no auto-ack, no detail file)");
        AddOption(suiteOption);
        AddOption(allSuitesOption);
        AddOption(noAutoAckOption);
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var parse = context.ParseResult;
            var json = parse.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await UpdateTriageHandler.RunAsync(
                Directory.GetCurrentDirectory(),
                parse.GetValueForOption(suiteOption),
                parse.GetValueForOption(allSuitesOption),
                parse.GetValueForOption(noAutoAckOption),
                parse.GetValueForOption(dryRunOption),
                json,
                context.GetCancellationToken());
        });
    }
}
