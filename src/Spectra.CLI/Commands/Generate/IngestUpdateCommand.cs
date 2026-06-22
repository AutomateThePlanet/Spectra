using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 063: <c>spectra ai ingest-update</c>. The fail-loud boundary for the inverted update
/// seam. Reads the model's edited test (from <c>--from</c> or stdin), validates it, deterministically
/// protects invariants against the original (id, manual/grounding, drift guard), and persists only
/// when valid and drift-free — keeping the original id (an edit, not a create). On any failure it
/// persists nothing and reports a specific, machine-readable error a retry skill can act on.
///
/// Spec 077: adds <c>--all</c> batch mode that enumerates staged update files in
/// <c>.spectra/updates/{suite}/updated-{id}.json</c> and ingests all in one call.
/// </summary>
public sealed class IngestUpdateCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitContentInvalid = 5; // EMPTY/MALFORMED/TRUNCATED/NO_TESTS/DRIFT_DETECTED
    private const int ExitSchemaInvalid = 6;  // SCHEMA_INVALID

    public IngestUpdateCommand()
        : base("ingest-update", "Validate and persist an edited test, preserving id and manual fields (fail-loud)")
    {
        var suiteArgument = new Argument<string>("suite", "Suite the edited test belongs to");
        var testIdOption = new Option<string?>("--test-id", "Id of the original test being edited (required without --all)");
        var fromOption = new Option<string?>("--from", "File containing the model's edited-test JSON (omit to read stdin)");
        var allFlag = new Option<bool>("--all", "Batch mode: ingest all staged updates from .spectra/updates/{suite}/ (Spec 077)");

        AddArgument(suiteArgument);
        AddOption(testIdOption);
        AddOption(fromOption);
        AddOption(allFlag);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForArgument(suiteArgument);
            var testId = context.ParseResult.GetValueForOption(testIdOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var json = outputFormat == OutputFormat.Json;

            if (all)
            {
                if (!string.IsNullOrEmpty(from) || !string.IsNullOrEmpty(testId))
                {
                    Console.Error.WriteLine("--all is mutually exclusive with --from and --test-id.");
                    context.ExitCode = ExitError;
                    return;
                }
                context.ExitCode = await RunBatchAsync(suite, json, context.GetCancellationToken());
            }
            else
            {
                if (string.IsNullOrEmpty(testId))
                {
                    Console.Error.WriteLine("--test-id is required (unless using --all).");
                    context.ExitCode = ExitError;
                    return;
                }
                context.ExitCode = await RunAsync(suite, testId, from, json, context.GetCancellationToken());
            }
        });
    }

    private static async Task<int> RunAsync(string suite, string testId, string? from, bool json, CancellationToken ct)
    {
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

        // Read edited content from file or stdin.
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

        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);
        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        var original = existingTests.FirstOrDefault(t =>
            string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
        if (original is null)
        {
            Console.Error.WriteLine($"Original test '{testId}' was not found in suite '{suite}'. "
                + "ingest-update edits an existing test — it does not create new ones.");
            return ExitError;
        }

        var persistence = new TestPersistenceService(
            new TestFileWriter(), new IndexGenerator(), new IndexWriter());
        var ingestor = new UpdatedTestIngestor(persistence);

        var result = await ingestor.IngestAsync(content, testsPath, suite, original, existingTests, ct);

        if (result.IsSuccess)
        {
            var id = result.PersistedTests.Count > 0 ? result.PersistedTests[0].Id : original.Id;
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    persisted = result.PersistedTests.Count,
                    id
                }));
            }
            else
            {
                Console.Out.WriteLine($"Updated test '{id}' in suite '{suite}' (id preserved).");
            }
            return ExitSuccess;
        }

        // Fail loud — nothing persisted.
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                error_code = result.ErrorCode,
                errors = result.Errors
            }));
        }
        else
        {
            Console.Error.WriteLine($"Update ingest failed [{result.ErrorCode}] — nothing persisted:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        return result.ErrorCode == IngestErrorCode.SchemaInvalid ? ExitSchemaInvalid : ExitContentInvalid;
    }

    private static async Task<int> RunBatchAsync(string suite, bool json, CancellationToken ct)
    {
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

        var stagingDir = Path.Combine(currentDir, ".spectra", "updates", suite);
        if (!Directory.Exists(stagingDir))
        {
            EmitBatchResult(json, suite, 0, 0, 0);
            return ExitSuccess;
        }

        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);
        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        var persistence = new TestPersistenceService(
            new TestFileWriter(), new IndexGenerator(), new IndexWriter());
        var ingestor = new UpdatedTestIngestor(persistence);

        int written = 0, skippedNoOriginal = 0, errors = 0;

        var stagingFiles = Directory.GetFiles(stagingDir, "updated-*.json").OrderBy(f => f);
        foreach (var sf in stagingFiles)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(sf);
            var testId = fileName.StartsWith("updated-", StringComparison.OrdinalIgnoreCase)
                ? fileName["updated-".Length..] : null;
            if (string.IsNullOrEmpty(testId)) continue;

            var original = existingTests.FirstOrDefault(t =>
                string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
            if (original is null)
            {
                skippedNoOriginal++;
                continue;
            }

            string content;
            try { content = await File.ReadAllTextAsync(sf, ct); }
            catch { errors++; continue; }

            IngestResult result;
            try { result = await ingestor.IngestAsync(content, testsPath, suite, original, existingTests, ct); }
            catch { errors++; continue; }

            if (result.IsSuccess)
                written++;
            else
                errors++;
        }

        EmitBatchResult(json, suite, written, skippedNoOriginal, errors);
        return ExitSuccess;
    }

    private static void EmitBatchResult(bool json, string suite, int written, int skippedNoOriginal, int errors)
    {
        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "ingest-update",
                mode = "batch",
                suite,
                written,
                skipped_no_original = skippedNoOriginal,
                errors
            }));
        }
        else
        {
            Console.Out.WriteLine(
                $"Batch update-ingest for suite '{suite}': " +
                $"written={written} skipped_no_original={skippedNoOriginal} errors={errors}");
        }
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
            var relativePath = Path.GetRelativePath(suitePath, file);
            var parsed = parser.Parse(fileContent, relativePath);
            if (parsed.IsSuccess)
                tests.Add(parsed.Value!);
        }

        return tests;
    }
}
