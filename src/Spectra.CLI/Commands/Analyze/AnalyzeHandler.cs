using System.Text.Json;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Source;
using Spectra.Core.Coverage;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using CoverageModels = Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Commands.Analyze;

/// <summary>
/// Handles the analyze command execution.
/// </summary>
public sealed class AnalyzeHandler
{
    private readonly VerbosityLevel _verbosity;

    public AnalyzeHandler(VerbosityLevel verbosity = VerbosityLevel.Normal)
    {
        _verbosity = verbosity;
    }

    /// <summary>
    /// Executes the analyze command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? outputPath,
        ReportFormat format,
        bool analyzeAutomationCoverage = false,
        CancellationToken ct = default)
    {
        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(basePath, "spectra.config.json");

            // Load configuration
            SpectraConfig? config = null;
            if (File.Exists(configPath))
            {
                var configJson = await File.ReadAllTextAsync(configPath, ct);
                config = JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            if (config is null)
            {
                Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
                return ExitCodes.Error;
            }

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine("Analyzing test coverage...");
            }

            // Build document map
            var mapBuilder = new DocumentMapBuilder(config.Source);
            var documentMap = await mapBuilder.BuildAsync(basePath, ct);

            // Load all tests
            var testsDir = Path.Combine(basePath, config.Tests?.Dir ?? "tests");
            var allTests = new List<TestCase>();
            var suiteCoverages = new List<SuiteCoverage>();

            if (Directory.Exists(testsDir))
            {
                foreach (var suiteDir in Directory.GetDirectories(testsDir))
                {
                    var suiteName = Path.GetFileName(suiteDir);
                    var batchReader = new BatchReadTestsTool();
                    var readResult = await batchReader.ExecuteAsync(suiteDir, ct: ct);

                    if (readResult.Success && readResult.Tests is not null)
                    {
                        allTests.AddRange(readResult.Tests);

                        var coveredDocs = readResult.Tests
                            .SelectMany(t => t.SourceRefs)
                            .Distinct()
                            .ToList();

                        suiteCoverages.Add(new SuiteCoverage
                        {
                            Name = suiteName,
                            TestCount = readResult.Tests.Count,
                            DocumentsCovered = coveredDocs.Count,
                            CoveredDocuments = coveredDocs
                        });
                    }
                }
            }

            // Analyze coverage
            var documentCoverages = new List<DocumentCoverage>();
            var gaps = new List<CoverageGap>();

            foreach (var doc in documentMap.Documents)
            {
                var testsForDoc = allTests
                    .Where(t => t.SourceRefs.Contains(doc.Path))
                    .ToList();

                var isCovered = testsForDoc.Count > 0;

                documentCoverages.Add(new DocumentCoverage
                {
                    Path = doc.Path,
                    IsCovered = isCovered,
                    TestCount = testsForDoc.Count,
                    TestIds = testsForDoc.Select(t => t.Id).ToList()
                });

                if (!isCovered)
                {
                    gaps.Add(new CoverageGap
                    {
                        DocumentPath = doc.Path,
                        Reason = "No tests reference this document",
                        Severity = DetermineSeverity(doc),
                        Suggestion = $"Create tests for {doc.Path}"
                    });
                }
            }

            // Build report
            var report = new CoverageReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalDocuments = documentMap.Documents.Count,
                TotalTests = allTests.Count,
                CoveredDocuments = documentCoverages.Count(d => d.IsCovered),
                UncoveredDocuments = documentCoverages.Count(d => !d.IsCovered),
                Suites = suiteCoverages,
                Documents = documentCoverages,
                Gaps = gaps.OrderByDescending(g => g.Severity).ToList()
            };

            // Output report
            if (outputPath is not null)
            {
                var writer = new ReportWriter();
                await writer.WriteAsync(outputPath, report, format, ct);

                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine($"Report written to: {outputPath}");
                }
            }
            else
            {
                // Print to console
                var writer = new ReportWriter();
                var content = format switch
                {
                    ReportFormat.Json => writer.FormatAsJson(report),
                    ReportFormat.Markdown => writer.FormatAsMarkdown(report),
                    _ => writer.FormatAsText(report)
                };
                Console.WriteLine(content);
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine($"Coverage: {report.CoveragePercentage:F1}%");
            Console.WriteLine($"  {report.CoveredDocuments}/{report.TotalDocuments} documents covered");
            Console.WriteLine($"  {report.TotalTests} tests across {suiteCoverages.Count} suite(s)");

            if (gaps.Count > 0)
            {
                Console.WriteLine($"  {gaps.Count} coverage gap(s) identified");
            }

            // Automation coverage analysis
            if (analyzeAutomationCoverage)
            {
                Console.WriteLine();
                await AnalyzeAutomationCoverageAsync(basePath, config, testsDir, format, outputPath, ct);
            }

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private static GapSeverity DetermineSeverity(DocumentEntry doc)
    {
        // Use file size and heading count as heuristics
        if (doc.SizeKb > 10 || doc.Headings.Count > 5)
        {
            return GapSeverity.High;
        }

        if (doc.SizeKb > 5 || doc.Headings.Count > 2)
        {
            return GapSeverity.Medium;
        }

        return GapSeverity.Low;
    }

    /// <summary>
    /// Analyzes automation coverage using bidirectional link reconciliation.
    /// </summary>
    private async Task AnalyzeAutomationCoverageAsync(
        string basePath,
        SpectraConfig config,
        string testsDir,
        ReportFormat format,
        string? outputPath,
        CancellationToken ct)
    {
        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine("Analyzing automation coverage...");
        }

        // Load suite indexes
        var suiteIndexes = new Dictionary<string, MetadataIndex>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(testsDir))
        {
            foreach (var suiteDir in Directory.GetDirectories(testsDir))
            {
                var indexPath = Path.Combine(suiteDir, "_index.json");
                if (File.Exists(indexPath))
                {
                    var indexJson = await File.ReadAllTextAsync(indexPath, ct);
                    var index = JsonSerializer.Deserialize<MetadataIndex>(indexJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (index is not null)
                    {
                        var suiteName = Path.GetFileName(suiteDir);
                        suiteIndexes[suiteName] = index;

                        if (_verbosity >= VerbosityLevel.Detailed)
                        {
                            Console.WriteLine($"  Loaded index for suite: {suiteName} ({index.Tests.Count} tests)");
                        }
                    }
                }
            }
        }

        if (suiteIndexes.Count == 0)
        {
            Console.WriteLine("  No test suite indexes found. Run 'spectra index' first.");
            return;
        }

        // Scan automation files
        var scanner = new AutomationScanner(basePath);
        var automationFiles = await scanner.ScanAsync(ct);

        if (_verbosity >= VerbosityLevel.Detailed)
        {
            Console.WriteLine($"  Scanned {automationFiles.Count} automation files");
        }

        // Reconcile links
        var reconciler = new LinkReconciler();
        var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

        // Calculate coverage
        var calculator = new CoverageCalculator();
        var coverageReport = calculator.Calculate(suiteIndexes, reconciliation);

        // Output results
        OutputAutomationCoverageReport(coverageReport, format, outputPath);
    }

    /// <summary>
    /// Outputs the automation coverage report.
    /// </summary>
    private void OutputAutomationCoverageReport(
        CoverageModels.CoverageReport report,
        ReportFormat format,
        string? outputPath)
    {
        Console.WriteLine("Automation Coverage Analysis");
        Console.WriteLine("============================");
        Console.WriteLine();
        Console.WriteLine($"Total Tests: {report.Summary.TotalTests}");
        Console.WriteLine($"Automated: {report.Summary.Automated}");
        Console.WriteLine($"Manual Only: {report.Summary.ManualOnly}");
        Console.WriteLine($"Coverage: {report.Summary.CoveragePercentage:F1}%");
        Console.WriteLine();

        // Coverage by suite
        if (report.BySuite.Count > 0)
        {
            Console.WriteLine("Coverage by Suite:");
            foreach (var suite in report.BySuite)
            {
                Console.WriteLine($"  {suite.Suite}: {suite.CoveragePercentage:F1}% ({suite.Automated}/{suite.Total})");
            }
            Console.WriteLine();
        }

        // Coverage by component
        if (report.ByComponent.Count > 0)
        {
            Console.WriteLine("Coverage by Component:");
            foreach (var component in report.ByComponent)
            {
                Console.WriteLine($"  {component.Component}: {component.CoveragePercentage:F1}% ({component.Automated}/{component.Total})");
            }
            Console.WriteLine();
        }

        // Issues
        if (report.UnlinkedTests.Count > 0)
        {
            Console.WriteLine($"Unlinked Tests ({report.UnlinkedTests.Count}):");
            foreach (var test in report.UnlinkedTests.Take(10))
            {
                Console.WriteLine($"  {test.TestId}: {test.Title}");
            }
            if (report.UnlinkedTests.Count > 10)
            {
                Console.WriteLine($"  ... and {report.UnlinkedTests.Count - 10} more");
            }
            Console.WriteLine();
        }

        if (report.OrphanedAutomation.Count > 0)
        {
            Console.WriteLine($"Orphaned Automation ({report.OrphanedAutomation.Count}):");
            foreach (var orphan in report.OrphanedAutomation.Take(10))
            {
                Console.WriteLine($"  {orphan.File}: references {string.Join(", ", orphan.ReferencedIds)}");
            }
            if (report.OrphanedAutomation.Count > 10)
            {
                Console.WriteLine($"  ... and {report.OrphanedAutomation.Count - 10} more");
            }
            Console.WriteLine();
        }

        if (report.BrokenLinks.Count > 0)
        {
            Console.WriteLine($"Broken Links ({report.BrokenLinks.Count}):");
            foreach (var broken in report.BrokenLinks.Take(10))
            {
                Console.WriteLine($"  {broken.TestId}: {broken.AutomatedBy} ({broken.Reason})");
            }
            if (report.BrokenLinks.Count > 10)
            {
                Console.WriteLine($"  ... and {report.BrokenLinks.Count - 10} more");
            }
            Console.WriteLine();
        }

        if (report.Mismatches.Count > 0)
        {
            Console.WriteLine($"Link Mismatches ({report.Mismatches.Count}):");
            foreach (var mismatch in report.Mismatches.Take(10))
            {
                Console.WriteLine($"  {mismatch.TestId}: {mismatch.Issue}");
            }
            if (report.Mismatches.Count > 10)
            {
                Console.WriteLine($"  ... and {report.Mismatches.Count - 10} more");
            }
        }

        // Write JSON report if output specified and format is JSON
        if (outputPath is not null && format == ReportFormat.Json)
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(outputPath.Replace(".txt", "-automation.json"), json);
        }
    }
}
