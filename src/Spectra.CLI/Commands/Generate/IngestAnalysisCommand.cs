using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Baseline;
using Spectra.CLI.Criteria;
using Spectra.CLI.Generation;
using Spectra.CLI.Generation.Plan;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Config;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 059: <c>spectra ai ingest-analysis</c>. The fail-loud, model-free boundary for the
/// analyze-first step. Reads the agent's behavior JSON (from <c>--from</c> or stdin), runs the
/// deterministic accounting relocated from <c>BehaviorAnalyzer</c>, and emits the recommendation
/// the generation skill presents for approval. Spec 094: on success it also saves the suite's generation plan
/// (<c>.spectra/generation-plan/&lt;suite&gt;.json</c>) — the behaviours' sections and the batches.
///
/// Mirrors <see cref="IngestVerdictCommand"/> / <see cref="IngestTestsCommand"/>. Damage (empty /
/// unparseable) fails loud with a specific exit code, never a silent zero-recommendation.
/// </summary>
public sealed class IngestAnalysisCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitEmpty = 5;        // EmptyResponse
    private const int ExitParseInvalid = 6; // ParseFailure

    private const string CommandName = "ingest-analysis";

    /// <summary>Spec 094: the plan file could not be written (exit 1).</summary>
    public const string ErrorPlanWriteFailed = "PLAN_WRITE_FAILED";

    public IngestAnalysisCommand()
        : base("ingest-analysis", "Classify agent behavior analysis into a recommendation (fail-loud, no model call)")
    {
        var suiteOption = new Option<string?>(["--suite", "-s"], "Suite the analysis targets (for coverage dedup)");
        var fromOption = new Option<string?>("--from", "File containing the agent's JSON output (omit to read stdin)");
        var focusOption = new Option<string?>(["--focus", "-f"], "Focus area filter applied to the behaviors");

        AddOption(suiteOption);
        AddOption(fromOption);
        AddOption(focusOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var focus = context.ParseResult.GetValueForOption(focusOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(
                suite, from, focus, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string? suite, string? from, string? focus, bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suite))
        {
            Console.Error.WriteLine("A target suite is required (--suite).");
            return ExitError;
        }

        var currentDir = Directory.GetCurrentDirectory();

        // Spec 094 (083 rule): the ingest now writes the generation plan, so it loads the config as a write command
        // reading `source` (a missing config is CONFIG_MISSING, an invalid one — e.g. generation.batch_size — CONFIG_INVALID).
        var loaded = await ConfigLoader.LoadForWriteAsync(currentDir, ConfigSections.Source, ct);
        if (loaded.Error is not null)
        {
            CriteriaCommandIO.WriteFailure(json, CommandName, loaded.ErrorCode, loaded.Error);
            return ExitError;
        }

        var config = loaded.Config!;
        BaselineCommandIO.PrintWarnings(loaded.Warnings);

        // Read agent content from file or stdin.
        string content;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!File.Exists(from))
            {
                Console.Error.WriteLine($"Content file not found: {from}");
                return ExitError;
            }
            content = await File.ReadAllTextAsync(from, ct);
        }
        else
        {
            content = await Console.In.ReadToEndAsync(ct);
        }

        // Load existing tests + coverage snapshot (same as the compile side).
        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);
        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        CoverageSnapshot? snapshot = null;
        if (existingTests.Count > 0)
        {
            var criteriaDir = Path.Combine(currentDir, config.Coverage?.CriteriaDir ?? "docs/criteria");
            var criteriaIndexFile = Path.Combine(criteriaDir, "_criteria_index.yaml");
            var docIndexFile = Path.Combine(currentDir, "docs", "_index.md");
            var snapshotBuilder = new CoverageSnapshotBuilder(currentDir);
            snapshot = await snapshotBuilder.BuildAsync(
                suite, testsPath, criteriaDir, criteriaIndexFile, docIndexFile, ct);
        }

        var result = AnalysisRecommendationBuilder.Build(content, existingTests, snapshot, focus);

        if (result.IsSuccess)
        {
            // Spec 094 (FR-002–FR-005): resolve every behaviour's source and save the suite's generation plan.
            var plan = await BuildPlanAsync(currentDir, config, suite, focus, result, ct);
            try
            {
                await GenerationPlanStore.SaveAsync(currentDir, plan, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CriteriaCommandIO.WriteFailure(json, CommandName, ErrorPlanWriteFailed,
                    $"Could not write {GenerationPlanStore.RelativePathFor(suite)}: {ex.Message}");
                return ExitError;
            }

            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["outcome"] = result.Outcome.ToString(),
                    ["already_covered"] = result.AlreadyCovered,
                    ["recommended"] = result.RecommendedCount,
                    ["breakdown"] = result.Breakdown,
                    ["technique_breakdown"] = result.TechniqueBreakdown,
                    ["documents_analyzed"] = result.DocumentsAnalyzed,
                    ["boundary_gaps"] = result.BoundaryGaps.Select(g => new Dictionary<string, object?>
                    {
                        ["field"] = g.Field,
                        ["kind"] = g.Kind,
                        ["description"] = g.Description,
                        ["source"] = g.Source
                    }),
                    ["plan"] = GenerationPlanStore.RelativePathFor(suite),
                    ["planned_total"] = plan.PlannedTotal,
                    ["count_rule"] = plan.CountRule,
                    ["batches"] = plan.Batches.Select(b => new Dictionary<string, object?>
                    {
                        ["id"] = b.BatchId,
                        ["count"] = b.Count,
                        ["source_refs"] = b.SourceRefs,
                        ["behaviors"] = b.BehaviorIds.Select(id => plan.Behavior(id).Title)
                    }),
                    ["unresolved"] = plan.Behaviors.Where(b => !b.Resolved).Select(b => new Dictionary<string, object?>
                    {
                        ["id"] = b.Id,
                        ["title"] = b.Title,
                        ["source"] = b.Source,
                        ["reason"] = b.Reason
                    }),
                    ["notes"] = plan.Notes
                }, CriteriaCommandIO.Json));
            }

            // The human summary: stdout in human mode, stderr in JSON mode (never suppressed).
            WriteSummary(json ? Console.Error : Console.Out, result, plan);
            return ExitSuccess;
        }

        // Fail loud — damage. No recommendation produced.
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                outcome = result.Outcome.ToString(),
                errors = result.Errors
            }));
        }
        else
        {
            Console.Error.WriteLine($"Analysis ingest failed [{result.Outcome}] — no recommendation produced:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        return result.Outcome == AnalysisIngestOutcome.EmptyResponse ? ExitEmpty : ExitParseInvalid;
    }

    /// <summary>Spec 094 (FR-002): resolves the behaviours' sources and builds the plan.</summary>
    private static async Task<GenerationPlan> BuildPlanAsync(
        string repoRoot, SpectraConfig config, string suite, string? focus, AnalysisRecommendation result, CancellationToken ct)
    {
        var context = await BaselineContext.CreateAsync(repoRoot, config, ct);
        var resolved = new List<ResolvedBehavior>();
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var notes = new List<string>();
        foreach (var behavior in result.Behaviors)
        {
            var resolution = await PlanSectionResolver.ResolveAsync(context, behavior.Source, ct);
            resolved.Add(new ResolvedBehavior(
                behavior.Title, behavior.Category ?? string.Empty, behavior.Technique ?? string.Empty, behavior.Source ?? string.Empty,
                resolution.Ref, resolution.DocPath, resolution.Position, resolution.Reason));
            if (!resolution.Resolved)
            {
                continue;
            }

            sections[resolution.Ref!] = resolution.Fingerprint!;
            var note = $"{resolution.Ref}: more than one heading shares the anchor key '{resolution.AnchorKey}'; the first was used";
            if (resolution.Ambiguous && !notes.Contains(note))
            {
                notes.Add(note);
            }
        }

        return GenerationPlanBuilder.Build(
            suite, string.IsNullOrWhiteSpace(focus) ? null : focus, config.Generation.EffectiveBatchSize,
            result.RecommendedCount, resolved, sections, notes);
    }

    private static void WriteSummary(TextWriter output, AnalysisRecommendation result, GenerationPlan plan)
    {
        output.WriteLine($"Total behaviors:    {result.TotalBehaviors}");
        output.WriteLine($"Already covered:    {result.AlreadyCovered}");
        output.WriteLine($"Recommended:        {result.RecommendedCount}");
        output.WriteLine($"Documents analyzed: {result.DocumentsAnalyzed}");
        if (result.Breakdown.Count > 0)
        {
            output.WriteLine("Breakdown:");
            foreach (var kvp in result.Breakdown)
                output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        if (result.TechniqueBreakdown.Count > 0)
        {
            output.WriteLine("Techniques:");
            foreach (var kvp in result.TechniqueBreakdown)
                output.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        // Spec 062: advisory boundary-coverage gaps — printed only when present (no noise).
        if (result.BoundaryGaps.Count > 0)
        {
            output.WriteLine("Boundary gaps:");
            foreach (var g in result.BoundaryGaps)
            {
                var src = string.IsNullOrWhiteSpace(g.Source) ? "" : $"  [{g.Source}]";
                output.WriteLine($"  {g.Kind} · {g.Field} — {g.Description}{src}");
            }
        }

        // Spec 094 (FR-007): the plan — one line per batch (a '|' in a title shown as '/'), one per unresolved behaviour.
        output.WriteLine($"Plan: {GenerationPlanStore.RelativePathFor(plan.Suite)}");
        foreach (var batch in plan.Batches)
        {
            output.WriteLine(BatchLine(plan, batch));
        }

        foreach (var behavior in plan.Behaviors.Where(b => !b.Resolved))
        {
            output.WriteLine($"unresolved: {behavior.Id} {behavior.Title} — {behavior.Reason}");
        }

        if (plan.CountRule == GenerationPlan.OnePerBatch)
        {
            output.WriteLine($"counts raised to one per batch (recommended {plan.Recommended} < {plan.Batches.Count} batches)");
        }
    }

    /// <summary>Spec 094: <c>batch &lt;id&gt; | count &lt;k&gt; | &lt;refs&gt; | &lt;titles&gt;</c> — the line the analyst relays verbatim.</summary>
    internal static string BatchLine(GenerationPlan plan, PlannedBatch batch) =>
        $"batch {batch.BatchId} | count {batch.Count} | {string.Join(", ", batch.SourceRefs)} | "
        + string.Join("; ", batch.BehaviorIds.Select(id => plan.Behavior(id).Title.Replace('|', '/')));

    private static async Task<List<TestCase>> LoadExistingTestsAsync(
        string suitePath, string testsPath, CancellationToken ct)
    {
        var tests = new List<TestCase>();
        if (!Directory.Exists(suitePath))
            return tests;

        var parser = new TestCaseParser();
        var files = Directory.GetFiles(suitePath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith('_'));

        foreach (var file in files)
        {
            var fileContent = await File.ReadAllTextAsync(file, ct);
            var relativePath = Path.GetRelativePath(testsPath, file);
            var parsed = parser.Parse(fileContent, relativePath);
            if (parsed.IsSuccess)
                tests.Add(parsed.Value!);
        }

        return tests;
    }
}
