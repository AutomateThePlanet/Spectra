using Spectra.CLI.Baseline;
using Spectra.CLI.Commands.Generate;
using Spectra.CLI.IO;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Parsing;
using YamlDotNet.RepresentationModel;

namespace Spectra.CLI.Commands.Delete;

/// <summary>A test to delete, resolved from its parsed frontmatter.</summary>
public sealed record DeletionTarget(string Id, string File, string Suite, string? Title, IReadOnlyList<string> AutomatedBy);

/// <summary>A test whose scalar <c>depends_on</c> names a deleted test; its key is dropped through the lossless writer.</summary>
public sealed record ScalarDependent(string DependentId, string File, string RemovedDep, TestCase Test);

/// <summary>Everything a delete changes besides the test files themselves (Spec 090, FR-C1/FR-C2).</summary>
public sealed record DeletionPlan(
    IReadOnlyList<DeletionTarget> Targets,
    IReadOnlyList<ScalarDependent> ScalarDependents,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<(string Suite, string Id)> BaselineEntries,
    IReadOnlyList<string> StagingFiles,
    IReadOnlyList<string> VerdictFiles);

/// <summary>What <see cref="TestDeletionService.ApplyAsync"/> did, and what failed.</summary>
public sealed record DeletionOutcome(
    IReadOnlyList<DeletionTarget> Deleted,
    IReadOnlyList<ScalarDependent> DependentsCleaned,
    IReadOnlyList<(string Suite, string Id)> BaselineEntriesRemoved,
    IReadOnlyList<string> StagingFilesRemoved,
    IReadOnlyList<string> VerdictFilesRemoved,
    IReadOnlyList<(string Id, string Message)> Errors);

/// <summary>
/// Spec 090 (FR-C1–FR-C3): deletes tests without leaving anything broken or stale behind. One service for
/// <c>spectra delete</c> and legacy <c>ai update</c>.
/// <list type="bullet">
/// <item>Ids come from each file's parsed frontmatter — never from a regex over the file.</item>
/// <item>A dependent's scalar <c>depends_on</c> naming a deleted test is dropped through the 078 lossless writer, so the
/// body, other keys, comments and newline style stay byte-identical. A block or flow list (which doesn't parse today) is
/// left untouched and reported as a warning.</item>
/// <item>The deleted tests' baseline entries (under the baseline lock), staging files and verdict files go too.</item>
/// </list>
/// </summary>
public sealed class TestDeletionService
{
    private static readonly string[] StagingFolders = ["updates", "repairs"];
    private static readonly string[] StagingPrefixes = ["updated-", "repaired-", IngestUpdateCommand.FailedPrefix];

    private readonly string _workspace;
    private readonly string _testCasesDir;
    private readonly List<CatalogEntry> _catalog = [];

    private TestDeletionService(string workspace, string testCasesDir)
    {
        _workspace = workspace;
        _testCasesDir = testCasesDir;
    }

    /// <summary>Reads every test file under the tests folder once (parsed, or its frontmatter alone when it doesn't parse).</summary>
    public static async Task<TestDeletionService> LoadAsync(string workspace, string testCasesDir, CancellationToken ct = default)
    {
        var service = new TestDeletionService(workspace, testCasesDir);
        if (!Directory.Exists(testCasesDir))
        {
            return service;
        }

        var parser = new TestCaseParser();
        foreach (var file in Directory.EnumerateFiles(testCasesDir, "*.md", SearchOption.AllDirectories)
                     .Where(f => !Path.GetFileName(f).StartsWith('_'))
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try
            {
                text = await File.ReadAllTextAsync(file, ct);
            }
            catch (IOException)
            {
                continue;
            }

            var suite = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
            var parsed = parser.Parse(text, Path.GetFileName(file));
            if (parsed.IsSuccess)
            {
                service._catalog.Add(new CatalogEntry(file, suite, parsed.Value!.Id, parsed.Value, null));
            }
            else
            {
                var frontmatter = FrontmatterOnly(text);
                service._catalog.Add(new CatalogEntry(file, suite, frontmatter?.Id, null, frontmatter?.DependsOn));
            }
        }

        return service;
    }

    /// <summary>Resolves ids to test files by their parsed frontmatter id (case-insensitive). Unknown ids map to null.</summary>
    public IReadOnlyList<(string Id, DeletionTarget? Target)> Resolve(IEnumerable<string> ids) =>
        ids.Select(id =>
        {
            var entry = _catalog.FirstOrDefault(e => e.Id is not null && string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
            return (id, entry is null
                ? null
                : new DeletionTarget(entry.Id!, entry.File, entry.Suite, entry.Test?.Title,
                    entry.Test?.AutomatedBy ?? (IReadOnlyList<string>)[]));
        }).ToList();

    /// <summary>Everything deleting <paramref name="targets"/> would change; nothing is written.</summary>
    public async Task<DeletionPlan> PlanAsync(IReadOnlyList<DeletionTarget> targets, CancellationToken ct = default)
    {
        var deleted = targets.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scalar = new List<ScalarDependent>();
        var warnings = new List<string>();
        foreach (var entry in _catalog.Where(e => e.Id is null || !deleted.Contains(e.Id)))
        {
            if (entry.Test is { DependsOn: { } dep } test && deleted.Contains(dep.Trim()))
            {
                scalar.Add(new ScalarDependent(test.Id, entry.File, dep.Trim(), test));
            }
            else if (entry.Test is null && entry.DependsOn is { } node)
            {
                foreach (var removed in DependencyValues(node).Where(deleted.Contains))
                {
                    var who = entry.Id ?? Relative(entry.File);
                    warnings.Add(node is YamlSequenceNode
                        ? $"{who}: depends_on is a list (not supported) and names {removed}; not changed ({Relative(entry.File)})"
                        : $"{who}: depends_on names {removed} but the file doesn't parse; not changed ({Relative(entry.File)})");
                }
            }
        }

        var baselineEntries = new List<(string, string)>();
        foreach (var group in targets.GroupBy(t => t.Suite, StringComparer.Ordinal))
        {
            try
            {
                var baseline = await BaselineStore.LoadSuiteAsync(_workspace, _testCasesDir, group.Key, ct);
                baselineEntries.AddRange(group.Where(t => baseline.Tests.ContainsKey(t.Id)).Select(t => (group.Key, t.Id)));
            }
            catch (BaselineStoreException ex)
            {
                warnings.Add($"{ex.Message}; the baseline entries of the deleted tests in suite '{group.Key}' were not removed.");
            }
        }

        var staging = targets.SelectMany(t => StagingFolders.SelectMany(folder => StagingPrefixes.Select(prefix =>
                Path.Combine(_workspace, ".spectra", folder, t.Suite, $"{prefix}{t.Id}.json"))))
            .Where(File.Exists)
            .ToList();
        var verdicts = targets.SelectMany(t => VerdictFiles.Existing(_workspace, t.Id)).ToList();

        return new DeletionPlan(targets, scalar, warnings, baselineEntries, staging, verdicts);
    }

    /// <summary>
    /// Applies a plan: indexes, scalar dependents (lossless writer), baseline entries (under the lock), staging and
    /// verdict files, then the test files last. Each step's failures are collected, not thrown.
    /// </summary>
    public async Task<DeletionOutcome> ApplyAsync(DeletionPlan plan, CancellationToken ct = default)
    {
        var errors = new List<(string, string)>();

        foreach (var group in plan.Targets.GroupBy(t => t.Suite, StringComparer.Ordinal))
        {
            try
            {
                await UpdateSuiteIndexAsync(group.Key, group.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase), ct);
            }
            catch (Exception ex)
            {
                errors.AddRange(group.Select(t => (t.Id, $"Failed to update _index.json: {ex.Message}")));
            }
        }

        var cleaned = new List<ScalarDependent>();
        var writer = new TestFileWriter();
        foreach (var d in plan.ScalarDependents)
        {
            try
            {
                await writer.WriteAsync(d.File, d.Test with { DependsOn = null }, ct);
                cleaned.Add(d);
            }
            catch (Exception ex)
            {
                errors.Add((d.DependentId, $"Failed to update depends_on: {ex.Message}"));
            }
        }

        var baselineRemoved = new List<(string, string)>();
        foreach (var group in plan.BaselineEntries.GroupBy(e => e.Suite, StringComparer.Ordinal))
        {
            try
            {
                var removed = await BaselineStore.WithLockAsync(_workspace,
                    () => BaselineStore.RemoveEntriesAsync(_workspace, _testCasesDir, group.Key, group.Select(e => e.Id).ToList(), ct), ct);
                baselineRemoved.AddRange(removed.Select(id => (group.Key, id)));
            }
            catch (Exception ex)
            {
                errors.AddRange(group.Select(e => (e.Id, $"Failed to update the baseline: {ex.Message}")));
            }
        }

        var staging = DeleteFiles(plan.StagingFiles, errors);
        var verdicts = DeleteFiles(plan.VerdictFiles, errors);

        var deleted = new List<DeletionTarget>();
        foreach (var target in plan.Targets)
        {
            try
            {
                File.Delete(target.File);
                deleted.Add(target);
            }
            catch (Exception ex)
            {
                errors.Add((target.Id, $"Failed to delete file: {ex.Message}"));
            }
        }

        return new DeletionOutcome(deleted, cleaned, baselineRemoved, staging, verdicts, errors);
    }

    public string Relative(string path) => Path.GetRelativePath(_workspace, path).Replace('\\', '/');

    private List<string> DeleteFiles(IEnumerable<string> paths, List<(string, string)> errors)
    {
        var removed = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                File.Delete(path);
                removed.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add((Path.GetFileName(path), $"Failed to delete {Relative(path)}: {ex.Message}"));
            }
        }

        return removed;
    }

    private async Task UpdateSuiteIndexAsync(string suite, HashSet<string> removedIds, CancellationToken ct)
    {
        var indexPath = Path.Combine(_testCasesDir, suite, "_index.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        var indexWriter = new IndexWriter();
        var index = await indexWriter.ReadAsync(indexPath, ct);
        if (index?.Tests is null)
        {
            return;
        }

        var remaining = index.Tests.Where(t => t.Id is null || !removedIds.Contains(t.Id)).ToList();
        if (remaining.Count == index.Tests.Count)
        {
            return;
        }

        await indexWriter.WriteAsync(indexPath, new MetadataIndex { Suite = index.Suite, GeneratedAt = DateTime.UtcNow, Tests = remaining }, ct);
    }

    private static IEnumerable<string> DependencyValues(YamlNode node) => node switch
    {
        YamlScalarNode s when !string.IsNullOrWhiteSpace(s.Value) => [s.Value.Trim()],
        YamlSequenceNode seq => seq.Children.OfType<YamlScalarNode>().Where(c => !string.IsNullOrWhiteSpace(c.Value)).Select(c => c.Value!.Trim()),
        _ => []
    };

    /// <summary>The frontmatter's <c>id</c> and <c>depends_on</c> of a file that doesn't parse as a test.</summary>
    private static (string? Id, YamlNode? DependsOn)? FrontmatterOnly(string text)
    {
        var lf = text.Replace("\r\n", "\n");
        if (!lf.StartsWith("---\n", StringComparison.Ordinal))
        {
            return null;
        }

        var end = lf.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(lf[4..(end + 1)]));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                return null;
            }

            root.Children.TryGetValue(new YamlScalarNode("id"), out var id);
            root.Children.TryGetValue(new YamlScalarNode("depends_on"), out var dependsOn);
            return ((id as YamlScalarNode)?.Value?.Trim(), dependsOn);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }

    private sealed record CatalogEntry(string File, string Suite, string? Id, TestCase? Test, YamlNode? DependsOn);
}
