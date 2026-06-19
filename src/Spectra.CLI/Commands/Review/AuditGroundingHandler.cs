using System.Text.Json;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Review;

/// <summary>
/// Spec 072 FR2: Reports per-test grounding state for a suite. Serves as both the human
/// inspection surface and the resume oracle for compile-repair-batch. Model-free, deterministic.
/// Mirrors ReviewFlaggedHandler structure.
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

    public async Task<int> RunAsync(string suite, bool json, CancellationToken ct)
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
        if (!Directory.Exists(verdictDir))
        {
            EmitEmpty(suite, json);
            return 0;
        }

        var parser = new TestCaseParser();
        var indexLookup = index.Tests
            .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

        var entries = new List<AuditGroundingEntry>();
        var verdictFiles = Directory.GetFiles(verdictDir, "critic-verdict-*.json");

        foreach (var vf in verdictFiles.OrderBy(f => f))
        {
            ct.ThrowIfCancellationRequested();

            string verdictJson;
            try { verdictJson = await File.ReadAllTextAsync(vf, ct); }
            catch { continue; }

            string? id;
            string verdict;
            double score;
            try
            {
                using var doc = JsonDocument.Parse(verdictJson);
                var root = doc.RootElement;
                verdict = root.TryGetProperty("verdict", out var v) ? v.GetString() ?? "unknown" : "unknown";
                score = root.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number
                    ? s.GetDouble() : 0.0;
                var fileName = Path.GetFileNameWithoutExtension(vf); // critic-verdict-TC-NNN
                id = fileName.StartsWith("critic-verdict-", StringComparison.OrdinalIgnoreCase)
                    ? fileName["critic-verdict-".Length..] : null;
            }
            catch { continue; }

            if (string.IsNullOrEmpty(id)) continue;

            string? filePath = null;
            bool groundingWritten = false;
            bool flaggedForReview = false;

            if (indexLookup.TryGetValue(id, out var testEntry))
            {
                var testFilePath = Path.Combine(testsPath, testEntry.File);
                filePath = Path.GetRelativePath(_currentDir, testFilePath).Replace('\\', '/');

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
                        }
                    }
                    catch { /* skip grounding check on parse error */ }
                }
            }

            var actionNeeded = groundingWritten
                ? (flaggedForReview ? "review" : "none")
                : "repair";

            entries.Add(new AuditGroundingEntry
            {
                Id = id,
                Verdict = verdict,
                Score = score,
                GroundingWritten = groundingWritten,
                FlaggedForReview = flaggedForReview,
                ActionNeeded = actionNeeded,
                File = filePath
            });
        }

        var summary = new AuditGroundingSummary
        {
            Total = entries.Count,
            GroundingWritten = entries.Count(e => e.GroundingWritten),
            PartialPendingRepair = entries.Count(e => e.ActionNeeded == "repair"),
            FlaggedForReview = entries.Count(e => e.FlaggedForReview)
        };

        if (json)
        {
            JsonResultWriter.Write(new AuditGroundingResult
            {
                Command = "audit-grounding",
                Status = "success",
                Suite = suite,
                Tests = entries,
                Summary = summary
            });
        }
        else
        {
            EmitHuman(suite, entries, summary);
        }

        return 0;
    }

    private static void EmitEmpty(string suite, bool json)
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
                    Total = 0, GroundingWritten = 0, PartialPendingRepair = 0, FlaggedForReview = 0
                }
            });
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
                    $"  {e.Id,-12} {e.Verdict,-10} {e.Score,5:F2}  {(e.GroundingWritten ? "yes" : "no"),-8}  {(e.FlaggedForReview ? "yes" : "no"),-7}  {e.ActionNeeded}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Summary: {summary.Total} total | {summary.GroundingWritten} grounded | " +
            $"{summary.PartialPendingRepair} pending repair | {summary.FlaggedForReview} flagged");
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
