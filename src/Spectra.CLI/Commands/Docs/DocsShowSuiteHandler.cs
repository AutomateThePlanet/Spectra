using System.Text.Json;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Handler for <c>spectra docs show-suite &lt;id&gt;</c>.
/// </summary>
public sealed class DocsShowSuiteHandler
{
    private readonly ProgressReporter _progress = new();

    public async Task<int> ExecuteAsync(string suiteId, CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            _progress.Error("spectra.config.json not found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        SpectraConfig? config;
        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            config = JsonSerializer.Deserialize<SpectraConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _progress.Error($"Failed to load spectra.config.json: {ex.Message}");
            return ExitCodes.Error;
        }

        if (config is null) return ExitCodes.Error;

        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(currentDir, config.Source);
        var indexDir = LegacyIndexMigrator.ResolveIndexDir(currentDir, config.Source);
        var manifest = await new DocIndexManifestReader().ReadAsync(manifestPath, ct);
        if (manifest is null)
        {
            _progress.Error(
                $"Manifest not found at {Path.GetRelativePath(currentDir, manifestPath)}. " +
                "Run 'spectra docs index' first.");
            return ExitCodes.Error;
        }

        var suite = manifest.Groups.FirstOrDefault(g =>
            string.Equals(g.Id, suiteId, StringComparison.Ordinal));
        if (suite is null)
        {
            var available = string.Join(", ", manifest.Groups.Select(g => g.Id));
            _progress.Error(
                $"Suite '{suiteId}' not found in manifest. Available: {available}");
            return ExitCodes.Error;
        }

        var suiteFilePath = Path.Combine(indexDir, suite.IndexFile);
        if (!File.Exists(suiteFilePath))
        {
            _progress.Error(
                $"Suite '{suiteId}' is in the manifest but its index file is missing at " +
                $"{Path.GetRelativePath(currentDir, suiteFilePath)}.");
            return ExitCodes.Error;
        }

        var content = await File.ReadAllTextAsync(suiteFilePath, ct);
        Console.Write(content);
        return ExitCodes.Success;
    }
}
