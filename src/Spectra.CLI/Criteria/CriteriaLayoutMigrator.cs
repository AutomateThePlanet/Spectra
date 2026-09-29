using System.Text.Json.Serialization;
using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Criteria;

/// <summary>
/// Spec 079 FR-016: one-time, deterministic, idempotent move from base-name criteria files to per-doc files.
/// <list type="bullet">
/// <item>normalises every document source's <c>source_doc</c>;</item>
/// <item>moves (never copies) each document source's file to its derived path, rewriting <c>source_doc</c>
/// inside it;</item>
/// <item>seeds the committed high-water marks from the IDs present;</item>
/// <item>reports index entries that share one file (a past collision) and leaves them untouched.</item>
/// </list>
/// Callers hold the criteria lock.
/// </summary>
public static class CriteriaLayoutMigrator
{
    public const string CollisionRecommendation =
        "Re-extract both docs (spectra-criteria full re-extract); each then gets its own criteria file.";

    public static async Task<MigrationReport> MigrateAsync(CriteriaWorkspace ws, bool dryRun, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ws);

        var documentSources = ws.Index.Sources.Where(CriteriaPaths.IsDocumentSource).ToList();

        // Normalised doc per source (computed up front so a dry run can report without mutating).
        var normalizedDoc = documentSources.ToDictionary(
            s => s,
            s => CriteriaPaths.NormalizeSourceDoc(s.SourceDoc, ws.RepoRoot).Path ?? CriteriaPaths.NormalizeLexical(s.SourceDoc),
            ReferenceEqualityComparer.Instance);
        var normalizedCount = documentSources.Count(s => !string.Equals(s.SourceDoc, normalizedDoc[s], StringComparison.Ordinal));

        var groups = documentSources
            .GroupBy(s => CriteriaPaths.NormalizeLexical(s.File), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var collisions = groups
            .Where(g => g.Count() > 1)
            .Select(g => new MigrationCollision(g.Key,
                g.Select(s => normalizedDoc[s]).OrderBy(d => d, StringComparer.Ordinal).ToList(), CollisionRecommendation))
            .OrderBy(c => c.File, StringComparer.Ordinal)
            .ToList();

        var moves = new List<MigrationMove>();
        foreach (var source in groups.Where(g => g.Count() == 1).Select(g => g.Single()))
        {
            var doc = normalizedDoc[source];
            var from = CriteriaPaths.NormalizeLexical(source.File);
            var to = CriteriaPaths.DerivedFile(doc);

            if (!dryRun)
            {
                source.SourceDoc = doc;
            }

            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                continue;
            }

            moves.Add(new MigrationMove(doc, from, to));
            if (dryRun)
            {
                continue;
            }

            var fromPath = ws.FullPath(from);
            if (File.Exists(fromPath))
            {
                var criteria = await ws.ReadStrictAsync(from, ct);
                foreach (var criterion in criteria)
                {
                    criterion.SourceDoc = doc;
                }

                await ws.WriteCriteriaAsync(to, criteria, doc, source.DocHash, ct);
                if (!SamePath(fromPath, ws.FullPath(to)))
                {
                    File.Delete(fromPath);
                }
            }

            source.File = to;
        }

        if (!dryRun)
        {
            foreach (var collided in groups.Where(g => g.Count() > 1).SelectMany(g => g))
            {
                collided.SourceDoc = normalizedDoc[collided];
            }
        }

        // Seed high-water marks (against a copy on a dry run).
        var target = dryRun
            ? new CriteriaIndex { IdHighWaterMarks = ws.Index.IdHighWaterMarks is null ? null : new(ws.Index.IdHighWaterMarks) }
            : ws.Index;
        var seeded = CriteriaIdAllocator.Seed(target, ws.AllIds);

        var alreadyMigrated = moves.Count == 0 && normalizedCount == 0 && seeded.Count == 0;
        if (!dryRun && !alreadyMigrated)
        {
            await ws.SaveIndexAsync(ct);
        }

        return new MigrationReport(dryRun, moves, normalizedCount, collisions, seeded, alreadyMigrated);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}

public sealed record MigrationMove(
    [property: JsonPropertyName("doc")] string Doc,
    [property: JsonPropertyName("from_file")] string FromFile,
    [property: JsonPropertyName("to_file")] string ToFile);

public sealed record MigrationCollision(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("docs")] IReadOnlyList<string> Docs,
    [property: JsonPropertyName("recommendation")] string Recommendation);

public sealed record MigrationReport(
    [property: JsonPropertyName("dry_run")] bool DryRun,
    [property: JsonPropertyName("moved")] IReadOnlyList<MigrationMove> Moved,
    [property: JsonPropertyName("normalized_source_docs")] int NormalizedSourceDocs,
    [property: JsonPropertyName("collisions")] IReadOnlyList<MigrationCollision> Collisions,
    [property: JsonPropertyName("high_water_marks_seeded")] IReadOnlyDictionary<string, int> HighWaterMarksSeeded,
    [property: JsonPropertyName("already_migrated")] bool AlreadyMigrated);
