using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Config;
using Spectra.Core.Criteria;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 079 FR-021: <c>spectra docs retire --doc &lt;path&gt; [--dry-run]</c> — explicitly retire a document that
/// was deleted: remove its criteria-index entry and its criteria file, and report every criterion as removed
/// (with the tests still referencing them). Model-free; refuses a doc that still exists. ID high-water marks are
/// never lowered.
/// </summary>
public sealed class DocsRetireCommand : Command
{
    public const string ErrorNotIndexed = "NOT_INDEXED";
    public const string ErrorDocStillExists = "DOC_STILL_EXISTS";
    private const string Name = "docs-retire";

    public DocsRetireCommand()
        : base("retire", "Retire a deleted document's acceptance criteria (explicit; --dry-run to preview)")
    {
        var docOption = new Option<string?>(["--doc", "-d"], "The deleted document (as listed by 'docs changed')") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Report the changeset, but write nothing");
        AddOption(docOption);
        AddOption(dryRunOption);

        this.SetHandler(async context =>
        {
            var json = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await RunAsync(context.ParseResult.GetValueForOption(docOption),
                context.ParseResult.GetValueForOption(dryRunOption), json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? docArg, bool dryRun, bool json, CancellationToken ct)
    {
        var root = Directory.GetCurrentDirectory();
        // Spec 083: reads `tests` (reference counts) — a missing one warns; no section is required.
        var loaded = await CriteriaCommandIO.LoadConfigAsync(root, ConfigSections.Tests, ct);
        if (!loaded.IsSuccess)
        {
            CriteriaCommandIO.WriteFailure(json, Name, loaded.ErrorCode, loaded.Error!);
            return ExitCodes.Error;
        }
        var config = loaded.Config!;
        var configWarnings = loaded.Warnings;

        var normalized = CriteriaPaths.NormalizeSourceDoc(docArg, root);
        if (!normalized.IsSuccess)
        {
            CriteriaCommandIO.WriteFailure(json, Name, normalized.ErrorCode, $"The document path must be inside the repository: {docArg}", configWarnings);
            return ExitCodes.Error;
        }

        var doc = normalized.Path!;
        try
        {
            var outcome = await CriteriaStore.WithLockAsync(root, async () =>
            {
                var ws = await CriteriaStore.LoadAsync(config, root, ct);
                var source = ws.FindDocumentSource(doc);
                if (source is null)
                    return RetireOutcome.Refuse(ErrorNotIndexed, $"'{doc}' is not an indexed document source.");
                if (File.Exists(Path.Combine(root, doc)))
                    return RetireOutcome.Refuse(ErrorDocStillExists, $"'{doc}' still exists; retire applies only to deleted documents.");

                MigrationReport? migration = null;
                if (!dryRun && CriteriaPaths.IsOldLayout(ws.Index))
                {
                    var report = await CriteriaLayoutMigrator.MigrateAsync(ws, dryRun: false, ct);
                    if (!report.AlreadyMigrated)
                    {
                        migration = report;
                        ws = await CriteriaStore.LoadAsync(config, root, ct);
                        source = ws.FindDocumentSource(doc)!;
                    }
                }

                var file = CriteriaPaths.NormalizeLexical(source.File);
                var shared = CriteriaProvenance.IsShared(ws.Index, source);
                var previous = string.IsNullOrWhiteSpace(file) ? [] : await ws.ReadStrictAsync(file, ct);
                if (shared)
                {
                    previous = CriteriaProvenance.OwnedBy(previous, doc, root);
                }

                var (testsById, _) = await CriteriaReferenceCounter.LoadAsync(root, config, ct);
                var changeset = CriteriaChangeset.Build(doc, previous, []).WithReferences(testsById);

                if (!dryRun)
                {
                    if (!shared && !string.IsNullOrWhiteSpace(file) && File.Exists(ws.FullPath(file)))
                        File.Delete(ws.FullPath(file));
                    ws.Index.Sources.Remove(source);
                    await ws.SaveIndexAsync(ct);

                    // Spec 080 B2: a retired doc's criteria leave the shared file, so they stop counting in coverage.
                    if (shared)
                        await ws.LeaveSharedFileAsync(file, doc, ct);
                }

                return RetireOutcome.Done(changeset, migration);
            }, ct);

            if (outcome.ErrorCode is not null)
            {
                CriteriaCommandIO.WriteFailure(json, Name, outcome.ErrorCode, outcome.Message!, configWarnings);
                return ExitCodes.NotFound; // 4: refused
            }

            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = Name,
                    success = true,
                    doc,
                    dry_run = dryRun,
                    written = !dryRun,
                    changeset = outcome.Changeset,
                    migration = outcome.Migration
                }, CriteriaCommandIO.Json));
            }

            foreach (var warning in configWarnings)
            {
                (json ? Console.Error : Console.Out).WriteLine($"Warning: {warning}");
            }

            CriteriaChangesetPresenter.Write(json ? Console.Error : Console.Out, outcome.Changeset!,
                dryRun ? $"Retiring {doc} would remove (dry run — nothing written):" : $"Retired {doc}; removed:");
            return ExitCodes.Success;
        }
        catch (CriteriaStoreException ex)
        {
            CriteriaCommandIO.WriteFailure(json, Name, ex.ErrorCode, ex.Message, configWarnings);
            return ExitCodes.Error;
        }
    }

    private sealed record RetireOutcome(string? ErrorCode, string? Message, CriteriaChangeset? Changeset, MigrationReport? Migration)
    {
        public static RetireOutcome Refuse(string code, string message) => new(code, message, null, null);

        public static RetireOutcome Done(CriteriaChangeset changeset, MigrationReport? migration) => new(null, null, changeset, migration);
    }
}
