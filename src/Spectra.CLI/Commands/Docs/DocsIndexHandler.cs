#pragma warning disable CS0618 // RequirementDefinition is obsolete — Spec 047 keeps the legacy extractor; full merge is out of scope.
using System.Text.Json;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.CLI.Source;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Handles the docs index command execution.
/// </summary>
public sealed class DocsIndexHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly OutputFormat _outputFormat;
    private readonly bool _noInteraction;
    private readonly bool _skipCriteria;
    private readonly bool _noMigrate;
    private readonly bool _includeArchived;
    private readonly IReadOnlyList<string>? _suiteFilter;
    private readonly ProgressReporter _progress;

    private static readonly Progress.ProgressManager _progressManager =
        new("docs-index", Progress.ProgressPhases.DocsIndex, title: "Documentation Index");

    public DocsIndexHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        OutputFormat outputFormat = OutputFormat.Human,
        bool noInteraction = false,
        bool skipCriteria = false,
        bool noMigrate = false,
        bool includeArchived = false,
        IReadOnlyList<string>? suiteFilter = null)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _outputFormat = outputFormat;
        _noInteraction = noInteraction;
        _skipCriteria = skipCriteria;
        _noMigrate = noMigrate;
        _includeArchived = includeArchived;
        _suiteFilter = suiteFilter;
        _progress = new ProgressReporter(outputFormat: outputFormat);
    }

    public async Task<int> ExecuteAsync(bool force, CancellationToken ct = default)
    {
        // Spec 040: cooperative cancellation via .spectra/.cancel sentinel.
        using var cancelRegistration = await Cancellation.CancellationManager.Instance
            .RegisterCommandAsync("docs index", ct).ConfigureAwait(false);
        ct = Cancellation.CancellationManager.Instance.Token;

        try
        {
            return await ExecuteCoreAsync(force, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Cancellation.CancelledResultWriter.WriteMinimal("docs index", "indexing");
            return ExitCodes.Cancelled;
        }
    }

    private async Task<int> ExecuteCoreAsync(bool force, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Delete stale result/progress files
        DeleteResultFiles();

        // Load config
        if (!File.Exists(configPath))
        {
            _progress.Error("spectra.config.json not found. Run 'spectra init' first.");
            WriteErrorResult("spectra.config.json not found");
            return ExitCodes.Error;
        }

        var config = await LoadConfigAsync(configPath, ct);
        if (config is null)
        {
            _progress.Error("Failed to load spectra.config.json");
            WriteErrorResult("Failed to load spectra.config.json");
            return ExitCodes.Error;
        }

        var indexService = new DocumentIndexService();
        var legacyIndexPath = DocumentIndexService.ResolveIndexPath(currentDir, config.Source);
        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(currentDir, config.Source);

        if (_dryRun)
        {
            var (total, changed) = await indexService.GetUpdateStatsAsync(currentDir, config.Source, ct);
            _progress.Info($"Dry run: {total} documents found, {changed} would be updated");
            _progress.Info($"Manifest path: {Path.GetRelativePath(currentDir, manifestPath)}");
            return ExitCodes.Success;
        }

        // ── Migration phase: detect legacy layout and migrate before indexing ──
        var migrator = new LegacyIndexMigrator();
        MigrationRecord? migrationRecord = null;
        if (migrator.NeedsMigration(currentDir, config.Source))
        {
            if (_noMigrate)
            {
                var msg = $"Legacy '{Path.GetRelativePath(currentDir, legacyIndexPath)}' detected and " +
                          "--no-migrate was specified. Re-run without --no-migrate to migrate to the v2 layout.";
                _progress.Error(msg);
                WriteErrorResult(msg);
                return ExitCodes.Error;
            }

            WriteProgressResult("migrating", "Migrating legacy index to v2 layout...", currentDir, manifestPath);
            try
            {
                migrationRecord = await _progress.StatusAsync(
                    "Migrating legacy documentation index...",
                    async () => await migrator.MigrateAsync(currentDir, config.Source, config.Coverage, ct));

                if (migrationRecord.Performed && _outputFormat != OutputFormat.Json)
                {
                    var summary = $"Migrated {migrationRecord.DocumentsMigrated} docs across " +
                                  $"{migrationRecord.SuitesCreated} suites.";
                    if (!string.IsNullOrEmpty(migrationRecord.LargestSuiteId))
                    {
                        summary += $" Largest suite: {migrationRecord.LargestSuiteId} " +
                                   $"(~{migrationRecord.LargestSuiteTokens:N0} tokens).";
                    }
                    if (!string.IsNullOrEmpty(migrationRecord.LegacyFile))
                    {
                        summary += $" Legacy index preserved as {migrationRecord.LegacyFile} — " +
                                   "safe to delete after verification.";
                    }
                    _progress.Success(summary);
                    foreach (var warning in migrationRecord.Warnings)
                    {
                        _progress.Warning(warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _progress.Error($"Migration failed: {ex.Message}");
                WriteErrorResult($"Migration failed: {ex.Message}");
                return ExitCodes.Error;
            }
        }

        // ── Indexing phase: build v2 layout ──
        WriteProgressResult("scanning", "Scanning for documentation files...", currentDir, manifestPath);

        var newLayout = await _progress.StatusAsync(
            force ? "Rebuilding documentation index..." : "Updating documentation index...",
            async () => await indexService.EnsureNewLayoutAsync(
                currentDir,
                config.Source,
                config.Coverage,
                forceRebuild: force,
                suiteFilter: _suiteFilter,
                ct));

        WriteProgressResult("writing-manifest",
            $"Indexed {newLayout.Manifest.TotalDocuments} documents across {newLayout.Manifest.Groups.Count} suite(s)",
            currentDir,
            manifestPath,
            documentsIndexed: newLayout.Manifest.TotalDocuments,
            documentsTotal: newLayout.Manifest.TotalDocuments);

        if (_outputFormat != OutputFormat.Json)
        {
            _progress.Success(
                $"Documentation index updated: {newLayout.Manifest.TotalDocuments} documents, " +
                $"{newLayout.Manifest.TotalWords:N0} words, ~{newLayout.Manifest.TotalTokensEstimated:N0} tokens, " +
                $"{newLayout.Manifest.Groups.Count} suite(s).");
            _progress.Info($"Manifest written to: {Path.GetRelativePath(currentDir, manifestPath)}");
            foreach (var warning in newLayout.ResolutionWarnings)
            {
                _progress.Warning(warning);
            }
        }

        // ── Criteria extraction phase ──
        int? criteriaExtracted = null;
        string? criteriaFile = null;
        string? criteriaWarning = null;
        if (!_skipCriteria)
        {
            WriteProgressResult("extracting-criteria", "Extracting acceptance criteria...", currentDir, manifestPath,
                documentsIndexed: newLayout.Manifest.TotalDocuments,
                documentsTotal: newLayout.Manifest.TotalDocuments);

            (criteriaExtracted, criteriaFile, criteriaWarning) = await TryExtractCriteriaAsync(currentDir, config, ct);
        }

        // ── Final result ──
        var suiteEntries = newLayout.Manifest.Groups
            .Select(g => new SuiteResultEntry
            {
                Id = g.Id,
                DocumentCount = g.DocumentCount,
                TokensEstimated = g.TokensEstimated,
                SkipAnalysis = g.SkipAnalysis,
                ExcludedBy = g.ExcludedBy,
                ExcludedPattern = g.ExcludedPattern,
                IndexFile = $"{Path.GetRelativePath(currentDir, newLayout.IndexDir).Replace('\\', '/')}/{g.IndexFile}",
            })
            .ToList();

        var result = new DocsIndexResult
        {
            Command = "docs-index",
            Status = "completed",
            Message = "Documentation index updated",
            DocumentsIndexed = newLayout.Manifest.TotalDocuments,
            DocumentsUpdated = newLayout.ChangedDocuments + newLayout.NewDocuments,
            DocumentsTotal = newLayout.Manifest.TotalDocuments,
            DocumentsNew = newLayout.NewDocuments,
            DocumentsSkipped = newLayout.SkippedDocuments,
            // Legacy field — points at the manifest now since the single-file
            // path is gone. Phase 4 either repurposes or removes this.
            IndexPath = Path.GetRelativePath(currentDir, manifestPath).Replace('\\', '/'),
            Manifest = Path.GetRelativePath(currentDir, manifestPath).Replace('\\', '/'),
            Suites = suiteEntries,
            Migration = migrationRecord,
            CriteriaExtracted = criteriaExtracted,
            CriteriaFile = criteriaFile,
            CriteriaWarning = criteriaWarning,
        };

        WriteResultFile(result, isTerminal: true);

        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
            return ExitCodes.Success;
        }

        NextStepHints.Print("docs-index", true, _verbosity, outputFormat: _outputFormat);
        return ExitCodes.Success;
    }

    private async Task<(int? criteriaCount, string? criteriaFile, string? criteriaWarning)> TryExtractCriteriaAsync(
        string currentDir, SpectraConfig config, CancellationToken ct)
    {
        var provider = config.Ai.Providers.FirstOrDefault(p => p.Enabled);
        if (provider is null)
            return (null, null, null);

        var reqsPath = Path.Combine(currentDir, config.Coverage.RequirementsFile);

        // Ensure criteria directory exists
        var reqsDir = Path.GetDirectoryName(reqsPath);
        if (!string.IsNullOrEmpty(reqsDir))
            Directory.CreateDirectory(reqsDir);

        var parser = new RequirementsParser();
        var existing = await parser.ParseAsync(reqsPath, ct);

        var docBuilder = new DocumentMapBuilder();
        var documentMap = await docBuilder.BuildAsync(currentDir, ct);

        // Spec 040 §3.7 / FR-015: criteria extraction is AI-facing — drop
        // documents in skip_analysis suites unless --include-archived was passed.
        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(currentDir, config.Source);
        var indexDir = LegacyIndexMigrator.ResolveIndexDir(currentDir, config.Source);
        documentMap = await ManifestDocumentFilter.FilterAsync(
            documentMap, manifestPath, indexDir, _includeArchived, ct);

        if (documentMap.Documents.Count == 0)
            return (0, null, null);

        if (_verbosity >= VerbosityLevel.Normal)
            _progress.Info($"Extracting acceptance criteria from {documentMap.Documents.Count} document(s)...");

        try
        {
            var extractor = new RequirementsExtractor(
                provider,
                currentDir,
                _verbosity >= VerbosityLevel.Normal ? s => _progress.Info(s) : null);

            // Spec 047: per-document deadline (matching AnalyzeHandler) replaces the
            // 60s corpus deadline. One slow doc no longer aborts the whole corpus.
            var loopResult = await ExtractCriteriaLoopAsync(
                documents: documentMap.Documents,
                existing: existing,
                extractPerDoc: (doc, token) => extractor.ExtractFromDocumentAsync(doc, existing, token),
                perDocDeadline: PerDocumentDeadline,
                onSlowDoc: path => _progress.Warning(
                    $"Acceptance criteria extraction timed out for {path}. " +
                    "Run 'spectra ai analyze --extract-criteria' separately for this document."),
                onDocFailure: (path, ex) =>
                {
                    _progress.Warning($"Acceptance criteria extraction failed for {path}: {ex.Message}");
                    if (_verbosity >= VerbosityLevel.Detailed)
                        Console.Error.WriteLine(ex.StackTrace);
                },
                ct: ct);

            var extracted = loopResult.Aggregated;

            // Spec 048: corpus-wide zero-criteria gate. Fires when we indexed
            // documents but extraction produced nothing across the whole corpus
            // (commonly seen on large projects where every per-doc extraction
            // came back inconclusive; Spec 047 leaves those uncached/uncounted).
            // Suppressed when --skip-criteria gates this method off entirely.
            var corpusWarning = ComputeCriteriaWarning(documentMap.Documents.Count, extracted.Count);
            if (corpusWarning is not null)
                _progress.Warning(corpusWarning);

            if (extracted.Count == 0)
            {
                if (_verbosity >= VerbosityLevel.Normal && corpusWarning is null)
                    _progress.Info("No new acceptance criteria found in documentation.");
                return (0, null, corpusWarning);
            }

            if (_dryRun)
            {
                _progress.Info($"Would extract {extracted.Count} acceptance criteria (dry run).");
                return (extracted.Count, null, null);
            }

            var writer = new RequirementsWriter();
            var writeResult = await writer.MergeAndWriteAsync(reqsPath, extracted, ct);

            _progress.Success($"Acceptance criteria extracted: {writeResult.Merged.Count} new, " +
                              $"{writeResult.SkippedCount} duplicates skipped, {writeResult.TotalInFile} total");

            return (writeResult.TotalInFile, Path.GetRelativePath(currentDir, reqsPath), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _progress.Warning($"Acceptance criteria extraction failed: {ex.Message}");
            if (_verbosity >= VerbosityLevel.Detailed)
                Console.Error.WriteLine(ex.StackTrace);
            return (null, null, null);
        }
    }

    /// <summary>
    /// Spec 048: pure projection from the corpus-level counts to the zero-criteria
    /// warning string. Returns the warning when documents were indexed but zero
    /// criteria were extracted; null otherwise. Extracted as an internal static so
    /// the gate is testable in isolation without standing up the full handler.
    /// </summary>
    internal static string? ComputeCriteriaWarning(int documentsIndexed, int criteriaExtractedTotal)
    {
        if (documentsIndexed <= 0 || criteriaExtractedTotal > 0)
            return null;
        return $"Indexed {documentsIndexed} document(s) but extracted 0 acceptance criteria. " +
               "Test generation will not be able to link criteria. " +
               "Run: spectra ai analyze --extract-criteria";
    }

    /// <summary>Spec 047: per-document deadline (mirrors <c>AnalyzeHandler</c>).</summary>
    internal static readonly TimeSpan PerDocumentDeadline = TimeSpan.FromMinutes(2);

    internal sealed record CriteriaLoopResult(
        IReadOnlyList<Spectra.Core.Models.Coverage.RequirementDefinition> Aggregated,
        IReadOnlyList<string> TimedOutDocuments,
        IReadOnlyList<string> FailedDocuments);

    /// <summary>
    /// Spec 047: per-document extraction loop with an injectable per-doc deadline
    /// and extractor delegate. Extracted as <c>internal static</c> so tests can
    /// drive the loop without standing up a real Copilot session.
    ///
    /// One slow document is reported via <paramref name="onSlowDoc"/> and skipped;
    /// other documents continue. An exception in one document is reported via
    /// <paramref name="onDocFailure"/> and that document is skipped; remaining
    /// documents continue. Cancellation propagates.
    /// </summary>
    internal static async Task<CriteriaLoopResult> ExtractCriteriaLoopAsync(
        IReadOnlyList<Spectra.Core.Models.DocumentEntry> documents,
        IReadOnlyList<Spectra.Core.Models.Coverage.RequirementDefinition> existing,
        Func<Spectra.Core.Models.DocumentEntry, CancellationToken, Task<RequirementsExtractionResult>> extractPerDoc,
        TimeSpan perDocDeadline,
        Action<string>? onSlowDoc,
        Action<string, Exception>? onDocFailure,
        CancellationToken ct)
    {
        var aggregated = new List<Spectra.Core.Models.Coverage.RequirementDefinition>();
        var timedOut = new List<string>();
        var failed = new List<string>();

        foreach (var doc in documents)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var extractTask = extractPerDoc(doc, ct);
                var deadlineTask = Task.Delay(perDocDeadline, ct);
                var completed = await Task.WhenAny(extractTask, deadlineTask);

                if (completed == deadlineTask)
                {
                    timedOut.Add(doc.Path);
                    onSlowDoc?.Invoke(doc.Path);
                    continue;
                }

                var perDoc = await extractTask;

                // Spec 054: the extractor no longer throws on empty/parse failure — it returns a
                // typed outcome. Aggregate only genuine (cacheable) extractions; a non-cacheable
                // outcome is an inconclusive document, surfaced via the same failed channel that
                // a thrown exception used to use.
                if (perDoc.IsCacheable)
                {
                    aggregated.AddRange(perDoc.Requirements);
                }
                else
                {
                    failed.Add(doc.Path);
                    onDocFailure?.Invoke(
                        doc.Path,
                        new InvalidOperationException($"Extraction inconclusive ({perDoc.Outcome})."));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed.Add(doc.Path);
                onDocFailure?.Invoke(doc.Path, ex);
            }
        }

        return new CriteriaLoopResult(aggregated, timedOut, failed);
    }

    private static async Task<SpectraConfig?> LoadConfigAsync(string configPath, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            return JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Result/progress file helpers ──

    private static void DeleteResultFiles()
    {
        _progressManager.Reset();
    }

    private static void WriteResultFile(DocsIndexResult result, bool isTerminal)
    {
        if (isTerminal)
            _progressManager.Complete(result);
        else
            _progressManager.Update(result);
    }

    private static void WriteProgressResult(string status, string message, string currentDir, string indexPath,
        int documentsIndexed = 0, int documentsTotal = 0)
    {
        var result = new DocsIndexResult
        {
            Command = "docs-index",
            Status = status,
            Message = message,
            DocumentsIndexed = documentsIndexed,
            DocumentsTotal = documentsTotal,
            IndexPath = Path.GetRelativePath(currentDir, indexPath)
        };
        _progressManager.Update(result);
    }

    private static void WriteErrorResult(string error)
    {
        _progressManager.Fail(error);
    }
}
