using System.Text.Json;
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

        // Spec 069 (FR-006/FR-007): docs index is index-only. Acceptance-criteria extraction is now
        // the skill-driven compile-extraction-prompt → in-session turn → ingest-criteria seam
        // (the spectra-criteria skill), not an inline model call — a short-lived non-interactive
        // process can no longer make a model turn. No docs/requirements/_requirements.yaml is produced.
        _ = (_skipCriteria, _includeArchived); // retired flags: accepted as no-ops

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
