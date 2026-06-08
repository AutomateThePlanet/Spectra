using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Generation;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Prompts;
using Spectra.CLI.Source;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 059: <c>spectra ai compile-analysis-prompt</c>. Deterministic, model-free compiler for the
/// behavior-analysis prompt. Resolves the suite's documents (with the same token-budget pre-flight
/// as generation) plus coverage grounding, emits the analysis prompt to stdout, and writes nothing
/// to disk. Refuses to emit (exit 4) when a required input is missing or the budget is exceeded.
/// </summary>
public sealed class CompileAnalysisPromptCommand : Command
{
    // Local, contract-specific exit codes (see contracts/compile-analysis-prompt.md).
    private const int ExitSuccess = 0;
    private const int ExitRefused = 4;
    private const int ExitError = 1;

    public CompileAnalysisPromptCommand()
        : base("compile-analysis-prompt", "Compile a behavior-analysis prompt (deterministic, no model call)")
    {
        var suiteOption = new Option<string?>(["--suite", "-s"], "Target test suite");
        var docSuiteOption = new Option<string?>("--doc-suite", "Doc-suite filter (Spec 040)");
        var focusOption = new Option<string?>(["--focus", "-f"], "Focus area folded into the analysis prompt");
        var includeArchivedOption = new Option<bool>("--include-archived", "Include skip_analysis suites (Spec 040)");

        AddOption(suiteOption);
        AddOption(docSuiteOption);
        AddOption(focusOption);
        AddOption(includeArchivedOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var docSuite = context.ParseResult.GetValueForOption(docSuiteOption);
            var focus = context.ParseResult.GetValueForOption(focusOption) ?? "";
            var includeArchived = context.ParseResult.GetValueForOption(includeArchivedOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(
                suite, docSuite, focus, includeArchived,
                outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string? suite, string? docSuite, string focus, bool includeArchived,
        bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suite))
        {
            EmitRefusal(json, "suite", "A target suite is required.");
            return ExitRefused;
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

        // Load source documents with full content (same as the generate handler).
        IReadOnlyList<SourceDocument> documents;
        try
        {
            var docLoader = new SourceDocumentLoader(config.Source);
            documents = await docLoader.LoadAllAsync(currentDir, ct: ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error loading documentation: {ex.Message}");
            return ExitError;
        }

        if (documents.Count == 0)
        {
            EmitRefusal(json, "documents", "No source documentation found in docs/.");
            return ExitRefused;
        }

        // Filter documents by doc-suite and run the pre-flight token-budget check
        // before emitting (mirrors GenerateHandler). Budget overflow → exit 4.
        try
        {
            var inputBuilder = new AnalyzerInputBuilder();
            var manifestPath = LegacyIndexMigrator.ResolveManifestPath(currentDir, config.Source);
            var indexDir = LegacyIndexMigrator.ResolveIndexDir(currentDir, config.Source);
            var inputResult = await inputBuilder.BuildAsync(
                basePath: currentDir,
                manifestPath: manifestPath,
                indexDir: indexDir,
                allDocuments: documents,
                suiteFilter: docSuite ?? suite,
                focusFilter: focus,
                budgetTokens: config.Analysis.MaxPromptTokens,
                includeArchived: includeArchived,
                ct: ct);

            documents = inputResult.FilteredDocuments;
        }
        catch (PreFlightBudgetExceededException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitRefused;
        }

        if (documents.Count == 0)
        {
            EmitRefusal(json, "documents",
                $"No documents matched suite '{suite}'. Run 'spectra docs index' and check available suites.");
            return ExitRefused;
        }

        // Build coverage snapshot + existing tests exactly as the generate handler does.
        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);
        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        var coverageContext = await BuildCoverageContextAsync(
            currentDir, config, suite, testsPath, existingTests.Count, ct);

        var templateLoader = new PromptTemplateLoader(currentDir);
        var result = AnalysisPromptCompiler.Compile(
            documents, focus, config, templateLoader, coverageContext);

        if (!result.IsSuccess)
        {
            EmitRefusal(json, result.MissingInput!, result.Message!);
            return ExitRefused;
        }

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new { prompt = result.Prompt }));
        }
        else
        {
            Console.Out.Write(result.Prompt);
            if (!result.Prompt!.EndsWith('\n')) Console.Out.WriteLine();
        }
        return ExitSuccess;
    }

    private static async Task<string?> BuildCoverageContextAsync(
        string currentDir, SpectraConfig config, string suite, string testsPath,
        int existingTestCount, CancellationToken ct)
    {
        if (existingTestCount == 0)
            return null;

        var criteriaDir = Path.Combine(currentDir, config.Coverage?.CriteriaDir ?? "docs/criteria");
        var criteriaIndexFile = Path.Combine(criteriaDir, "_criteria_index.yaml");
        var docIndexFile = Path.Combine(currentDir, "docs", "_index.md");
        var snapshotBuilder = new CoverageSnapshotBuilder(currentDir);
        var snapshot = await snapshotBuilder.BuildAsync(
            suite, testsPath, criteriaDir, criteriaIndexFile, docIndexFile, ct);

        return snapshot.HasData ? CoverageContextFormatter.Format(snapshot) : null;
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

    private static void EmitRefusal(bool json, string missingInput, string message)
    {
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                refused = true,
                missing_input = missingInput,
                message
            }));
        }
        else
        {
            Console.Error.WriteLine($"Refusing to emit prompt — missing required input '{missingInput}': {message}");
        }
    }
}
