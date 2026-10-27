using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 071: <c>spectra ai ingest-grounding</c>. Reads a per-test critic verdict JSON and writes
/// the condensed grounding block into the named test's .md file. Deterministic and model-free.
/// Refuses hallucinated verdicts (exit 4) — those must be handled by record-drop + delete.
/// Spec 073: adds <c>--all</c> batch form to write grounding for all eligible tests in one pass,
/// eliminating per-test shell loops. Partial verdicts are skipped unless <c>--repaired</c> is set
/// (pre-repair filter: a partial test needs one repair attempt before its block is written).
/// Spec 091 (F2): <c>--all --ids a,b</c> limits the batch to the listed tests (unknown ones → <c>not_found</c>);
/// <c>--all --repaired</c> requires it (<c>IDS_REQUIRED</c>), so only the tests re-critiqued after a repair are marked
/// repaired. Spec 091 (F3): in <c>--all</c>, a hallucinated verdict for a test that still exists is reported under
/// <c>hallucinated</c> (kept for review), not counted as an error.
/// </summary>
public sealed class IngestGroundingCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitHallucinatedRefused = 4; // refusals: hallucinated; Spec 090: MANUAL_VERDICT, VERDICT_STALE

    /// <summary>Spec 090 (FR-A3): a Manual verdict is a human judgement the critic never replaces.</summary>
    public const string ErrorManualVerdict = "MANUAL_VERDICT";

    /// <summary>Spec 091 (FR-007): <c>--all --repaired</c> names the tests that were re-critiqued after the repair.</summary>
    public const string ErrorIdsRequired = "IDS_REQUIRED";

    // Spec 091 (FR-006a): usage errors, plain stderr in both output modes (like the existing "Either …" message).
    internal const string TestWithAllMessage = "Use either --test <id> or --all, not both.";
    internal const string IdsWithoutAllMessage = IdList.IdsWithoutAllMessage;
    internal const string NeitherMessage = "Either --test <id> or --all must be specified.";
    private const int ExitVerdictEmpty = 5;
    private const int ExitVerdictParseFailed = 6;

    public IngestGroundingCommand()
        : base("ingest-grounding", "Write critic verdict as grounding block to a test .md file (Spec 071, no model call)")
    {
        var suiteOption = new Option<string>(["--suite", "-s"], "Suite name to resolve the test from") { IsRequired = true };
        var testOption = new Option<string?>(["--test", "-t"], "Test ID to write grounding for (e.g., TC-113); omit when using --all");
        var allFlag = new Option<bool>("--all", "Batch mode: write grounding for all eligible tests in the suite (Spec 073)");
        var fromOption = new Option<string?>("--from", "Verdict JSON file path (default: .spectra/verdicts/critic-verdict-{id}.json)");
        var repairedFlag = new Option<bool>("--repaired", "Mark test as repaired (sets repaired: true in the grounding block)");
        var repairAttemptsOption = new Option<int>("--repair-attempts", () => 0, "Number of repair attempts performed");
        var idsOption = new Option<string?>("--ids", "With --all: only these tests (comma-separated); required with --all --repaired (Spec 091)");

        AddOption(suiteOption);
        AddOption(testOption);
        AddOption(allFlag);
        AddOption(fromOption);
        AddOption(repairedFlag);
        AddOption(repairAttemptsOption);
        AddOption(idsOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var testId = context.ParseResult.GetValueForOption(testOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var repaired = context.ParseResult.GetValueForOption(repairedFlag);
            var repairAttempts = context.ParseResult.GetValueForOption(repairAttemptsOption);
            var idsGiven = IdList.IsGiven(context.ParseResult.GetValueForOption(idsOption)); // Spec 097: empty = not given
            var ids = ParseIds(context.ParseResult.GetValueForOption(idsOption));
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var json = outputFormat == OutputFormat.Json;
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            // Spec 091 (FR-006a, FR-007): argument checks, in order, before any config, index or verdict is read.
            if (all && !string.IsNullOrEmpty(testId))
            {
                Console.Error.WriteLine(TestWithAllMessage);
                context.ExitCode = ExitError;
            }
            else if (idsGiven && !all)
            {
                Console.Error.WriteLine(IdsWithoutAllMessage);
                context.ExitCode = ExitError;
            }
            else if (!all && string.IsNullOrEmpty(testId))
            {
                Console.Error.WriteLine(NeitherMessage);
                context.ExitCode = ExitError;
            }
            else if (!all && dryRun)
            {
                // Spec 097 (FR-018): a dry run never writes, and only the batch form supports it.
                Console.Error.WriteLine(DryRunJson.RequiresAllMessage);
                context.ExitCode = ExitError;
            }
            else if (all && repaired && ids.Count == 0)
            {
                CriteriaCommandIO.WriteFailure(json, "ingest-grounding", ErrorIdsRequired,
                    "--all --repaired needs --ids with the tests re-critiqued after the repair; nothing was written.");
                context.ExitCode = ExitHallucinatedRefused;
            }
            else if (all)
            {
                context.ExitCode = await RunBatchAsync(
                    suite, repaired, repairAttempts, idsGiven ? ids : null, json, dryRun, context.GetCancellationToken());
            }
            else
            {
                context.ExitCode = await RunAsync(suite, testId!, from, repaired, repairAttempts, json, context.GetCancellationToken());
            }
        });
    }

    /// <summary>Spec 091 (FR-006): <c>--ids</c> split on commas, trimmed, empties dropped, de-duplicated case-insensitively.</summary>
    internal static IReadOnlyList<string> ParseIds(string? value) => IdList.Parse(value);

    private static async Task<int> RunBatchAsync(
        string suite, bool repaired, int repairAttempts, IReadOnlyList<string>? ids, bool json, bool dryRun, CancellationToken ct)
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

        var indexLookup = index.Tests
            .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

        // Spec 091 (FR-006): only the listed tests are considered; listed ids not in the suite are reported.
        var listed = ids is null ? null : new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        var notFound = ids?.Where(id => !indexLookup.ContainsKey(id)).ToList() ?? [];

        var verdictDir = Path.Combine(currentDir, ".spectra", "verdicts");
        if (!Directory.Exists(verdictDir))
        {
            EmitBatchResult(json, dryRun, suite, 0, 0, 0, 0, [], [], [], notFound);
            return ExitSuccess;
        }

        var parser = new TestCaseParser(captureLayout: false);
        var service = new GroundingWriteBackService(new TestFileWriter());
        int written = 0, skippedAlreadyWritten = 0, skippedPartialPreRepair = 0, errors = 0;
        var manual = new List<string>();
        var stale = new List<string>();
        var hallucinated = new List<string>();

        var verdictFiles = Directory.GetFiles(verdictDir, "critic-verdict-*.json").OrderBy(f => f);
        foreach (var vf in verdictFiles)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(vf);
            var testId = fileName.StartsWith("critic-verdict-", StringComparison.OrdinalIgnoreCase)
                ? fileName["critic-verdict-".Length..] : null;
            if (string.IsNullOrEmpty(testId)) continue;
            if (listed is not null && !listed.Contains(testId)) continue;

            // Filter to this suite via index lookup
            if (!indexLookup.TryGetValue(testId, out var testEntry)) continue;

            // Resolve test file — uses suitePath (matches GeneratedTestIngestor.ParseTestCase format)
            var testFilePath = Path.Combine(suitePath, testEntry.File);
            if (!File.Exists(testFilePath)) continue; // hallucinated — file gone, skip silently

            TestCase? existing = null;
            try
            {
                var existingContent = await File.ReadAllTextAsync(testFilePath, ct);
                var rel = Path.GetRelativePath(suitePath, testFilePath);
                var parsedExisting = parser.Parse(existingContent, rel);
                existing = parsedExisting.IsSuccess ? parsedExisting.Value : null;
            }
            catch { /* on parse error, attempt write */ }

            // Spec 090 (FR-A3): a Manual verdict is never overwritten (checked before the already-written skip).
            if (existing?.Grounding?.Verdict == VerificationVerdict.Manual)
            {
                manual.Add(testId);
                continue;
            }

            // Idempotent: skip tests whose .md already has a grounding block
            if (existing?.Grounding is not null)
            {
                skippedAlreadyWritten++;
                continue;
            }

            // Spec 090 (FR-A2) / 091 (FR-002): a verdict given to other content, or older than the last prompt, is not written.
            if (existing is not null && VerdictFreshness.Check(currentDir, testId, existing, vf).IsStale)
            {
                stale.Add(testId);
                continue;
            }

            string verdictJson;
            try { verdictJson = await File.ReadAllTextAsync(vf, ct); }
            catch { errors++; continue; }
            if (string.IsNullOrWhiteSpace(verdictJson)) { errors++; continue; }

            // The verdict is parsed once; if it can't be, WriteAsync refuses it below (an error).
            var verdictStr = VerdictOf(verdictJson);

            // Spec 091 (FR-010): a hallucinated verdict for a test that still exists is a test kept for review (the
            // update flow leaves that decision to the user) — reported by id, never written, not an error.
            if (string.Equals(verdictStr, "hallucinated", StringComparison.OrdinalIgnoreCase))
            {
                hallucinated.Add(testId);
                continue;
            }

            // Pre-repair filter: without --repaired, skip partial verdicts.
            // Partial tests need one repair attempt before their grounding block is written.
            if (!repaired && string.Equals(verdictStr, "partial", StringComparison.OrdinalIgnoreCase))
            {
                skippedPartialPreRepair++;
                continue;
            }

            // Write grounding block — reuses same service as per-test mode (no code duplication)
            var result = await service.WriteAsync(testFilePath, verdictJson, repairAttempts, repaired, ct, dryRun);
            if (result.IsSuccess) written++;
            else errors++;
        }

        EmitBatchResult(json, dryRun, suite, written, skippedAlreadyWritten, skippedPartialPreRepair, errors, manual, stale, hallucinated, notFound);
        return ExitSuccess;
    }

    /// <summary>The verdict string of a critic JSON, or null when it can't be parsed.</summary>
    private static string? VerdictOf(string verdictJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(verdictJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("verdict", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void EmitBatchResult(bool json, bool dryRun, string suite, int written,
        int skippedAlreadyWritten, int skippedPartialPreRepair, int errors,
        IReadOnlyList<string> manual, IReadOnlyList<string> stale, IReadOnlyList<string> hallucinated,
        IReadOnlyList<string> notFound)
    {
        if (json)
        {
            Console.Out.WriteLine(DryRunJson.Serialize(new
            {
                command = "ingest-grounding",
                mode = "batch",
                suite,
                written,
                skipped_already_written = skippedAlreadyWritten,
                skipped_partial_pre_repair = skippedPartialPreRepair,
                errors,
                skipped_manual = manual.Count,
                manual,
                skipped_stale = stale.Count,
                stale,
                skipped_hallucinated = hallucinated.Count,
                hallucinated,
                not_found = notFound
            }, dryRun));
        }

        // The human summary: stdout in human mode, stderr in JSON mode (never suppressed).
        var human = json ? Console.Error : Console.Out;
        human.WriteLine(
            $"Batch grounding-ingest for suite '{suite}': written={written} " +
            $"skipped_already_written={skippedAlreadyWritten} " +
            $"skipped_partial_pre_repair={skippedPartialPreRepair} errors={errors}" +
            (manual.Count > 0 ? $" skipped_manual={manual.Count} ({string.Join(", ", manual)})" : "") +
            (stale.Count > 0 ? $" skipped_stale={stale.Count} ({string.Join(", ", stale)})" : "") +
            (hallucinated.Count > 0 ? $" skipped_hallucinated={hallucinated.Count}" : ""));
        foreach (var id in hallucinated)
        {
            human.WriteLine($"{id}: hallucinated, waiting for review");
        }

        foreach (var id in notFound)
        {
            human.WriteLine($"{id}: not found in suite '{suite}'");
        }
    }

    private static async Task<int> RunAsync(
        string suite, string testId, string? from, bool repaired, int repairAttempts,
        bool json, CancellationToken ct)
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

        var verdictPath = from ?? Path.Combine(currentDir, ".spectra", "verdicts", $"critic-verdict-{testId}.json");
        if (!File.Exists(verdictPath))
        {
            Console.Error.WriteLine($"Verdict file not found: {verdictPath}");
            return ExitVerdictEmpty;
        }

        var verdictJson = await File.ReadAllTextAsync(verdictPath, ct);
        if (string.IsNullOrWhiteSpace(verdictJson))
        {
            Console.Error.WriteLine($"Verdict file is empty: {verdictPath}");
            return ExitVerdictEmpty;
        }

        // Spec 090 (FR-A3, FR-A2): never overwrite a Manual verdict; never write a verdict given to other content.
        var current = new TestCaseParser(captureLayout: false).Parse(await File.ReadAllTextAsync(testFilePath, ct), entry.File);
        if (current.IsSuccess && current.Value!.Grounding?.Verdict == VerificationVerdict.Manual)
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-grounding", ErrorManualVerdict,
                $"{testId} has a Manual verdict; the critic never replaces it. Nothing was written.");
            return ExitHallucinatedRefused;
        }

        if (current.IsSuccess && VerdictFreshness.Check(currentDir, testId, current.Value!, verdictPath) is { IsStale: true } stale)
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-grounding", VerdictFreshness.ErrorVerdictStale,
                stale.Message(testId) + " Nothing was written.");
            return ExitHallucinatedRefused;
        }

        var service = new GroundingWriteBackService(new TestFileWriter());
        var result = await service.WriteAsync(testFilePath, verdictJson, repairAttempts, repaired, ct);

        if (result.IsSuccess)
        {
            var g = result.Grounding!;
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    id = testId,
                    suite,
                    verdict = g.Verdict.ToString().ToLowerInvariant(),
                    score = g.Score,
                    flagged_for_review = g.FlaggedForReview,
                    repair_attempts = g.RepairAttempts,
                    repaired = g.Repaired
                }));
            }
            else
            {
                Console.Out.WriteLine($"Grounding written for {testId} in suite '{suite}': verdict={g.Verdict.ToString().ToLowerInvariant()} score={g.Score:F2}");
                if (g.FlaggedForReview)
                    Console.Out.WriteLine($"  Flagged for review ({g.CondensedFindings.Count} condensed finding(s)).");
                if (g.Repaired)
                    Console.Out.WriteLine($"  Repaired after {g.RepairAttempts} attempt(s).");
            }
            return ExitSuccess;
        }

        return result.Outcome switch
        {
            GroundingWriteBackService.WriteBackOutcome.HallucinatedRefused =>
                EmitRefusalAndReturn(json, testId, result.ErrorMessage!, ExitHallucinatedRefused),
            GroundingWriteBackService.WriteBackOutcome.VerdictFailure =>
                EmitRefusalAndReturn(json, testId, result.ErrorMessage!, ExitVerdictParseFailed),
            _ =>
                EmitRefusalAndReturn(json, testId, result.ErrorMessage!, ExitError)
        };
    }

    private static int EmitRefusalAndReturn(bool json, string testId, string message, int exitCode)
    {
        if (json)
            Console.Error.WriteLine(JsonSerializer.Serialize(new { success = false, id = testId, error = message }));
        else
            Console.Error.WriteLine($"ingest-grounding failed for {testId}: {message}");
        return exitCode;
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
