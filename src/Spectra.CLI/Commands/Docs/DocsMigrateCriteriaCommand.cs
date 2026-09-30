using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Config;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 079 FR-016a: <c>spectra docs migrate-criteria [--dry-run]</c> — the explicit one-time move from base-name
/// criteria files to per-doc files. Deterministic and idempotent; collisions are reported, never guessed.
/// Write commands (ingest, import, retire, apply-move) run the same migration automatically.
/// </summary>
public sealed class DocsMigrateCriteriaCommand : Command
{
    public DocsMigrateCriteriaCommand()
        : base("migrate-criteria", "Move criteria files to the per-document layout (deterministic, idempotent)")
    {
        var dryRunOption = new Option<bool>("--dry-run", "Report what would move, but write nothing");
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var json = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await RunAsync(
                context.ParseResult.GetValueForOption(dryRunOption), json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(bool dryRun, bool json, CancellationToken ct)
    {
        var root = Directory.GetCurrentDirectory();
        // Spec 082 A1: criteria are written under `coverage`, so it is required.
        var loaded = await CriteriaCommandIO.LoadConfigAsync(root, ConfigSections.Coverage, ConfigSections.None, ct);
        if (!loaded.IsSuccess)
        {
            CriteriaCommandIO.WriteFailure(json, "docs-migrate-criteria", loaded.ErrorCode, loaded.Error!);
            return ExitCodes.Error;
        }
        var config = loaded.Config!;

        MigrationReport report;
        try
        {
            report = await CriteriaStore.WithLockAsync(root, async () =>
                await CriteriaLayoutMigrator.MigrateAsync(await CriteriaStore.LoadAsync(config, root, ct), dryRun, ct), ct);
        }
        catch (CriteriaStoreException ex)
        {
            CriteriaCommandIO.WriteFailure(json, "docs-migrate-criteria", ex.ErrorCode, ex.Message);
            return ExitCodes.Error;
        }

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "docs-migrate-criteria",
                success = true,
                dry_run = report.DryRun,
                moved = report.Moved,
                normalized_source_docs = report.NormalizedSourceDocs,
                collisions = report.Collisions,
                high_water_marks_seeded = report.HighWaterMarksSeeded,
                already_migrated = report.AlreadyMigrated
            }, CriteriaCommandIO.Json));
        }
        else
        {
            if (report.AlreadyMigrated)
            {
                Console.Out.WriteLine("Criteria storage already uses the per-document layout.");
            }
            else
            {
                var verb = dryRun ? "Would move" : "Moved";
                foreach (var m in report.Moved)
                    Console.Out.WriteLine($"  {verb} {m.FromFile} → {m.ToFile}  ({m.Doc})");
                if (report.NormalizedSourceDocs > 0)
                    Console.Out.WriteLine($"  Normalised source_doc on {report.NormalizedSourceDocs} entr{(report.NormalizedSourceDocs == 1 ? "y" : "ies")}.");
                foreach (var (prefix, mark) in report.HighWaterMarksSeeded)
                    Console.Out.WriteLine($"  Seeded id high-water mark {prefix} = {mark}");
            }

            foreach (var c in report.Collisions)
                Console.Out.WriteLine($"  Collision: {c.File} is shared by {string.Join(" and ", c.Docs)} — {c.Recommendation}");
            if (dryRun)
                Console.Out.WriteLine("Dry run — nothing written.");
        }

        return ExitCodes.Success;
    }
}
