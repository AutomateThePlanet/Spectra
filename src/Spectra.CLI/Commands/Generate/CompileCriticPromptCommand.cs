using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 055: <c>spectra ai compile-critic-prompt</c>. Deterministic, model-free compiler that emits
/// the critic verification prompt for one generated test. Writes nothing to disk; no model call.
/// Mirrors <see cref="CompilePromptCommand"/> / <see cref="CompileExtractionPromptCommand"/>.
///
/// The <c>spectra-critic</c> <c>context: fork</c> subagent runs the emitted prompt in a fresh,
/// isolated context (artifact + selected source docs only) and hands its verdict JSON to
/// <see cref="IngestVerdictCommand"/>.
/// </summary>
public sealed class CompileCriticPromptCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitRefused = 4;
    private const int ExitError = 1;

    public CompileCriticPromptCommand()
        : base("compile-critic-prompt",
            "Compile a critic verification prompt for a generated test (deterministic, no model call)")
    {
        // Spec 071 (critic-seam cleanup): when --suite is set, --test is interpreted as a test ID
        // (resolved to test-cases/{suite}/{id}.md via _index.json) instead of a filesystem path. This
        // closes the ID→path gap the critic step hit. The legacy bare --test <path> form is preserved.
        var suiteOption = new Option<string?>(["--suite", "-s"], "Suite to resolve the test(s) from (makes --test an ID, not a path; omit --test to emit all)");
        var testOption = new Option<string?>(["--test", "-t"], "Test artifact: a path (legacy) or, with --suite, the test ID to resolve");
        var docsOption = new Option<string?>(["--docs", "-d"], "Source document(s) to ground against (file or directory)");

        AddOption(suiteOption);
        AddOption(testOption);
        AddOption(docsOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var test = context.ParseResult.GetValueForOption(testOption);
            var docs = context.ParseResult.GetValueForOption(docsOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, test, docs, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? suite, string? testPath, string? docsPath, bool json, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Suite-aware resolution (Spec 071): the agent supplies the suite name (and optionally the test
        // ID) it already knows; the command resolves the test file(s) from _index.json on disk — no
        // hand-built paths, no enumeration. This branch runs BEFORE the legacy path check below.
        if (!string.IsNullOrWhiteSpace(suite))
        {
            return await RunSuiteAsync(suite, testPath, docsPath, currentDir, json, ct);
        }

        if (string.IsNullOrWhiteSpace(testPath))
        {
            EmitRefusal(json, "test_artifact", "A test artifact path is required (--test).");
            return ExitRefused;
        }

        var testFull = Path.IsPathRooted(testPath) ? testPath : Path.Combine(currentDir, testPath);
        if (!File.Exists(testFull))
        {
            Console.Error.WriteLine($"Test artifact not found: {testPath}");
            return ExitError;
        }

        TestCase? test;
        try
        {
            test = await LoadTestAsync(testFull, currentDir, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading test artifact: {ex.Message}");
            return ExitError;
        }

        IReadOnlyList<SourceDocument> docs;
        try
        {
            docs = await LoadDocumentsAsync(docsPath, currentDir, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading documents: {ex.Message}");
            return ExitError;
        }

        var result = CriticPromptCompiler.Compile(test, docs);
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

    /// <summary>
    /// Spec 071: resolves the test set for a suite from <c>test-cases/{suite}/_index.json</c> and emits the
    /// critic prompt(s). With <paramref name="testId"/> set, targets one test BY ID; otherwise emits all.
    /// Fails loud (exit 1) when the suite/index or the requested id cannot be resolved — never silently skips.
    /// </summary>
    private static async Task<int> RunSuiteAsync(
        string suite, string? testId, string? docsPath, string currentDir, bool json, CancellationToken ct)
    {
        var (testsDir, config) = await ResolveTestsDirAndConfigAsync(currentDir, ct);
        var suitePath = Path.Combine(currentDir, testsDir, suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);

        MetadataIndex? index;
        try
        {
            index = await new IndexWriter().ReadAsync(indexPath, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading suite index '{indexPath}': {ex.Message}");
            return ExitError;
        }
        if (index is null || index.Tests.Count == 0)
        {
            Console.Error.WriteLine($"Suite '{suite}' not found or has no indexed tests ({indexPath}).");
            return ExitError;
        }

        IReadOnlyList<TestIndexEntry> targets;
        if (!string.IsNullOrWhiteSpace(testId))
        {
            var entry = index.Tests.FirstOrDefault(t => string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                Console.Error.WriteLine($"Test id '{testId}' not found in suite '{suite}' ({indexPath}).");
                return ExitError;
            }
            targets = [entry];
        }
        else
        {
            targets = index.Tests;
        }

        // When --docs is provided, load once and share across all tests. Otherwise docs are resolved
        // per-test from each test's source_refs (P1: auto-grounding from the test frontmatter).
        IReadOnlyList<SourceDocument>? sharedDocs = null;
        if (!string.IsNullOrWhiteSpace(docsPath))
        {
            try
            {
                sharedDocs = await LoadDocumentsAsync(docsPath, currentDir, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading documents: {ex.Message}");
                return ExitError;
            }
        }

        // Load criteria for this suite once — mirrors the generation path (CompilePromptCommand).
        var criteria = await CriteriaContextLoader.LoadCriteriaContextAsync(
            currentDir, suite, config, ct);

        var compiled = new List<(string Id, string Prompt)>();
        foreach (var entry in targets)
        {
            var full = Path.Combine(suitePath, entry.File);
            if (!File.Exists(full))
            {
                Console.Error.WriteLine($"Test artifact not found for id '{entry.Id}' in suite '{suite}': {full}");
                return ExitError;
            }

            TestCase? test;
            try
            {
                test = await LoadTestAsync(full, currentDir, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading test artifact '{entry.Id}': {ex.Message}");
                return ExitError;
            }

            // Resolve the source docs for this test: --docs override > auto-resolve from source_refs.
            IReadOnlyList<SourceDocument> docs;
            if (sharedDocs is not null)
            {
                docs = sharedDocs;
            }
            else if (test?.SourceRefs.Count > 0)
            {
                try
                {
                    docs = await LoadDocumentsFromRefsAsync(test.SourceRefs, currentDir, ct);
                }
                catch (FileNotFoundException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return ExitError;
                }
            }
            else
            {
                docs = [];
            }

            var result = CriticPromptCompiler.Compile(test, docs, criteria.Context);
            if (!result.IsSuccess)
            {
                EmitRefusal(json, result.MissingInput!, result.Message!);
                return ExitRefused;
            }
            compiled.Add((entry.Id, result.Prompt!));
        }

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                prompts = compiled.Select(c => new { id = c.Id, prompt = c.Prompt }).ToArray()
            }));
        }
        else if (compiled.Count == 1)
        {
            // Byte-identical to the legacy single-prompt output so the critic subagent (raw stdout) is unaffected.
            Console.Out.Write(compiled[0].Prompt);
            if (!compiled[0].Prompt.EndsWith('\n')) Console.Out.WriteLine();
        }
        else
        {
            foreach (var (id, prompt) in compiled)
            {
                Console.Out.WriteLine($"===== CRITIC PROMPT: {id} =====");
                Console.Out.Write(prompt);
                if (!prompt.EndsWith('\n')) Console.Out.WriteLine();
            }
        }
        return ExitSuccess;
    }

    /// <summary>
    /// Resolves the configured tests directory (default <c>test-cases</c>) from spectra.config.json if present.
    /// Lenient by design: a missing/unparseable config falls back to the default, mirroring the sibling compilers.
    /// </summary>
    private static async Task<(string Dir, SpectraConfig? Config)> ResolveTestsDirAndConfigAsync(
        string currentDir, CancellationToken ct)
    {
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
            return ("test-cases", null);

        try
        {
            var configJson = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SpectraConfig>(configJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return (config?.Tests?.Dir ?? "test-cases", config);
        }
        catch (JsonException)
        {
            return ("test-cases", null);
        }
    }

    private static async Task<TestCase?> LoadTestAsync(string fullPath, string currentDir, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(fullPath, ct);

        if (fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var rel = Path.GetRelativePath(currentDir, fullPath);
            var parsed = new TestCaseParser().Parse(content, rel);
            return parsed.IsSuccess ? parsed.Value : null;
        }

        // Generated-test JSON: a single object, or the first element of an array.
        using var doc = JsonDocument.Parse(content);
        var el = doc.RootElement.ValueKind == JsonValueKind.Array
            ? (doc.RootElement.GetArrayLength() > 0 ? doc.RootElement[0] : default)
            : doc.RootElement;
        return el.ValueKind == JsonValueKind.Object ? FromJson(el) : null;
    }

    private static TestCase? FromJson(JsonElement el)
    {
        var id = el.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        var title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
            return null;

        return new TestCase
        {
            Id = id,
            Title = title,
            Priority = Priority.Medium,
            Preconditions = el.TryGetProperty("preconditions", out var pre) ? pre.GetString() : null,
            Steps = ReadStringArray(el, "steps"),
            ExpectedResult = el.TryGetProperty("expected_result", out var er) ? er.GetString() ?? "" : "",
            TestData = el.TryGetProperty("test_data", out var td) ? td.GetString() : null,
            SourceRefs = ReadStringArray(el, "source_refs"),
            FilePath = $"{id}.md"
        };
    }

    private static List<string> ReadStringArray(JsonElement element, string property)
    {
        var values = new List<string>();
        if (element.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                if (item.GetString() is string s) values.Add(s);
        }
        return values;
    }

    /// <summary>
    /// Resolves each source_ref to its file on disk (stripping any fragment) and loads its content.
    /// Deduplicates by resolved path. Throws <see cref="FileNotFoundException"/> when a ref cannot
    /// be found — callers should surface this as a distinct error (not a silent empty list).
    /// </summary>
    private static async Task<IReadOnlyList<SourceDocument>> LoadDocumentsFromRefsAsync(
        IReadOnlyList<string> sourceRefs, string currentDir, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var docs = new List<SourceDocument>();
        foreach (var r in sourceRefs)
        {
            var file = r.Split('#')[0].Trim();
            if (string.IsNullOrWhiteSpace(file))
                continue;
            var full = Path.IsPathRooted(file) ? file : Path.Combine(currentDir, file);
            if (!seen.Add(Path.GetFullPath(full)))
                continue;
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

    private static async Task<IReadOnlyList<SourceDocument>> LoadDocumentsAsync(
        string? docsPath, string currentDir, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(docsPath))
            return [];

        var full = Path.IsPathRooted(docsPath) ? docsPath : Path.Combine(currentDir, docsPath);
        var files = new List<string>();
        if (Directory.Exists(full))
            files.AddRange(Directory.GetFiles(full, "*.md", SearchOption.AllDirectories));
        else if (File.Exists(full))
            files.Add(full);

        // Deterministic order (FR-002): order by path.
        files.Sort(StringComparer.Ordinal);

        var docs = new List<SourceDocument>();
        foreach (var f in files)
        {
            var content = await File.ReadAllTextAsync(f, ct);
            docs.Add(new SourceDocument
            {
                Path = Path.GetRelativePath(currentDir, f).Replace('\\', '/'),
                Title = Path.GetFileNameWithoutExtension(f),
                Content = content
            });
        }
        return docs;
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
