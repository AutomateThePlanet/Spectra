using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 059: <c>spectra ai ingest-analysis</c>. The fail-loud, model-free boundary for the
/// analyze-first step. Reads the agent's behavior JSON (from <c>--from</c> or stdin), runs the
/// deterministic accounting relocated from <c>BehaviorAnalyzer</c>, and emits the recommendation
/// the generation skill presents for approval. Persists nothing — the recommendation is advisory.
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
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitError;
        }

        SpectraConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<SpectraConfig>(
                await File.ReadAllTextAsync(configPath, ct),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error reading config: {ex.Message}");
            return ExitError;
        }
        if (config is null)
        {
            Console.Error.WriteLine("Could not parse spectra.config.json.");
            return ExitError;
        }

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
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    outcome = result.Outcome.ToString(),
                    already_covered = result.AlreadyCovered,
                    recommended = result.RecommendedCount,
                    breakdown = result.Breakdown,
                    technique_breakdown = result.TechniqueBreakdown,
                    documents_analyzed = result.DocumentsAnalyzed
                }));
            }
            else
            {
                Console.Out.WriteLine($"Total behaviors:    {result.TotalBehaviors}");
                Console.Out.WriteLine($"Already covered:    {result.AlreadyCovered}");
                Console.Out.WriteLine($"Recommended:        {result.RecommendedCount}");
                Console.Out.WriteLine($"Documents analyzed: {result.DocumentsAnalyzed}");
                if (result.Breakdown.Count > 0)
                {
                    Console.Out.WriteLine("Breakdown:");
                    foreach (var kvp in result.Breakdown)
                        Console.Out.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
                if (result.TechniqueBreakdown.Count > 0)
                {
                    Console.Out.WriteLine("Techniques:");
                    foreach (var kvp in result.TechniqueBreakdown)
                        Console.Out.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
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
