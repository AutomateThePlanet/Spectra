using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Criteria;
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
        var keepBaselineFlag = new Option<bool>("--keep-baseline",
            "Keep the existing source-change baseline (critic repairs); only add entries for new refs and criteria (Spec 085)");

        AddArgument(suiteArgument);
        AddOption(testIdOption);
        AddOption(fromOption);
        AddOption(allFlag);
        AddOption(keepBaselineFlag);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForArgument(suiteArgument);
            var testId = context.ParseResult.GetValueForOption(testIdOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var mode = context.ParseResult.GetValueForOption(keepBaselineFlag) ? RecordMode.KeepBaseline : RecordMode.Rebuild;
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
                context.ExitCode = await RunBatchAsync(suite, json, mode, context.GetCancellationToken());
            }
            else
            {
                if (string.IsNullOrEmpty(testId))
                {
                    Console.Error.WriteLine("--test-id is required (unless using --all).");
                    context.ExitCode = ExitError;
                    return;
                }
                context.ExitCode = await RunAsync(suite, testId, from, json, mode, context.GetCancellationToken());
            }
        });
    }

    private static async Task<int> RunAsync(string suite, string testId, string? from, bool json, RecordMode mode, CancellationToken ct)
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

        // Spec 084 FR-027: lock and strict baseline load before persisting; replace the edited test's entry after.
        IngestResult result;
        BaselineWriteSummary? baselineSummary = null;
        BaselineStoreException? afterPersist = null;
        var warnings = new List<string>();
        try
        {
            result = await BaselineStore.WithLockAsync(currentDir, async () =>
            {
                var baseline = await BaselineStore.LoadSuiteAsync(currentDir, testsPath, suite, ct);
                var ingested = await ingestor.IngestAsync(content, testsPath, suite, original, existingTests, ct);

                // Spec 088 FR-015: an unchanged edit is a review — its baseline is rebuilt (kept with --keep-baseline).
                IReadOnlyList<TestCase> record = ingested.Unchanged
                    ? (mode == RecordMode.Rebuild ? [ingested.Original!] : [])
                    : ingested.PersistedTests;
                if (ingested.IsSuccess && record.Count > 0)
                {
                    (baselineSummary, afterPersist) = await RecordAsync(
                        currentDir, config, testsPath, suite, baseline, record, mode, warnings, ct);
                }

                return ingested;
            }, ct);
        }
        catch (BaselineStoreException ex)
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-update", ex.ErrorCode, ex.Message + " Nothing was persisted.");
            return ExitError;
        }

        if (result.IsSuccess && afterPersist is not null)
        {
            var ids = result.Unchanged ? [original.Id] : result.PersistedTests.Select(t => t.Id).ToList();
            return WriteAfterPersistFailure(json, suite, ids, afterPersist);
        }

        if (result is { IsSuccess: true, Unchanged: true })
        {
            // Spec 088 FR-015: reviewed, no edit needed — nothing written, the baseline rebuilt (kept with --keep-baseline).
            BaselineCommandIO.PrintWarnings(warnings);
            var summary = baselineSummary ?? new BaselineWriteSummary { Mode = mode };
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    persisted = 0,
                    unchanged = true,
                    id = original.Id,
                    baseline = summary.ToJson(),
                    warnings
                }));
            }
            else
            {
                Console.Out.WriteLine(mode == RecordMode.Rebuild
                    ? $"Test '{original.Id}' needs no change; its baseline was refreshed."
                    : $"Test '{original.Id}' needs no change; its baseline was kept (--keep-baseline).");
                Console.Out.WriteLine(summary.ToHuman());
            }
            return ExitSuccess;
        }

        if (result.IsSuccess)
        {
            var id = result.PersistedTests.Count > 0 ? result.PersistedTests[0].Id : original.Id;
            BaselineCommandIO.PrintWarnings(warnings);
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    persisted = result.PersistedTests.Count,
                    unchanged = false,
                    id,
                    baseline = baselineSummary!.ToJson(),
                    warnings
                }));
            }
            else
            {
                Console.Out.WriteLine($"Updated test '{id}' in suite '{suite}' (id preserved).");
                Console.Out.WriteLine(baselineSummary!.ToHuman());
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

    private static async Task<int> RunBatchAsync(string suite, bool json, RecordMode mode, CancellationToken ct)
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
            EmitBatchResult(json, suite, []);
            return ExitSuccess;
        }

        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);
        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        var persistence = new TestPersistenceService(
            new TestFileWriter(), new IndexGenerator(), new IndexWriter());
        var ingestor = new UpdatedTestIngestor(persistence);

        var outcomes = new List<StagedOutcome>();
        BaselineWriteSummary? baselineSummary = null;
        BaselineStoreException? afterPersist = null;
        var warnings = new List<string>();

        // Spec 084 FR-027: one baseline lock for the batch; strict load before the first persist.
        try
        {
            await BaselineStore.WithLockAsync(currentDir, async () =>
            {
                var baseline = await BaselineStore.LoadSuiteAsync(currentDir, testsPath, suite, ct);
                await IngestStagedAsync();

                // Spec 088 FR-015: an unchanged edit is a review — its baseline is rebuilt like a written one's.
                var record = outcomes
                    .Where(o => o.Outcome == StagedOutcome.Written
                        || (o.Outcome == StagedOutcome.Unchanged && mode == RecordMode.Rebuild))
                    .Select(o => o.Test!)
                    .ToList();
                if (record.Count > 0)
                {
                    (baselineSummary, afterPersist) = await RecordAsync(
                        currentDir, config, testsPath, suite, baseline, record, mode, warnings, ct);
                }

                return 0;
            }, ct);
        }
        catch (BaselineStoreException ex)
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-update", ex.ErrorCode, ex.Message + " Nothing was persisted.");
            return ExitError;
        }

        if (afterPersist is not null)
        {
            // Spec 088 FR-016: no staging file is deleted when the baseline was not recorded.
            var ids = outcomes.Where(o => o.Outcome != StagedOutcome.Failed).Select(o => o.TestId).ToList();
            return WriteAfterPersistFailure(json, suite, ids, afterPersist);
        }

        // Spec 088 FR-016: the staging files of consumed edits (written or unchanged) are deleted; failed ones stay.
        foreach (var consumed in outcomes.Where(o => o.Outcome != StagedOutcome.Failed))
        {
            try
            {
                File.Delete(consumed.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not delete the staging file {consumed.File}: {ex.Message}");
            }
        }

        BaselineCommandIO.PrintWarnings(warnings);
        EmitBatchResult(json, suite, outcomes, baselineSummary ?? new BaselineWriteSummary { Mode = mode }, warnings);
        return ExitSuccess;

        async Task IngestStagedAsync()
        {
            var stagingFiles = Directory.GetFiles(stagingDir, "updated-*.json").OrderBy(f => f, StringComparer.Ordinal);
            foreach (var sf in stagingFiles)
            {
                ct.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(sf);
                var testId = fileName.StartsWith("updated-", StringComparison.OrdinalIgnoreCase)
                    ? fileName["updated-".Length..] : null;
                if (string.IsNullOrEmpty(testId)) continue;

                var file = Path.GetRelativePath(currentDir, sf).Replace(Path.DirectorySeparatorChar, '/');
                var original = existingTests.FirstOrDefault(t =>
                    string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
                if (original is null)
                {
                    outcomes.Add(StagedOutcome.Fail(testId, sf, file, StagedOutcome.NoOriginal,
                        $"No test '{testId}' in suite '{suite}'; ingest-update edits an existing test."));
                    continue;
                }

                string content;
                try
                {
                    content = await File.ReadAllTextAsync(sf, ct);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    outcomes.Add(StagedOutcome.Fail(original.Id, sf, file, StagedOutcome.Unreadable, ex.Message));
                    continue;
                }

                var stagedId = StagedId(content);
                if (stagedId is not null && !string.Equals(stagedId, original.Id, StringComparison.OrdinalIgnoreCase))
                {
                    outcomes.Add(StagedOutcome.Fail(original.Id, sf, file, StagedOutcome.IdMismatch,
                        $"The staged test's id '{stagedId}' differs from the file name's '{original.Id}'."));
                    continue;
                }

                IngestResult result;
                try
                {
                    result = await ingestor.IngestAsync(content, testsPath, suite, original, existingTests, ct);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    outcomes.Add(StagedOutcome.Fail(original.Id, sf, file, StagedOutcome.PersistFailed, ex.Message));
                    continue;
                }

                if (!result.IsSuccess)
                {
                    outcomes.Add(StagedOutcome.Fail(original.Id, sf, file, result.ErrorCode!, [.. result.Errors]));
                }
                else if (result.Unchanged)
                {
                    outcomes.Add(new StagedOutcome(original.Id, sf, file, StagedOutcome.Unchanged, result.Original));
                }
                else
                {
                    // Later edits in the batch regenerate the index from the already-edited tests.
                    var edited = result.PersistedTests[0];
                    outcomes.Add(new StagedOutcome(original.Id, sf, file, StagedOutcome.Written, edited));
                    existingTests = existingTests.Select(t => ReferenceEquals(t, original) ? edited : t).ToList();
                }
            }
        }
    }

    /// <summary>Spec 088: the <c>id</c> of the staged array's first object, or null when it can't be read.</summary>
    private static string? StagedId(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var first = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 ? root[0] : root;
            return first.ValueKind == JsonValueKind.Object
                && first.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null; // the ingestor reports malformed content
        }
    }

    /// <summary>Spec 084: records the persisted tests' entries; an I/O failure here is reported, not thrown.</summary>
    private static async Task<(BaselineWriteSummary?, BaselineStoreException?)> RecordAsync(
        string root, SpectraConfig config, string testsPath, string suite, Spectra.Core.Baseline.SourceBaseline baseline,
        IReadOnlyList<TestCase> persisted, RecordMode mode, List<string> warnings, CancellationToken ct)
    {
        try
        {
            return (await BaselineRecorder.RecordPersistedAsync(root, config, testsPath, suite, baseline, persisted, mode, warnings, ct), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BaselineStoreException)
        {
            return (null, ex as BaselineStoreException ?? new BaselineStoreException(BaselineStore.ErrorBaselineWriteFailed, ex.Message));
        }
    }

    private static int WriteAfterPersistFailure(bool json, string suite, List<string> ids, BaselineStoreException error)
    {
        CriteriaCommandIO.WriteFailure(json, "ingest-update", error.ErrorCode,
            $"The tests were persisted ({string.Join(", ", ids)}) but their baseline was not recorded: {error.Message} "
            + $"Run 'spectra ai update-ack --suite {suite} --ids {string.Join(",", ids)}' to record it.",
            extras: new Dictionary<string, object?> { ["persisted"] = ids });
        return ExitError;
    }

    private static void EmitBatchResult(
        bool json, string suite, IReadOnlyList<StagedOutcome> outcomes, BaselineWriteSummary? baseline = null,
        IReadOnlyList<string>? warnings = null)
    {
        var written = outcomes.Count(o => o.Outcome == StagedOutcome.Written);
        var unchanged = outcomes.Count(o => o.Outcome == StagedOutcome.Unchanged);
        var skippedNoOriginal = outcomes.Count(o => o.ErrorCode == StagedOutcome.NoOriginal);
        var errors = outcomes.Count(o => o.Outcome == StagedOutcome.Failed && o.ErrorCode != StagedOutcome.NoOriginal);

        // Spec 088: the per-test summary is never suppressed — stdout in human mode, stderr in JSON mode.
        var human = json ? Console.Error : Console.Out;
        human.WriteLine(
            $"Batch update-ingest for suite '{suite}': " +
            $"written={written} unchanged={unchanged} skipped_no_original={skippedNoOriginal} errors={errors}");
        foreach (var o in outcomes)
        {
            human.WriteLine(o.Outcome switch
            {
                StagedOutcome.Written => $"  {o.TestId}: updated",
                StagedOutcome.Unchanged => $"  {o.TestId}: unchanged (reviewed, no edit needed)",
                _ => $"  {o.TestId}: failed [{o.ErrorCode}] {string.Join(" ", o.Errors)} (kept {o.File})"
            });
        }

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "ingest-update",
                mode = "batch",
                suite,
                written,
                unchanged,
                skipped_no_original = skippedNoOriginal,
                errors,
                results = outcomes.Select(o => o.ToJson()).ToList(),
                baseline = (baseline ?? new BaselineWriteSummary()).ToJson(),
                warnings = warnings ?? []
            }));
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

/// <summary>Spec 088 FR-016: what <c>ingest-update --all</c> did with one staging file.</summary>
internal sealed record StagedOutcome(string TestId, string Path, string File, string Outcome, TestCase? Test = null)
{
    public const string Written = "written";
    public const string Unchanged = "unchanged";
    public const string Failed = "failed";

    public const string NoOriginal = "NO_ORIGINAL";
    public const string Unreadable = "UNREADABLE";
    public const string IdMismatch = "ID_MISMATCH";
    public const string PersistFailed = "PERSIST_FAILED";

    public string? ErrorCode { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static StagedOutcome Fail(string testId, string path, string file, string code, params string[] errors) =>
        new(testId, path, file, Failed) { ErrorCode = code, Errors = errors };

    public object ToJson() => Outcome == Failed
        ? new { test = TestId, file = File, outcome = Outcome, error_code = ErrorCode, errors = Errors }
        : new { test = TestId, file = File, outcome = Outcome };
}
