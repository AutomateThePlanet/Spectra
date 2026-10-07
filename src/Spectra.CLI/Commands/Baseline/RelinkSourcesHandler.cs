using System.Text;
using Spectra.CLI.Baseline;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Commands.Baseline;

/// <summary>
/// Spec 086 (FR-008–FR-011, research R5): the logic of <c>spectra ai relink-sources</c>. For every ref whose file is
/// missing and that git history renamed (baselined or not): rewrite the ref in the test file (path only, anchor kept),
/// update the suite index, move the baseline key with its old fingerprint (only if one exists), and clear the orphan
/// status when every ref then resolves. <c>--dry-run</c> reports the same plan and writes nothing, not even the lock.
/// </summary>
public static class RelinkSourcesHandler
{
    public const string Command = "relink-sources";

    private sealed record RefMove(string From, string To);

    private sealed record TestPlan(TestCase Test, IReadOnlyList<string?> Items, IReadOnlyList<RefMove> Moves, bool ClearOrphan);

    private sealed record NotRelinked(string Suite, string Test, string Ref, string Reason);

    private sealed record SuitePlan(string Suite, IReadOnlyList<TestPlan> Tests);

    public static async Task<int> RunAsync(
        string root, string? suite, bool allSuites, int minSimilarity, bool dryRun, bool json, CancellationToken ct)
    {
        if (allSuites == !string.IsNullOrWhiteSpace(suite))
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments,
                "Give exactly one of --suite <name> and --all-suites.");
        }

        if (BaselineCommandIO.ValidateMinSimilarity(minSimilarity) is { } invalid)
        {
            return BaselineCommandIO.Fail(json, Command, BaselineCommandIO.ErrorInvalidArguments, invalid);
        }

        var loaded = await BaselineCommandIO.LoadConfigAsync(root, ct);
        if (!loaded.IsSuccess)
        {
            return BaselineCommandIO.Fail(json, Command, loaded.ErrorCode!, loaded.Error!);
        }

        var config = loaded.Config!;
        var warnings = new List<string>(loaded.Warnings);
        BaselineCommandIO.PrintWarnings(loaded.Warnings);
        var testsPath = BaselineCommandIO.TestsPath(root, config);

        var (suites, scopeError, scopeMessage) = BaselineCommandIO.ResolveScope(testsPath, suite, allSuites);
        if (suites is null)
        {
            return BaselineCommandIO.Fail(json, Command, scopeError!, scopeMessage!, warnings);
        }

        var plans = new List<SuitePlan>();
        var notRelinked = new List<NotRelinked>();

        async Task<int> Body()
        {
            var context = await BaselineContext.CreateAsync(root, config, ct);
            var renames = context.CreateRenameResolver(minSimilarity);
            foreach (var name in suites)
            {
                var tests = await BaselineCommandIO.LoadSuiteTestsAsync(root, testsPath, name, warnings, ct);
                var suitePlan = new List<TestPlan>();
                foreach (var test in tests.Tests)
                {
                    if (await PlanAsync(name, test, context, renames, notRelinked, ct) is { } plan)
                    {
                        suitePlan.Add(plan);
                    }
                }

                plans.Add(new SuitePlan(name, suitePlan));
                if (!dryRun && suitePlan.Count > 0)
                {
                    await ApplyAsync(root, testsPath, name, suitePlan, warnings, ct);
                }
            }

            return 0;
        }

        try
        {
            if (dryRun) await Body();
            else await BaselineStore.WithLockAsync(root, Body, ct);
        }
        catch (BaselineStoreException ex)
        {
            return BaselineCommandIO.Fail(json, Command, ex.ErrorCode, ex.Message, warnings);
        }

        BaselineCommandIO.PrintLateWarnings(warnings, loaded.Warnings.Count);
        var relinked = plans.SelectMany(p => p.Tests.Select(t => (p.Suite, Plan: t))).ToList();
        var document = new Dictionary<string, object?>
        {
            ["command"] = Command,
            ["success"] = true,
            ["scope"] = BaselineCommandIO.ScopeJson(allSuites ? null : suite),
            ["dry_run"] = dryRun,
            ["min_similarity"] = minSimilarity,
            ["relinked"] = relinked.Select(r => new Dictionary<string, object?>
            {
                ["suite"] = r.Suite,
                ["test"] = r.Plan.Test.Id,
                ["refs"] = r.Plan.Moves.Select(m => new Dictionary<string, object?> { ["from"] = m.From, ["to"] = m.To }).ToList(),
                ["orphan_cleared"] = r.Plan.ClearOrphan
            }).ToList(),
            ["not_relinked"] = notRelinked.Select(n => new Dictionary<string, object?>
            {
                ["suite"] = n.Suite, ["test"] = n.Test, ["ref"] = n.Ref, ["reason"] = n.Reason
            }).ToList(),
            ["counts"] = new Dictionary<string, object?>
            {
                ["tests_relinked"] = relinked.Count,
                ["refs_relinked"] = relinked.Sum(r => r.Plan.Moves.Count),
                ["refs_not_relinked"] = notRelinked.Count
            },
            ["warnings"] = warnings
        };

        BaselineCommandIO.Emit(json, document, writer => WriteHuman(writer, relinked, notRelinked, dryRun, minSimilarity));
        return ExitCodes.Success;
    }

    /// <summary>The test's relinks: which items change, which are dropped (the target is already listed), and orphan clearing.</summary>
    private static async Task<TestPlan?> PlanAsync(
        string suite, TestCase test, BaselineContext context, RenameResolver renames, List<NotRelinked> notRelinked,
        CancellationToken ct)
    {
        var newRefs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            if (await context.RelinkCandidateAsync(sourceRef, renames, ct) is not { } candidate)
            {
                continue;
            }

            if (candidate.NewRef is null)
            {
                notRelinked.Add(new NotRelinked(suite, test.Id, sourceRef, candidate.Reason!));
            }
            else
            {
                newRefs[sourceRef] = candidate.NewRef;
            }
        }

        if (newRefs.Count == 0)
        {
            return null;
        }

        // FR-009: a relinked ref whose new value the test already lists is dropped instead of duplicated.
        var items = new List<string?>();
        var listed = new HashSet<string>(test.SourceRefs.Where(r => !newRefs.ContainsKey(r)), StringComparer.Ordinal);
        var moves = new List<RefMove>();
        foreach (var sourceRef in test.SourceRefs)
        {
            if (!newRefs.TryGetValue(sourceRef, out var target))
            {
                items.Add(sourceRef);
                continue;
            }

            if (!moves.Any(m => m.From == sourceRef))
            {
                moves.Add(new RefMove(sourceRef, target));
            }

            items.Add(listed.Add(target) ? target : null);
        }

        // FR-010: orphan status is cleared only when every ref then resolves (file and anchor).
        var clearOrphan = false;
        if (string.Equals(test.Status, "orphaned", StringComparison.OrdinalIgnoreCase))
        {
            clearOrphan = true;
            foreach (var item in items.Where(i => i is not null).Distinct(StringComparer.Ordinal))
            {
                if ((await context.GetSectionAsync(item!, ct)).Current.Missing)
                {
                    clearOrphan = false;
                    break;
                }
            }
        }

        return new TestPlan(test, items, moves, clearOrphan);
    }

    /// <summary>Writes the test files, updates the suite index, and moves the baseline keys (inside the lock).</summary>
    private static async Task ApplyAsync(
        string root, string testsPath, string suite, IReadOnlyList<TestPlan> plans, List<string> warnings, CancellationToken ct)
    {
        var suitePath = Path.Combine(testsPath, suite);
        var rewritten = new List<TestCase>();
        foreach (var plan in plans)
        {
            var path = Path.Combine(suitePath, plan.Test.FilePath);
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

            var rewrite = TestRefRewriter.Rewrite(text, plan.Test, plan.Items, plan.ClearOrphan, BaselineStore.Relative(root, path));
            if (rewrite.Warning is not null)
            {
                warnings.Add(rewrite.Warning);
            }

            await WriteAtomicAsync(path, rewrite.Text, bom, ct);
            rewritten.Add(plan.Test with
            {
                SourceRefs = plan.Items.Where(i => i is not null).Select(i => i!).ToList(),
                Status = plan.ClearOrphan ? null : plan.Test.Status,
                OrphanedReason = plan.ClearOrphan ? null : plan.Test.OrphanedReason,
                OrphanedDate = plan.ClearOrphan ? null : plan.Test.OrphanedDate
            });
        }

        // The index: update the relinked entries in place (entries of files that don't parse are kept).
        var indexPath = IndexWriter.GetIndexPath(suitePath);
        var indexWriter = new IndexWriter();
        var generator = new IndexGenerator();
        var existing = File.Exists(indexPath) ? await indexWriter.ReadAsync(indexPath, ct) : null;
        var scan = await BaselineStore.ScanSuiteAsync(root, testsPath, suite, ct);
        await indexWriter.WriteAsync(indexPath,
            existing is null ? generator.Generate(suite, scan.Tests) : generator.Update(existing, rewritten), ct);

        // FR-009: the baseline key moves with its OLD fingerprint; an existing entry for the new key wins.
        var baseline = await BaselineStore.LoadSuiteAsync(root, testsPath, suite, ct);
        foreach (var plan in plans)
        {
            if (!baseline.Tests.TryGetValue(plan.Test.Id, out var entry))
            {
                continue; // no entry: nothing to move; the new ref stays unbaselined until the next --init
            }

            foreach (var move in plan.Moves)
            {
                if (entry.Refs.Remove(move.From, out var fingerprint))
                {
                    entry.Refs.TryAdd(move.To, fingerprint);
                }
            }
        }

        await BaselineStore.SaveSuiteAsync(testsPath, suite, baseline, scan, warnings, ct);
    }

    private static async Task WriteAtomicAsync(string path, string text, bool bom, CancellationToken ct)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temp, text, new UTF8Encoding(bom), ct);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void WriteHuman(
        TextWriter writer, IReadOnlyList<(string Suite, TestPlan Plan)> relinked, IReadOnlyList<NotRelinked> notRelinked,
        bool dryRun, int minSimilarity)
    {
        var verb = dryRun ? "would be relinked" : "relinked";
        writer.WriteLine($"Relink sources — {relinked.Count} test(s) {verb}, {relinked.Sum(r => r.Plan.Moves.Count)} ref(s) "
            + $"(rename similarity ≥ {minSimilarity}%)");
        foreach (var (suite, plan) in relinked)
        {
            foreach (var move in plan.Moves)
            {
                writer.WriteLine($"  {suite}/{plan.Test.Id}  {move.From} → {move.To}");
            }

            if (plan.ClearOrphan)
            {
                writer.WriteLine($"  {suite}/{plan.Test.Id}  orphan status cleared");
            }
        }

        if (notRelinked.Count > 0)
        {
            writer.WriteLine($"  Not relinked ({notRelinked.Count}):");
            foreach (var n in notRelinked)
            {
                writer.WriteLine($"    {n.Suite}/{n.Test}  {n.Ref}  — {n.Reason}");
            }
        }

        if (dryRun)
        {
            writer.WriteLine("  Dry run: nothing written.");
        }
    }
}
