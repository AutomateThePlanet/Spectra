using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 072 FR1: <c>spectra ai compile-repair-batch</c>. Deterministic, model-free batch repair-
/// manifest compiler. Reads all partial verdict JSONs for the suite, filters out tests whose
/// grounding block is already written (the resume checkpoint), compiles each repair prompt via
/// <see cref="RepairPromptCompiler.Compile"/> in one pass, and emits a JSON manifest to stdout.
/// No model call. Idempotent: re-running skips already-grounded tests automatically.
/// </summary>
public sealed class CompileRepairBatchCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;

    public CompileRepairBatchCommand()
        : base("compile-repair-batch",
            "Compile a batch repair manifest for all ungrounded partial tests in a suite (Spec 072, no model call)")
    {
        var suiteOption = new Option<string>(["--suite", "-s"], "Suite name to compile repair manifest for")
        { IsRequired = true };

        AddOption(suiteOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            context.ExitCode = await RunAsync(suite, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string suite, CancellationToken ct)
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

        var testsPath = Path.Combine(currentDir, testsDir);
        var verdictDir = Path.Combine(currentDir, ".spectra", "verdicts");
        if (!Directory.Exists(verdictDir))
        {
            // No verdicts yet — emit an empty manifest
            Console.Out.WriteLine("[]");
            return ExitSuccess;
        }

        var indexLookup = index.Tests
            .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

        var parser = new TestCaseParser();
        var entries = new List<RepairBatchEntry>();

        var verdictFiles = Directory.GetFiles(verdictDir, "critic-verdict-*.json")
            .OrderBy(f => f);

        foreach (var vf in verdictFiles)
        {
            ct.ThrowIfCancellationRequested();

            string verdictJson;
            try { verdictJson = await File.ReadAllTextAsync(vf, ct); }
            catch { continue; }

            // Extract id from filename
            var fileName = Path.GetFileNameWithoutExtension(vf);
            var testId = fileName.StartsWith("critic-verdict-", StringComparison.OrdinalIgnoreCase)
                ? fileName["critic-verdict-".Length..] : null;
            if (string.IsNullOrEmpty(testId)) continue;

            // Classify verdict — only process partials
            var classification = VerdictIngestor.Classify(verdictJson);
            if (!classification.IsSuccess) continue;
            var verResult = classification.Result!;
            if (verResult.Verdict != VerificationVerdict.Partial) continue;

            // Resolve test file — entry.File is relative to suitePath (e.g., "TC-401.md") — matches GeneratedTestIngestor.ParseTestCase
            if (!indexLookup.TryGetValue(testId, out var testEntry)) continue;
            var testFilePath = Path.Combine(suitePath, testEntry.File);
            if (!File.Exists(testFilePath)) continue;

            // Resume checkpoint: skip if grounding block already written
            try
            {
                var content = await File.ReadAllTextAsync(testFilePath, ct);
                var rel = Path.GetRelativePath(testsPath, testFilePath);
                var parsed = parser.Parse(content, rel);
                if (parsed.IsSuccess && parsed.Value is { } tc && tc.Grounding is not null)
                    continue; // Already grounded — done, skip
            }
            catch { continue; }

            // Load the test for prompt compilation
            var fileContent = await File.ReadAllTextAsync(testFilePath, ct);
            var relPath = Path.GetRelativePath(currentDir, testFilePath);
            var testParsed = parser.Parse(fileContent, relPath);
            if (!testParsed.IsSuccess || testParsed.Value is null) continue;
            var test = testParsed.Value;

            var nonGrounded = verResult.Findings
                .Where(f => f.Status != FindingStatus.Grounded)
                .ToList();

            // Load source docs
            IReadOnlyList<SourceDocument> sourceDocs;
            try { sourceDocs = await LoadDocumentsFromRefsAsync(test.SourceRefs, currentDir, ct); }
            catch { sourceDocs = []; }

            var (prompt, _) = RepairPromptCompiler.Compile(test, nonGrounded, sourceDocs);
            if (prompt is null) continue;

            entries.Add(new RepairBatchEntry
            {
                Id = testId,
                Prompt = prompt,
                SourceRefs = test.SourceRefs.ToList(),
                File = Path.GetRelativePath(currentDir, testFilePath).Replace('\\', '/')
            });
        }

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Console.Out.WriteLine(json);
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
            if (!File.Exists(full)) continue;
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

/// <summary>
/// One entry in the repair batch manifest.
/// </summary>
public sealed class RepairBatchEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("source_refs")]
    public required IReadOnlyList<string> SourceRefs { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }
}
