using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 084 (FR-026): <c>spectra ai source-baseline --init (--suite &lt;s&gt; | --all-suites) [--force] [--dry-run]</c>;
/// Spec 086: <c>[--from-date &lt;ISO date&gt; | --from-ref &lt;rev&gt; --owner &lt;path&gt;]</c>.
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
        var fromDateOption = new Option<string?>("--from-date",
            "Fill missing entries from each repository's last commit at or before this ISO date (follows renames back)");
        var fromRefOption = new Option<string?>("--from-ref", "Fill missing entries from this commit of the --owner repository");
        var ownerOption = new Option<string?>("--owner", "The repository (a path inside it) that --from-ref names a commit in");
        var minSimilarityOption = new Option<int?>("--min-similarity",
            "With --from-*: rename similarity threshold in percent for following renames back (50–100, default 90)");
        AddOption(initOption);
        AddOption(fromDateOption);
        AddOption(fromRefOption);
        AddOption(ownerOption);
        AddOption(minSimilarityOption);
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
                parse.GetValueForOption(fromDateOption),
                parse.GetValueForOption(fromRefOption),
                parse.GetValueForOption(ownerOption),
                parse.GetValueForOption(minSimilarityOption),
                json,
                context.GetCancellationToken());
        });
    }
}
