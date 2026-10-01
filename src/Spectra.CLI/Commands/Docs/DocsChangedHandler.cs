using System.Text.Json;
using Spectra.CLI.Commands.Analyze;
using Spectra.CLI.Criteria;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Source;
using Spectra.Core.Criteria;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 069 (FR-005): <c>spectra docs changed</c> — the model-free incremental-skip surface for the
/// skill-driven criteria extraction loop. Compares each source document's current SHA-256 against the
/// recorded <c>doc_hash</c> in <c>_criteria_index.yaml</c> and reports <c>new|changed|unchanged</c>.
/// No model call, no writes — it only exposes the skip key that the per-document loop used to keep
/// internal, so the skill never sends an unchanged document through a model turn.
/// </summary>
public sealed class DocsChangedHandler
{
    private readonly OutputFormat _outputFormat;
    private readonly bool _includeUnchanged;
    private readonly bool _includeArchived;

    public DocsChangedHandler(OutputFormat outputFormat, bool includeUnchanged, bool includeArchived)
    {
        _outputFormat = outputFormat;
        _includeUnchanged = includeUnchanged;
        _includeArchived = includeArchived;
    }

    public Task<int> ExecuteAsync(CancellationToken ct = default)
        => ExecuteAsync(Directory.GetCurrentDirectory(), Console.Out, ct);

    /// <summary>
    /// Testable core: operates on an explicit workspace root and writes to an explicit
    /// <see cref="TextWriter"/> (rather than the process cwd / global <c>Console.Out</c>), so tests
    /// can capture output without a non-thread-safe global console swap.
    /// </summary>
    public async Task<int> ExecuteAsync(string currentDir, TextWriter output, CancellationToken ct = default)
    {
        // Spec 080 A5: a read command — built-in defaults when there is no config; exit 1 on a malformed or
        // invalid one (never a silent fallback).
        var loaded = await Spectra.Core.Config.ConfigLoader.LoadForReadAsync(
            currentDir, Spectra.Core.Config.ConfigSections.Source, ct);
        if (!loaded.IsSuccess)
        {
            // Spec 082 A4: in JSON mode a config failure is the shared failure document with its named code.
            if (_outputFormat == OutputFormat.Json)
                CriteriaCommandIO.WriteFailureJson("docs-changed", loaded.ErrorCode, [loaded.Error!]);
            else
                Console.Error.WriteLine(loaded.Error);
            return ExitCodes.Error;
        }
        var config = loaded.Config!;
        foreach (var warning in loaded.Warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }

        var criteriaIndexPath = Path.Combine(currentDir, config.Coverage.CriteriaFile);

        // Same document enumeration + skip-analysis filter the extraction path uses, so this list is
        // exactly the work-list the skill will iterate.
        var documentMap = await new DocumentMapBuilder(config.Source).BuildAsync(currentDir, ct);
        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(currentDir, config.Source);
        var indexDir = LegacyIndexMigrator.ResolveIndexDir(currentDir, config.Source);
        documentMap = await ManifestDocumentFilter.FilterAsync(documentMap, manifestPath, indexDir, _includeArchived, ct);

        var criteriaIndex = await new CriteriaIndexReader().ReadAsync(criteriaIndexPath, ct);

        var entries = new List<ChangedDocEntry>();
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in documentMap.Documents)
        {
            if (AnalyzeHandler.ShouldSkipDocument(Path.GetFileName(doc.Path)))
                continue;

            string currentHash;
            try
            {
                currentHash = await FileHasher.ComputeFileHashAsync(Path.Combine(currentDir, doc.Path), ct);
            }
            catch
            {
                continue; // unreadable doc — not our concern here
            }

            var path = CriteriaPaths.NormalizeLexical(doc.Path);
            discovered.Add(path);

            // Spec 079 FR-017: compare normalised paths, so an index entry written as docs\x.md or ./docs/x.md
            // still matches.
            var existing = FindDocumentSource(criteriaIndex, path, currentDir);
            var indexedHash = existing?.DocHash;

            var status = indexedHash is null
                ? "new"
                : (!string.Equals(indexedHash, currentHash, StringComparison.OrdinalIgnoreCase) ? "changed" : "unchanged");

            entries.Add(new ChangedDocEntry(path, DeriveComponent(path), status, currentHash, indexedHash));
        }

        // Spec 079 FR-018: index sources whose document no longer exists on disk. (A doc that exists but was
        // filtered out of discovery, e.g. an archived suite, is not deleted.)
        var deleted = criteriaIndex.Sources
            .Where(CriteriaPaths.IsDocumentSource)
            .Select(s => (Source: s, Path: CriteriaPaths.NormalizeSourceDoc(s.SourceDoc, currentDir).Path
                ?? CriteriaPaths.NormalizeLexical(s.SourceDoc)))
            .Where(x => !discovered.Contains(x.Path) && !File.Exists(Path.Combine(currentDir, x.Path)))
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(x => new ChangedDocEntry(x.Path, DeriveComponent(x.Path), "deleted", null, x.Source.DocHash,
                CriteriaCount: x.Source.CriteriaCount))
            .ToList();

        // Spec 079 FR-019: exactly one new doc and exactly one deleted source with the same content hash is a
        // move. Ambiguous matches stay new + deleted. Read-only: applying it is `spectra docs apply-move`.
        foreach (var hashGroup in deleted.Where(d => d.IndexedHash is not null)
                     .GroupBy(d => d.IndexedHash!, StringComparer.OrdinalIgnoreCase).ToList())
        {
            var candidates = entries
                .Where(e => e.Status == "new" && string.Equals(e.CurrentHash, hashGroup.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (hashGroup.Count() != 1 || candidates.Count != 1)
            {
                continue;
            }

            var from = hashGroup.Single();
            var to = candidates.Single();
            entries.Remove(to);
            deleted.Remove(from);
            entries.Add(to with { Status = "moved", From = from.Path, IndexedHash = from.IndexedHash, CriteriaCount = from.CriteriaCount });
        }

        entries.AddRange(deleted);
        entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path)); // stable, deterministic order

        var duplicates = CriteriaDuplicateScanner.Scan(
            await CriteriaStore.ScanFilesAsync(Path.GetFullPath(Path.Combine(currentDir, config.Coverage.CriteriaDir)), ct));
        var migrationNeeded = CriteriaPaths.IsOldLayout(criteriaIndex);
        var warnings = new List<string>(loaded.Warnings);
        if (migrationNeeded)
        {
            warnings.Add("Criteria files use the pre-2.6 layout; run 'spectra docs migrate-criteria' "
                + "(criteria writes also migrate automatically).");
        }

        warnings.AddRange(duplicates.Select(d =>
            $"Criterion id {d.Id} is defined more than once ({string.Join(", ", d.Files)}); not renumbered."));

        var changed = entries.Where(e => e.Status != "unchanged").ToList();
        var unchangedCount = entries.Count - changed.Count;
        var emitted = _includeUnchanged ? entries : changed;

        if (_outputFormat == OutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                command = "docs-changed",
                status = "success",
                changed = emitted.Select(e => new
                {
                    path = e.Path,
                    from = e.From,
                    component = e.Component,
                    status = e.Status,
                    current_hash = e.CurrentHash,
                    indexed_hash = e.IndexedHash,
                    criteria_count = e.CriteriaCount
                }).ToArray(),
                unchanged_count = unchangedCount,
                duplicate_ids = duplicates,
                migration_needed = migrationNeeded,
                warnings
            }, JsonOutput));
        }
        else
        {
            if (emitted.Count == 0)
            {
                output.WriteLine("All documents up to date — no changed criteria to extract.");
            }
            else
            {
                foreach (var e in emitted)
                {
                    var detail = e.Status switch
                    {
                        "moved" => $"  (from: {e.From})",
                        "deleted" => $"  ({e.CriteriaCount} criteria)",
                        _ => $"  (component: {e.Component})"
                    };
                    output.WriteLine($"  [{e.Status,-9}] {e.Path}{detail}");
                }

                output.WriteLine($"\n{changed.Count} changed, {unchangedCount} unchanged.");
            }

            foreach (var warning in warnings)
                output.WriteLine($"Warning: {warning}");
        }

        return ExitCodes.Success;
    }

    private static readonly JsonSerializerOptions JsonOutput = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Spec 081 C1: repo-aware comparison, so an index entry stored as an absolute path still matches.
    private static CriteriaSource? FindDocumentSource(CriteriaIndex index, string path, string currentDir) =>
        index.Sources.FirstOrDefault(s => CriteriaPaths.IsDocumentSource(s)
            && CriteriaProvenance.SameDocument(s.SourceDoc, path, currentDir));

    private static string DeriveComponent(string path) =>
        Path.GetFileNameWithoutExtension(path).Replace(' ', '-').ToLowerInvariant();

    private sealed record ChangedDocEntry(
        string Path, string Component, string Status, string? CurrentHash, string? IndexedHash,
        string? From = null, int? CriteriaCount = null);
}
