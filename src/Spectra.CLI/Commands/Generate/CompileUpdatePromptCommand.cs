using System.CommandLine;
using Spectra.CLI.Baseline;
using Spectra.CLI.Generation;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.CLI.Services;
using Spectra.Core.Config;
using Spectra.Core.Models;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 063 / 088: <c>spectra ai compile-update-prompt --suite &lt;s&gt; (--test-id &lt;id&gt; | --group &lt;gid&gt;)</c>.
/// Deterministic, model-free compiler for the update seam. Emits — to stdout, writing nothing — an <i>edit</i> prompt
/// for one test or a whole triage group: each referenced doc section once (current text, previous normalised version,
/// what changed — or why it no longer resolves), only the criteria the tests link, and each test under
/// <c>### TEST &lt;id&gt;</c>. Refusals are one JSON document on stderr in the shared shape (exit 4).
/// </summary>
public sealed class CompileUpdatePromptCommand : Command
{
    public const string CommandName = "compile-update-prompt";
    public const string ErrorNothingToReconcile = "NOTHING_TO_RECONCILE";
    public const string ErrorPromptTooLarge = "PROMPT_TOO_LARGE";

    public CompileUpdatePromptCommand()
        : base("compile-update-prompt", "Compile an edit prompt for one test or a triage group (deterministic, no model call)")
    {
        var suiteOption = new Option<string?>(["--suite", "-s"], "Suite containing the tests to update");
        var testIdOption = new Option<string?>("--test-id", "One test to compile an update prompt for");
        var groupOption = new Option<string?>("--group", "A group from the last update-triage (G1, G2, …): one prompt for all its tests");

        AddOption(suiteOption);
        AddOption(testIdOption);
        AddOption(groupOption);

        this.SetHandler(async context =>
        {
            var parse = context.ParseResult;
            context.ExitCode = await RunAsync(
                Directory.GetCurrentDirectory(),
                parse.GetValueForOption(suiteOption),
                parse.GetValueForOption(testIdOption),
                parse.GetValueForOption(groupOption),
                parse.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json,
                context.GetCancellationToken());
        });
    }

    public static async Task<int> RunAsync(
        string root, string? suite, string? testId, string? group, bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suite) || string.IsNullOrWhiteSpace(testId) == string.IsNullOrWhiteSpace(group))
        {
            return BaselineCommandIO.Fail(json, CommandName, BaselineCommandIO.ErrorInvalidArguments,
                "Give --suite <name> and exactly one of --test-id <id> and --group <id>.");
        }

        // Spec 083 read rule: a missing config means defaults; source and tests are read.
        var loaded = await ConfigLoader.LoadForReadAsync(root, ConfigSections.Source | ConfigSections.Tests, ct);
        if (!loaded.IsSuccess)
        {
            return BaselineCommandIO.Fail(json, CommandName, loaded.ErrorCode!, loaded.Error!);
        }

        var config = loaded.Config!;
        var warnings = new List<string>(loaded.Warnings);
        BaselineCommandIO.PrintWarnings(loaded.Warnings);
        var testsPath = BaselineCommandIO.TestsPath(root, config);
        var (suites, scopeError, scopeMessage) = BaselineCommandIO.ResolveScope(testsPath, suite, allSuites: false);
        if (suites is null)
        {
            return BaselineCommandIO.Fail(json, CommandName, scopeError!, scopeMessage!, warnings);
        }

        string prompt;
        try
        {
            var suiteTests = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, suite!, warnings, ct);
            var context = await BaselineContext.CreateAsync(root, config, ct);
            var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, suite!, ct);

            List<TestCase> targets;
            if (group is null)
            {
                var test = suiteTests.Tests.FirstOrDefault(t => string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
                if (test is null)
                {
                    return BaselineCommandIO.Fail(json, CommandName, BaselineCommandIO.ErrorTestNotFound,
                        $"Test '{testId}' was not found in suite '{suite}'.", warnings);
                }

                targets = [test];
            }
            else
            {
                var byId = suiteTests.Tests
                    .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                (_, targets) = await TriageGroupResolver.ResolveAsync(root, suite!, group, byId, context, null, ct);
            }

            // Spec 088 C1: the same light threshold triage uses.
            var threshold = config.Update?.EffectiveLightThresholdWords ?? 12;
            var updateContext = await UpdateContextBuilder.BuildAsync(targets, baseline, context, threshold, ct);
            if (updateContext.NothingToReconcile.Count > 0)
            {
                return BaselineCommandIO.Fail(json, CommandName, ErrorNothingToReconcile,
                    $"Nothing to reconcile for {string.Join(", ", updateContext.NothingToReconcile)}: no referenced doc section "
                    + "resolves and no linked acceptance criterion exists. Fix the source_refs / criteria (or delete the test) first.",
                    warnings);
            }

            var template = new PromptTemplateLoader(root).LoadTemplate("test-update");
            var compiled = UpdatePromptCompiler.CompileContext(updateContext, template, ProfileFormatLoader.LoadFormat(root), group is not null);
            if (!compiled.IsSuccess)
            {
                return BaselineCommandIO.Fail(json, CommandName, compiled.RefusalCode!, compiled.Message!, warnings);
            }

            // Spec 088 FR-013: the existing token estimator and budget.
            var estimated = TokenEstimator.Estimate(compiled.Prompt);
            var limit = config.Analysis?.MaxPromptTokens ?? PreFlightTokenChecker.DefaultBudgetTokens;
            if (estimated > limit)
            {
                return BaselineCommandIO.Fail(json, CommandName, ErrorPromptTooLarge,
                    $"The prompt is about {estimated} tokens, over the {limit}-token budget (analysis.max_prompt_tokens). "
                    + "Compile the tests one by one with --test-id, or lower update.group_size and triage again.", warnings);
            }

            if (compiled.Warning is not null)
            {
                BaselineCommandIO.PrintWarnings([compiled.Warning]);
            }

            prompt = compiled.Prompt!;
        }
        catch (BaselineStoreException ex)
        {
            return BaselineCommandIO.Fail(json, CommandName, ex.ErrorCode, ex.Message, warnings);
        }

        BaselineCommandIO.PrintLateWarnings(warnings, loaded.Warnings.Count);
        Console.Out.Write(prompt);
        if (!prompt.EndsWith('\n'))
        {
            Console.Out.WriteLine();
        }

        return ExitCodes.Success;
    }
}
