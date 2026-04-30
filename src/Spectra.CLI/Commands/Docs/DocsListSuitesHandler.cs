using System.Text.Json;
using Spectra.CLI.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectre.Console;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Handler for <c>spectra docs list-suites</c>.
/// </summary>
public sealed class DocsListSuitesHandler
{
    private readonly OutputFormat _outputFormat;
    private readonly ProgressReporter _progress;

    public DocsListSuitesHandler(OutputFormat outputFormat = OutputFormat.Human)
    {
        _outputFormat = outputFormat;
        _progress = new ProgressReporter(outputFormat: outputFormat);
    }

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
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

        if (config is null)
        {
            _progress.Error("Failed to load spectra.config.json");
            return ExitCodes.Error;
        }

        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(currentDir, config.Source);
        var manifest = await new DocIndexManifestReader().ReadAsync(manifestPath, ct);
        if (manifest is null)
        {
            _progress.Error(
                $"Manifest not found at {Path.GetRelativePath(currentDir, manifestPath)}. " +
                "Run 'spectra docs index' first.");
            return ExitCodes.Error;
        }

        var entries = manifest.Groups
            .Select(g => new SuiteResultEntry
            {
                Id = g.Id,
                DocumentCount = g.DocumentCount,
                TokensEstimated = g.TokensEstimated,
                SkipAnalysis = g.SkipAnalysis,
                ExcludedBy = g.ExcludedBy,
                ExcludedPattern = g.ExcludedPattern,
                IndexFile = $"{Path.GetRelativePath(currentDir, LegacyIndexMigrator.ResolveIndexDir(currentDir, config.Source)).Replace('\\', '/')}/{g.IndexFile}",
            })
            .ToList();

        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(new ListSuitesResult
            {
                Command = "docs-list-suites",
                Status = "completed",
                Suites = entries,
                TotalSuites = entries.Count,
                TotalDocuments = manifest.TotalDocuments,
            });
            return ExitCodes.Success;
        }

        if (entries.Count == 0)
        {
            _progress.Info("No suites in manifest.");
            return ExitCodes.Success;
        }

        var table = new Table()
            .AddColumn("ID")
            .AddColumn(new TableColumn("Docs").RightAligned())
            .AddColumn(new TableColumn("Tokens").RightAligned())
            .AddColumn("Analysis")
            .AddColumn("Excluded By");

        foreach (var entry in entries)
        {
            var analysisStatus = entry.SkipAnalysis ? "[red]skip[/]" : "[green]on[/]";
            var excludedBy = entry.SkipAnalysis ? entry.ExcludedBy : "-";
            table.AddRow(
                Markup.Escape(entry.Id),
                entry.DocumentCount.ToString("N0"),
                entry.TokensEstimated.ToString("N0"),
                analysisStatus,
                Markup.Escape(excludedBy ?? "-"));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[dim]{entries.Count} suite(s) | {manifest.TotalDocuments:N0} document(s) | " +
            $"~{manifest.TotalTokensEstimated:N0} estimated tokens[/]");
        return ExitCodes.Success;
    }
}

/// <summary>
/// Result for the <c>list-suites</c> command JSON output.
/// </summary>
public sealed class ListSuitesResult : CommandResult
{
    [System.Text.Json.Serialization.JsonPropertyName("suites")]
    public required IReadOnlyList<SuiteResultEntry> Suites { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total_suites")]
    public required int TotalSuites { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total_documents")]
    public required int TotalDocuments { get; init; }
}
