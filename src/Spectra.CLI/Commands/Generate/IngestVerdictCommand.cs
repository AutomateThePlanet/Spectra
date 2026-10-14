using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Criteria;
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
/// Spec 055: <c>spectra ai ingest-verdict</c>. The fail-loud verdict-ingest boundary. Reads an
/// agent-produced critic JSON (from <c>--from</c> or stdin), classifies it, and reports the verdict
/// + gate decision. Damage (empty / missing-field / unparseable) fails loud with a specific error —
/// never a silent <c>Partial</c>/<c>0.5</c> soft pass (FR-006). The verdict stays advisory-gating:
/// only <c>hallucinated</c> drops (FR-005).
///
/// Spec 077: adds <c>--suite --all</c> batch mode to classify all verdict files for a suite in one
/// call, eliminating per-test shell loops. Per-test <c>--from</c> mode is unchanged.
/// </summary>
public sealed class IngestVerdictCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitRefused = 4; // Spec 090: VERDICT_STALE
    private const int ExitEmpty = 5;        // EmptyResponse
    private const int ExitParseInvalid = 6; // ParseFailure

    public IngestVerdictCommand()
        : base("ingest-verdict", "Classify an agent-produced critic verdict (fail-loud, no model call)")
    {
        var fromOption = new Option<string?>("--from", "File containing the critic's JSON response (omit to read stdin)");
        var suiteOption = new Option<string?>(["--suite", "-s"], "Suite name (required with --all)");
        var allFlag = new Option<bool>("--all", "Batch mode: classify all verdict files for the suite (Spec 077)");
        var idsOption = new Option<string?>("--ids", "With --all: only these tests (comma-separated) (Spec 093)");

        AddOption(fromOption);
        AddOption(suiteOption);
        AddOption(allFlag);
        AddOption(idsOption);

        this.SetHandler(async (context) =>
        {
            var from = context.ParseResult.GetValueForOption(fromOption);
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var idsGiven = IdList.IsGiven(context.ParseResult.GetValueForOption(idsOption)); // Spec 097: empty = not given
            var ids = IdList.Parse(context.ParseResult.GetValueForOption(idsOption));
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var json = outputFormat == OutputFormat.Json;
            // Spec 097 (FR-018): ingest-verdict only reads and classifies — it writes nothing in either form — so
            // --dry-run is accepted in both and only marks the JSON.
            var dryRun = context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption);

            // Spec 093 (FR-017): --ids belongs to the batch form only (the 091 rule of ingest-grounding).
            if (idsGiven && !all)
            {
                Console.Error.WriteLine(IdList.IdsWithoutAllMessage);
                context.ExitCode = ExitError;
                return;
            }

            if (all)
            {
                if (!string.IsNullOrEmpty(from))
                {
                    Console.Error.WriteLine("--from and --all are mutually exclusive.");
                    context.ExitCode = ExitError;
                    return;
                }
                if (string.IsNullOrEmpty(suite))
                {
                    Console.Error.WriteLine("--suite is required when using --all.");
                    context.ExitCode = ExitError;
                    return;
                }
                context.ExitCode = await RunBatchAsync(suite, idsGiven ? ids : null, json, dryRun, context.GetCancellationToken());
            }
            else
            {
                context.ExitCode = await RunAsync(from, json, dryRun, context.GetCancellationToken());
            }
        });
    }

    private static async Task<int> RunAsync(string? from, bool json, bool dryRun, CancellationToken ct)
    {
        string content;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!File.Exists(from))
            {
                Console.Error.WriteLine($"Content file not found: {from}");
                return ExitError;
            }
            content = await File.ReadAllTextAsync(from, ct);

            // Spec 090 (FR-A2): a verdict file in .spectra/verdicts/ with a fingerprint record must still describe the
            // test it judged; Spec 091 (FR-002): and must not be older than the last critic prompt for it. Stdin and
            // other paths have no record and are accepted as before.
            var root = Directory.GetCurrentDirectory();
            if (VerdictFreshness.IdOfVerdictFile(root, from) is { } id
                && await VerdictFreshness.CheckRecordedTestAsync(root, id, from, ct) is { IsStale: true } stale)
            {
                CriteriaCommandIO.WriteFailure(json, "ingest-verdict", VerdictFreshness.ErrorVerdictStale, stale.Message(id));
                return ExitRefused;
            }
        }
        else
        {
            content = await Console.In.ReadToEndAsync(ct);
        }

        var result = VerdictIngestor.Classify(content);

        if (result.IsSuccess)
        {
            var verdict = result.Result!.Verdict.ToString().ToLowerInvariant();
            if (json)
            {
                Console.Out.WriteLine(DryRunJson.Serialize(new
                {
                    outcome = result.Outcome.ToString(),
                    verdict,
                    score = result.Result.Score,
                    drop = result.Drops
                }, dryRun));
            }
            else
            {
                Console.Out.WriteLine(
                    $"verdict={verdict} score={result.Result.Score:F2} gate={(result.Drops ? "drop" : "pass")}");
            }
            return ExitSuccess;
        }

        // Fail loud — damage. Nothing is treated as a verdict.
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
            Console.Error.WriteLine($"Verdict ingest failed [{result.Outcome}] — no verdict applied:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        return result.Outcome == VerdictIngestOutcome.EmptyResponse ? ExitEmpty : ExitParseInvalid;
    }

    private static async Task<int> RunBatchAsync(string suite, IReadOnlyList<string>? ids, bool json, bool dryRun, CancellationToken ct)
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

        // Spec 093 (FR-017): only the listed tests are considered; listed ids not in the suite are reported.
        var listed = ids is null ? null : new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        var notFound = ids?.Where(id => !indexLookup.ContainsKey(id)).ToList() ?? [];

        var verdictDir = Path.Combine(currentDir, ".spectra", "verdicts");
        if (!Directory.Exists(verdictDir))
        {
            EmitBatchResult(json, dryRun, suite, 0, 0, 0, 0, [], [], notFound);
            return ExitSuccess;
        }

        int grounded = 0, partial = 0, hallucinated = 0, errors = 0;
        var stale = new List<string>();
        var results = new List<VerdictResult>();
        var parser = new TestCaseParser();

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
            if (!indexLookup.TryGetValue(testId, out var entry)) continue;

            // Spec 090 (FR-A2) / 091 (FR-002): a verdict given to other content, or older than the last prompt, is not classified.
            var testPath = Path.Combine(suitePath, entry.File);
            if (File.Exists(testPath)
                && parser.Parse(await File.ReadAllTextAsync(testPath, ct), entry.File) is { IsSuccess: true } parsed
                && VerdictFreshness.Check(currentDir, testId, parsed.Value!, vf).IsStale)
            {
                stale.Add(testId);
                continue;
            }

            string content;
            try { content = await File.ReadAllTextAsync(vf, ct); }
            catch { errors++; continue; }

            var result = VerdictIngestor.Classify(content);
            if (!result.IsSuccess) { errors++; continue; }

            switch (result.Result!.Verdict)
            {
                case VerificationVerdict.Grounded:
                case VerificationVerdict.Manual:
                    grounded++; break;
                case VerificationVerdict.Partial:
                    partial++; break;
                case VerificationVerdict.Hallucinated:
                    hallucinated++; break;
                default:
                    errors++; continue;
            }

            // Spec 093 (FR-016): the verdict the CLI classified, per test — the skills route by it.
            results.Add(new VerdictResult(
                testId, result.Result.Verdict.ToString().ToLowerInvariant(), result.Result.Score, result.Drops ? "drop" : "pass"));
        }

        EmitBatchResult(json, dryRun, suite, grounded, partial, hallucinated, errors, stale, results, notFound);
        return ExitSuccess;
    }

    /// <summary>Spec 093 (FR-016): one classified verdict in the batch result.</summary>
    private sealed record VerdictResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("verdict")] string Verdict,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("gate")] string Gate);

    private static void EmitBatchResult(bool json, bool dryRun, string suite,
        int grounded, int partial, int hallucinated, int errors, IReadOnlyList<string> stale,
        IReadOnlyList<VerdictResult> results, IReadOnlyList<string> notFound)
    {
        if (json)
        {
            Console.Out.WriteLine(DryRunJson.Serialize(new
            {
                command = "ingest-verdict",
                mode = "batch",
                suite,
                grounded,
                partial,
                hallucinated,
                errors,
                skipped_stale = stale.Count,
                stale,
                results,
                not_found = notFound
            }, dryRun));
        }

        // The human summary: stdout in human mode, stderr in JSON mode (never suppressed; the 091 rule).
        var human = json ? Console.Error : Console.Out;
        human.WriteLine(
            $"Batch verdict-ingest for suite '{suite}': " +
            $"grounded={grounded} partial={partial} hallucinated={hallucinated} errors={errors}" +
            (stale.Count > 0 ? $" skipped_stale={stale.Count} ({string.Join(", ", stale)})" : ""));
        foreach (var r in results)
        {
            human.WriteLine(FormattableString.Invariant($"{r.Id}: {r.Verdict} {r.Score:0.##} ({r.Gate})"));
        }

        foreach (var id in notFound)
        {
            human.WriteLine($"{id}: not found in suite '{suite}'");
        }
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
