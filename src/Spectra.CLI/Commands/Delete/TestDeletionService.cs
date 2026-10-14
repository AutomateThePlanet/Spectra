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
/// <c>spectra delete</c>, <c>spectra suite delete</c> (Spec 096) and legacy <c>ai update</c>.
/// <list type="bullet">
/// <item>Ids come from each file's frontmatter — never from a regex over the file.</item>
/// <item>A dependent's scalar <c>depends_on</c> naming a deleted test is dropped through the 078 lossless writer, so the
/// body, other keys, comments and newline style stay byte-identical. A block or flow list (which doesn't parse today) is
/// left untouched and reported as a warning.</item>
/// <item>The deleted tests' baseline entries (under the baseline lock), staging files and verdict files go too.</item>
/// <item>Spec 096 (FR-010): the catalog is each file's frontmatter (<c>id</c>, <c>depends_on</c>) only; a file is parsed
/// in full only when it is a target or names a deleted test in a scalar <c>depends_on</c> (<see cref="FullParses"/>).</item>
/// </list>
/// </summary>
public sealed class TestDeletionService
{
    private static readonly string[] StagingFolders = ["updates", "repairs"];
    private static readonly string[] StagingPrefixes = ["updated-", "repaired-", IngestUpdateCommand.FailedPrefix];

    private readonly string _workspace;
    private readonly string _testCasesDir;
    private readonly List<CatalogEntry> _catalog = [];
    private readonly Dictionary<string, CatalogEntry> _byFile = new(PathComparer);
    private readonly Dictionary<string, string> _indexed = new(StringComparer.OrdinalIgnoreCase);
    private readonly TestCaseParser _parser = new();

    private TestDeletionService(string workspace, string testCasesDir)
    {
        _workspace = workspace;
        _testCasesDir = testCasesDir;
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Spec 096 (analyze H2): how many files were parsed in full — the deterministic guard for FR-010.</summary>
    internal int FullParses { get; private set; }

    /// <summary>
    /// Reads every test file under the tests folder once. Spec 096 (FR-010): only a file that mentions <c>depends_on</c>
    /// has its frontmatter (<c>id</c>, <c>depends_on</c>) read now — every other id is read when needed — and each
    /// suite's <c>_index.json</c> maps ids to files for <see cref="Resolve"/>. A file whose frontmatter can't be read on
    /// its own is parsed in full, as before 096.
    /// </summary>
    public static async Task<TestDeletionService> LoadAsync(string workspace, string testCasesDir, CancellationToken ct = default)
    {
        var service = new TestDeletionService(workspace, testCasesDir);
        if (!Directory.Exists(testCasesDir))
        {
            return service;
        }

        var files = Directory.EnumerateFiles(testCasesDir, "*.md", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        // Spec 096 (FR-010): the reads are the cost (per-file open latency), so they run in parallel and synchronously;
        // everything after them is in path order.
        var texts = new string?[files.Length];
        Parallel.For(0, files.Length, new ParallelOptions { CancellationToken = ct }, i =>
        {
            try
            {
                texts[i] = File.ReadAllText(files[i]);
            }
            catch (IOException)
            {
                // unreadable: left out, as before
            }
        });

        for (var i = 0; i < files.Length; i++)
        {
            if (texts[i] is not { } text)
            {
                continue;
            }

            var file = files[i];
            var entry = new CatalogEntry(file, Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty, text);
            if (text.Contains("depends_on", StringComparison.Ordinal))
            {
                service.ReadFrontmatter(entry);
            }

            service._catalog.Add(entry);
            service._byFile.TryAdd(Path.GetFullPath(file), entry);
        }

        await service.ReadIndexesAsync(ct);
        return service;
    }

    /// <summary>
    /// Resolves ids to test files (case-insensitive). Spec 096 (FR-010): through the suite indexes when the indexed file
    /// still has that id; otherwise by every file's frontmatter id, the first file in path order winning (as before 096).
    /// Unknown ids map to null. Only a resolved target is parsed in full (its title and <c>automated_by</c>).
    /// </summary>
    public IReadOnlyList<(string Id, DeletionTarget? Target)> Resolve(IEnumerable<string> ids) =>
        ids.Select(id =>
        {
            var entry = _indexed.TryGetValue(id, out var path) && _byFile.TryGetValue(path, out var indexed) && HasId(indexed, id)
                ? indexed
                : _catalog.FirstOrDefault(e => HasId(e, id));
            if (entry is null)
            {
                return (id, (DeletionTarget?)null);
            }

            var test = TestOf(entry);
            return (id, new DeletionTarget(entry.Id!, entry.File, entry.Suite, test?.Title,
                test?.AutomatedBy ?? (IReadOnlyList<string>)[]));
        }).ToList();

    /// <summary>
    /// Spec 096 (suite delete): every test with an id under <paramref name="suite"/>'s folder (any depth), in path order,
    /// as targets of that suite.
    /// </summary>
    public IReadOnlyList<DeletionTarget> SuiteTargets(string suite)
    {
        var folder = Path.Combine(_testCasesDir, suite) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return _catalog.Where(e => e.File.StartsWith(folder, comparison) && IdOf(e) is not null)
            .Select(e =>
            {
                var test = TestOf(e);
                return new DeletionTarget(e.Id!, e.File, suite, test?.Title, test?.AutomatedBy ?? (IReadOnlyList<string>)[]);
            })
            .ToList();
    }

    /// <summary>Everything deleting <paramref name="targets"/> would change; nothing is written.</summary>
    public Task<DeletionPlan> PlanAsync(IReadOnlyList<DeletionTarget> targets, CancellationToken ct = default) =>
        PlanAsync(targets, includeBaseline: true, ct);

    /// <summary>
    /// Everything deleting <paramref name="targets"/> would change; nothing is written. With
    /// <paramref name="includeBaseline"/> false (Spec 096, suite delete) the deleted tests' baseline entries are left
    /// alone — the caller removes the whole baseline file — so no baseline is read and no baseline lock is taken.
    /// </summary>
    public async Task<DeletionPlan> PlanAsync(IReadOnlyList<DeletionTarget> targets, bool includeBaseline, CancellationToken ct = default)
    {
        var deleted = targets.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scalar = new List<ScalarDependent>();
        var warnings = new List<string>();
        foreach (var entry in _catalog.Where(e => e.Id is null || !deleted.Contains(e.Id)))
        {
            if (entry.DependsOn is not { } node || !DependencyValues(node).Any(deleted.Contains))
            {
                continue;
            }

            if (node is YamlScalarNode && TestOf(entry) is { DependsOn: { } dep } test && deleted.Contains(dep.Trim()))
            {
                scalar.Add(new ScalarDependent(test.Id, entry.File, dep.Trim(), test));
                continue;
            }

            foreach (var removed in DependencyValues(node).Where(deleted.Contains))
            {
                var who = entry.Id ?? Relative(entry.File);
                warnings.Add(node is YamlSequenceNode
                    ? $"{who}: depends_on is a list (not supported) and names {removed}; not changed ({Relative(entry.File)})"
                    : $"{who}: depends_on names {removed} but the file doesn't parse; not changed ({Relative(entry.File)})");
            }
        }

        var baselineEntries = new List<(string, string)>();
        foreach (var group in includeBaseline ? targets.GroupBy(t => t.Suite, StringComparer.Ordinal) : [])
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

    /// <summary>
    /// Each suite's <c>_index.json</c>: id → file (the suite folder plus the bare <c>file</c>, or the legacy
    /// <c>suite/file</c> form under the tests folder). An unreadable index is skipped — resolution falls back to a scan.
    /// </summary>
    private async Task ReadIndexesAsync(CancellationToken ct)
    {
        var reader = new IndexWriter();
        foreach (var suiteDir in Directory.EnumerateDirectories(_testCasesDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var indexPath = Path.Combine(suiteDir, "_index.json");
            if (!File.Exists(indexPath))
            {
                continue;
            }

            MetadataIndex? index;
            try
            {
                index = await reader.ReadAsync(indexPath, ct);
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var test in index?.Tests ?? [])
            {
                if (string.IsNullOrWhiteSpace(test.Id) || string.IsNullOrWhiteSpace(test.File))
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(suiteDir, test.File));
                if (!_byFile.ContainsKey(path))
                {
                    path = Path.GetFullPath(Path.Combine(_testCasesDir, test.File));
                }

                _indexed.TryAdd(test.Id, path);
            }
        }
    }

    private bool HasId(CatalogEntry entry, string id) =>
        IdOf(entry) is { } entryId && string.Equals(entryId, id, StringComparison.OrdinalIgnoreCase);

    private string? IdOf(CatalogEntry entry)
    {
        if (!entry.FrontmatterRead)
        {
            ReadFrontmatter(entry);
        }

        return entry.Id;
    }

    /// <summary>The entry's frontmatter <c>id</c> and <c>depends_on</c>; a full parse when the frontmatter can't be read alone.</summary>
    private void ReadFrontmatter(CatalogEntry entry)
    {
        entry.FrontmatterRead = true;
        if (FrontmatterOnly(entry.Text) is { } frontmatter)
        {
            entry.Id = frontmatter.Id;
            entry.DependsOn = frontmatter.DependsOn;
            return;
        }

        var test = TestOf(entry);
        entry.Id = test?.Id;
        entry.DependsOn = test?.DependsOn is { } dep ? new YamlScalarNode(dep) : null;
    }

    /// <summary>The entry's full parse, done once on first use; null when the file doesn't parse as a test.</summary>
    private TestCase? TestOf(CatalogEntry entry)
    {
        if (!entry.Parsed)
        {
            entry.Test = Parse(entry.File, entry.Text);
            entry.Parsed = true;
        }

        return entry.Test;
    }

    private TestCase? Parse(string file, string text)
    {
        FullParses++;
        var parsed = _parser.Parse(text, Path.GetFileName(file));
        return parsed.IsSuccess ? parsed.Value : null;
    }

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

    /// <summary>The frontmatter's <c>id</c> and <c>depends_on</c>; null when there is no readable YAML frontmatter.</summary>
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

    /// <summary>One test file: its frontmatter <c>id</c> and <c>depends_on</c>, and its full parse, each read once needed.</summary>
    private sealed class CatalogEntry(string file, string suite, string text)
    {
        public string File { get; } = file;

        public string Suite { get; } = suite;

        public string Text { get; } = text;

        public bool FrontmatterRead { get; set; }

        public string? Id { get; set; }

        public YamlNode? DependsOn { get; set; }

        public bool Parsed { get; set; }

        public TestCase? Test { get; set; }
    }
}
