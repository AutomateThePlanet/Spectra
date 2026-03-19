// Spectra MCP Execution Server
// Entry point for stdio transport

using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Parsing;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Reports;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools.Data;
using Spectra.MCP.Tools.Reporting;
using Spectra.MCP.Tools.RunManagement;
using Spectra.MCP.Tools.TestExecution;

namespace Spectra.MCP;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var basePath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var config = new McpConfig
        {
            BasePath = basePath,
            ReportsPath = Path.Combine(basePath, ".execution", "reports")
        };

        // Ensure directories exist
        Directory.CreateDirectory(Path.Combine(basePath, ".execution"));
        Directory.CreateDirectory(config.ReportsPath);

        // Initialize infrastructure
        var db = new ExecutionDb(Path.Combine(basePath, ".execution"));
        var runRepo = new RunRepository(db);
        var resultRepo = new ResultRepository(db);
        var identity = new UserIdentityResolver();
        var logger = new McpLogging(config.LogLevel);

        // Initialize engine and services
        var engine = new ExecutionEngine(runRepo, resultRepo, identity, config);
        var reportGenerator = new ReportGenerator();
        var reportWriter = new ReportWriter(config.ReportsPath);

        var indexWriter = new IndexWriter();
        var parser = new TestCaseParser();

        // Index and test case loaders
        Func<string, IEnumerable<TestIndexEntry>> indexLoader = suite =>
        {
            var indexPath = Path.Combine(basePath, "tests", suite, "_index.json");
            if (!File.Exists(indexPath)) return [];
            var index = indexWriter.ReadAsync(indexPath).GetAwaiter().GetResult();
            return index?.Tests ?? [];
        };

        Func<string, string, TestCase?> testCaseLoader = (suite, testId) =>
        {
            var entries = indexLoader(suite);
            var entry = entries.FirstOrDefault(e => e.Id == testId);
            if (entry is null) return null;

            // Handle both old format (suite\file.md) and new format (file.md)
            var fileName = entry.File;
            if (fileName.StartsWith(suite + Path.DirectorySeparatorChar) ||
                fileName.StartsWith(suite + Path.AltDirectorySeparatorChar))
            {
                // Old format: strip the suite prefix to avoid doubling
                fileName = fileName.Substring(suite.Length + 1);
            }

            var testPath = Path.Combine(basePath, "tests", suite, fileName);
            if (!File.Exists(testPath)) return null;

            var parseResult = parser.Parse(File.ReadAllText(testPath), testPath);
            return parseResult.IsSuccess ? parseResult.Value : null;
        };

        Func<string, IEnumerable<SuiteInfo>> suiteLoader = _ =>
        {
            var testsDir = Path.Combine(basePath, "tests");
            if (!Directory.Exists(testsDir)) return [];

            return Directory.GetDirectories(testsDir)
                .Select(d =>
                {
                    var name = Path.GetFileName(d);
                    var indexPath = Path.Combine(d, "_index.json");
                    if (!File.Exists(indexPath)) return null;

                    var index = indexWriter.ReadAsync(indexPath).GetAwaiter().GetResult();
                    if (index is null) return null;
                    return new SuiteInfo(name, index.Tests.Count, d);
                })
                .Where(s => s is not null)
                .Cast<SuiteInfo>();
        };

        // Register tools
        var registry = new ToolRegistry();

        // Run management tools
        registry.Register("start_execution_run", new StartExecutionRunTool(engine, indexLoader));
        registry.Register("get_execution_status", new GetExecutionStatusTool(engine));
        registry.Register("pause_execution_run", new PauseExecutionRunTool(engine));
        registry.Register("resume_execution_run", new ResumeExecutionRunTool(engine));
        registry.Register("cancel_execution_run", new CancelExecutionRunTool(engine));
        registry.Register("finalize_execution_run", new FinalizeExecutionRunTool(engine, reportGenerator, reportWriter, indexLoader));
        registry.Register("list_available_suites", new ListAvailableSuitesTool(suiteLoader));

        // Test execution tools
        registry.Register("get_test_case_details", new GetTestCaseDetailsTool(engine, testCaseLoader));
        registry.Register("advance_test_case", new AdvanceTestCaseTool(engine));
        registry.Register("skip_test_case", new SkipTestCaseTool(engine));
        registry.Register("bulk_record_results", new BulkRecordResultsTool(engine));
        registry.Register("add_test_note", new AddTestNoteTool(engine));
        registry.Register("retest_test_case", new RetestTestCaseTool(engine));
        registry.Register("save_screenshot", new SaveScreenshotTool(engine, config.ReportsPath));

        // Reporting tools
        registry.Register("get_run_history", new GetRunHistoryTool(runRepo));
        registry.Register("get_execution_summary", new GetExecutionSummaryTool(runRepo, resultRepo));

        // Data tools (deterministic, no AI dependency)
        registry.Register("validate_tests", new ValidateTestsTool(basePath));
        registry.Register("rebuild_indexes", new RebuildIndexesTool(basePath));
        registry.Register("analyze_coverage_gaps", new AnalyzeCoverageGapsTool(basePath));

        // Create and run server
        var server = new McpServer(registry, logger: logger);

        logger.LogInfo($"Spectra MCP Server starting. Base path: {basePath}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await server.RunAsync(cts.Token);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }
}
