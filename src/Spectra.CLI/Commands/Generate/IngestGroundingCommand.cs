using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 071: <c>spectra ai ingest-grounding</c>. Reads a per-test critic verdict JSON and writes
/// the condensed grounding block into the named test's .md file. Deterministic and model-free.
/// Refuses hallucinated verdicts (exit 4) — those must be handled by record-drop + delete.
/// Spec 073: adds <c>--all</c> batch form to write grounding for all eligible tests in one pass,
/// eliminating per-test shell loops. Partial verdicts are skipped unless <c>--repaired</c> is set
/// (pre-repair filter: a partial test needs one repair attempt before its block is written).
/// </summary>
public sealed class IngestGroundingCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitHallucinatedRefused = 4;
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
        // Spec 078 FR-003/FR-010 (research R2): without this, the batch path skips every
        // already-grounded test, so a second verification pass writes nothing — preservation is
        // never exercised and suppression never fires. The default skip stays: Specs 073/077 rely on
        // the post-loop batch call being repeat-safe.
        var reVerifyFlag = new Option<bool>("--re-verify",
            "Batch mode: do not skip already-grounded tests. Rewrites each one, preserving its review record and applying the suppression comparison (Spec 078)");

        AddOption(suiteOption);
        AddOption(testOption);
        AddOption(allFlag);
        AddOption(fromOption);
        AddOption(repairedFlag);
        AddOption(repairAttemptsOption);
        AddOption(reVerifyFlag);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var testId = context.ParseResult.GetValueForOption(testOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var repaired = context.ParseResult.GetValueForOption(repairedFlag);
            var repairAttempts = context.ParseResult.GetValueForOption(repairAttemptsOption);
            var reVerify = context.ParseResult.GetValueForOption(reVerifyFlag);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var json = outputFormat == OutputFormat.Json;

            if (all)
            {
                context.ExitCode = await RunBatchAsync(suite, repaired, repairAttempts, reVerify, json, context.GetCancellationToken());
            }
            else if (!string.IsNullOrEmpty(testId))
            {
                context.ExitCode = await RunAsync(suite, testId, from, repaired, repairAttempts, json, context.GetCancellationToken());
            }
            else
            {
                Console.Error.WriteLine("Either --test <id> or --all must be specified.");
                context.ExitCode = ExitError;
            }
        });
    }

    private static async Task<int> RunBatchAsync(
        string suite, bool repaired, int repairAttempts, bool reVerify, bool json, CancellationToken ct)
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

        var verdictDir = Path.Combine(currentDir, ".spectra", "verdicts");
        if (!Directory.Exists(verdictDir))
        {
            EmitBatchResult(json, suite, 0, 0, 0, 0);
            return ExitSuccess;
        }

        var indexLookup = index.Tests
            .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

        var parser = new TestCaseParser();
        var service = new GroundingWriteBackService(new TestFileWriter());
        int written = 0, skippedAlreadyWritten = 0, skippedPartialPreRepair = 0, errors = 0;
        // Spec 078 FR-011: `reraises_suppressed: 0` on a suite with known review records is the
        // signature of the baseline bug — it is the number that proves suppression is actually wired.
        int reraisesSuppressed = 0, reviewRecordsPreserved = 0;
        var suppressedIds = new List<string>();

        var verdictFiles = Directory.GetFiles(verdictDir, "critic-verdict-*.json").OrderBy(f => f);
        foreach (var vf in verdictFiles)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(vf);
            var testId = fileName.StartsWith("critic-verdict-", StringComparison.OrdinalIgnoreCase)
                ? fileName["critic-verdict-".Length..] : null;
            if (string.IsNullOrEmpty(testId)) continue;

            // Filter to this suite via index lookup
            if (!indexLookup.TryGetValue(testId, out var testEntry)) continue;

            // Resolve test file — uses suitePath (matches GeneratedTestIngestor.ParseTestCase format)
            var testFilePath = Path.Combine(suitePath, testEntry.File);
            if (!File.Exists(testFilePath)) continue; // hallucinated — file gone, skip silently

            // Idempotent: skip tests whose .md already has a grounding block.
            // Spec 078 (research R2): --re-verify bypasses this. Without the bypass, a second
            // verification pass over an already-grounded suite writes nothing at all — WriteAsync is
            // never reached, so FR-003's preservation is never exercised and FR-010's suppression
            // never fires. The skip itself must stay for the default path: Specs 073/077 built their
            // non-stop batch flows on this call being repeat-safe.
            if (!reVerify)
            {
                try
                {
                    var existingContent = await File.ReadAllTextAsync(testFilePath, ct);
                    var rel = Path.GetRelativePath(suitePath, testFilePath);
                    var parsedExisting = parser.Parse(existingContent, rel);
                    if (parsedExisting.IsSuccess && parsedExisting.Value is { } existing && existing.Grounding is not null)
                    {
                        skippedAlreadyWritten++;
                        continue;
                    }
                }
                catch { /* on parse error, attempt write */ }
            }

            string verdictJson;
            try { verdictJson = await File.ReadAllTextAsync(vf, ct); }
            catch { errors++; continue; }
            if (string.IsNullOrWhiteSpace(verdictJson)) { errors++; continue; }

            // Pre-repair filter: without --repaired, skip partial verdicts.
            // Partial tests need one repair attempt before their grounding block is written.
            if (!repaired)
            {
                try
                {
                    using var doc = JsonDocument.Parse(verdictJson);
                    var verdictStr = doc.RootElement.TryGetProperty("verdict", out var v)
                        ? v.GetString() ?? "" : "";
                    if (string.Equals(verdictStr, "partial", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedPartialPreRepair++;
                        continue;
                    }
                }
                catch { /* if unparseable, fall through and let WriteAsync refuse it */ }
            }

            // Write grounding block — reuses same service as per-test mode (no code duplication)
            var result = await service.WriteAsync(testFilePath, verdictJson, repairAttempts, repaired, ct);
            if (result.IsSuccess)
            {
                written++;
                // Spec 078 FR-011: a withheld concern must never be invisible.
                if (result.ReraiseSuppressed)
                {
                    reraisesSuppressed++;
                    suppressedIds.Add(testId);
                }
                if (result.ReviewRecordPreserved) reviewRecordsPreserved++;
            }
            else errors++;
        }

        EmitBatchResult(json, suite, written, skippedAlreadyWritten, skippedPartialPreRepair, errors,
            reVerify, reraisesSuppressed, reviewRecordsPreserved, suppressedIds);
        return ExitSuccess;
    }

    private static void EmitBatchResult(bool json, string suite, int written,
        int skippedAlreadyWritten, int skippedPartialPreRepair, int errors,
        bool reVerify = false, int reraisesSuppressed = 0, int reviewRecordsPreserved = 0,
        List<string>? suppressedIds = null)
    {
        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "ingest-grounding",
                mode = "batch",
                suite,
                re_verify = reVerify,
                written,
                skipped_already_written = skippedAlreadyWritten,
                skipped_partial_pre_repair = skippedPartialPreRepair,
                reraises_suppressed = reraisesSuppressed,
                review_records_preserved = reviewRecordsPreserved,
                errors
            }));
        }
        else
        {
            Console.Out.WriteLine(
                $"Batch grounding-ingest for suite '{suite}': written={written} " +
                $"skipped_already_written={skippedAlreadyWritten} " +
                $"skipped_partial_pre_repair={skippedPartialPreRepair} errors={errors}");

            if (reviewRecordsPreserved > 0)
                Console.Out.WriteLine($"  Review records preserved: {reviewRecordsPreserved}");

            if (reraisesSuppressed > 0)
            {
                // Naming the ids matters: a count alone does not tell a reviewer which concerns were
                // withheld, and that is exactly what they need to act on.
                var ids = suppressedIds is { Count: > 0 } ? $"   ({string.Join(", ", suppressedIds)})" : "";
                Console.Out.WriteLine($"  Re-raises suppressed:     {reraisesSuppressed}{ids}");
            }
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
                    repaired = g.Repaired,
                    // Spec 078 FR-011
                    reraise_suppressed = result.ReraiseSuppressed,
                    suppressed_by_review_at = result.SuppressedByReviewAt?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    review_record_preserved = result.ReviewRecordPreserved
                }));
            }
            else
            {
                Console.Out.WriteLine($"Grounding written for {testId} in suite '{suite}': verdict={g.Verdict.ToString().ToLowerInvariant()} score={g.Score:F2}");
                if (g.FlaggedForReview)
                    Console.Out.WriteLine($"  Flagged for review ({g.CondensedFindings.Count} condensed finding(s)).");
                if (g.Repaired)
                    Console.Out.WriteLine($"  Repaired after {g.RepairAttempts} attempt(s).");
                if (result.ReraiseSuppressed)
                    Console.Out.WriteLine(
                        $"  Re-raise suppressed — review by {g.ReviewedBy} on " +
                        $"{result.SuppressedByReviewAt?.UtcDateTime:yyyy-MM-dd} is newer than the prior verification.");
                if (result.ReviewRecordPreserved)
                    Console.Out.WriteLine("  Review record preserved.");
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
