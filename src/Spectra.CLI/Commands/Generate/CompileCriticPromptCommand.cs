using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;
using Spectra.Core.Models;
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
        var testOption = new Option<string?>(["--test", "-t"], "Path to the test artifact to verify (test .md or generated-test JSON)");
        var docsOption = new Option<string?>(["--docs", "-d"], "Source document(s) to ground against (file or directory)");

        AddOption(testOption);
        AddOption(docsOption);

        this.SetHandler(async (context) =>
        {
            var test = context.ParseResult.GetValueForOption(testOption);
            var docs = context.ParseResult.GetValueForOption(docsOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(test, docs, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? testPath, string? docsPath, bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(testPath))
        {
            EmitRefusal(json, "test_artifact", "A test artifact path is required (--test).");
            return ExitRefused;
        }

        var currentDir = Directory.GetCurrentDirectory();
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
