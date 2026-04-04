using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Source;
using Spectra.Core.Models.Config;

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

        _progress.Success($"Documentation index updated: {index.TotalDocuments} documents, " +
                          $"{index.TotalWordCount:N0} words, ~{index.TotalEstimatedTokens:N0} tokens");
        _progress.Info($"Index written to: {Path.GetRelativePath(currentDir, indexPath)}");

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
}
