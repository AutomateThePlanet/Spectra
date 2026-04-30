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
    private static readonly Regex DependsOnLineRegex = new(
        @"^(\s*-\s+[""']?)(?<id>[A-Za-z][A-Za-z0-9_-]*-\d+)([""']?\s*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

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

        // Phase 1: resolve. Build a plan: (id, file, suite, automated_by[]).
        var plan = await ResolveAsync(testCasesDir, ids, ct).ConfigureAwait(false);

        // Phase 2: pre-flight checks — bail before any write.
        var skipped = new List<SkippedTest>();
        var preflightFailures = new List<DeleteError>();
        var preflightExitCode = ExitCodes.Success;

        foreach (var item in plan)
        {
            if (item.File is null)
            {
                skipped.Add(new SkippedTest { Id = item.Id, Reason = "TEST_NOT_FOUND" });
                preflightExitCode = ExitCodes.NotFound;
            }
            else if (item.AutomatedBy.Count > 0 && !noAutomationCheck && !force)
            {
                skipped.Add(new SkippedTest { Id = item.Id, Reason = "AUTOMATION_LINKED" });
                if (preflightExitCode == ExitCodes.Success)
                {
                    preflightExitCode = ExitCodes.AutomationLinked;
                }
            }
        }

        var resolvable = plan.Where(p => p.File is not null && skipped.All(s => !s.Id.Equals(p.Id, StringComparison.OrdinalIgnoreCase))).ToList();

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

        // Build dependency-cleanup plan
        var idsToDelete = resolvable.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependents = await FindDependentsAsync(testCasesDir, idsToDelete, ct).ConfigureAwait(false);

        if (dryRun)
        {
            var deletedPreview = resolvable.Select(p => new DeletedTest
            {
                Id = p.Id,
                Suite = p.Suite!,
                File = MakeRelative(workspace, p.File!),
                Title = p.Title,
                AutomatedBy = p.AutomatedBy,
                StrandedAutomation = p.AutomatedBy
            }).ToList();
            var dryResult = new DeleteResult
            {
                Command = "delete",
                Status = "completed",
                DryRun = true,
                Deleted = deletedPreview,
                DependencyCleanup = dependents.Select(d => new DependencyCleanup { TestId = d.DependentId, RemovedDep = d.RemovedDep }).ToList(),
                Skipped = skipped,
                Message = $"Dry run: {resolvable.Count} test(s) would be deleted; {dependents.Count} depends_on reference(s) would be removed."
            };
            EmitResult(dryResult);
            return ExitCodes.Success;
        }

        // Interactive confirmation (unless --force / --no-interaction)
        if (!force && !noInteraction && _outputFormat == OutputFormat.Human)
        {
            if (!Confirm(resolvable, dependents))
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

        // Phase 3: write. Atomic per-file rewrites.
        var deleted = new List<DeletedTest>();
        var cleanups = new List<DependencyCleanup>();
        var errors = new List<DeleteError>();

        // 3a. Update _index.json for each affected suite
        var suiteToIds = resolvable.GroupBy(p => p.Suite!, StringComparer.OrdinalIgnoreCase);
        foreach (var grp in suiteToIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await UpdateSuiteIndexAsync(testCasesDir, grp.Key, grp.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                foreach (var item in grp)
                {
                    errors.Add(new DeleteError { Id = item.Id, Message = $"Failed to update _index.json: {ex.Message}" });
                }
            }
        }

        // 3b. Rewrite each dependent test's depends_on
        foreach (var d in dependents)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await StripDependsOnAsync(d.File, idsToDelete, ct).ConfigureAwait(false);
                cleanups.Add(new DependencyCleanup { TestId = d.DependentId, RemovedDep = d.RemovedDep });
            }
            catch (Exception ex)
            {
                errors.Add(new DeleteError { Id = d.DependentId, Message = $"Failed to update depends_on: {ex.Message}" });
            }
        }

        // 3c. Delete test files last so a partial failure doesn't strand state.
        foreach (var item in resolvable)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Delete(item.File!);
                deleted.Add(new DeletedTest
                {
                    Id = item.Id,
                    Suite = item.Suite!,
                    File = MakeRelative(workspace, item.File!),
                    Title = item.Title,
                    AutomatedBy = item.AutomatedBy,
                    StrandedAutomation = item.AutomatedBy
                });
            }
            catch (Exception ex)
            {
                errors.Add(new DeleteError { Id = item.Id, Message = $"Failed to delete file: {ex.Message}" });
            }
        }

        var result = new DeleteResult
        {
            Command = "delete",
            Status = errors.Count == 0 ? "completed" : "failed",
            DryRun = false,
            Deleted = deleted,
            DependencyCleanup = cleanups,
            Skipped = skipped,
            Errors = errors,
            Message = $"Deleted {deleted.Count} test(s); cleaned up {cleanups.Count} depends_on reference(s)."
        };
        EmitResult(result);

        return errors.Count == 0 ? ExitCodes.Success : ExitCodes.Error;
    }

    private static async Task<List<ResolvedItem>> ResolveAsync(string testCasesDir, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var plan = new List<ResolvedItem>();
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var match = await FindTestCaseFileAsync(testCasesDir, id, ct).ConfigureAwait(false);
            if (match is null)
            {
                plan.Add(new ResolvedItem { Id = id });
                continue;
            }

            var (file, suite, title, automatedBy) = match.Value;
            plan.Add(new ResolvedItem
            {
                Id = id,
                File = file,
                Suite = suite,
                Title = title,
                AutomatedBy = automatedBy
            });
        }
        return plan;
    }

    private static async Task<(string File, string Suite, string? Title, IReadOnlyList<string> AutomatedBy)?> FindTestCaseFileAsync(
        string testCasesDir, string id, CancellationToken ct)
    {
        if (!Directory.Exists(testCasesDir))
        {
            return null;
        }

        // Common case: file is `<id>.md` directly inside its suite dir
        foreach (var suiteDir in Directory.EnumerateDirectories(testCasesDir))
        {
            var candidate = Path.Combine(suiteDir, $"{id}.md");
            if (File.Exists(candidate))
            {
                var suite = Path.GetFileName(suiteDir);
                var (title, automatedBy) = await ReadFrontmatterMetadataAsync(candidate, ct).ConfigureAwait(false);
                return (candidate, suite, title, automatedBy);
            }
        }

        // Fallback: scan all .md files for matching id: in frontmatter
        foreach (var file in Directory.EnumerateFiles(testCasesDir, "*.md", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch (IOException) { continue; }

            if (Regex.IsMatch(content, $@"^\s*id:\s*[""']?{Regex.Escape(id)}[""']?\s*$", RegexOptions.Multiline))
            {
                var suite = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
                var (title, automatedBy) = await ReadFrontmatterMetadataAsync(file, ct).ConfigureAwait(false);
                return (file, suite, title, automatedBy);
            }
        }

        return null;
    }

    private static async Task<(string? Title, IReadOnlyList<string> AutomatedBy)> ReadFrontmatterMetadataAsync(string filePath, CancellationToken ct)
    {
        string? title = null;
        var automation = new List<string>();
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
            var inFrontmatter = false;
            var inAutomatedBy = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (i == 0 && line.Trim() == "---") { inFrontmatter = true; continue; }
                if (inFrontmatter && line.Trim() == "---") { break; }
                if (!inFrontmatter) { continue; }

                if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                {
                    title = line["title:".Length..].Trim().Trim('"', '\'');
                    inAutomatedBy = false;
                }
                else if (line.StartsWith("automated_by:", StringComparison.OrdinalIgnoreCase))
                {
                    inAutomatedBy = true;
                    var inline = line["automated_by:".Length..].Trim();
                    if (!string.IsNullOrEmpty(inline) && inline != "[]")
                    {
                        automation.Add(inline.Trim('"', '\''));
                    }
                }
                else if (inAutomatedBy && line.TrimStart().StartsWith("- "))
                {
                    automation.Add(line.TrimStart()[2..].Trim().Trim('"', '\''));
                }
                else if (!line.StartsWith(" ") && !line.StartsWith("\t"))
                {
                    inAutomatedBy = false;
                }
            }
        }
        catch
        {
            // best-effort
        }
        return (title, automation);
    }

    private static async Task<List<(string DependentId, string File, string RemovedDep)>> FindDependentsAsync(
        string testCasesDir, HashSet<string> idsToDelete, CancellationToken ct)
    {
        var results = new List<(string, string, string)>();
        foreach (var file in Directory.EnumerateFiles(testCasesDir, "*.md", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch (IOException) { continue; }

            var matches = DependsOnLineRegex.Matches(content);
            if (matches.Count == 0) { continue; }

            string? dependentId = null;
            var idMatch = Regex.Match(content, @"^\s*id:\s*[""']?(?<id>[A-Za-z][A-Za-z0-9_-]*-\d+)[""']?\s*$", RegexOptions.Multiline);
            if (idMatch.Success)
            {
                dependentId = idMatch.Groups["id"].Value;
            }

            foreach (Match m in matches)
            {
                var dep = m.Groups["id"].Value;
                if (idsToDelete.Contains(dep) && dependentId is not null && !idsToDelete.Contains(dependentId))
                {
                    results.Add((dependentId, file, dep));
                }
            }
        }
        return results;
    }

    private static async Task StripDependsOnAsync(string filePath, HashSet<string> idsToRemove, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var rewritten = DependsOnLineRegex.Replace(content, m =>
        {
            var id = m.Groups["id"].Value;
            return idsToRemove.Contains(id) ? string.Empty : m.Value;
        });
        // Collapse the empty replacements' line endings
        rewritten = Regex.Replace(rewritten, @"\r?\n\r?\n+", Environment.NewLine + Environment.NewLine);
        if (rewritten != content)
        {
            await AtomicFileWriter.WriteAllTextAsync(filePath, rewritten, ct).ConfigureAwait(false);
        }
    }

    private static async Task UpdateSuiteIndexAsync(string testCasesDir, string suite, HashSet<string> removedIds, CancellationToken ct)
    {
        var indexPath = Path.Combine(testCasesDir, suite, "_index.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        var writer = new IndexWriter();
        var index = await writer.ReadAsync(indexPath, ct).ConfigureAwait(false);
        if (index?.Tests is null)
        {
            return;
        }

        var remaining = index.Tests.Where(t => t.Id is null || !removedIds.Contains(t.Id)).ToList();
        if (remaining.Count == index.Tests.Count)
        {
            return; // no change
        }

        var newIndex = new MetadataIndex
        {
            Suite = index.Suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = remaining
        };
        await writer.WriteAsync(indexPath, newIndex, ct).ConfigureAwait(false);
    }

    private bool Confirm(IReadOnlyList<ResolvedItem> resolvable, IReadOnlyList<(string DependentId, string File, string RemovedDep)> dependents)
    {
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

    private sealed class ResolvedItem
    {
        public required string Id { get; init; }
        public string? File { get; init; }
        public string? Suite { get; init; }
        public string? Title { get; init; }
        public IReadOnlyList<string> AutomatedBy { get; init; } = Array.Empty<string>();
    }
}
