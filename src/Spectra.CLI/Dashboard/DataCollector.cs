using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Dashboard;
using Spectra.Core.Storage;

namespace Spectra.CLI.Dashboard;

/// <summary>
/// Collects data from test suite indexes and execution reports for dashboard generation.
/// </summary>
public sealed class DataCollector
{
    private readonly string _basePath;
    private readonly string _testsPath;
    private readonly string _reportsPath;
    private readonly ExecutionDbReader _dbReader;

    public DataCollector(string basePath)
    {
        _basePath = basePath;
        _testsPath = Path.Combine(basePath, "tests");
        _reportsPath = Path.Combine(basePath, "reports");
        _dbReader = new ExecutionDbReader(basePath);
    }

    /// <summary>
    /// Collects all data needed for dashboard generation.
    /// </summary>
    public async Task<DashboardData> CollectAsync()
    {
        var suiteIndexes = await LoadSuiteIndexesAsync();
        var runSummaries = await CollectRunHistoryAsync();
        var testEntries = BuildTestEntries(suiteIndexes);
        var suiteStats = BuildSuiteStats(suiteIndexes, runSummaries);

        return new DashboardData
        {
            GeneratedAt = DateTime.UtcNow,
            Repository = Path.GetFileName(_basePath),
            Suites = suiteStats,
            Runs = runSummaries,
            Tests = testEntries
        };
    }

    /// <summary>
    /// Loads all _index.json files from test suites.
    /// </summary>
    private async Task<Dictionary<string, MetadataIndex>> LoadSuiteIndexesAsync()
    {
        var indexes = new Dictionary<string, MetadataIndex>();

        if (!Directory.Exists(_testsPath))
        {
            return indexes;
        }

        foreach (var suiteDir in Directory.GetDirectories(_testsPath))
        {
            var indexPath = Path.Combine(suiteDir, "_index.json");
            if (!File.Exists(indexPath))
            {
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(indexPath);
                var index = JsonSerializer.Deserialize<MetadataIndex>(json);
                if (index is not null)
                {
                    indexes[index.Suite] = index;
                }
            }
            catch (JsonException)
            {
                // Skip malformed index files
            }
        }

        return indexes;
    }

    /// <summary>
    /// Collects run history from both reports/ directory and .execution/ database.
    /// </summary>
    private async Task<IReadOnlyList<RunSummary>> CollectRunHistoryAsync()
    {
        var runs = new List<RunSummary>();

        // Read from .execution/spectra.db
        try
        {
            var dbRuns = await _dbReader.GetRunSummariesAsync();
            runs.AddRange(dbRuns);
        }
        catch (FileNotFoundException)
        {
            // Database doesn't exist, that's OK
        }

        // Read from reports/ directory
        if (Directory.Exists(_reportsPath))
        {
            foreach (var reportFile in Directory.GetFiles(_reportsPath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(reportFile);
                    var report = JsonSerializer.Deserialize<ExecutionReportJson>(json);
                    if (report is not null)
                    {
                        // Check if we already have this run from the database
                        if (!runs.Any(r => r.RunId == report.RunId))
                        {
                            runs.Add(ConvertReportToSummary(report));
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed report files
                }
            }
        }

        // Sort by start time descending
        return runs.OrderByDescending(r => r.StartedAt).ToList();
    }

    /// <summary>
    /// Builds test entries from suite indexes.
    /// </summary>
    private IReadOnlyList<TestEntry> BuildTestEntries(Dictionary<string, MetadataIndex> indexes)
    {
        var entries = new List<TestEntry>();

        foreach (var (suite, index) in indexes)
        {
            foreach (var test in index.Tests)
            {
                entries.Add(new TestEntry
                {
                    Id = test.Id,
                    Suite = suite,
                    Title = test.Title,
                    File = test.File,
                    Priority = test.Priority,
                    Tags = test.Tags,
                    Component = test.Component,
                    SourceRefs = test.SourceRefs,
                    AutomatedBy = null, // Will be populated by coverage analysis
                    HasAutomation = false // Will be populated by coverage analysis
                });
            }
        }

        return entries.OrderBy(e => e.Suite).ThenBy(e => e.Id).ToList();
    }

    /// <summary>
    /// Builds suite statistics from indexes and run history.
    /// </summary>
    private IReadOnlyList<SuiteStats> BuildSuiteStats(
        Dictionary<string, MetadataIndex> indexes,
        IReadOnlyList<RunSummary> runs)
    {
        var stats = new List<SuiteStats>();

        foreach (var (suite, index) in indexes)
        {
            var byPriority = index.Tests
                .GroupBy(t => t.Priority)
                .ToDictionary(g => g.Key, g => g.Count());

            var byComponent = index.Tests
                .Where(t => !string.IsNullOrEmpty(t.Component))
                .GroupBy(t => t.Component!)
                .ToDictionary(g => g.Key, g => g.Count());

            var allTags = index.Tests
                .SelectMany(t => t.Tags)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var lastRun = runs.FirstOrDefault(r =>
                r.Suite.Equals(suite, StringComparison.OrdinalIgnoreCase));

            stats.Add(new SuiteStats
            {
                Name = suite,
                TestCount = index.TestCount,
                ByPriority = byPriority,
                ByComponent = byComponent,
                Tags = allTags,
                LastRun = lastRun,
                AutomationCoverage = 0 // Will be populated by coverage analysis
            });
        }

        return stats.OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Converts a JSON execution report to a run summary.
    /// </summary>
    private static RunSummary ConvertReportToSummary(ExecutionReportJson report)
    {
        return new RunSummary
        {
            RunId = report.RunId,
            Suite = report.Suite ?? "unknown",
            Status = report.Status ?? "completed",
            StartedAt = report.StartedAt,
            CompletedAt = report.CompletedAt,
            StartedBy = report.StartedBy ?? "unknown",
            DurationSeconds = report.CompletedAt.HasValue
                ? (int)(report.CompletedAt.Value - report.StartedAt).TotalSeconds
                : null,
            Total = report.Total,
            Passed = report.Passed,
            Failed = report.Failed,
            Skipped = report.Skipped,
            Blocked = report.Blocked
        };
    }

    /// <summary>
    /// Internal model for deserializing execution report JSON files.
    /// </summary>
    private sealed class ExecutionReportJson
    {
        public string RunId { get; set; } = "";
        public string? Suite { get; set; }
        public string? Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? StartedBy { get; set; }
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Blocked { get; set; }
    }
}
