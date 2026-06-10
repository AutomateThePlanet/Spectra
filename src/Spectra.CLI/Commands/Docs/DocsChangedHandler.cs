using System.Text.Json;
using Spectra.CLI.Commands.Analyze;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Source;
using Spectra.Core.Models.Config;
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
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("spectra.config.json not found. Run 'spectra init' first.");
            return ExitCodes.Error;
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
            return ExitCodes.Error;
        }
        if (config is null)
        {
            Console.Error.WriteLine("Could not parse spectra.config.json.");
            return ExitCodes.Error;
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

            var existing = criteriaIndex.Sources
                .FirstOrDefault(s => string.Equals(s.SourceDoc, doc.Path, StringComparison.OrdinalIgnoreCase));
            var indexedHash = existing?.DocHash;

            var status = indexedHash is null
                ? "new"
                : (!string.Equals(indexedHash, currentHash, StringComparison.OrdinalIgnoreCase) ? "changed" : "unchanged");

            var component = Path.GetFileNameWithoutExtension(doc.Path).Replace(' ', '-').ToLowerInvariant();
            entries.Add(new ChangedDocEntry(doc.Path.Replace('\\', '/'), component, status, currentHash, indexedHash));
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path)); // stable, deterministic order

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
                    component = e.Component,
                    status = e.Status,
                    current_hash = e.CurrentHash,
                    indexed_hash = e.IndexedHash
                }).ToArray(),
                unchanged_count = unchangedCount
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else if (emitted.Count == 0)
        {
            output.WriteLine("All documents up to date — no changed criteria to extract.");
        }
        else
        {
            foreach (var e in emitted)
                output.WriteLine($"  [{e.Status,-9}] {e.Path}  (component: {e.Component})");
            output.WriteLine($"\n{changed.Count} changed, {unchangedCount} unchanged.");
        }

        return ExitCodes.Success;
    }

    private sealed record ChangedDocEntry(string Path, string Component, string Status, string CurrentHash, string? IndexedHash);
}
