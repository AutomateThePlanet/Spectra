using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Extraction;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 054: <c>spectra ai ingest-criteria</c>. The model-free, fail-loud boundary that classifies
/// agent-extracted content and persists only a genuine <see cref="ExtractionOutcome.Extracted"/>
/// result. Mirrors <see cref="IngestTestsCommand"/>. No model call.
/// </summary>
public sealed class IngestCriteriaCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitEmptyResponse = 5; // EmptyResponse
    private const int ExitParseFailure = 6;  // ParseFailure

    public IngestCriteriaCommand()
        : base("ingest-criteria", "Validate and persist agent-extracted acceptance criteria (fail-loud)")
    {
        var docOption = new Option<string?>(["--doc", "-d"], "Source document the criteria belong to") { IsRequired = true };
        var componentOption = new Option<string?>(["--component", "-c"], "Component override (defaults to a slug from the filename)");
        var fromOption = new Option<string?>("--from", "File containing the agent's JSON output (omit to read stdin)");
        var dryRunOption = new Option<bool>("--dry-run", "Classify and report, but persist nothing");

        AddOption(docOption);
        AddOption(componentOption);
        AddOption(fromOption);
        AddOption(dryRunOption);

        this.SetHandler(async (context) =>
        {
            var doc = context.ParseResult.GetValueForOption(docOption);
            var component = context.ParseResult.GetValueForOption(componentOption);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(doc, component, from, dryRun, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string? doc, string? component, string? from, bool dryRun, bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(doc))
        {
            Console.Error.WriteLine("A source document is required (--doc).");
            return ExitError;
        }

        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitError;
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
            return ExitError;
        }
        if (config is null)
        {
            Console.Error.WriteLine("Could not parse spectra.config.json.");
            return ExitError;
        }

        // Read agent content from file or stdin.
        string content;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!File.Exists(from))
            {
                Console.Error.WriteLine($"Content file not found: {from}");
                return ExitError;
            }
            content = await File.ReadAllTextAsync(from, ct);
        }
        else
        {
            content = await Console.In.ReadToEndAsync(ct);
        }

        // Best-effort source-document hash for the cache entry (skipped if the doc is unreadable).
        var docFullPath = Path.IsPathRooted(doc) ? doc : Path.Combine(currentDir, doc);
        var docHash = File.Exists(docFullPath)
            ? await CriteriaIngestor.TryComputeDocHashAsync(docFullPath, ct)
            : null;

        var ingestor = new CriteriaIngestor(config);
        var result = await ingestor.IngestAsync(content, currentDir, doc, component, docHash, dryRun, ct);

        if (result.IsSuccess)
        {
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    outcome = result.Outcome.ToString(),
                    persisted = result.PersistedCriteria.Count,
                    ids = result.PersistedCriteria.Select(c => c.Id).ToArray(),
                    dry_run = dryRun
                }));
            }
            else
            {
                var verb = dryRun ? "Would persist" : "Persisted";
                Console.Out.WriteLine($"{verb} {result.PersistedCriteria.Count} criterion(s) for '{doc}': "
                    + string.Join(", ", result.PersistedCriteria.Select(c => c.Id)));
            }
            return ExitSuccess;
        }

        // Fail loud — nothing persisted (FR-003, FR-006).
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                outcome = result.Outcome.ToString(),
                errors = result.Errors
            }));
        }
        else
        {
            Console.Error.WriteLine($"Ingest failed [{result.Outcome}] — nothing persisted:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        return result.Outcome == ExtractionOutcome.EmptyResponse ? ExitEmptyResponse : ExitParseFailure;
    }
}
