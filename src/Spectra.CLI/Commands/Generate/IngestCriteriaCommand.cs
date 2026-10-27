using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Extraction;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Config;
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
    private const int ExitInvalidCriterionId = 7; // Spec 079: unknown / foreign / repeated criterion id

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
        // Spec 080 A5 / SC-006: early errors (before any response is classified) use the shared criteria failure
        // shape, so JSON mode is always one document on stderr. They carry no `outcome`.
        if (string.IsNullOrWhiteSpace(doc))
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-criteria", null, "A source document is required (--doc).");
            return ExitError;
        }

        var currentDir = Directory.GetCurrentDirectory();
        // Spec 083: reads `tests` (reference counts) — a missing one warns; no section is required.
        var loaded = await CriteriaCommandIO.LoadConfigAsync(currentDir, ConfigSections.Tests, ct);
        if (!loaded.IsSuccess)
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-criteria", loaded.ErrorCode, loaded.Error!);
            return ExitError;
        }
        var config = loaded.Config!;
        var configWarnings = loaded.Warnings;

        // Read agent content from file or stdin.
        string content;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!File.Exists(from))
            {
                CriteriaCommandIO.WriteFailure(json, "ingest-criteria", null, $"Content file not found: {from}", configWarnings);
                return ExitError;
            }
            content = await File.ReadAllTextAsync(from, ct);
        }
        else
        {
            content = await Console.In.ReadToEndAsync(ct);
        }

        // Best-effort source-document hash for the cache entry (skipped if the doc is unreadable).
        // A Windows-style docs\file.md is not a path on Unix; '/' works on every OS.
        var docOnDisk = doc.Replace('\\', '/');
        var docFullPath = Path.IsPathRooted(docOnDisk) ? docOnDisk : Path.Combine(currentDir, docOnDisk);
        var docHash = File.Exists(docFullPath)
            ? await CriteriaIngestor.TryComputeDocHashAsync(docFullPath, ct)
            : null;

        var ingestor = new CriteriaIngestor(config);
        var result = await ingestor.IngestAsync(content, currentDir, doc, component, docHash, dryRun, ct);

        if (result.IsSuccess)
        {
            // Spec 079: the success JSON is the whole of stdout in JSON mode; the human changeset summary goes to
            // stderr there, and to stdout in human mode. It is never suppressed.
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    outcome = result.Outcome.ToString(),
                    doc = result.Doc,
                    persisted = result.PersistedCriteria.Count,
                    ids = result.PersistedCriteria.Select(c => c.Id).ToArray(),
                    dry_run = dryRun,
                    written = result.Written,
                    changeset = result.Changeset,
                    duplicate_ids = result.DuplicateIds,
                    migration = result.Migration,
                    warnings = configWarnings.Concat(result.Warnings).ToArray()
                }));
            }

            var human = json ? Console.Error : Console.Out;
            if (!json)
            {
                var verb = dryRun ? "Would persist" : result.Written ? "Persisted" : "Unchanged —";
                human.WriteLine($"{verb} {result.PersistedCriteria.Count} criterion(s) for '{result.Doc}': "
                    + string.Join(", ", result.PersistedCriteria.Select(c => c.Id)));
            }

            if (result.Migration is { } migration)
            {
                human.WriteLine($"Migrated criteria storage first: {migration.Moved.Count} file(s) moved, "
                    + $"{migration.Collisions.Count} collision(s) reported.");
            }

            if (result.Changeset is not null)
            {
                CriteriaChangesetPresenter.Write(human, result.Changeset,
                    dryRun ? $"Criteria changeset for {result.Doc} (dry run — nothing written)" : null);
            }

            CriteriaChangesetPresenter.WriteDuplicates(human, result.DuplicateIds);
            foreach (var warning in configWarnings.Concat(result.Warnings))
            {
                human.WriteLine($"Warning: {warning}");
            }

            return ExitSuccess;
        }

        // Fail loud — nothing persisted (FR-003, FR-006). Failure output stays machine-readable, no summary.
        if (json)
        {
            // Spec 081 C4: the shared criteria failure shape, plus the ingest outcome.
            CriteriaCommandIO.WriteFailureJson(
                "ingest-criteria",
                result.ErrorCode,
                result.Errors.Count > 0 ? result.Errors : [result.Outcome.ToString()],
                extras: new Dictionary<string, object?> { ["outcome"] = result.Outcome.ToString() });
        }
        else
        {
            Console.Error.WriteLine($"Ingest failed [{result.ErrorCode ?? result.Outcome.ToString()}] — nothing persisted:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        if (result.ErrorCode is not null)
        {
            return result.ErrorCode == CriteriaIngestor.ErrorInvalidCriterionId ? ExitInvalidCriterionId : ExitError;
        }

        return result.Outcome == ExtractionOutcome.EmptyResponse ? ExitEmptyResponse : ExitParseFailure;
    }
}
