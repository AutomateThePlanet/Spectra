using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Agent.Copilot;
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
    private readonly ProgressReporter _progress;

    private static readonly JsonSerializerOptions ResultFileOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public DocsIndexHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        OutputFormat outputFormat = OutputFormat.Human,
        bool noInteraction = false,
        bool skipCriteria = false)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _outputFormat = outputFormat;
        _noInteraction = noInteraction;
        _skipCriteria = skipCriteria;
        _progress = new ProgressReporter(outputFormat: outputFormat);
    }

    public async Task<int> ExecuteAsync(bool force, CancellationToken ct = default)
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
        var indexPath = DocumentIndexService.ResolveIndexPath(currentDir, config.Source);

        if (_dryRun)
        {
            var (total, changed) = await indexService.GetUpdateStatsAsync(currentDir, config.Source, ct);
            _progress.Info($"Dry run: {total} documents found, {changed} would be updated");
            _progress.Info($"Index path: {indexPath}");
            return ExitCodes.Success;
        }

        // Phase 1: Scanning
        WriteProgressResult("scanning", "Scanning for documentation files...", currentDir, indexPath);

        var index = await _progress.StatusAsync(
            force ? "Rebuilding documentation index..." : "Updating documentation index...",
            async () => await indexService.EnsureIndexAsync(currentDir, config.Source, force, ct));

        // Phase 2: Indexing complete
        WriteProgressResult("indexing", $"Indexed {index.TotalDocuments} documents", currentDir, indexPath,
            documentsIndexed: index.TotalDocuments, documentsTotal: index.TotalDocuments);

        if (_outputFormat != OutputFormat.Json)
        {
            _progress.Success($"Documentation index updated: {index.TotalDocuments} documents, " +
                              $"{index.TotalWordCount:N0} words, ~{index.TotalEstimatedTokens:N0} tokens");
            _progress.Info($"Index written to: {Path.GetRelativePath(currentDir, indexPath)}");
        }

        // Phase 3: Auto-extract acceptance criteria (unless skipped)
        int? criteriaExtracted = null;
        string? criteriaFile = null;
        if (!_skipCriteria)
        {
            WriteProgressResult("extracting-criteria", "Extracting acceptance criteria...", currentDir, indexPath,
                documentsIndexed: index.TotalDocuments, documentsTotal: index.TotalDocuments);

            (criteriaExtracted, criteriaFile) = await TryExtractCriteriaAsync(currentDir, config, ct);
        }

        // Phase 4: Completed — write final result
        var result = new DocsIndexResult
        {
            Command = "docs-index",
            Status = "completed",
            Message = "Documentation index updated",
            DocumentsIndexed = index.TotalDocuments,
            DocumentsUpdated = index.TotalDocuments,
            DocumentsTotal = index.TotalDocuments,
            IndexPath = Path.GetRelativePath(currentDir, indexPath),
            CriteriaExtracted = criteriaExtracted,
            CriteriaFile = criteriaFile
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

    private async Task<(int? criteriaCount, string? criteriaFile)> TryExtractCriteriaAsync(
        string currentDir, SpectraConfig config, CancellationToken ct)
    {
        var provider = config.Ai.Providers.FirstOrDefault(p => p.Enabled);
        if (provider is null)
            return (null, null);

        var reqsPath = Path.Combine(currentDir, config.Coverage.RequirementsFile);

        // Ensure criteria directory exists
        var reqsDir = Path.GetDirectoryName(reqsPath);
        if (!string.IsNullOrEmpty(reqsDir))
            Directory.CreateDirectory(reqsDir);

        var parser = new RequirementsParser();
        var existing = await parser.ParseAsync(reqsPath, ct);

        var docBuilder = new DocumentMapBuilder();
        var documentMap = await docBuilder.BuildAsync(currentDir, ct);

        if (documentMap.Documents.Count == 0)
            return (0, null);

        if (_verbosity >= VerbosityLevel.Normal)
            _progress.Info($"Extracting acceptance criteria from {documentMap.Documents.Count} document(s)...");

        try
        {
            var extractor = new RequirementsExtractor(
                provider,
                currentDir,
                _verbosity >= VerbosityLevel.Normal ? s => _progress.Info(s) : null);

            // Hard timeout via Task.WhenAny — Copilot SDK may not honor CancellationToken
            var extractTask = extractor.ExtractAsync(documentMap.Documents, existing, ct);
            var deadlineTask = Task.Delay(TimeSpan.FromSeconds(60), ct);
            var completed = await Task.WhenAny(extractTask, deadlineTask);

            if (completed == deadlineTask)
            {
                _progress.Warning("Acceptance criteria extraction timed out. Run 'spectra ai analyze --extract-criteria' separately.");
                return (0, null);
            }

            var extracted = await extractTask;

            if (extracted.Count == 0)
            {
                if (_verbosity >= VerbosityLevel.Normal)
                    _progress.Info("No new acceptance criteria found in documentation.");
                return (0, null);
            }

            if (_dryRun)
            {
                _progress.Info($"Would extract {extracted.Count} acceptance criteria (dry run).");
                return (extracted.Count, null);
            }

            var writer = new RequirementsWriter();
            var writeResult = await writer.MergeAndWriteAsync(reqsPath, extracted, ct);

            _progress.Success($"Acceptance criteria extracted: {writeResult.Merged.Count} new, " +
                              $"{writeResult.SkippedCount} duplicates skipped, {writeResult.TotalInFile} total");

            return (writeResult.TotalInFile, Path.GetRelativePath(currentDir, reqsPath));
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
            return (null, null);
        }
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

    private static string GetResultFilePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");

    private static string GetProgressPagePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), ".spectra-progress.html");

    private static void DeleteResultFiles()
    {
        try
        {
            var resultPath = GetResultFilePath();
            if (File.Exists(resultPath))
                File.Delete(resultPath);

            var progressPath = GetProgressPagePath();
            if (File.Exists(progressPath))
                File.Delete(progressPath);
        }
        catch
        {
            // Non-critical
        }
    }

    private static void FlushWriteFile(string path, string json)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(fs);
        writer.Write(json);
        writer.Flush();
        fs.Flush(true); // Force OS to flush to disk
    }

    private static void WriteResultFile(DocsIndexResult result, bool isTerminal)
    {
        try
        {
            var json = JsonSerializer.Serialize(result, ResultFileOptions);
            FlushWriteFile(GetResultFilePath(), json);
            Progress.ProgressPageWriter.WriteProgressPage(GetProgressPagePath(), json, isTerminal);
        }
        catch
        {
            // Non-critical
        }
    }

    private static void WriteProgressResult(string status, string message, string currentDir, string indexPath,
        int documentsIndexed = 0, int documentsTotal = 0)
    {
        try
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
            var json = JsonSerializer.Serialize(result, ResultFileOptions);
            FlushWriteFile(GetResultFilePath(), json);
            Progress.ProgressPageWriter.WriteProgressPage(GetProgressPagePath(), json, isTerminal: false);
        }
        catch
        {
            // Non-critical
        }
    }

    private static void WriteErrorResult(string error)
    {
        try
        {
            var result = new DocsIndexResult
            {
                Command = "docs-index",
                Status = "failed",
                Message = error,
                IndexPath = ""
            };
            var json = JsonSerializer.Serialize(result, ResultFileOptions);
            FlushWriteFile(GetResultFilePath(), json);
            Progress.ProgressPageWriter.WriteProgressPage(GetProgressPagePath(), json, isTerminal: true);
        }
        catch
        {
            // Non-critical
        }
    }
}
