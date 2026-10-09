using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Delete;

/// <summary>
/// Spec 040: implements <c>spectra delete &lt;test-id&gt;...</c>.
/// </summary>
/// <remarks>
/// Decision 7: three-phase execution — resolve (read-only), pre-flight,
/// then atomic write pass. If pre-flight fails (TEST_NOT_FOUND or
/// AUTOMATION_LINKED), nothing on disk is touched. Cascade
/// <c>depends_on</c> cleanup runs in the same pass that removes the test
/// files and updates the per-suite <c>_index.json</c>.
/// </remarks>
public sealed class DeleteHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public DeleteHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> ids,
        string? suiteAlias,
        bool dryRun,
        bool force,
        bool noAutomationCheck,
        bool noInteraction,
        CancellationToken ct = default)
    {
        // --suite alias delegates to `suite delete` (handled by SuiteCommand).
        if (!string.IsNullOrWhiteSpace(suiteAlias))
        {
            return await new Spectra.CLI.Commands.Suite.SuiteDeleteHandler(_verbosity, _outputFormat)
                .ExecuteAsync(suiteAlias!, dryRun, force, noInteraction, ct).ConfigureAwait(false);
        }

        if (ids.Count == 0)
        {
            EmitError("delete", "Missing required arguments: test-id(s) or --suite");
            return ExitCodes.MissingArguments;
        }

        var workspace = Directory.GetCurrentDirectory();
        var testCasesDir = ResolveTestCasesDir(workspace);

        if (!Directory.Exists(testCasesDir))
        {
            EmitError("delete", $"Test cases directory not found: {testCasesDir}");
            return ExitCodes.Error;
        }

        // Phase 1: resolve by each file's parsed frontmatter id (Spec 090 FR-C1: never a regex over the file).
        var service = await TestDeletionService.LoadAsync(workspace, testCasesDir, ct).ConfigureAwait(false);
        var resolved = service.Resolve(ids);

        // Phase 2: pre-flight checks — bail before any write.
        var skipped = new List<SkippedTest>();
        var preflightFailures = new List<DeleteError>();
        var preflightExitCode = ExitCodes.Success;

        foreach (var (id, target) in resolved)
        {
            if (target is null)
            {
                skipped.Add(new SkippedTest { Id = id, Reason = "TEST_NOT_FOUND" });
                preflightExitCode = ExitCodes.NotFound;
            }
            else if (target.AutomatedBy.Count > 0 && !noAutomationCheck && !force)
            {
                skipped.Add(new SkippedTest { Id = id, Reason = "AUTOMATION_LINKED" });
                if (preflightExitCode == ExitCodes.Success)
                {
                    preflightExitCode = ExitCodes.AutomationLinked;
                }
            }
        }

        var resolvable = resolved
            .Where(r => r.Target is not null && skipped.All(s => !s.Id.Equals(r.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.Target!)
            .ToList();

        // If pre-flight fully blocks, emit a result without writing.
        if (resolvable.Count == 0 || preflightExitCode == ExitCodes.NotFound || preflightExitCode == ExitCodes.AutomationLinked)
        {
            var failResult = new DeleteResult
            {
                Command = "delete",
                Status = "failed",
                DryRun = dryRun,
                Skipped = skipped,
                Errors = preflightFailures,
                Message = preflightExitCode switch
                {
                    ExitCodes.NotFound => $"{skipped.Count(s => s.Reason == "TEST_NOT_FOUND")} test ID(s) not found.",
                    ExitCodes.AutomationLinked => $"{skipped.Count(s => s.Reason == "AUTOMATION_LINKED")} test(s) have automation links. Use --force or --no-automation-check to proceed.",
                    _ => null
                }
            };
            EmitResult(failResult);
            return preflightExitCode;
        }

        // Spec 090 (FR-C1/FR-C2): the cleanup plan — scalar dependents, list-form warnings, baseline entries, staging
        // and verdict files.
        var plan = await service.PlanAsync(resolvable, ct).ConfigureAwait(false);

        if (dryRun)
        {
            var dryResult = new DeleteResult
            {
                Command = "delete",
                Status = "completed",
                DryRun = true,
                Deleted = resolvable.Select(t => ToDeletedTest(workspace, t)).ToList(),
                DependencyCleanup = plan.ScalarDependents.Select(d => new DependencyCleanup { TestId = d.DependentId, RemovedDep = d.RemovedDep }).ToList(),
                Skipped = skipped,
                BaselineEntriesRemoved = plan.BaselineEntries.Select(e => $"{e.Suite}/{e.Id}").ToList(),
                StagingFilesRemoved = plan.StagingFiles.Select(service.Relative).ToList(),
                VerdictFilesRemoved = plan.VerdictFiles.Select(service.Relative).ToList(),
                Warnings = plan.Warnings,
                Message = $"Dry run: {resolvable.Count} test(s) would be deleted; {plan.ScalarDependents.Count} depends_on reference(s) would be removed."
            };
            EmitResult(dryResult);
            EmitHumanSummary(dryResult);
            return ExitCodes.Success;
        }

        // Interactive confirmation (unless --force / --no-interaction)
        if (!force && !noInteraction && _outputFormat == OutputFormat.Human)
        {
            if (!Confirm(resolvable, plan))
            {
                EmitResult(new DeleteResult
                {
                    Command = "delete",
                    Status = "completed",
                    Message = "Cancelled by user."
                });
                return ExitCodes.Success;
            }
        }

        // Phase 3: write — indexes, dependents (lossless), baseline, staging and verdict files, test files last.
        var outcome = await service.ApplyAsync(plan, ct).ConfigureAwait(false);
        var errors = outcome.Errors.Select(e => new DeleteError { Id = e.Id, Message = e.Message }).ToList();

        var result = new DeleteResult
        {
            Command = "delete",
            Status = errors.Count == 0 ? "completed" : "failed",
            DryRun = false,
            Deleted = outcome.Deleted.Select(t => ToDeletedTest(workspace, t)).ToList(),
            DependencyCleanup = outcome.DependentsCleaned.Select(d => new DependencyCleanup { TestId = d.DependentId, RemovedDep = d.RemovedDep }).ToList(),
            Skipped = skipped,
            Errors = errors,
            BaselineEntriesRemoved = outcome.BaselineEntriesRemoved.Select(e => $"{e.Suite}/{e.Id}").ToList(),
            StagingFilesRemoved = outcome.StagingFilesRemoved.Select(service.Relative).ToList(),
            VerdictFilesRemoved = outcome.VerdictFilesRemoved.Select(service.Relative).ToList(),
            Warnings = plan.Warnings,
            Message = $"Deleted {outcome.Deleted.Count} test(s); cleaned up {outcome.DependentsCleaned.Count} depends_on reference(s)."
        };
        EmitResult(result);
        EmitHumanSummary(result);

        return errors.Count == 0 ? ExitCodes.Success : ExitCodes.Error;
    }

    private static DeletedTest ToDeletedTest(string workspace, DeletionTarget t) => new()
    {
        Id = t.Id,
        Suite = t.Suite,
        File = MakeRelative(workspace, t.File),
        Title = t.Title,
        AutomatedBy = t.AutomatedBy,
        StrandedAutomation = t.AutomatedBy
    };

    /// <summary>Spec 090: the extra cleanup and the list-form warnings, in human mode.</summary>
    private void EmitHumanSummary(DeleteResult result)
    {
        if (_outputFormat != OutputFormat.Human)
        {
            return;
        }

        var verb = result.DryRun ? "Would remove" : "Removed";
        if (result.BaselineEntriesRemoved.Count > 0)
            Console.WriteLine($"{verb} baseline entries: {string.Join(", ", result.BaselineEntriesRemoved)}");
        if (result.StagingFilesRemoved.Count > 0)
            Console.WriteLine($"{verb} staging files: {string.Join(", ", result.StagingFilesRemoved)}");
        if (result.VerdictFilesRemoved.Count > 0)
            Console.WriteLine($"{verb} verdict files: {string.Join(", ", result.VerdictFilesRemoved)}");
        foreach (var warning in result.Warnings)
            Console.Error.WriteLine($"Warning: {warning}");
    }

    private bool Confirm(IReadOnlyList<DeletionTarget> resolvable, DeletionPlan plan)
    {
        var dependents = plan.ScalarDependents;
        Console.WriteLine();
        Console.WriteLine($"Delete {resolvable.Count} test(s)?");
        foreach (var item in resolvable)
        {
            Console.WriteLine($"  {item.Id}  ({item.Suite})  {item.Title ?? string.Empty}");
            if (item.AutomatedBy.Count > 0)
            {
                Console.WriteLine($"    ⚠ stranded automation: {string.Join(", ", item.AutomatedBy)}");
            }
        }
        if (dependents.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Will also remove {dependents.Count} depends_on reference(s):");
            foreach (var d in dependents.Take(5))
            {
                Console.WriteLine($"  {d.DependentId} ←/ {d.RemovedDep}");
            }
            if (dependents.Count > 5)
            {
                Console.WriteLine($"  …and {dependents.Count - 5} more");
            }
        }
        Console.WriteLine();
        Console.WriteLine("This is a hard delete. Use Git to recover.");
        Console.Write("[y/N]: ");
        var input = Console.ReadLine()?.Trim();
        return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTestCasesDir(string workspace)
    {
        var configPath = Path.Combine(workspace, "spectra.config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (!string.IsNullOrWhiteSpace(config?.Tests?.Dir))
                {
                    return Path.Combine(workspace, config.Tests.Dir);
                }
            }
            catch { /* fall through */ }
        }
        return Path.Combine(workspace, "test-cases");
    }

    private static string MakeRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private void EmitResult(CommandResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");
        try
        {
            using var fs = new FileStream(resultPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            writer.Write(JsonSerializer.Serialize(result, result.GetType(), options));
            writer.Flush();
            fs.Flush(true);
        }
        catch { /* non-critical */ }
    }

    private void EmitError(string command, string message)
    {
        var error = new ErrorResult
        {
            Command = command,
            Status = "failed",
            Error = message
        };
        EmitResult(error);
        if (_outputFormat == OutputFormat.Human)
        {
            Console.Error.WriteLine(message);
        }
    }
}
