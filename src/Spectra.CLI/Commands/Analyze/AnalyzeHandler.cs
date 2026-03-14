using System.Text.Json;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Source;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

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
}
