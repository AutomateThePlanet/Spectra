using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 071 FR4: <c>spectra ai compile-repair-prompt</c>. Deterministic, model-free repair-prompt
/// compiler for partial tests. Reads the test artifact and its per-test verdict JSON, emits a
/// plain-text repair prompt to stdout. Refuses (exit 4) for non-partial verdicts.
/// Mirrors compile-critic-prompt in structure; never calls a model.
/// </summary>
public sealed class CompileRepairPromptCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitRefused = 4;
    private const int ExitVerdictEmpty = 5;
    private const int ExitVerdictParseFailed = 6;

    public CompileRepairPromptCommand()
        : base("compile-repair-prompt", "Compile a targeted repair prompt for a partial test (Spec 071, no model call)")
    {
        var suiteOption = new Option<string>(["--suite", "-s"], "Suite name to resolve the test from") { IsRequired = true };
        var testOption = new Option<string>(["--test", "-t"], "Test ID to compile repair prompt for") { IsRequired = true };
        var fromOption = new Option<string?>("--from", "Verdict JSON file path (default: .spectra/verdicts/critic-verdict-{id}.json)");

        AddOption(suiteOption);
        AddOption(testOption);
        AddOption(fromOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var testId = context.ParseResult.GetValueForOption(testOption)!;
            var from = context.ParseResult.GetValueForOption(fromOption);
            context.ExitCode = await RunAsync(suite, testId, from, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string suite, string testId, string? from, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = await ResolveTestsDirAsync(currentDir, ct);
        var suitePath = Path.Combine(currentDir, testsDir, suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);

        var index = await new IndexWriter().ReadAsync(indexPath, ct);
        if (index is null)
        {
            Console.Error.WriteLine($"Suite index not found: {indexPath}");
            return ExitError;
        }

        var entry = index.Tests.FirstOrDefault(t => string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            Console.Error.WriteLine($"Test id '{testId}' not found in suite '{suite}'.");
            return ExitError;
        }

        var testFilePath = Path.Combine(suitePath, entry.File);
        if (!File.Exists(testFilePath))
        {
            Console.Error.WriteLine($"Test file not found: {testFilePath}");
            return ExitError;
        }

        var fileContent = await File.ReadAllTextAsync(testFilePath, ct);
        var relativePath = Path.GetRelativePath(currentDir, testFilePath);
        var parsed = new TestCaseParser().Parse(fileContent, relativePath);
        if (!parsed.IsSuccess || parsed.Value is null)
        {
            Console.Error.WriteLine($"Could not parse test file: {testFilePath}");
            return ExitError;
        }
        var test = parsed.Value;

        // Resolve verdict file
        var verdictPath = from ?? Path.Combine(currentDir, ".spectra", "verdicts", $"critic-verdict-{testId}.json");
        if (!File.Exists(verdictPath))
        {
            Console.Error.WriteLine($"Verdict file not found: {verdictPath}. Run the critic for this test first.");
            return ExitVerdictEmpty;
        }

        var verdictJson = await File.ReadAllTextAsync(verdictPath, ct);
        if (string.IsNullOrWhiteSpace(verdictJson))
        {
            Console.Error.WriteLine($"Verdict file is empty: {verdictPath}");
            return ExitVerdictEmpty;
        }

        var classification = VerdictIngestor.Classify(verdictJson);
        if (!classification.IsSuccess)
        {
            Console.Error.WriteLine($"Could not parse verdict file: {string.Join("; ", classification.Errors)}");
            return ExitVerdictParseFailed;
        }

        var verResult = classification.Result!;

        // Only compile repair prompts for partial verdicts
        if (verResult.Verdict != VerificationVerdict.Partial)
        {
            Console.Error.WriteLine(
                $"Refusing to compile repair prompt: verdict is '{verResult.Verdict.ToString().ToLowerInvariant()}' (repair is for partial verdicts only).");
            return ExitRefused;
        }

        var nonGrounded = verResult.Findings
            .Where(f => f.Status != FindingStatus.Grounded)
            .ToList();

        // Load source docs from test's source_refs
        IReadOnlyList<SourceDocument> sourceDocs;
        try
        {
            sourceDocs = await LoadDocumentsFromRefsAsync(test.SourceRefs, currentDir, ct);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitError;
        }

        var (prompt, refusalReason) = RepairPromptCompiler.Compile(test, nonGrounded, sourceDocs);
        if (prompt is null)
        {
            Console.Error.WriteLine($"Refused to compile repair prompt: {refusalReason}");
            return ExitRefused;
        }

        Console.Out.Write(prompt);
        if (!prompt.EndsWith('\n')) Console.Out.WriteLine();
        return ExitSuccess;
    }

    private static async Task<IReadOnlyList<SourceDocument>> LoadDocumentsFromRefsAsync(
        IReadOnlyList<string> sourceRefs, string currentDir, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var docs = new List<SourceDocument>();
        foreach (var r in sourceRefs)
        {
            var file = r.Split('#')[0].Trim();
            if (string.IsNullOrWhiteSpace(file)) continue;
            var full = Path.IsPathRooted(file) ? file : Path.Combine(currentDir, file);
            if (!seen.Add(Path.GetFullPath(full))) continue;
            if (!File.Exists(full))
                throw new FileNotFoundException($"Source ref not found: {r}", full);
            var content = await File.ReadAllTextAsync(full, ct);
            docs.Add(new SourceDocument
            {
                Path = Path.GetRelativePath(currentDir, full).Replace('\\', '/'),
                Title = Path.GetFileNameWithoutExtension(full),
                Content = content
            });
        }
        return docs;
    }

    private static async Task<string> ResolveTestsDirAsync(string currentDir, CancellationToken ct)
    {
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath)) return "test-cases";
        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SpectraConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Tests?.Dir ?? "test-cases";
        }
        catch { return "test-cases"; }
    }
}
