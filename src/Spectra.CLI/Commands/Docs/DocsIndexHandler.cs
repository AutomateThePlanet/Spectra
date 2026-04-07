using System.Text.Json;
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
    private readonly ProgressReporter _progress;

    public DocsIndexHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, bool dryRun = false, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _outputFormat = outputFormat;
        _progress = new ProgressReporter(outputFormat: outputFormat);
    }

    public async Task<int> ExecuteAsync(bool force, CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config
        if (!File.Exists(configPath))
        {
            _progress.Error("spectra.config.json not found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        var config = await LoadConfigAsync(configPath, ct);
        if (config is null)
        {
            _progress.Error("Failed to load spectra.config.json");
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

        var index = await _progress.StatusAsync(
            force ? "Rebuilding documentation index..." : "Updating documentation index...",
            async () => await indexService.EnsureIndexAsync(currentDir, config.Source, force, ct));

        if (_outputFormat != OutputFormat.Json)
        {
            _progress.Success($"Documentation index updated: {index.TotalDocuments} documents, " +
                              $"{index.TotalWordCount:N0} words, ~{index.TotalEstimatedTokens:N0} tokens");
            _progress.Info($"Index written to: {Path.GetRelativePath(currentDir, indexPath)}");
        }

        // Auto-extract requirements if AI provider is configured
        await TryExtractRequirementsAsync(currentDir, config, ct);

        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(new DocsIndexResult
            {
                Command = "docs-index",
                Status = "success",
                DocumentsIndexed = index.TotalDocuments,
                DocumentsUpdated = index.TotalDocuments,
                IndexPath = Path.GetRelativePath(currentDir, indexPath)
            });
            return ExitCodes.Success;
        }

        NextStepHints.Print("docs-index", true, _verbosity, outputFormat: _outputFormat);
        return ExitCodes.Success;
    }

    private async Task TryExtractRequirementsAsync(string currentDir, SpectraConfig config, CancellationToken ct)
    {
        var provider = config.Ai.Providers.FirstOrDefault(p => p.Enabled);
        if (provider is null)
            return;

        var reqsPath = Path.Combine(currentDir, config.Coverage.RequirementsFile);

        // Ensure requirements directory exists
        var reqsDir = Path.GetDirectoryName(reqsPath);
        if (!string.IsNullOrEmpty(reqsDir))
            Directory.CreateDirectory(reqsDir);

        var parser = new RequirementsParser();
        var existing = await parser.ParseAsync(reqsPath, ct);

        var docBuilder = new DocumentMapBuilder();
        var documentMap = await docBuilder.BuildAsync(currentDir, ct);

        if (documentMap.Documents.Count == 0)
            return;

        if (_verbosity >= VerbosityLevel.Normal)
            _progress.Info($"Extracting requirements from {documentMap.Documents.Count} document(s)...");

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
                _progress.Warning("Requirements extraction timed out. Run 'spectra ai analyze --extract-requirements' separately.");
                return;
            }

            var extracted = await extractTask;

            if (extracted.Count == 0)
            {
                if (_verbosity >= VerbosityLevel.Normal)
                    _progress.Info("No new requirements found in documentation.");
                return;
            }

            if (_dryRun)
            {
                _progress.Info($"Would extract {extracted.Count} requirement(s) (dry run).");
                return;
            }

            var writer = new RequirementsWriter();
            var writeResult = await writer.MergeAndWriteAsync(reqsPath, extracted, ct);

            _progress.Success($"Requirements extracted: {writeResult.Merged.Count} new, " +
                              $"{writeResult.SkippedCount} duplicates skipped, {writeResult.TotalInFile} total");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _progress.Warning($"Requirements extraction failed: {ex.Message}");
            if (_verbosity >= VerbosityLevel.Detailed)
                Console.Error.WriteLine(ex.StackTrace);
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
}
