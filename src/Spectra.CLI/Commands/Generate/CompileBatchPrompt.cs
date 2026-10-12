using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Criteria;
using Spectra.CLI.Generation;
using Spectra.CLI.Generation.Plan;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.CLI.Services;
using Spectra.CLI.Skills;
using Spectra.Core.Baseline;
using Spectra.Core.Config;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 094 (FR-009–FR-011, FR-010a; research R5–R8, R12, R16, R17): <c>compile-prompt --suite S --batch &lt;id&gt;</c>.
/// Loads the suite's generation plan and compiles the prompt for one batch. Modes:
/// <list type="bullet">
/// <item><c>batch</c> — the batch's behaviours, its sections inlined (each once) and their criteria;</item>
/// <item><c>count_fallback</c> — the project's <c>test-generation.md</c> predates batch prompts and was customised (or has
/// no manifest record): count mode with the user's template, focus = the batch's titles and refs;</item>
/// <item><c>count</c> — <c>--batch unsourced</c>: count mode, focus = the unsourced titles.</item>
/// </list>
/// Checks, in order: plan, batch, staleness (not for <c>unsourced</c>), criteria, size (batch mode only). Every refusal is
/// exit 4 in the shared failure shape; nothing is written. The prompt goes to stdout; the mode and warnings to stderr.
/// </summary>
internal static class CompileBatchPrompt
{
    private const string CommandName = "compile-prompt";

    public const string ErrorPlanMissing = "PLAN_MISSING";
    public const string ErrorBatchNotFound = "BATCH_NOT_FOUND";
    public const string ErrorPlanStale = "PLAN_STALE";
    public const string ErrorMissingInput = "MISSING_INPUT";

    public const string ModeBatch = "batch";
    public const string ModeCountFallback = "count_fallback";
    public const string ModeCount = "count";

    public const string BundledTemplateWarning =
        "using the bundled test-generation template; run spectra update-skills to refresh your copy";

    public const string CustomTemplateWarning =
        "custom test-generation template predates batch prompts — this batch is compiled in count mode (the generator reads "
        + "the docs); run spectra prompts reset test-generation to use section batches";

    public const string UnrecordedTemplateWarning =
        "custom test-generation template predates batch prompts — this batch is compiled in count mode (the generator reads "
        + "the docs); run spectra prompts reset test-generation (or spectra update-skills if you never edited it) to use "
        + "section batches";

    public static async Task<int> RunAsync(
        string suite, string batchId, int? countOverride, string? focus, bool json, CancellationToken ct)
    {
        var root = Directory.GetCurrentDirectory();
        var loaded = await ConfigLoader.LoadForReadAsync(root, ConfigSections.Source, ct);
        if (loaded.Error is not null)
        {
            CriteriaCommandIO.WriteFailure(json, CommandName, loaded.ErrorCode, loaded.Error);
            return ExitCodes.Error;
        }

        var config = loaded.Config!;
        var warnings = new List<string>(loaded.Warnings);

        // 1. The plan.
        var planLoad = await GenerationPlanStore.LoadAsync(root, suite, ct);
        var planPath = GenerationPlanStore.RelativePathFor(suite);
        if (planLoad.Plan is null)
        {
            return Fail(json, ErrorPlanMissing, [planLoad.Problem is null
                ? $"No generation plan for suite '{suite}' ({planPath}). Run the analysis first (spectra ai ingest-analysis --suite {suite})."
                : $"The generation plan {planPath} is unreadable: {planLoad.Problem}; re-run the analysis."], warnings);
        }

        var plan = planLoad.Plan;

        // 2. The batch.
        var batch = plan.FindBatch(batchId.Trim());
        if (batch is null)
        {
            return Fail(json, ErrorBatchNotFound,
                [$"No batch '{batchId}' in the plan for suite '{suite}'. Batches: {string.Join(", ", plan.Batches.Select(b => b.BatchId))}."],
                warnings);
        }

        var count = countOverride ?? batch.Count;
        var templateLoader = new PromptTemplateLoader(root);
        var profileFormat = ProfileFormatLoader.LoadFormat(root);

        if (batch.BatchId == GenerationPlan.Unsourced)
        {
            var titles = batch.BehaviorIds.Select(id => plan.Behavior(id).Title);
            return await CompileCountModeAsync(root, config, suite, Join(titles, focus), count, templateLoader, profileFormat,
                ModeCount, batch.BatchId, warnings, json, ct);
        }

        // 3. Staleness: every section of the batch, as it is now, against the plan's fingerprint.
        var context = await BaselineContext.CreateAsync(root, config, ct);
        var resolutions = new List<SectionResolution>();
        var stale = new List<string>();
        foreach (var sectionRef in batch.SourceRefs)
        {
            var now = await PlanSectionResolver.ResolveAsync(context, sectionRef, ct);
            if (!now.Resolved)
            {
                stale.Add($"{sectionRef} no longer resolves: {now.Reason}");
            }
            else if (!plan.Sections.TryGetValue(sectionRef, out var recorded) || recorded != now.Fingerprint)
            {
                stale.Add($"{sectionRef} changed since the analysis");
            }

            resolutions.Add(now);
        }

        if (stale.Count > 0)
        {
            return Fail(json, ErrorPlanStale,
                [$"The documentation of batch {batch.BatchId} changed since the analysis; re-run the analysis.", .. stale], warnings);
        }

        // The template decides the mode, never a refusal (FR-010a).
        var template = templateLoader.LoadTemplate("test-generation");
        if (BatchPromptCompiler.PredatesBatch(template))
        {
            var unedited = await IsUneditedCopyAsync(root, ct);
            if (unedited == true)
            {
                template = BuiltInTemplates.GetTemplate("test-generation")!;
                warnings.Add(BundledTemplateWarning);
            }
            else
            {
                warnings.Add(unedited is null ? UnrecordedTemplateWarning : CustomTemplateWarning);
                var described = batch.BehaviorIds.Select(id => $"{plan.Behavior(id).Title} ({plan.Behavior(id).Ref})");
                return await CompileCountModeAsync(root, config, suite, Join(described, focus), count, templateLoader,
                    profileFormat, ModeCountFallback, batch.BatchId, warnings, json, ct);
            }
        }

        // 4. Criteria of the batch's sections.
        var sections = new List<BatchSection>();
        foreach (var resolution in resolutions)
        {
            var headings = await context.GetHeadingsAsync(resolution.FullPath!, ct);
            var covered = resolution.Position < 0
                ? Enumerable.Range(0, headings.Count).ToList()
                : SectionExtractor.CoveredHeadings(headings, SectionExtractor.AnchorOf(resolution.Ref!));
            sections.Add(new BatchSection(resolution.Ref!, resolution.DocPath!, resolution.Position < 0, covered));
        }

        var selection = await BatchCriteriaSelector.SelectAsync(
            root, config, sections, (doc, token) => context.GetHeadingsAsync(Path.Combine(root, doc), token), ct);
        warnings.AddRange(selection.Warnings);
        if (selection.Criteria.Count == 0)
        {
            return Fail(json, ErrorMissingInput,
                [$"No acceptance criteria for the documents of batch {batch.BatchId} ({string.Join(", ", sections.Select(s => s.DocPath).Distinct())}). "
                 + "Refusing to emit a prompt without grounding criteria. Extract criteria for these documents first."],
                warnings, new Dictionary<string, object?> { ["missing_input"] = "criteria_context" });
        }

        // The sections to inline: each once; an outer ref absorbs the refs inside it.
        var inline = new List<(string Ref, string Raw)>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var absorbed = Enumerable.Range(0, sections.Count).Any(j => j != i
                && sections[j].DocPath == section.DocPath
                && !section.WholeDocument
                && (sections[j].WholeDocument
                    || (resolutions[j].Position != resolutions[i].Position && sections[j].CoveredHeadings.Contains(resolutions[i].Position))));
            if (!absorbed && inline.All(x => x.Ref != section.Ref))
            {
                inline.Add((section.Ref, await context.GetRawSectionAsync(section.Ref, ct) ?? string.Empty));
            }
        }

        var prompt = BatchPromptCompiler.Compile(template, plan, batch, inline,
            CriteriaContextLoader.Format(selection.Criteria, "\n") ?? string.Empty, count, focus, profileFormat);

        // 5. Size (088's estimator and budget).
        var estimated = TokenEstimator.Estimate(prompt);
        var limit = config.Analysis?.MaxPromptTokens ?? PreFlightTokenChecker.DefaultBudgetTokens;
        if (estimated > limit)
        {
            return Fail(json, CompileUpdatePromptCommand.ErrorPromptTooLarge,
                [$"The prompt for batch {batch.BatchId} is about {estimated} tokens, over the {limit}-token budget (analysis.max_prompt_tokens). "
                 + "Re-run the analysis with more specific sources, or raise the budget."], warnings);
        }

        Emit(prompt, ModeBatch, batch.BatchId, warnings, json);
        return ExitCodes.Success;
    }

    /// <summary>Count mode (<c>unsourced</c>, <c>count_fallback</c>): the suite-match criteria, the given template, no size check.</summary>
    private static async Task<int> CompileCountModeAsync(
        string root, SpectraConfig config, string suite, string userPrompt, int count, PromptTemplateLoader templateLoader,
        string profileFormat, string mode, string batchId, List<string> warnings, bool json, CancellationToken ct)
    {
        var criteria = await CriteriaContextLoader.LoadCriteriaContextAsync(root, suite, config, ct);
        var result = PromptCompiler.Compile(
            userPrompt: userPrompt,
            requestedCount: count,
            criteriaContext: criteria.Context,
            templateLoader: templateLoader,
            profileFormat: profileFormat,
            testimizeData: null);
        if (!result.IsSuccess)
        {
            return Fail(json, ErrorMissingInput, [result.Message!], warnings,
                new Dictionary<string, object?> { ["missing_input"] = result.MissingInput });
        }

        Emit(result.Prompt!, mode, batchId, warnings, json);
        return ExitCodes.Success;
    }

    /// <summary>
    /// R16: whether the project's <c>test-generation.md</c> is the copy SPECTRA installed — true when its hash equals the
    /// skills manifest's record (init's relative key or update-skills' absolute key), false when it differs, null when
    /// there is no record.
    /// </summary>
    private static async Task<bool?> IsUneditedCopyAsync(string root, CancellationToken ct)
    {
        var relative = Path.Combine(".spectra", "prompts", "test-generation.md");
        var path = Path.Combine(root, relative);
        if (!File.Exists(path))
        {
            return null;
        }

        var manifest = await new SkillsManifestStore(root).LoadAsync(ct);
        string? recorded = null;
        foreach (var key in new[] { path, relative, relative.Replace('\\', '/') })
        {
            if (manifest.Files.TryGetValue(key, out var hash))
            {
                recorded = hash;
                break;
            }
        }

        return recorded is null ? null : recorded == await FileHasher.ComputeFileHashAsync(path, ct);
    }

    private static string Join(IEnumerable<string> items, string? focus)
    {
        var text = string.Join("; ", items);
        return string.IsNullOrWhiteSpace(focus) ? text : $"{text}; {focus.Trim()}";
    }

    /// <summary>The prompt on stdout; the mode and warnings on stderr (one JSON line in JSON mode).</summary>
    private static void Emit(string prompt, string mode, string batchId, IReadOnlyList<string> warnings, bool json)
    {
        Console.Out.Write(prompt);
        if (!prompt.EndsWith('\n'))
        {
            Console.Out.WriteLine();
        }

        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["command"] = CommandName,
                ["success"] = true,
                ["mode"] = mode,
                ["batch"] = batchId,
                ["warnings"] = warnings
            }, CriteriaCommandIO.Json));
            return;
        }

        Console.Error.WriteLine($"Mode: {mode}");
        BaselineCommandIO.PrintWarnings(warnings);
    }

    private static int Fail(
        bool json, string code, IReadOnlyList<string> errors, IReadOnlyList<string> warnings,
        IReadOnlyDictionary<string, object?>? extras = null)
    {
        if (json)
        {
            CriteriaCommandIO.WriteFailureJson(CommandName, code, errors, warnings, extras);
        }
        else
        {
            BaselineCommandIO.PrintWarnings(warnings);
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            Console.Error.WriteLine($"[{code}]");
        }

        return BaselineCommandIO.ExitCodeOf(code);
    }
}
