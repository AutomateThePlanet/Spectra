using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 084 (FR-026): <c>spectra ai source-baseline --init (--suite &lt;s&gt; | --all-suites) [--force] [--dry-run]</c>.
/// Adopts current content as the baseline for tests that have none (or completes partial entries); never changes an
/// existing fingerprint without <c>--force</c>. Never touches test files. Model-free.
/// </summary>
public sealed class SourceBaselineCommand : Command
{
    public SourceBaselineCommand()
        : base("source-baseline", "Create source-change baselines for tests from current content (--init)")
    {
        var initOption = new Option<bool>("--init", "Create entries for tests without one (required)");
        var suiteOption = new Option<string?>("--suite", "The suite to baseline");
        var allSuitesOption = new Option<bool>("--all-suites", "Baseline every suite");
        var forceOption = new Option<bool>("--force", "Recompute every entry in scope from current content");
        var dryRunOption = new Option<bool>("--dry-run", "Report what would be written, but write nothing");
        AddOption(initOption);
        AddOption(suiteOption);
        AddOption(allSuitesOption);
        AddOption(forceOption);
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var parse = context.ParseResult;
            var json = parse.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await SourceBaselineHandler.RunAsync(
                Directory.GetCurrentDirectory(),
                parse.GetValueForOption(initOption),
                parse.GetValueForOption(suiteOption),
                parse.GetValueForOption(allSuitesOption),
                parse.GetValueForOption(forceOption),
                parse.GetValueForOption(dryRunOption),
                json,
                context.GetCancellationToken());
        });
    }
}
