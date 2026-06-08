using System.Text.Json;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Reports;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Commands.Run;

/// <summary>
/// Spec 065: builds the execution engine and its file-system loaders for the CLI <c>run</c>
/// command group. This is the CLI-side equivalent of the MCP server's wiring
/// (<c>Spectra.MCP/Program.cs</c>) — the SAME <see cref="ExecutionEngine"/> over the SAME
/// <c>.execution/spectra.db</c>, so a short-lived CLI process drives execution identically to the
/// long-lived MCP server (the queue is reconstructed losslessly from the DB per Spec 064).
/// </summary>
public sealed class RunServices : IAsyncDisposable
{
    private readonly ExecutionDb _db;

    public string BasePath { get; }
    public ExecutionEngine Engine { get; }
    public RunRepository RunRepo { get; }
    public ResultRepository ResultRepo { get; }
    public QueueSnapshotRepository SnapshotRepo { get; }
    public ReportGenerator ReportGenerator { get; }
    public ReportWriter ReportWriter { get; }
    public McpConfig Config { get; }

    public Func<string, IEnumerable<TestIndexEntry>> IndexLoader { get; }
    public Func<string, string, TestCase?> TestCaseLoader { get; }
    public Func<IEnumerable<string>> SuiteListLoader { get; }
    public Func<IReadOnlyDictionary<string, SavedSelectionConfig>> SelectionsLoader { get; }

    public RunServices(string? basePath = null)
    {
        BasePath = basePath ?? Directory.GetCurrentDirectory();
        var reportsPath = Path.Combine(BasePath, ".execution", "reports");
        Config = new McpConfig { BasePath = BasePath, ReportsPath = reportsPath };

        Directory.CreateDirectory(Path.Combine(BasePath, ".execution"));
        Directory.CreateDirectory(reportsPath);

        _db = new ExecutionDb(Path.Combine(BasePath, ".execution"));
        RunRepo = new RunRepository(_db);
        ResultRepo = new ResultRepository(_db);
        SnapshotRepo = new QueueSnapshotRepository(_db);
        Engine = new ExecutionEngine(RunRepo, ResultRepo, SnapshotRepo, new UserIdentityResolver(), Config);
        ReportGenerator = new ReportGenerator();
        ReportWriter = new ReportWriter(reportsPath);

        var indexWriter = new IndexWriter();
        var parser = new TestCaseParser();
        var basePath2 = BasePath;

        IndexLoader = suite =>
        {
            var indexPath = Path.Combine(basePath2, "test-cases", suite, "_index.json");
            if (!File.Exists(indexPath)) return [];
            var index = indexWriter.ReadAsync(indexPath).GetAwaiter().GetResult();
            return index?.Tests ?? [];
        };

        TestCaseLoader = (suite, testId) =>
        {
            var entry = IndexLoader(suite).FirstOrDefault(e => e.Id == testId);
            if (entry is null) return null;

            var fileName = entry.File;
            if (fileName.StartsWith(suite + Path.DirectorySeparatorChar) ||
                fileName.StartsWith(suite + Path.AltDirectorySeparatorChar))
            {
                fileName = fileName.Substring(suite.Length + 1);
            }

            var testPath = Path.Combine(basePath2, "test-cases", suite, fileName);
            if (!File.Exists(testPath)) return null;

            var parseResult = parser.Parse(File.ReadAllText(testPath), testPath);
            return parseResult.IsSuccess ? parseResult.Value : null;
        };

        SuiteListLoader = () =>
        {
            var testsDir = Path.Combine(basePath2, "test-cases");
            if (!Directory.Exists(testsDir)) return [];
            return Directory.GetDirectories(testsDir)
                .Select(Path.GetFileName)
                .Where(name => name is not null && File.Exists(Path.Combine(testsDir, name, "_index.json")))
                .Cast<string>();
        };

        SelectionsLoader = () =>
        {
            var configPath = Path.Combine(basePath2, "spectra.config.json");
            if (!File.Exists(configPath)) return new Dictionary<string, SavedSelectionConfig>();
            try
            {
                var json = File.ReadAllText(configPath);
                var spectraConfig = JsonSerializer.Deserialize<SpectraConfig>(json);
                return spectraConfig?.Selections ?? new Dictionary<string, SavedSelectionConfig>();
            }
            catch
            {
                return new Dictionary<string, SavedSelectionConfig>();
            }
        };
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();
}
