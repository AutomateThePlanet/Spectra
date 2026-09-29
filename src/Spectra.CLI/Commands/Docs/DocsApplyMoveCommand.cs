using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Criteria;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 079 FR-020: <c>spectra docs apply-move --from &lt;old&gt; --to &lt;new&gt; [--dry-run]</c> — apply a move
/// reported by <c>docs changed</c>. The criteria and their IDs are kept; only <c>source_doc</c> and the criteria
/// file location follow the new path. No extraction runs. Refuses unless the content is unchanged.
/// </summary>
public sealed class DocsApplyMoveCommand : Command
{
    public const string ErrorTargetMissing = "TARGET_MISSING";
    public const string ErrorTargetIndexed = "TARGET_INDEXED";
    public const string ErrorContentChanged = "CONTENT_CHANGED";
    private const string Name = "docs-apply-move";

    public DocsApplyMoveCommand()
        : base("apply-move", "Apply a document move reported by 'docs changed' (keeps criteria and IDs)")
    {
        var fromOption = new Option<string?>("--from", "The document's old path") { IsRequired = true };
        var toOption = new Option<string?>("--to", "The document's new path") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Report what would change, but write nothing");
        AddOption(fromOption);
        AddOption(toOption);
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var json = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await RunAsync(context.ParseResult.GetValueForOption(fromOption),
                context.ParseResult.GetValueForOption(toOption), context.ParseResult.GetValueForOption(dryRunOption),
                json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? fromArg, string? toArg, bool dryRun, bool json, CancellationToken ct)
    {
        var root = Directory.GetCurrentDirectory();
        var (config, error) = await CriteriaCommandIO.LoadConfigAsync(root, ct);
        if (config is null)
        {
            CriteriaCommandIO.WriteFailure(json, Name, null, error!);
            return ExitCodes.Error;
        }

        var fromResult = CriteriaPaths.NormalizeSourceDoc(fromArg, root);
        var toResult = CriteriaPaths.NormalizeSourceDoc(toArg, root);
        if (!fromResult.IsSuccess || !toResult.IsSuccess)
        {
            CriteriaCommandIO.WriteFailure(json, Name, CriteriaPaths.ErrorInvalidDocPath, "Both paths must be inside the repository.");
            return ExitCodes.Error;
        }

        var (from, to) = (fromResult.Path!, toResult.Path!);
        try
        {
            var outcome = await CriteriaStore.WithLockAsync(root, async () =>
            {
                var ws = await CriteriaStore.LoadAsync(config, root, ct);
                var source = ws.FindDocumentSource(from);
                if (source is null)
                    return MoveOutcome.Refuse(DocsRetireCommand.ErrorNotIndexed, $"'{from}' is not an indexed document source.");
                if (File.Exists(Path.Combine(root, from)))
                    return MoveOutcome.Refuse(DocsRetireCommand.ErrorDocStillExists, $"'{from}' still exists; it has not moved.");
                var toFull = Path.Combine(root, to);
                if (!File.Exists(toFull))
                    return MoveOutcome.Refuse(ErrorTargetMissing, $"'{to}' does not exist.");
                if (ws.FindDocumentSource(to) is not null)
                    return MoveOutcome.Refuse(ErrorTargetIndexed, $"'{to}' already has criteria.");
                var hash = await FileHasher.ComputeFileHashAsync(toFull, ct);
                if (!string.Equals(hash, source.DocHash, StringComparison.OrdinalIgnoreCase))
                    return MoveOutcome.Refuse(ErrorContentChanged, $"'{to}' differs from the content recorded for '{from}'; extract it instead.");

                MigrationReport? migration = null;
                if (!dryRun && CriteriaPaths.IsOldLayout(ws.Index))
                {
                    var report = await CriteriaLayoutMigrator.MigrateAsync(ws, dryRun: false, ct);
                    if (!report.AlreadyMigrated)
                    {
                        migration = report;
                        ws = await CriteriaStore.LoadAsync(config, root, ct);
                        source = ws.FindDocumentSource(from)!;
                    }
                }

                var oldFile = CriteriaPaths.NormalizeLexical(source.File);
                var newFile = CriteriaPaths.DerivedFile(to);
                if (ws.FileOwnedByOther(newFile, source) is { } owner)
                    return MoveOutcome.Error(CriteriaPaths.ErrorFileCollision, $"Criteria file '{newFile}' already belongs to '{owner.SourceDoc ?? owner.File}'.");

                var criteria = string.IsNullOrWhiteSpace(oldFile) ? [] : await ws.ReadStrictAsync(oldFile, ct);
                if (!dryRun)
                {
                    foreach (var criterion in criteria)
                        criterion.SourceDoc = to;
                    await ws.WriteCriteriaAsync(newFile, criteria, to, source.DocHash, ct);

                    var sharedOld = ws.Index.Sources.Any(s => !ReferenceEquals(s, source)
                        && string.Equals(CriteriaPaths.NormalizeLexical(s.File), oldFile, StringComparison.OrdinalIgnoreCase));
                    if (!sharedOld && oldFile != newFile && File.Exists(ws.FullPath(oldFile)))
                        File.Delete(ws.FullPath(oldFile));

                    source.SourceDoc = to;
                    source.File = newFile;
                    await ws.SaveIndexAsync(ct);
                }

                return MoveOutcome.Done(criteria.Count, migration);
            }, ct);

            if (outcome.ErrorCode is not null)
            {
                CriteriaCommandIO.WriteFailure(json, Name, outcome.ErrorCode, outcome.Message!);
                return outcome.Refused ? ExitCodes.NotFound : ExitCodes.Error;
            }

            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = Name,
                    success = true,
                    from,
                    to,
                    criteria_count = outcome.CriteriaCount,
                    dry_run = dryRun,
                    written = !dryRun,
                    migration = outcome.Migration
                }, CriteriaCommandIO.Json));
            }
            else
            {
                Console.Out.WriteLine(dryRun
                    ? $"Would move {outcome.CriteriaCount} criteria from {from} to {to} (dry run — nothing written)."
                    : $"Moved {outcome.CriteriaCount} criteria from {from} to {to}; IDs unchanged.");
            }

            return ExitCodes.Success;
        }
        catch (CriteriaStoreException ex)
        {
            CriteriaCommandIO.WriteFailure(json, Name, ex.ErrorCode, ex.Message);
            return ExitCodes.Error;
        }
    }

    private sealed record MoveOutcome(string? ErrorCode, string? Message, bool Refused, int CriteriaCount, MigrationReport? Migration)
    {
        public static MoveOutcome Refuse(string code, string message) => new(code, message, true, 0, null);

        public static MoveOutcome Error(string code, string message) => new(code, message, false, 0, null);

        public static MoveOutcome Done(int count, MigrationReport? migration) => new(null, null, false, count, migration);
    }
}
