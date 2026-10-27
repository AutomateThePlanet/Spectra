using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 084 (FR-024, FR-025): <c>spectra ai update-ack --suite &lt;s&gt; (--ids a,b | --group &lt;gid&gt;)
/// [--dry-run]</c> — "reviewed, no edit needed": refreshes those tests' baseline entries to current content. Never
/// touches a test file. Refuses tests whose ref or criterion is REMOVED.
/// </summary>
public sealed class UpdateAckCommand : Command
{
    public UpdateAckCommand()
        : base("update-ack", "Acknowledge reviewed tests: refresh their source-change baseline (no test edit)")
    {
        var suiteOption = new Option<string?>("--suite", "The suite of the tests");
        var idsOption = new Option<string?>("--ids", "Comma-separated test IDs");
        var groupOption = new Option<string?>("--group", "A group ID from the last update-triage (e.g. G2)");
        var dryRunOption = new Option<bool>("--dry-run", "Report what would be written, but write nothing");
        AddOption(suiteOption);
        AddOption(idsOption);
        AddOption(groupOption);
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var parse = context.ParseResult;
            var json = parse.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await UpdateAckHandler.RunAsync(
                Directory.GetCurrentDirectory(),
                parse.GetValueForOption(suiteOption),
                parse.GetValueForOption(idsOption),
                parse.GetValueForOption(groupOption),
                parse.GetValueForOption(dryRunOption),
                json,
                context.GetCancellationToken());
        });
    }
}
