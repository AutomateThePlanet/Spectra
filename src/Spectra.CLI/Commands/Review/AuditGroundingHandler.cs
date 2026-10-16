using System.Text.Json;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Review;

/// <summary>
/// Spec 072 FR2: Reports per-test grounding state for a suite. Serves as both the human
/// inspection surface and the resume oracle for compile-repair-batch. Model-free, deterministic.
/// Mirrors ReviewFlaggedHandler structure.
/// Spec 097 (FR-004–FR-009a): only the suite's indexed tests are listed (verdict files of other ids are ignored); a
/// verdict that fails the 090/091 freshness check is <c>stale</c> and needs the critic; <c>--action</c> / <c>--ids</c>
/// filter the listed entries while the summary covers the whole suite.
/// </summary>
public sealed class AuditGroundingHandler
{
    private readonly string _currentDir;
    private readonly string _testsDir;

    public AuditGroundingHandler(string currentDir, string testsDir)
    {
        _currentDir = currentDir;
        _testsDir = testsDir;
    }

    /// <summary>Spec 097 (FR-005) / Spec 098 (FR-007): the <c>--action</c> values, in the order the usage message names them.</summary>
    public static readonly IReadOnlyList<string> Actions = ["critic", "ground", "repair", "review", "none"];

    public async Task<int> RunAsync(
        string suite, bool json, CancellationToken ct,
        IReadOnlySet<string>? actions = null, IReadOnlyList<string>? ids = null)
    {
        var testsPath = Path.Combine(_currentDir, _testsDir);
        var suitePath = Path.Combine(testsPath, suite);

        if (!Directory.Exists(suitePath))
        {
            Console.Error.WriteLine($"Suite not found: {suitePath}");
            return 1;
        }

        var indexPath = IndexWriter.GetIndexPath(suitePath);
        var index = await new IndexWriter().ReadAsync(indexPath, ct);
        if (index is null)
        {
            Console.Error.WriteLine($"Suite index not found: {indexPath}");
            return 1;
        }

        var verdictDir = Path.Combine(_currentDir, ".spectra", "verdicts");

        var parser = new TestCaseParser(captureLayout: false);
        var indexLookup = index.Tests
            .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

        var entries = new List<AuditGroundingEntry>();
        var verdictFiles = Directory.Exists(verdictDir) ? Directory.GetFiles(verdictDir, "critic-verdict-*.json") : [];

        foreach (var vf in verdictFiles.OrderBy(f => f))
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(vf); // critic-verdict-TC-NNN
            var id = fileName.StartsWith("critic-verdict-", StringComparison.OrdinalIgnoreCase)
                ? fileName["critic-verdict-".Length..] : null;

            // Spec 097 (FR-004): a verdict of a test not in this suite's index (another suite's, or a stray) is ignored.
            if (string.IsNullOrEmpty(id) || !indexLookup.TryGetValue(id, out var testEntry)) continue;

            string verdictJson;
            try { verdictJson = await File.ReadAllTextAsync(vf, ct); }
            catch { continue; }

            string verdict;
            double score;
            try
            {
                using var doc = JsonDocument.Parse(verdictJson);
                var root = doc.RootElement;
                verdict = root.TryGetProperty("verdict", out var v) ? v.GetString() ?? "unknown" : "unknown";
                score = root.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number
                    ? s.GetDouble() : 0.0;
            }
            catch { continue; }

            bool groundingWritten = false;
            bool flaggedForReview = false;
            bool stale = false;

            var testFilePath = Path.Combine(suitePath, testEntry.File);
            var filePath = Path.GetRelativePath(_currentDir, testFilePath).Replace('\\', '/');

            if (File.Exists(testFilePath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(testFilePath, ct);
                    var rel = Path.GetRelativePath(testsPath, testFilePath);
                    var parsed = parser.Parse(content, rel);
                    if (parsed.IsSuccess && parsed.Value is { } tc)
                    {
                        groundingWritten = tc.Grounding is not null;
                        flaggedForReview = tc.Grounding is { FlaggedForReview: true };
                        // Spec 097 (FR-007): the check the ingest applies (fingerprint, record file time).
                        stale = VerdictFreshness.Check(_currentDir, id, tc, vf).IsStale;
                    }
                }
                catch { /* skip grounding check on parse error */ }
            }

            var actionNeeded = ActionFor(stale, groundingWritten, flaggedForReview, verdict);

            entries.Add(new AuditGroundingEntry
            {
                Id = id,
                Verdict = verdict,
                Score = score,
                GroundingWritten = groundingWritten,
                FlaggedForReview = flaggedForReview,
                ActionNeeded = actionNeeded,
                File = filePath,
                Stale = stale ? true : null
            });
        }

        // Spec 090 (FR-A7): every indexed test without a verdict file is listed too, judged by its grounding block —
        // .spectra/verdicts/ is gitignored, so a fresh clone has blocks but no verdict files. The entries above (tests
        // with a verdict file) are unchanged.
        var withVerdict = entries.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var test in index.Tests.Where(t => !withVerdict.Contains(t.Id)).OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var testFilePath = Path.Combine(suitePath, test.File);
            Spectra.Core.Models.Grounding.GroundingMetadata? grounding = null;
            if (File.Exists(testFilePath))
            {
                try
                {
                    var parsed = parser.Parse(await File.ReadAllTextAsync(testFilePath, ct), Path.GetRelativePath(testsPath, testFilePath));
                    grounding = parsed.IsSuccess ? parsed.Value?.Grounding : null;
                }
                catch { /* unreadable: reported as unverified */ }
            }

            entries.Add(EntryFromBlock(test.Id, grounding, Path.GetRelativePath(_currentDir, testFilePath).Replace('\\', '/')));
        }

        // Spec 097 (FR-006): listed ids that aren't in this suite.
        var notFound = ids?.Where(i => !indexLookup.ContainsKey(i)).ToList();

        if (entries.Count == 0)
        {
            EmitEmpty(suite, json, notFound);
            return 0;
        }

        // Spec 097 (FR-007): the summary covers the whole suite, before any filter.
        var summary = new AuditGroundingSummary
        {
            Total = entries.Count,
            GroundingWritten = entries.Count(e => e.GroundingWritten),
            PartialPendingRepair = entries.Count(e => e.ActionNeeded == "repair"),
            PendingGround = entries.Count(e => e.ActionNeeded == "ground"),
            PendingReview = entries.Count(e => e.ActionNeeded == "review" && !e.GroundingWritten),
            FlaggedForReview = entries.Count(e => e.FlaggedForReview),
            Unverified = entries.Count(e => e.Verdict == Unverified),
            Stale = entries.Count(e => e.Stale == true)
        };

        // Spec 097 (FR-005, FR-006): the filters apply to the listed entries only.
        var wanted = ids?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var listed = entries
            .Where(e => actions is null || actions.Contains(e.ActionNeeded))
            .Where(e => wanted is null || wanted.Contains(e.Id))
            .ToList();

        if (json)
        {
            JsonResultWriter.Write(new AuditGroundingResult
            {
                Command = "audit-grounding",
                Status = "success",
                Suite = suite,
                Tests = listed,
                Summary = summary,
                NotFound = notFound
            });
        }
        else
        {
            EmitHuman(suite, listed, summary);
        }

        WriteNotFound(suite, notFound, json);
        return 0;
    }

    /// <summary>
    /// Spec 098 (FR-006): what a test with a verdict file needs. Stale → the critic again; a written block → review when
    /// flagged, else nothing; no block → by the verdict: grounded needs only its block (<c>ground</c>), partial a repair,
    /// hallucinated the user's decision (<c>review</c>), anything unreadable the critic again.
    /// </summary>
    internal static string ActionFor(bool stale, bool groundingWritten, bool flaggedForReview, string verdict)
    {
        if (stale)
        {
            return "critic";
        }

        if (groundingWritten)
        {
            return flaggedForReview ? "review" : "none";
        }

        return verdict.ToLowerInvariant() switch
        {
            "grounded" => "ground",
            "partial" => "repair",
            "hallucinated" => "review",
            _ => "critic"
        };
    }

    /// <summary>Spec 097 (FR-009a): one line per unknown id — stdout in human mode, stderr in JSON mode.</summary>
    private static void WriteNotFound(string suite, IReadOnlyList<string>? notFound, bool json)
    {
        var human = json ? Console.Error : Console.Out;
        foreach (var id in notFound ?? [])
        {
            human.WriteLine($"{id}: not found in suite '{suite}'");
        }
    }

    /// <summary>Spec 090 (FR-A7): the verdict of a test with no verdict file.</summary>
    internal const string Unverified = "none";

    /// <summary>
    /// Spec 090 (FR-A7): an entry for a test without a verdict file. A critic block → its verdict and the existing
    /// action rule; a Manual block → <c>manual</c>, nothing to do; no block → <c>none</c>, the critic must run.
    /// </summary>
    private static AuditGroundingEntry EntryFromBlock(string id, Spectra.Core.Models.Grounding.GroundingMetadata? grounding, string file)
    {
        if (grounding is null)
        {
            return new AuditGroundingEntry
            {
                Id = id, Verdict = Unverified, Score = 0.0, GroundingWritten = false, FlaggedForReview = false,
                ActionNeeded = "critic", File = file
            };
        }

        var manual = grounding.Verdict == Spectra.Core.Models.Grounding.VerificationVerdict.Manual;
        return new AuditGroundingEntry
        {
            Id = id,
            Verdict = grounding.Verdict.ToString().ToLowerInvariant(),
            Score = grounding.Score,
            GroundingWritten = true,
            FlaggedForReview = !manual && grounding.FlaggedForReview,
            ActionNeeded = !manual && grounding.FlaggedForReview ? "review" : "none",
            File = file
        };
    }

    private static void EmitEmpty(string suite, bool json, IReadOnlyList<string>? notFound)
    {
        if (json)
        {
            JsonResultWriter.Write(new AuditGroundingResult
            {
                Command = "audit-grounding",
                Status = "success",
                Suite = suite,
                Tests = [],
                Summary = new AuditGroundingSummary
                {
                    Total = 0, GroundingWritten = 0, PartialPendingRepair = 0, FlaggedForReview = 0, Unverified = 0, Stale = 0,
                    PendingGround = 0, PendingReview = 0
                },
                NotFound = notFound
            });
            WriteNotFound(suite, notFound, json);
            return;
        }
        else
        {
            Console.WriteLine($"Grounding audit — suite: {suite}");
            Console.WriteLine();
            Console.WriteLine("  No verdict files found. Run the critic pass first.");
            Console.WriteLine();
            Console.WriteLine("Summary: 0 total");
        }
    }

    private static void EmitHuman(string suite, List<AuditGroundingEntry> entries, AuditGroundingSummary summary)
    {
        Console.WriteLine($"Grounding audit — suite: {suite}");
        Console.WriteLine();

        if (entries.Count == 0)
        {
            Console.WriteLine("  No verdict files found.");
        }
        else
        {
            Console.WriteLine($"  {"ID",-12} {"Verdict",-10} {"Score",5}  {"Grounded",-8}  {"Flagged",-7}  Action");
            Console.WriteLine($"  {"─────────────",-12} {"──────────",-10} {"─────",5}  {"────────",-8}  {"───────",-7}  ──────");
            foreach (var e in entries.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"  {e.Id,-12} {e.Verdict,-10} {e.Score,5:F2}  {(e.GroundingWritten ? "yes" : "no"),-8}  {(e.FlaggedForReview ? "yes" : "no"),-7}  {e.ActionNeeded}{(e.Stale == true ? " (stale)" : "")}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Summary: {summary.Total} total | {summary.GroundingWritten} grounded | " +
            $"{summary.PartialPendingRepair} pending repair | {summary.FlaggedForReview} flagged" +
            (summary.PendingGround > 0 ? $" | {summary.PendingGround} pending ground" : "") +
            (summary.PendingReview > 0 ? $" | {summary.PendingReview} pending review" : "") +
            (summary.Unverified > 0 ? $" | {summary.Unverified} unverified" : "") +
            (summary.Stale > 0 ? $" | {summary.Stale} stale" : ""));
    }

    public static async Task<string> ResolveTestsDirAsync(string currentDir, CancellationToken ct)
    {
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath)) return "test-cases";
        try
        {
            var configJson = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SpectraConfig>(configJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Tests?.Dir ?? "test-cases";
        }
        catch { return "test-cases"; }
    }
}
