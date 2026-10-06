using System.CommandLine;
using Spectra.CLI.Baseline;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 086 (FR-008–FR-011): <c>spectra ai relink-sources (--suite &lt;s&gt; | --all-suites) [--min-similarity N]
/// [--dry-run]</c>. Relinks refs whose doc file git history renamed: the ref path only (the anchor is kept), the suite
/// index, the baseline key (old fingerprint), and orphan status where every ref then resolves. Read-only on git;
/// model-free; exit 0 whatever it finds.
/// </summary>
public sealed class RelinkSourcesCommand : Command
{
    public RelinkSourcesCommand()
        : base("relink-sources", "Rewrite source_refs whose doc files were renamed in git history (path only; anchor kept)")
    {
        var suiteOption = new Option<string?>("--suite", "The suite to relink");
        var allSuitesOption = new Option<bool>("--all-suites", "Relink every suite");
        var minSimilarityOption = new Option<int>("--min-similarity", () => RenameResolver.DefaultMinSimilarity,
            "Rename similarity threshold in percent (50–100)");
        var dryRunOption = new Option<bool>("--dry-run", "Report old → new refs, but write nothing");
        AddOption(suiteOption);
        AddOption(allSuitesOption);
        AddOption(minSimilarityOption);
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var parse = context.ParseResult;
            var json = parse.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await RelinkSourcesHandler.RunAsync(
                Directory.GetCurrentDirectory(),
                parse.GetValueForOption(suiteOption),
                parse.GetValueForOption(allSuitesOption),
                parse.GetValueForOption(minSimilarityOption),
                parse.GetValueForOption(dryRunOption),
                json,
                context.GetCancellationToken());
        });
    }
}
