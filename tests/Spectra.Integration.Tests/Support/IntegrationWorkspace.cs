using System.Text;
using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Integration.Tests.Support;

/// <summary>
/// Spec 052/059: a temporary project directory wired like a real Spectra workspace
/// (config + test-cases/ + docs/criteria/ + .spectra/). Composes the same production
/// persistence the seam's <c>ingest-tests</c> uses — <see cref="TestPersistenceService"/> →
/// the real <see cref="ExecutionEngine"/> reading the on-disk index via <see cref="IndexLoader"/> — so a
/// single test can cross the generation→persistence→execution seam offline. (Spec 070 removed the MCP
/// adapter; these tests now drive the engine directly, the same engine the <c>spectra run</c> CLI uses.)
/// </summary>
public sealed class IntegrationWorkspace : IAsyncDisposable
{
    private ExecutionDb? _db;

    public string Root { get; }
    public string TestsPath { get; }
    public string CriteriaDir { get; }
    public SpectraConfig Config { get; } = SpectraConfig.Default;

    public IntegrationWorkspace()
    {
        Root = Directory.CreateTempSubdirectory("spectra-integ-").FullName;
        TestsPath = Path.Combine(Root, "test-cases");
        CriteriaDir = Path.Combine(Root, "docs", "criteria");
        Directory.CreateDirectory(TestsPath);
        Directory.CreateDirectory(CriteriaDir);
        Directory.CreateDirectory(Path.Combine(Root, ".spectra"));
        File.WriteAllText(Path.Combine(Root, "spectra.config.json"), "{}");
    }

    /// <summary>Writes a {suite}.criteria.yaml whose entries are tagged with the suite component.</summary>
    public async Task SeedCriteriaAsync(string suite, params (string Id, string Text)[] criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine("criteria:");
        foreach (var (id, text) in criteria)
        {
            sb.AppendLine($"  - id: {id}");
            sb.AppendLine($"    text: \"{text}\"");
            sb.AppendLine($"    component: {suite}");
        }
        await File.WriteAllTextAsync(Path.Combine(CriteriaDir, $"{suite}.criteria.yaml"), sb.ToString());
    }

    /// <summary>Loads suite-relevant criteria context via the relocated loader the seam uses (Spec 059).</summary>
    public async Task<(string? Context, int SuiteMatchedCount)> LoadCriteriaAsync(string suite)
    {
        var result = await CriteriaContextLoader.LoadCriteriaContextAsync(Root, suite, Config, CancellationToken.None);
        return (result.Context, result.SuiteMatchedCount);
    }

    /// <summary>Persists tests + regenerates the suite index via the production service (049).</summary>
    public async Task PersistAsync(string suite, IReadOnlyList<TestCase> toWrite, IReadOnlyList<TestCase>? allForIndex = null)
    {
        var svc = new TestPersistenceService(new TestFileWriter(), new IndexGenerator(), new IndexWriter());
        await svc.PersistAsync(TestsPath, suite, toWrite, allForIndex ?? toWrite, CancellationToken.None);
    }

    public MetadataIndex ReadIndex(string suite)
        => JsonSerializer.Deserialize<MetadataIndex>(
            File.ReadAllText(Path.Combine(TestsPath, suite, "_index.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    /// <summary>Loads a suite's test entries from the on-disk index — the same discovery the CLI run surface uses.</summary>
    public Func<string, IEnumerable<TestIndexEntry>> IndexLoader => OnDiskIndexLoader.For(TestsPath);

    /// <summary>The real execution engine over this workspace's SQLite DB (the engine the <c>spectra run</c> CLI drives).</summary>
    public ExecutionEngine BuildEngine()
    {
        _db ??= new ExecutionDb(Root);
        var runRepo = new RunRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var identity = new UserIdentityResolver();
        var config = new McpConfig { BasePath = Root, ReportsPath = Path.Combine(Root, "reports") };
        return new ExecutionEngine(runRepo, resultRepo, new QueueSnapshotRepository(_db), identity, config);
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
            await _db.DisposeAsync();
        try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}
