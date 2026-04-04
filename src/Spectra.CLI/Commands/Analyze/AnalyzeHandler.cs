using System.Text.Json;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Coverage;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.IO;
using Spectra.CLI.Source;
using Spectra.Core.Coverage;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;
using LegacyModels = Spectra.Core.Models;

namespace Spectra.CLI.Commands.Analyze;

/// <summary>
/// Handles the analyze command execution with unified three-section coverage.
/// </summary>
public sealed class AnalyzeHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public AnalyzeHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    /// <summary>
    /// Executes the analyze command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? outputPath,
        ReportFormat format,
        bool analyzeAutomationCoverage = false,
        bool autoLink = false,
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

            if (Directory.Exists(testsDir))
            {
                foreach (var suiteDir in Directory.GetDirectories(testsDir))
                {
                    var batchReader = new BatchReadTestsTool();
                    var readResult = await batchReader.ExecuteAsync(suiteDir, ct: ct);

                    if (readResult.Success && readResult.Tests is not null)
                    {
                        allTests.AddRange(readResult.Tests);
                    }
                }
            }

            if (!analyzeAutomationCoverage && !autoLink)
            {
                // Legacy doc-only coverage mode
                return await RunLegacyCoverageAsync(
                    documentMap, allTests, outputPath, format, ct);
            }

            // === Unified three-section coverage analysis ===

            // 1. Documentation coverage
            var docAnalyzer = new DocumentationCoverageAnalyzer();
            var docCoverage = docAnalyzer.Analyze(documentMap, allTests);

            // 2. Requirements coverage
            var reqFilePath = Path.Combine(basePath, config.Coverage.RequirementsFile);
            var reqAnalyzer = new RequirementsCoverageAnalyzer();
            var reqCoverage = await reqAnalyzer.AnalyzeAsync(reqFilePath, allTests, ct);

            // 3. Automation coverage
            var suiteIndexes = await LoadSuiteIndexesAsync(testsDir, ct);
            AutomationCoverage autoCoverage;

            if (suiteIndexes.Count > 0)
            {
                var scanner = AutomationScanner.FromConfig(basePath, config.Coverage);
                var automationFiles = await scanner.ScanAsync(ct);

                if (_verbosity >= VerbosityLevel.Detailed)
                {
                    Console.WriteLine($"  Scanned {automationFiles.Count} automation files");
                }

                // Auto-link before coverage calculation so the report
                // and file state are consistent
                if (autoLink)
                {
                    await RunAutoLinkAsync(
                        basePath, testsDir, suiteIndexes, automationFiles, ct);
                }

                var reconciler = new LinkReconciler();
                var reconciliation = reconciler.Reconcile(suiteIndexes, automationFiles);

                var calculator = new CoverageCalculator();
                var calcReport = calculator.Calculate(suiteIndexes, reconciliation);

                autoCoverage = UnifiedCoverageBuilder.FromCalculatorReport(calcReport);
            }
            else
            {
                autoCoverage = new AutomationCoverage
                {
                    TotalTests = allTests.Count,
                    Automated = 0,
                    Percentage = 0m
                };
            }

            // Build unified report
            var builder = new UnifiedCoverageBuilder();
            var unifiedReport = builder.Build(docCoverage, reqCoverage, autoCoverage);

            // Output
            var reportWriter = new CoverageReportWriter();

            if (outputPath is not null)
            {
                var coverageFormat = format switch
                {
                    ReportFormat.Json => CoverageReportFormat.Json,
                    ReportFormat.Markdown => CoverageReportFormat.Markdown,
                    _ => CoverageReportFormat.Json
                };

                var extension = coverageFormat == CoverageReportFormat.Markdown ? ".md" : ".json";
                var finalPath = Path.HasExtension(outputPath) ? outputPath : outputPath + extension;

                await reportWriter.WriteAsync(finalPath, unifiedReport, coverageFormat, ct);

                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine($"Report written to: {finalPath}");
                }
            }
            else
            {
                var content = format switch
                {
                    ReportFormat.Json => reportWriter.FormatAsJson(unifiedReport),
                    ReportFormat.Markdown => reportWriter.FormatAsMarkdown(unifiedReport),
                    _ => reportWriter.FormatAsText(unifiedReport)
                };
                Console.WriteLine(content);
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine($"Documentation Coverage: {docCoverage.Percentage:F1}% ({docCoverage.CoveredDocs}/{docCoverage.TotalDocs} documents)");
            Console.WriteLine($"Requirements Coverage:  {reqCoverage.Percentage:F1}% ({reqCoverage.CoveredRequirements}/{reqCoverage.TotalRequirements} requirements)");
            Console.WriteLine($"Automation Coverage:    {autoCoverage.Percentage:F1}% ({autoCoverage.Automated}/{autoCoverage.TotalTests} tests)");

            var hasGaps = docCoverage.Percentage < 100 || reqCoverage.Percentage < 100 || autoCoverage.Percentage < 100;
            NextStepHints.Print("analyze", true, _verbosity, new HintContext { HasAutoLink = autoLink, HasGaps = hasGaps }, _outputFormat);
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

    /// <summary>
    /// Extracts testable requirements from documentation using AI.
    /// </summary>
    public async Task<int> RunExtractRequirementsAsync(
        bool dryRun,
        CancellationToken ct = default)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(currentDir, "spectra.config.json");

            // Load config
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

            // Auto-refresh document index
            var indexService = new DocumentIndexService();
            await indexService.EnsureIndexAsync(currentDir, config.Source, forceRebuild: false, ct);

            // Load source documents
            var docBuilder = new DocumentMapBuilder();
            var documentMap = await docBuilder.BuildAsync(currentDir, ct);

            if (documentMap.Documents.Count == 0)
            {
                Console.WriteLine("No documentation files found. Add docs to your source directory.");
                return ExitCodes.Success;
            }

            // Load existing requirements
            var reqsPath = Path.Combine(currentDir, config.Coverage.RequirementsFile);
            var parser = new Spectra.Core.Parsing.RequirementsParser();
            var existing = await parser.ParseAsync(reqsPath, ct);

            // Get primary provider
            var provider = config.Ai.Providers.FirstOrDefault(p => p.Enabled);
            if (provider is null)
            {
                Console.Error.WriteLine("No AI provider configured. Run 'spectra init' to configure.");
                return ExitCodes.Error;
            }

            // Extract requirements
            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine($"Extracting requirements from {documentMap.Documents.Count} document(s)...");
            }

            var extractor = new Agent.Copilot.RequirementsExtractor(
                provider,
                currentDir,
                _verbosity >= VerbosityLevel.Normal ? Console.WriteLine : null);

            var extracted = await extractor.ExtractAsync(documentMap.Documents, existing, ct);

            if (extracted.Count == 0)
            {
                Console.WriteLine("No requirements found in documentation.");
                return ExitCodes.Success;
            }

            // Merge and write
            var writer = new Spectra.Core.Parsing.RequirementsWriter();
            var result = writer.DetectDuplicates(existing, extracted);

            if (dryRun)
            {
                Console.WriteLine();
                Console.WriteLine($"Extracted {extracted.Count} requirement(s) (dry run — no files written):");
                Console.WriteLine();

                var withIds = writer.AllocateIds(existing, result.Merged);
                foreach (var req in withIds)
                {
                    Console.WriteLine($"  {req.Id}  [{req.Priority}]  {req.Title}");
                    Console.WriteLine($"          Source: {req.Source}");
                }

                if (result.Duplicates.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {result.Duplicates.Count} duplicate(s) would be skipped");
                }

                return ExitCodes.Success;
            }

            var writeResult = await writer.MergeAndWriteAsync(reqsPath, extracted, ct);

            // Report results
            Console.WriteLine();
            Console.WriteLine($"Requirements extraction complete:");
            Console.WriteLine($"  New:        {writeResult.Merged.Count}");
            Console.WriteLine($"  Duplicates: {writeResult.SkippedCount} (skipped)");
            Console.WriteLine($"  Total:      {writeResult.TotalInFile}");
            Console.WriteLine($"  File:       {Path.GetRelativePath(currentDir, reqsPath)}");

            if (writeResult.Merged.Count > 0 && _verbosity >= VerbosityLevel.Normal)
            {
                Console.WriteLine();
                foreach (var req in writeResult.Merged)
                {
                    Console.WriteLine($"  + {req.Id}  [{req.Priority}]  {req.Title}");
                }
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
            if (_verbosity >= VerbosityLevel.Detailed)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return ExitCodes.Error;
        }
    }

    /// <summary>
    /// Legacy doc-only coverage mode (when --coverage is not specified).
    /// </summary>
    private async Task<int> RunLegacyCoverageAsync(
        DocumentMap documentMap,
        List<TestCase> allTests,
        string? outputPath,
        ReportFormat format,
        CancellationToken ct)
    {
        var suiteCoverages = new List<LegacyModels.SuiteCoverage>();
        var documentCoverages = new List<LegacyModels.DocumentCoverage>();
        var gaps = new List<LegacyModels.CoverageGap>();

        // Build suite coverages from allTests
        var bySuite = allTests.GroupBy(t =>
        {
            var parts = t.FilePath.Split('/', '\\');
            return parts.Length > 1 ? parts[0] : "default";
        });

        foreach (var group in bySuite)
        {
            var coveredDocs = group
                .SelectMany(t => t.SourceRefs)
                .Distinct()
                .ToList();

            suiteCoverages.Add(new LegacyModels.SuiteCoverage
            {
                Name = group.Key,
                TestCount = group.Count(),
                DocumentsCovered = coveredDocs.Count,
                CoveredDocuments = coveredDocs
            });
        }

        foreach (var doc in documentMap.Documents)
        {
            var testsForDoc = allTests
                .Where(t => t.SourceRefs.Contains(doc.Path))
                .ToList();

            var isCovered = testsForDoc.Count > 0;

            documentCoverages.Add(new LegacyModels.DocumentCoverage
            {
                Path = doc.Path,
                IsCovered = isCovered,
                TestCount = testsForDoc.Count,
                TestIds = testsForDoc.Select(t => t.Id).ToList()
            });

            if (!isCovered)
            {
                gaps.Add(new LegacyModels.CoverageGap
                {
                    DocumentPath = doc.Path,
                    Reason = "No tests reference this document",
                    Severity = DetermineSeverity(doc),
                    Suggestion = $"Create tests for {doc.Path}"
                });
            }
        }

        var report = new LegacyModels.CoverageReport
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
            var writer = new ReportWriter();
            var content = format switch
            {
                ReportFormat.Json => writer.FormatAsJson(report),
                ReportFormat.Markdown => writer.FormatAsMarkdown(report),
                _ => writer.FormatAsText(report)
            };
            Console.WriteLine(content);
        }

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

    private static GapSeverity DetermineSeverity(DocumentEntry doc)
    {
        if (doc.SizeKb > 10 || doc.Headings.Count > 5)
            return GapSeverity.High;

        if (doc.SizeKb > 5 || doc.Headings.Count > 2)
            return GapSeverity.Medium;

        return GapSeverity.Low;
    }

    private static async Task<Dictionary<string, MetadataIndex>> LoadSuiteIndexesAsync(
        string testsDir,
        CancellationToken ct)
    {
        var suiteIndexes = new Dictionary<string, MetadataIndex>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(testsDir))
        {
            return suiteIndexes;
        }

        foreach (var suiteDir in Directory.GetDirectories(testsDir))
        {
            var indexPath = Path.Combine(suiteDir, "_index.json");
            if (!File.Exists(indexPath))
            {
                continue;
            }

            var indexJson = await File.ReadAllTextAsync(indexPath, ct);
            var index = JsonSerializer.Deserialize<MetadataIndex>(indexJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (index is not null)
            {
                var suiteName = Path.GetFileName(suiteDir);
                suiteIndexes[suiteName] = index;
            }
        }

        return suiteIndexes;
    }

    private async Task RunAutoLinkAsync(
        string basePath,
        string testsDir,
        Dictionary<string, MetadataIndex> suiteIndexes,
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles,
        CancellationToken ct)
    {
        // Build test file map: testId → (suite, filePath)
        var testFileMap = new Dictionary<string, (string Suite, string FilePath)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (suite, index) in suiteIndexes)
        {
            foreach (var test in index.Tests)
            {
                var absolutePath = Path.Combine(testsDir, test.File);
                testFileMap[test.Id] = (suite, absolutePath);
            }
        }

        var autoLinker = new AutoLinkService();
        var links = autoLinker.GenerateLinks(automationFiles, testFileMap);

        if (links.Count == 0)
        {
            Console.WriteLine("  No auto-link matches found.");
            return;
        }

        // Group by test file so we can batch updates
        var byTestFile = links
            .GroupBy(l => l.TestFilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(l => l.AutomationFilePath).Distinct().ToList());

        var updater = new FrontmatterUpdater();
        var updatedCount = 0;

        foreach (var (testFile, autoPaths) in byTestFile)
        {
            // Convert to relative paths
            var relativePaths = autoPaths
                .Select(p => Path.GetRelativePath(basePath, Path.Combine(basePath, p))
                    .Replace('\\', '/'))
                .ToList();

            if (await updater.UpdateFileAsync(testFile, relativePaths, ct))
            {
                updatedCount++;
            }
        }

        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine($"  Auto-linked {links.Count} tests to automation code ({updatedCount} files updated)");
        }
    }
}
