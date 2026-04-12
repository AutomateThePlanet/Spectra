using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Coverage;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Models.Dashboard;
using Spectra.Core.Models.Execution;
using Spectra.Core.Parsing;
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

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DataCollector(string basePath)
    {
        _basePath = basePath;
        _testsPath = Path.Combine(basePath, "test-cases");
        _reportsPath = Path.Combine(basePath, ".execution", "reports");
        _dbReader = new ExecutionDbReader(basePath);
    }

    /// <summary>
    /// Collects all data needed for dashboard generation.
    /// </summary>
    public async Task<DashboardData> CollectAsync()
    {
        var suiteIndexes = await LoadSuiteIndexesAsync();
        var runSummaries = await CollectRunHistoryAsync();
        var automatedTestIds = await ScanAutomatedTestIdsAsync();
        var testEntries = BuildTestEntries(suiteIndexes, automatedTestIds);
        var suiteStats = BuildSuiteStats(suiteIndexes, runSummaries, automatedTestIds);
        var trends = CalculateTrends(runSummaries);
        var coverage = BuildCoverageData(testEntries);
        var coverageSummary = await BuildCoverageSummaryAsync(suiteIndexes, testEntries);

        return new DashboardData
        {
            GeneratedAt = DateTime.UtcNow,
            Repository = Path.GetFileName(_basePath),
            Suites = suiteStats,
            Runs = runSummaries,
            Tests = testEntries,
            Coverage = coverage,
            Trends = trends,
            CoverageSummary = coverageSummary
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
    /// Scans automation directories and returns a map of test ID → automation file path.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ScanAutomatedTestIdsAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var configPath = Path.Combine(_basePath, "spectra.config.json");
            if (!File.Exists(configPath))
            {
                return result;
            }

            var configJson = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<SpectraConfig>(configJson, s_jsonOptions);
            if (config?.Coverage is null)
            {
                return result;
            }

            var scanner = AutomationScanner.FromConfig(_basePath, config.Coverage);
            var automationFiles = await scanner.ScanAsync();

            foreach (var (filePath, info) in automationFiles)
            {
                foreach (var testId in info.ReferencedTestIds)
                {
                    result.TryAdd(testId, filePath);
                }
            }
        }
        catch
        {
            // Non-fatal — dashboard still works without automation data
        }

        return result;
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

        // Read from reports/ directory - add new runs or enrich DB runs with detailed results
        if (Directory.Exists(_reportsPath))
        {
            foreach (var reportFile in Directory.GetFiles(_reportsPath, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(reportFile);
                    var report = JsonSerializer.Deserialize<ExecutionReportJson>(json, s_jsonOptions);
                    if (report is not null)
                    {
                        var runId = report.GetRunId();
                        if (string.IsNullOrEmpty(runId)) continue;

                        var existingIndex = runs.FindIndex(r => r.RunId == runId);
                        if (existingIndex >= 0)
                        {
                            // Enrich existing DB run with detailed results from report
                            var existing = runs[existingIndex];
                            if (existing.Results is null)
                            {
                                var enriched = ConvertReportToSummary(report);
                                runs[existingIndex] = new RunSummary
                                {
                                    RunId = existing.RunId,
                                    Suite = existing.Suite,
                                    Status = existing.Status,
                                    StartedAt = existing.StartedAt,
                                    CompletedAt = existing.CompletedAt,
                                    StartedBy = existing.StartedBy,
                                    DurationSeconds = existing.DurationSeconds,
                                    Total = existing.Total,
                                    Passed = existing.Passed,
                                    Failed = existing.Failed,
                                    Skipped = existing.Skipped,
                                    Blocked = existing.Blocked,
                                    Results = EmbedScreenshotsAsBase64(enriched.Results),
                                    ReportPath = existing.ReportPath
                                };
                            }
                        }
                        else
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

        // Enrich with report paths — find matching HTML report by run_id in filename or content
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.ReportPath is null && Directory.Exists(_reportsPath))
            {
                // Try legacy format first: {runId}.html
                var htmlPath = Path.Combine(_reportsPath, $"{run.RunId}.html");
                if (!File.Exists(htmlPath))
                {
                    // Try new format: find any HTML file whose matching JSON contains this run_id
                    foreach (var htmlFile in Directory.GetFiles(_reportsPath, "*.html"))
                    {
                        var jsonFile = Path.ChangeExtension(htmlFile, ".json");
                        if (File.Exists(jsonFile))
                        {
                            try
                            {
                                var jsonContent = File.ReadAllText(jsonFile);
                                if (jsonContent.Contains(run.RunId))
                                {
                                    htmlPath = htmlFile;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (File.Exists(htmlPath))
                {
                    var relativePath = $".execution/reports/{Path.GetFileName(htmlPath)}";
                    runs[i] = new RunSummary
                    {
                        RunId = run.RunId,
                        Suite = run.Suite,
                        Status = run.Status,
                        StartedAt = run.StartedAt,
                        CompletedAt = run.CompletedAt,
                        StartedBy = run.StartedBy,
                        DurationSeconds = run.DurationSeconds,
                        Total = run.Total,
                        Passed = run.Passed,
                        Failed = run.Failed,
                        Skipped = run.Skipped,
                        Blocked = run.Blocked,
                        Results = run.Results,
                        ReportPath = relativePath
                    };
                }
            }
        }

        // Sort by start time descending
        return runs.OrderByDescending(r => r.StartedAt).ToList();
    }

    /// <summary>
    /// Builds test entries from suite indexes, including parsed content.
    /// Uses scanner results to populate automation status.
    /// </summary>
    private IReadOnlyList<TestEntry> BuildTestEntries(
        Dictionary<string, MetadataIndex> indexes,
        IReadOnlyDictionary<string, string> automatedTestIds)
    {
        var entries = new List<TestEntry>();
        var parser = new TestCaseParser();

        foreach (var (suite, index) in indexes)
        {
            foreach (var test in index.Tests)
            {
                // Try to read and parse the test file for content
                string? content = null;
                IReadOnlyList<string>? steps = null;
                string? expectedResult = null;
                string? preconditions = null;

                try
                {
                    // test.File may be "TC-100.md" or "suite\TC-100.md" — handle both
                    var fileName = Path.GetFileName(test.File);
                    var testFilePath = Path.Combine(_testsPath, suite, fileName);
                    if (File.Exists(testFilePath))
                    {
                        content = File.ReadAllText(testFilePath);
                        var parseResult = parser.Parse(content, test.File);
                        if (parseResult.IsSuccess)
                        {
                            var testCase = parseResult.Value;
                            steps = testCase.Steps.Count > 0 ? testCase.Steps : null;
                            expectedResult = !string.IsNullOrWhiteSpace(testCase.ExpectedResult)
                                ? testCase.ExpectedResult : null;
                            preconditions = testCase.Preconditions;
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors - just include basic info
                }

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
                    AutomatedBy = automatedTestIds.TryGetValue(test.Id, out var autoFile) ? autoFile : null,
                    HasAutomation = automatedTestIds.ContainsKey(test.Id),
                    Steps = steps,
                    ExpectedResult = expectedResult,
                    Preconditions = preconditions,
                    Content = content
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
        IReadOnlyList<RunSummary> runs,
        IReadOnlyDictionary<string, string> automatedTestIds)
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

            var suiteAutomated = index.Tests.Count(t => automatedTestIds.ContainsKey(t.Id));
            var autoPct = index.TestCount > 0
                ? Math.Round((suiteAutomated * 100m) / index.TestCount, 2) : 0m;

            stats.Add(new SuiteStats
            {
                Name = suite,
                TestCount = index.TestCount,
                ByPriority = byPriority,
                ByComponent = byComponent,
                Tags = allTags,
                LastRun = lastRun,
                AutomationCoverage = autoPct
            });
        }

        return stats.OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Converts a JSON execution report to a run summary.
    /// </summary>
    private RunSummary ConvertReportToSummary(ExecutionReportJson report)
    {
        // Use summary object if available, otherwise use legacy fields
        var total = report.Summary?.Total ?? report.Total;
        var passed = report.Summary?.Passed ?? report.Passed;
        var failed = report.Summary?.Failed ?? report.Failed;
        var skipped = report.Summary?.Skipped ?? report.Skipped;
        var blocked = report.Summary?.Blocked ?? report.Blocked;

        var startedAt = report.GetStartedAt();
        var completedAt = report.GetCompletedAt();

        return new RunSummary
        {
            RunId = report.GetRunId(),
            Suite = report.Suite ?? "unknown",
            Status = report.Status ?? "completed",
            StartedAt = startedAt,
            CompletedAt = completedAt,
            StartedBy = report.GetStartedBy(),
            DurationSeconds = completedAt.HasValue
                ? Math.Max(0, (int)(completedAt.Value.ToUniversalTime() - startedAt.ToUniversalTime()).TotalSeconds)
                : null,
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Blocked = blocked,
            Results = EmbedScreenshotsAsBase64(report.Results),
            ReportPath = File.Exists(Path.Combine(_reportsPath, $"{report.GetRunId()}.html"))
                ? $".execution/reports/{report.GetRunId()}.html"
                : null
        };
    }

    /// <summary>
    /// Calculates trend data from run history.
    /// </summary>
    private static TrendData CalculateTrends(IReadOnlyList<RunSummary> runs)
    {
        if (runs.Count == 0)
        {
            return new TrendData();
        }

        // Calculate overall pass rate
        var totalPassed = runs.Sum(r => r.Passed);
        var totalTests = runs.Sum(r => r.Total);
        var overallPassRate = totalTests > 0 ? (totalPassed * 100m) / totalTests : 0m;

        // Group runs by date for trend points (aggregate by day)
        var pointsByDate = runs
            .GroupBy(r => r.StartedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var dayPassed = g.Sum(r => r.Passed);
                var dayTotal = g.Sum(r => r.Total);
                var dayFailed = g.Sum(r => r.Failed);
                return new TrendPoint
                {
                    Date = g.Key,
                    PassRate = dayTotal > 0 ? Math.Round((dayPassed * 100m) / dayTotal, 2) : 0m,
                    Total = dayTotal,
                    Passed = dayPassed,
                    Failed = dayFailed
                };
            })
            .ToList();

        // Calculate trend direction (compare first half to second half)
        var direction = "stable";
        if (pointsByDate.Count >= 2)
        {
            var midpoint = pointsByDate.Count / 2;
            var firstHalfAvg = pointsByDate.Take(midpoint).Average(p => p.PassRate);
            var secondHalfAvg = pointsByDate.Skip(midpoint).Average(p => p.PassRate);
            var diff = secondHalfAvg - firstHalfAvg;

            if (diff > 5) direction = "improving";
            else if (diff < -5) direction = "declining";
        }

        // Calculate trends by suite
        var suiteTrends = runs
            .GroupBy(r => r.Suite, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var suiteRuns = g.OrderByDescending(r => r.StartedAt).ToList();
                var suitePassed = suiteRuns.Sum(r => r.Passed);
                var suiteTotal = suiteRuns.Sum(r => r.Total);
                var currentRate = suiteTotal > 0 ? Math.Round((suitePassed * 100m) / suiteTotal, 2) : 0m;

                // Calculate change from previous runs
                decimal change = 0m;
                if (suiteRuns.Count >= 2)
                {
                    var recentRuns = suiteRuns.Take(suiteRuns.Count / 2 + 1).ToList();
                    var olderRuns = suiteRuns.Skip(suiteRuns.Count / 2 + 1).ToList();

                    if (olderRuns.Count > 0)
                    {
                        var recentPassed = recentRuns.Sum(r => r.Passed);
                        var recentTotal = recentRuns.Sum(r => r.Total);
                        var recentRate = recentTotal > 0 ? (recentPassed * 100m) / recentTotal : 0m;

                        var olderPassed = olderRuns.Sum(r => r.Passed);
                        var olderTotal = olderRuns.Sum(r => r.Total);
                        var olderRate = olderTotal > 0 ? (olderPassed * 100m) / olderTotal : 0m;

                        change = Math.Round(recentRate - olderRate, 2);
                    }
                }

                return new SuiteTrend
                {
                    Suite = g.Key,
                    PassRate = currentRate,
                    RunCount = suiteRuns.Count,
                    Change = change
                };
            })
            .OrderBy(s => s.Suite)
            .ToList();

        return new TrendData
        {
            Points = pointsByDate,
            OverallPassRate = Math.Round(overallPassRate, 2),
            Direction = direction,
            BySuite = suiteTrends
        };
    }

    /// <summary>
    /// Builds coverage data for visualization from test entries.
    /// </summary>
    private static CoverageData BuildCoverageData(IReadOnlyList<TestEntry> testEntries)
    {
        var nodes = new List<CoverageNode>();
        var links = new List<CoverageLink>();
        var documentNodes = new Dictionary<string, CoverageNode>();
        var automationNodes = new Dictionary<string, CoverageNode>();

        // Build test nodes and extract document/automation references
        foreach (var test in testEntries)
        {
            var testNodeId = $"test:{test.Id}";
            var testStatus = test.HasAutomation ? CoverageStatus.Covered : CoverageStatus.Partial;

            nodes.Add(new CoverageNode
            {
                Id = testNodeId,
                Type = NodeType.Test,
                Name = test.Title,
                Path = test.File,
                Status = testStatus
            });

            // Process source_refs -> document nodes
            foreach (var sourceRef in test.SourceRefs)
            {
                var docNodeId = $"doc:{NormalizeDocPath(sourceRef)}";

                if (!documentNodes.ContainsKey(docNodeId))
                {
                    documentNodes[docNodeId] = new CoverageNode
                    {
                        Id = docNodeId,
                        Type = NodeType.Document,
                        Name = Path.GetFileName(sourceRef),
                        Path = sourceRef,
                        Status = CoverageStatus.Covered // Has at least one test
                    };
                }

                // Document -> Test link
                links.Add(new CoverageLink
                {
                    Source = docNodeId,
                    Target = testNodeId,
                    Type = LinkType.DocumentToTest,
                    Status = LinkStatus.Valid
                });
            }

            // Process automated_by -> automation nodes
            if (!string.IsNullOrEmpty(test.AutomatedBy))
            {
                var automationNodeId = $"auto:{NormalizeDocPath(test.AutomatedBy)}";

                if (!automationNodes.ContainsKey(automationNodeId))
                {
                    automationNodes[automationNodeId] = new CoverageNode
                    {
                        Id = automationNodeId,
                        Type = NodeType.Automation,
                        Name = Path.GetFileName(test.AutomatedBy),
                        Path = test.AutomatedBy,
                        Status = CoverageStatus.Covered
                    };
                }

                // Test -> Automation link
                links.Add(new CoverageLink
                {
                    Source = testNodeId,
                    Target = automationNodeId,
                    Type = LinkType.TestToAutomation,
                    Status = LinkStatus.Valid
                });
            }
        }

        // Add document nodes
        nodes.AddRange(documentNodes.Values);

        // Add automation nodes
        nodes.AddRange(automationNodes.Values);

        // Update document status based on whether all linked tests have automation
        foreach (var docNode in documentNodes.Values)
        {
            var docLinks = links
                .Where(l => l.Source == docNode.Id && l.Type == LinkType.DocumentToTest)
                .ToList();

            var linkedTestIds = docLinks.Select(l => l.Target).ToHashSet();
            var allTestsAutomated = testEntries
                .Where(t => linkedTestIds.Contains($"test:{t.Id}"))
                .All(t => t.HasAutomation);

            if (!allTestsAutomated)
            {
                // Update to partial since not all tests are automated
                var index = nodes.IndexOf(docNode);
                nodes[index] = new CoverageNode
                {
                    Id = docNode.Id,
                    Type = docNode.Type,
                    Name = docNode.Name,
                    Path = docNode.Path,
                    Status = CoverageStatus.Partial,
                    Children = docNode.Children
                };
            }
        }

        return new CoverageData
        {
            Nodes = nodes.OrderBy(n => n.Type).ThenBy(n => n.Id).ToList(),
            Links = links.OrderBy(l => l.Source).ThenBy(l => l.Target).ToList()
        };
    }

    /// <summary>
    /// Builds the three-section coverage summary for the dashboard.
    /// </summary>
    private async Task<CoverageSummaryData?> BuildCoverageSummaryAsync(
        Dictionary<string, MetadataIndex> suiteIndexes,
        IReadOnlyList<TestEntry> testEntries)
    {
        try
        {
            // Build a lookup from test ID → index entry for criteria/automated_by
            // Use first-wins for duplicate IDs across suites
            var indexEntryLookup = new Dictionary<string, TestIndexEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in suiteIndexes.Values.SelectMany(idx => idx.Tests))
            {
                indexEntryLookup.TryAdd(entry.Id, entry);
            }

            // ── Documentation coverage ──
            // Map each doc path → test IDs that reference it
            var docToTests = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var test in testEntries)
            {
                foreach (var docRef in test.SourceRefs)
                {
                    var docPath = SourceRefNormalizer.NormalizePath(docRef);
                    if (!docToTests.TryGetValue(docPath, out var list))
                    {
                        list = [];
                        docToTests[docPath] = list;
                    }
                    list.Add(test.Id);
                }
            }

            var docDetails = new List<DocumentationCoverageDetail>();
            var docsDir = Path.Combine(_basePath, "docs");
            if (Directory.Exists(docsDir))
            {
                var docFiles = Directory.GetFiles(docsDir, "*.md", SearchOption.AllDirectories);
                foreach (var docFile in docFiles)
                {
                    var relativePath = SourceRefNormalizer.NormalizePath(
                        Path.GetRelativePath(_basePath, docFile));
                    var testIds = docToTests.TryGetValue(relativePath, out var ids)
                        ? ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList()
                        : [];
                    docDetails.Add(new DocumentationCoverageDetail
                    {
                        Doc = relativePath,
                        TestCount = testIds.Count,
                        Covered = testIds.Count > 0,
                        TestIds = testIds
                    });
                }
            }
            docDetails.Sort((a, b) => string.Compare(a.Doc, b.Doc, StringComparison.OrdinalIgnoreCase));

            var coveredDocs = docDetails.Count(d => d.Covered);
            var totalDocs = docDetails.Count;
            var docPercentage = totalDocs > 0
                ? Math.Round((coveredDocs * 100m) / totalDocs, 2) : 0m;

            // ── Undocumented tests (empty source_refs) ──
            var undocumentedTestIds = testEntries
                .Where(t => t.SourceRefs.Count == 0)
                .Select(t => t.Id)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var undocumentedTestCount = undocumentedTestIds.Count;

            // ── Acceptance criteria coverage ──
            var configPath = Path.Combine(_basePath, "spectra.config.json");
            var criteriaDetails = new List<Core.Models.Coverage.CriteriaCoverageDetail>();
            var hasCriteriaFile = false;

            if (File.Exists(configPath))
            {
                try
                {
                    var configJson = await File.ReadAllTextAsync(configPath);
                    var config = JsonSerializer.Deserialize<SpectraConfig>(configJson, s_jsonOptions);
                    if (config is not null)
                    {
                        var criteriaDir = Path.Combine(_basePath, config.Coverage.CriteriaDir);
                        var criteriaFilePath = Path.Combine(_basePath, config.Coverage.CriteriaFile);

                        // Build TestCase list from test entries for the analyzer
                        var testCases = new List<TestCase>();
                        foreach (var test in testEntries)
                        {
                            if (indexEntryLookup.TryGetValue(test.Id, out var idxEntry))
                            {
                                testCases.Add(new TestCase
                                {
                                    Id = test.Id,
                                    Title = test.Title,
                                    FilePath = string.Empty,
                                    Priority = Priority.Medium,
                                    ExpectedResult = string.Empty,
                                    Requirements = idxEntry.Requirements,
                                    Criteria = idxEntry.Criteria
                                });
                            }
                        }

                        // Use the proper analyzer that reads per-document .criteria.yaml files
                        var analyzer = new AcceptanceCriteriaCoverageAnalyzer();
                        var criteriaCoverage = await analyzer.AnalyzeAsync(criteriaFilePath, testCases);

                        hasCriteriaFile = criteriaCoverage.HasCriteriaFile;
                        criteriaDetails.AddRange(criteriaCoverage.Details);
                    }
                }
                catch
                {
                    // Config read failure is non-fatal for dashboard
                }
            }
            criteriaDetails.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));

            var criteriaCovered = criteriaDetails.Count(r => r.Covered);
            var criteriaTotal = criteriaDetails.Count;
            var criteriaPercentage = criteriaTotal > 0
                ? Math.Round((criteriaCovered * 100m) / criteriaTotal, 2) : 0m;

            // ── Automation coverage ──
            // Per-suite breakdown
            var suiteGroups = testEntries
                .GroupBy(t => t.Suite, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            var autoDetails = new List<AutomationSuiteDetail>();
            var unlinkedTests = new List<UnlinkedTestDetail>();

            foreach (var group in suiteGroups)
            {
                var suiteTests = group.ToList();
                var suiteTotal = suiteTests.Count;
                var suiteAutomated = suiteTests.Count(t => t.HasAutomation);
                var suitePct = suiteTotal > 0
                    ? Math.Round((suiteAutomated * 100m) / suiteTotal, 2) : 0m;

                autoDetails.Add(new AutomationSuiteDetail
                {
                    Suite = group.Key,
                    Total = suiteTotal,
                    Automated = suiteAutomated,
                    Percentage = suitePct
                });

                // Collect unlinked tests from this suite
                foreach (var test in suiteTests.Where(t => !t.HasAutomation))
                {
                    unlinkedTests.Add(new UnlinkedTestDetail
                    {
                        TestId = test.Id,
                        Suite = test.Suite,
                        Title = test.Title,
                        Priority = test.Priority
                    });
                }
            }

            var totalTests = testEntries.Count;
            var automatedTests = testEntries.Count(t => t.HasAutomation);
            var autoPercentage = totalTests > 0
                ? Math.Round((automatedTests * 100m) / totalTests, 2) : 0m;

            return new CoverageSummaryData
            {
                Documentation = new DocumentationSectionData
                {
                    Covered = coveredDocs,
                    Total = totalDocs,
                    Percentage = docPercentage,
                    Details = docDetails,
                    UndocumentedTestCount = undocumentedTestCount,
                    UndocumentedTestIds = undocumentedTestCount > 0 ? undocumentedTestIds : null
                },
                AcceptanceCriteria = new AcceptanceCriteriaSectionData
                {
                    Covered = criteriaCovered,
                    Total = criteriaTotal,
                    Percentage = criteriaPercentage,
                    HasCriteriaFile = hasCriteriaFile,
                    Details = criteriaDetails
                },
                Automation = new AutomationSectionData
                {
                    Covered = automatedTests,
                    Total = totalTests,
                    Percentage = autoPercentage,
                    Details = autoDetails,
                    UnlinkedTests = unlinkedTests.Count > 0 ? unlinkedTests : null
                }
            };
        }
        catch
        {
            return new CoverageSummaryData
            {
                Documentation = new DocumentationSectionData { Covered = 0, Total = 0, Percentage = 0, Details = [] },
                AcceptanceCriteria = new AcceptanceCriteriaSectionData { Covered = 0, Total = 0, Percentage = 0, HasCriteriaFile = false, Details = [] },
                Automation = new AutomationSectionData { Covered = 0, Total = 0, Percentage = 0, Details = [] }
            };
        }
    }

    /// <summary>
    /// Converts screenshot file paths in test results to base64 data URIs so the
    /// dashboard works when served from any location.
    /// </summary>
    private IReadOnlyList<TestResultEntry>? EmbedScreenshotsAsBase64(IReadOnlyList<TestResultEntry>? results)
    {
        if (results is null) return null;

        var processed = new List<TestResultEntry>();
        foreach (var r in results)
        {
            if (r.ScreenshotPaths is { Count: > 0 })
            {
                var base64Paths = new List<string>();
                foreach (var path in r.ScreenshotPaths)
                {
                    var fullPath = Path.Combine(_reportsPath, path);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var bytes = File.ReadAllBytes(fullPath);
                            var base64 = Convert.ToBase64String(bytes);
                            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                            var mime = ext switch
                            {
                                ".webp" => "image/webp",
                                ".png" => "image/png",
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".gif" => "image/gif",
                                _ => "image/png"
                            };
                            base64Paths.Add($"data:{mime};base64,{base64}");
                        }
                        catch
                        {
                            base64Paths.Add(path); // Keep original if conversion fails
                        }
                    }
                    else
                    {
                        base64Paths.Add(path); // Keep original if file not found
                    }
                }
                processed.Add(r with { ScreenshotPaths = base64Paths });
            }
            else
            {
                processed.Add(r);
            }
        }
        return processed;
    }

    /// <summary>
    /// Normalizes a document path for use as an ID.
    /// </summary>
    private static string NormalizeDocPath(string path)
    {
        return path.Replace('\\', '/').Replace('/', '_').Replace('.', '_');
    }

    /// <summary>
    /// Internal model for deserializing execution report JSON files.
    /// Supports both snake_case (actual reports) and PascalCase (tests) via PropertyNameCaseInsensitive.
    /// </summary>
    private sealed class ExecutionReportJson
    {
        // Support both "run_id" and "RunId" via aliases
        [JsonPropertyName("run_id")]
        public string? RunIdSnake { get; set; }
        public string? RunId { get; set; }
        public string GetRunId() => RunIdSnake ?? RunId ?? "";

        public string? Suite { get; set; }
        public string? Status { get; set; }

        // Support both "started_at" and "StartedAt"
        [JsonPropertyName("started_at")]
        public DateTime? StartedAtSnake { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime GetStartedAt() => StartedAtSnake ?? StartedAt ?? DateTime.UtcNow;

        // Support both "completed_at" and "CompletedAt"
        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAtSnake { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? GetCompletedAt() => CompletedAtSnake ?? CompletedAt;

        // Support both "executed_by" and "StartedBy"
        [JsonPropertyName("executed_by")]
        public string? ExecutedBy { get; set; }
        public string? StartedBy { get; set; }
        public string GetStartedBy() => ExecutedBy ?? StartedBy ?? "unknown";

        public ReportSummaryJson? Summary { get; set; }
        public List<TestResultEntry>? Results { get; set; }

        // Legacy fields for backwards compatibility
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Blocked { get; set; }
    }

    /// <summary>
    /// Internal model for deserializing report summary.
    /// </summary>
    private sealed class ReportSummaryJson
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Blocked { get; set; }
    }
}
