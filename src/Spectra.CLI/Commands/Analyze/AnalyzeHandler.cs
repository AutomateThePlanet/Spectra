using System.Text.Json;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Coverage;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.IO;
using Spectra.CLI.Results;
using Spectra.CLI.Source;
using Spectra.Core.Coverage;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;
using Spectre.Console;
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

    private static Progress.ProgressManager CreateCoverageProgress() =>
        new("analyze-coverage", Progress.ProgressPhases.Coverage, title: "Coverage Analysis");

    private static Progress.ProgressManager CreateExtractProgress() =>
        new("extract-criteria", Progress.ProgressPhases.ExtractCriteria, title: "Criteria Extraction");

    private static Progress.ProgressManager CreateResultOnlyProgress(string command) =>
        new(command, [], title: command);

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
        var progress = CreateCoverageProgress();
        progress.Reset();
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
                progress.Fail("No spectra.config.json found");
                return ExitCodes.Error;
            }

            // Migrate criteria folder from docs/requirements/ to docs/criteria/ if needed
            await MigrateCriteriaFolderAsync(basePath, _verbosity);

            progress.Update("scanning-tests", "Scanning test suites...");

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

            progress.Update("analyzing-docs", $"Matching {allTests.Count} tests to documentation...");

            // 1. Documentation coverage
            var docAnalyzer = new DocumentationCoverageAnalyzer();
            var docCoverage = docAnalyzer.Analyze(documentMap, allTests);

            progress.Update("analyzing-criteria", "Matching tests to acceptance criteria...");

            // 2. Acceptance criteria coverage
            var criteriaFilePath = Path.Combine(basePath, config.Coverage.CriteriaFile);
            var criteriaAnalyzer = new AcceptanceCriteriaCoverageAnalyzer();
            var criteriaCoverage = await criteriaAnalyzer.AnalyzeAsync(criteriaFilePath, allTests, ct);

            progress.Update("analyzing-automation", "Scanning automation directories...");

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
            var unifiedReport = builder.Build(docCoverage, criteriaCoverage, autoCoverage);

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
            Console.WriteLine($"Documentation Coverage:        {docCoverage.Percentage:F1}% ({docCoverage.CoveredDocs}/{docCoverage.TotalDocs} documents)");
            Console.WriteLine($"Acceptance Criteria Coverage:  {criteriaCoverage.Percentage:F1}% ({criteriaCoverage.CoveredCriteria}/{criteriaCoverage.TotalCriteria} acceptance criteria)");
            Console.WriteLine($"Automation Coverage:           {autoCoverage.Percentage:F1}% ({autoCoverage.Automated}/{autoCoverage.TotalTests} tests)");

            var coverageResult = new AnalyzeCoverageResult
            {
                Command = "analyze-coverage",
                Status = "completed",
                Documentation = new CoverageSection { Percentage = (double)docCoverage.Percentage, Covered = docCoverage.CoveredDocs, Total = docCoverage.TotalDocs },
                AcceptanceCriteria = new CoverageSection { Percentage = (double)criteriaCoverage.Percentage, Covered = criteriaCoverage.CoveredCriteria, Total = criteriaCoverage.TotalCriteria },
                Automation = new AutomationSection { Percentage = (double)autoCoverage.Percentage, Linked = autoCoverage.Automated, Total = autoCoverage.TotalTests }
            };
            progress.Complete(coverageResult);

            var hasGaps = docCoverage.Percentage < 100 || criteriaCoverage.Percentage < 100 || autoCoverage.Percentage < 100;
            NextStepHints.Print("analyze", true, _verbosity, new HintContext { HasAutoLink = autoLink, HasGaps = hasGaps }, _outputFormat);
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            progress.Fail("Operation cancelled");
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            progress.Fail(ex.Message);
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    /// <summary>
    /// Extracts testable acceptance criteria from documentation using AI, per-document.
    /// Uses incremental extraction: skips documents whose hash hasn't changed.
    /// </summary>
    public async Task<int> RunExtractCriteriaAsync(
        bool dryRun,
        bool force = false,
        CancellationToken ct = default)
    {
        var extractProgress = CreateExtractProgress();
        extractProgress.Reset();
        extractProgress.Start("Initializing criteria extraction...");
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
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "extract-criteria",
                        Status = "failed",
                        Error = "No spectra.config.json found. Run 'spectra init' first."
                    });
                }
                else
                {
                    Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
                }
                return ExitCodes.Error;
            }

            // Migrate criteria folder from docs/requirements/ to docs/criteria/ if needed
            await MigrateCriteriaFolderAsync(currentDir, _verbosity);

            // Resolve criteria directory and index file paths
            var criteriaDir = Path.Combine(currentDir, config.Coverage.CriteriaDir);
            var criteriaIndexPath = Path.Combine(currentDir, config.Coverage.CriteriaFile);

            if (!dryRun)
                Directory.CreateDirectory(criteriaDir);

            // Auto-refresh document index
            var indexService = new DocumentIndexService();
            await indexService.EnsureIndexAsync(currentDir, config.Source, forceRebuild: false, ct);

            // Build document map
            var docBuilder = new DocumentMapBuilder(config.Source);
            var documentMap = await docBuilder.BuildAsync(currentDir, ct);

            if (documentMap.Documents.Count == 0)
            {
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ExtractCriteriaResult
                    {
                        Command = "extract-criteria",
                        Status = "success",
                        Message = "No documentation files found.",
                        CriteriaExtracted = 0,
                        CriteriaNew = 0,
                        CriteriaUpdated = 0,
                        CriteriaUnchanged = 0,
                        OrphanedCriteria = 0,
                        TotalCriteria = 0,
                        IndexFile = Path.GetRelativePath(currentDir, criteriaIndexPath)
                    });
                }
                else
                {
                    Console.WriteLine("No documentation files found. Add docs to your source directory.");
                }
                return ExitCodes.Success;
            }

            // Read existing criteria index
            var indexReader = new CriteriaIndexReader();
            var criteriaIndex = await indexReader.ReadAsync(criteriaIndexPath, ct);

            // Get primary AI provider
            var provider = config.Ai.Providers.FirstOrDefault(p => p.Enabled);
            if (provider is null)
            {
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "extract-criteria",
                        Status = "failed",
                        Error = "No AI provider configured. Run 'spectra init' to configure."
                    });
                }
                else
                {
                    Console.Error.WriteLine("No AI provider configured. Run 'spectra init' to configure.");
                }
                return ExitCodes.Error;
            }

            extractProgress.Update("extracting", $"Extracting acceptance criteria from {documentMap.Documents.Count} document(s)...");

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.Error.WriteLine($"Extracting acceptance criteria from {documentMap.Documents.Count} document(s)...");
                if (force) Console.Error.WriteLine("  --force: re-extracting all documents.");
            }

            var extractor = new Agent.Copilot.CriteriaExtractor(
                provider,
                _verbosity >= VerbosityLevel.Normal ? s => Console.Error.WriteLine(s) : null);

            var fileWriter = new CriteriaFileWriter();
            var fileReader = new CriteriaFileReader();

            // Counters
            var documentsProcessed = 0;
            var documentsSkipped = 0;
            var documentsFailed = 0;
            var failedDocuments = new List<string>();
            var criteriaExtracted = 0;
            var criteriaNew = 0;
            var criteriaUpdated = 0;
            var criteriaUnchanged = 0;
            var allNewCriteria = new List<CriterionEntry>();

            // Track which source docs we've seen (for orphan detection)
            var processedSourceDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var doc in documentMap.Documents)
            {
                // Skip metadata and criteria files — they are not source documentation
                var docFileName = Path.GetFileName(doc.Path);
                if (ShouldSkipDocument(docFileName))
                {
                    documentsSkipped++;
                    continue;
                }

                var docFullPath = Path.Combine(currentDir, doc.Path);
                processedSourceDocs.Add(doc.Path);

                // Compute hash
                string docHash;
                try
                {
                    docHash = await FileHasher.ComputeFileHashAsync(docFullPath, ct);
                }
                catch
                {
                    documentsFailed++;
                    failedDocuments.Add(doc.Path);
                    continue;
                }

                // Find matching source in existing index
                var existingSource = criteriaIndex.Sources
                    .FirstOrDefault(s => string.Equals(s.SourceDoc, doc.Path, StringComparison.OrdinalIgnoreCase));

                // Skip if hash matches and not forced
                if (!force && existingSource is not null && existingSource.DocHash == docHash)
                {
                    documentsSkipped++;
                    criteriaUnchanged += existingSource.CriteriaCount;
                    continue;
                }

                // Read document content
                string content;
                try
                {
                    content = await File.ReadAllTextAsync(docFullPath, ct);
                }
                catch
                {
                    documentsFailed++;
                    failedDocuments.Add(doc.Path);
                    continue;
                }

                // Derive component from document filename
                var component = Path.GetFileNameWithoutExtension(doc.Path)
                    .Replace(' ', '-')
                    .ToLowerInvariant();

                // Extract criteria via AI
                IReadOnlyList<AcceptanceCriterion> extracted;
                try
                {
                    var extractTask = extractor.ExtractFromDocumentAsync(doc.Path, content, component, ct);
                    var deadlineTask = Task.Delay(TimeSpan.FromMinutes(2), ct);
                    var completed = await Task.WhenAny(extractTask, deadlineTask);

                    if (completed == deadlineTask)
                    {
                        if (_verbosity >= VerbosityLevel.Normal)
                            Console.Error.WriteLine($"  Timeout extracting from {doc.Path}, skipping.");
                        documentsFailed++;
                        failedDocuments.Add(doc.Path);
                        continue;
                    }

                    extracted = await extractTask;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Spec 043: full exception context to error log.
                    Spectra.CLI.Infrastructure.ErrorLogger.Write(
                        "criteria", $"doc={doc.Path}", ex);
                    if (_verbosity >= VerbosityLevel.Normal)
                        Console.Error.WriteLine($"  Failed to extract from {doc.Path}: {ex.Message}");
                    documentsFailed++;
                    failedDocuments.Add(doc.Path);
                    continue;
                }

                criteriaExtracted += extracted.Count;

                // Read existing per-doc criteria file for ID comparison
                var docBaseName = Path.GetFileNameWithoutExtension(doc.Path);
                var criteriaFileName = $"{docBaseName}.criteria.yaml";
                var criteriaFilePath = Path.Combine(criteriaDir, criteriaFileName);

                var existingCriteria = await fileReader.ReadAsync(criteriaFilePath, ct);
                var existingIdMap = existingCriteria
                    .Where(c => !string.IsNullOrEmpty(c.Id))
                    .ToDictionary(c => c.Text, c => c.Id, StringComparer.OrdinalIgnoreCase);

                // Determine next available ID number for this component
                var componentPrefix = $"AC-{component.ToUpperInvariant()}-";
                var maxId = existingCriteria
                    .Where(c => c.Id.StartsWith(componentPrefix, StringComparison.OrdinalIgnoreCase))
                    .Select(c =>
                    {
                        var numPart = c.Id[componentPrefix.Length..];
                        return int.TryParse(numPart, out var n) ? n : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();

                var nextId = maxId + 1;

                // Assign IDs to extracted criteria
                var isNewDoc = existingSource is null;
                var docNewCount = 0;
                var docUpdatedCount = 0;

                foreach (var criterion in extracted)
                {
                    if (existingIdMap.TryGetValue(criterion.Text, out var existingId))
                    {
                        criterion.Id = existingId;
                        docUpdatedCount++;
                    }
                    else
                    {
                        criterion.Id = $"{componentPrefix}{nextId:D3}";
                        nextId++;
                        docNewCount++;
                    }

                    criterion.SourceDoc = doc.Path;
                    criterion.SourceType = "document";
                }

                criteriaNew += docNewCount;
                criteriaUpdated += docUpdatedCount;
                documentsProcessed++;

                if (!dryRun)
                {
                    // Write per-doc criteria file
                    await fileWriter.WriteAsync(criteriaFilePath, extracted, doc.Path, docHash, ct);

                    // Update or add source in index
                    if (existingSource is not null)
                    {
                        existingSource.DocHash = docHash;
                        existingSource.CriteriaCount = extracted.Count;
                        existingSource.LastExtracted = DateTime.UtcNow;
                        existingSource.File = criteriaFileName;
                    }
                    else
                    {
                        criteriaIndex.Sources.Add(new CriteriaSource
                        {
                            File = criteriaFileName,
                            SourceDoc = doc.Path,
                            SourceType = "document",
                            DocHash = docHash,
                            CriteriaCount = extracted.Count,
                            LastExtracted = DateTime.UtcNow
                        });
                    }
                }

                // Collect entries for result reporting
                foreach (var c in extracted)
                {
                    allNewCriteria.Add(new CriterionEntry
                    {
                        Id = c.Id,
                        Text = c.Text,
                        Rfc2119 = c.Rfc2119,
                        Source = c.SourceDoc,
                        SourceType = c.SourceType,
                        Component = c.Component,
                        Priority = c.Priority
                    });
                }

                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.Error.WriteLine($"  {doc.Path}: {extracted.Count} criteria ({docNewCount} new, {docUpdatedCount} updated)");
                }
            }

            // Detect orphaned criteria: sources in index whose doc no longer exists
            var orphanedSources = criteriaIndex.Sources
                .Where(s => s.SourceType == "document"
                    && s.SourceDoc is not null
                    && !processedSourceDocs.Contains(s.SourceDoc))
                .ToList();

            var orphanedCount = orphanedSources.Sum(s => s.CriteriaCount);

            if (!dryRun)
            {
                // Remove orphaned sources from index
                foreach (var orphan in orphanedSources)
                {
                    criteriaIndex.Sources.Remove(orphan);
                }

                // Write updated index
                extractProgress.Update("building-index", "Rebuilding criteria index...");
                var indexWriter = new CriteriaIndexWriter();
                await indexWriter.WriteAsync(criteriaIndexPath, criteriaIndex, ct);
            }

            // Calculate totals
            var totalCriteria = criteriaIndex.Sources.Sum(s => s.CriteriaCount);
            if (dryRun)
            {
                // Estimate: unchanged + newly extracted - orphaned
                totalCriteria = criteriaUnchanged + criteriaExtracted;
            }

            // Report results
            var extractResult = new ExtractCriteriaResult
            {
                Command = "extract-criteria",
                Status = documentsFailed == documentMap.Documents.Count ? "failed"
                       : documentsFailed > 0 ? "partial"
                       : "completed",
                Message = dryRun ? "Dry run — no files written." : null,
                DocumentsProcessed = documentsProcessed,
                DocumentsSkipped = documentsSkipped,
                DocumentsFailed = documentsFailed,
                FailedDocuments = failedDocuments.Count > 0 ? failedDocuments : null,
                CriteriaExtracted = criteriaExtracted,
                CriteriaNew = criteriaNew,
                CriteriaUpdated = criteriaUpdated,
                CriteriaUnchanged = criteriaUnchanged,
                OrphanedCriteria = orphanedCount,
                TotalCriteria = totalCriteria,
                IndexFile = Path.GetRelativePath(currentDir, criteriaIndexPath),
                Criteria = allNewCriteria.Count > 0 ? allNewCriteria : null
            };
            extractProgress.Complete(extractResult);

            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(extractResult);
            }
            else
            {
                Console.WriteLine();
                if (dryRun) Console.WriteLine("Dry run — no files written.");
                Console.WriteLine($"Acceptance criteria extraction complete:");
                Console.WriteLine($"  Documents processed: {documentsProcessed}");
                Console.WriteLine($"  Documents skipped:   {documentsSkipped} (unchanged)");
                if (documentsFailed > 0)
                    Console.WriteLine($"  Documents failed:    {documentsFailed}");
                Console.WriteLine($"  Criteria extracted:  {criteriaExtracted}");
                Console.WriteLine($"  New criteria:        {criteriaNew}");
                Console.WriteLine($"  Updated criteria:    {criteriaUpdated}");
                Console.WriteLine($"  Unchanged criteria:  {criteriaUnchanged}");
                if (orphanedCount > 0)
                    Console.WriteLine($"  Orphaned criteria:   {orphanedCount} (removed)");
                Console.WriteLine($"  Total criteria:      {totalCriteria}");
                Console.WriteLine($"  Index file:          {Path.GetRelativePath(currentDir, criteriaIndexPath)}");

                if (allNewCriteria.Count > 0 && _verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine();
                    foreach (var c in allNewCriteria)
                    {
                        Console.WriteLine($"  {(criteriaNew > 0 ? "+" : " ")} {c.Id}  [{c.Priority}]  {c.Text}");
                    }
                }

                if (failedDocuments.Count > 0)
                {
                    Console.WriteLine();
                    Console.Error.WriteLine("Failed documents:");
                    foreach (var f in failedDocuments)
                        Console.Error.WriteLine($"  - {f}");
                }
            }

            // Exit codes: 0 all success, 2 some failed, 1 all failed
            if (documentsFailed == documentMap.Documents.Count && documentMap.Documents.Count > 0)
                return ExitCodes.Error;
            if (documentsFailed > 0)
                return ExitCodes.ValidationError;
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            extractProgress.Fail("Operation cancelled.");
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            extractProgress.Fail(ex.Message);
            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ErrorResult
                {
                    Command = "extract-criteria",
                    Status = "failed",
                    Error = ex.Message
                });
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (_verbosity >= VerbosityLevel.Detailed)
                    Console.Error.WriteLine(ex.StackTrace);
            }
            return ExitCodes.Error;
        }
    }

    /// <summary>
    /// Imports acceptance criteria from an external file (YAML, CSV, or JSON).
    /// Supports merge/replace modes and optional AI-powered splitting of compound criteria.
    /// </summary>
    public async Task<int> RunImportCriteriaAsync(
        string importPath,
        bool replace,
        bool skipSplitting,
        bool dryRun,
        CancellationToken ct = default)
    {
        var importProgress = CreateResultOnlyProgress("import-criteria");
        importProgress.Reset();
        try
        {
            var currentDir = Directory.GetCurrentDirectory();

            // 1. Validate the import file exists
            var fullImportPath = Path.IsPathRooted(importPath)
                ? importPath
                : Path.Combine(currentDir, importPath);

            if (!File.Exists(fullImportPath))
            {
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "import-criteria",
                        Status = "failed",
                        Error = $"Import file not found: {importPath}"
                    });
                }
                else
                {
                    Console.Error.WriteLine($"Error: Import file not found: {importPath}");
                }
                return ExitCodes.Error;
            }

            // Load config
            var configPath = Path.Combine(currentDir, "spectra.config.json");
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
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "import-criteria",
                        Status = "failed",
                        Error = "No spectra.config.json found. Run 'spectra init' first."
                    });
                }
                else
                {
                    Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
                }
                return ExitCodes.Error;
            }

            // Migrate criteria folder from docs/requirements/ to docs/criteria/ if needed
            await MigrateCriteriaFolderAsync(currentDir, _verbosity);

            var criteriaDir = Path.Combine(currentDir, config.Coverage.CriteriaDir);
            var criteriaIndexPath = Path.Combine(currentDir, config.Coverage.CriteriaFile);

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.Error.WriteLine($"Importing criteria from {importPath}...");
            }

            // 2. Detect format by extension
            var ext = Path.GetExtension(fullImportPath).ToLowerInvariant();

            // 3. Parse via the appropriate importer
            IReadOnlyList<AcceptanceCriterion> imported;
            switch (ext)
            {
                case ".yaml" or ".yml":
                    var yamlReader = new CriteriaFileReader();
                    imported = await yamlReader.ReadAsync(fullImportPath, ct);
                    break;
                case ".csv":
                    var csvImporter = new CsvCriteriaImporter();
                    imported = await csvImporter.ImportAsync(fullImportPath, "import", ct);
                    break;
                case ".json":
                    var jsonImporter = new JsonCriteriaImporter();
                    imported = await jsonImporter.ImportAsync(fullImportPath, "import", ct);
                    break;
                default:
                    if (_outputFormat == OutputFormat.Json)
                    {
                        JsonResultWriter.Write(new ErrorResult
                        {
                            Command = "import-criteria",
                            Status = "failed",
                            Error = $"Unsupported file format: {ext}. Expected .yaml, .yml, .csv, or .json."
                        });
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: Unsupported file format: {ext}. Expected .yaml, .yml, .csv, or .json.");
                    }
                    return ExitCodes.Error;
            }

            var importedCount = imported.Count;

            if (_verbosity >= VerbosityLevel.Normal)
            {
                Console.Error.WriteLine($"  Parsed {importedCount} criteria from {Path.GetFileName(fullImportPath)}.");
            }

            // 4. Split compound criteria if needed
            var splitCount = 0;
            var normalizedCount = 0;

            if (!skipSplitting)
            {
                var criteriaToSplit = imported
                    .Where(c => c.Text.Contains("\n- ") || c.Text.Contains("\n* ") ||
                                c.Text.StartsWith("- ") || c.Text.StartsWith("* "))
                    .ToList();

                if (criteriaToSplit.Count > 0)
                {
                    var provider = config.Ai.Providers.FirstOrDefault(p => p.Enabled);
                    if (provider is not null)
                    {
                        var extractor = new CriteriaExtractor(
                            provider,
                            _verbosity >= VerbosityLevel.Normal ? s => Console.Error.WriteLine(s) : null);

                        var expandedList = new List<AcceptanceCriterion>();

                        foreach (var criterion in imported)
                        {
                            if (criteriaToSplit.Contains(criterion))
                            {
                                try
                                {
                                    var splitResults = await extractor.SplitAndNormalizeAsync(
                                        criterion.Text,
                                        criterion.Source,
                                        criterion.Component,
                                        ct);

                                    if (splitResults.Count > 0)
                                    {
                                        // Preserve source metadata on split results
                                        foreach (var split in splitResults)
                                        {
                                            split.Source = criterion.Source;
                                            split.SourceType = criterion.SourceType;
                                            split.SourceDoc = criterion.SourceDoc;
                                        }
                                        expandedList.AddRange(splitResults);
                                        splitCount += splitResults.Count;
                                        normalizedCount++;
                                    }
                                    else
                                    {
                                        expandedList.Add(criterion);
                                    }
                                }
                                catch (Exception ex) when (ex is not OperationCanceledException)
                                {
                                    Spectra.CLI.Infrastructure.ErrorLogger.Write(
                                        "criteria", $"split criterion={criterion.Id}", ex);
                                    if (_verbosity >= VerbosityLevel.Normal)
                                        Console.Error.WriteLine($"  Failed to split criterion: {ex.Message}");
                                    expandedList.Add(criterion);
                                }
                            }
                            else
                            {
                                expandedList.Add(criterion);
                            }
                        }

                        imported = expandedList;

                        if (_verbosity >= VerbosityLevel.Normal && normalizedCount > 0)
                        {
                            Console.Error.WriteLine($"  Split {normalizedCount} compound criteria into {splitCount} atomic criteria.");
                        }
                    }
                }
            }

            // 5. Assign IDs to criteria that don't have them
            // Build a set of all existing IDs to avoid collisions
            var indexReader = new CriteriaIndexReader();
            var criteriaIndex = await indexReader.ReadAsync(criteriaIndexPath, ct);

            var globalMaxId = 0;
            var sourceMaxIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Scan existing criteria files for used IDs
            foreach (var source in criteriaIndex.Sources)
            {
                var sourceFilePath = Path.Combine(criteriaDir, source.File);
                if (Path.IsPathRooted(source.File))
                    sourceFilePath = source.File;

                var fileReader = new CriteriaFileReader();
                var existingInFile = await fileReader.ReadAsync(sourceFilePath, ct);
                foreach (var c in existingInFile)
                {
                    if (string.IsNullOrEmpty(c.Id)) continue;

                    // Try parse AC-{KEY}-{N} pattern
                    if (c.Id.StartsWith("AC-", StringComparison.OrdinalIgnoreCase) && c.Id.Length > 3)
                    {
                        var rest = c.Id[3..];
                        var lastDash = rest.LastIndexOf('-');
                        if (lastDash > 0 && int.TryParse(rest[(lastDash + 1)..], out var n))
                        {
                            var key = rest[..lastDash].ToUpperInvariant();
                            if (sourceMaxIds.TryGetValue(key, out var current))
                                sourceMaxIds[key] = Math.Max(current, n);
                            else
                                sourceMaxIds[key] = n;
                        }
                        else if (int.TryParse(rest, out var plainN))
                        {
                            globalMaxId = Math.Max(globalMaxId, plainN);
                        }
                    }
                }
            }

            // Also scan the imported criteria for existing IDs
            foreach (var c in imported)
            {
                if (!string.IsNullOrEmpty(c.Id) && c.Id.StartsWith("AC-", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = c.Id[3..];
                    var lastDash = rest.LastIndexOf('-');
                    if (lastDash > 0 && int.TryParse(rest[(lastDash + 1)..], out var n))
                    {
                        var key = rest[..lastDash].ToUpperInvariant();
                        if (sourceMaxIds.TryGetValue(key, out var current))
                            sourceMaxIds[key] = Math.Max(current, n);
                        else
                            sourceMaxIds[key] = n;
                    }
                    else if (int.TryParse(rest, out var plainN))
                    {
                        globalMaxId = Math.Max(globalMaxId, plainN);
                    }
                }
            }

            // Assign IDs to criteria without them
            foreach (var criterion in imported)
            {
                if (!string.IsNullOrEmpty(criterion.Id))
                    continue;

                if (!string.IsNullOrEmpty(criterion.Source))
                {
                    var sourceKey = criterion.Source
                        .Replace(' ', '-')
                        .ToUpperInvariant();
                    if (!sourceMaxIds.TryGetValue(sourceKey, out var nextN))
                        nextN = 0;
                    nextN++;
                    sourceMaxIds[sourceKey] = nextN;
                    criterion.Id = $"AC-{sourceKey}-{nextN:D3}";
                }
                else
                {
                    globalMaxId++;
                    criterion.Id = $"AC-{globalMaxId:D3}";
                }
            }

            // 6. Determine output file path
            var inputFileName = Path.GetFileNameWithoutExtension(fullImportPath);
            var importedDir = Path.Combine(criteriaDir, "imported");
            var outputFilePath = Path.Combine(importedDir, $"{inputFileName}.criteria.yaml");

            // 7. Merge or replace
            var fileReaderForTarget = new CriteriaFileReader();
            var existingCriteria = await fileReaderForTarget.ReadAsync(outputFilePath, ct);

            var merger = new CriteriaMerger();
            CriteriaMerger.MergeResult mergeResult;

            if (replace)
            {
                mergeResult = merger.Replace(existingCriteria, imported);
            }
            else
            {
                mergeResult = merger.Merge(existingCriteria, imported);
            }

            // 8. Write criteria file
            if (!dryRun)
            {
                Directory.CreateDirectory(importedDir);

                var fileWriter = new CriteriaFileWriter();
                await fileWriter.WriteAsync(outputFilePath, mergeResult.Criteria, importPath, null, ct);
            }

            // 9. Update master CriteriaIndex
            var relativeOutputFile = Path.GetRelativePath(
                Path.Combine(currentDir, config.Coverage.CriteriaDir),
                outputFilePath).Replace('\\', '/');

            // Use the imported dir relative path as the file entry
            var importedRelFile = $"imported/{inputFileName}.criteria.yaml";

            var existingSource = criteriaIndex.Sources
                .FirstOrDefault(s => string.Equals(s.File, importedRelFile, StringComparison.OrdinalIgnoreCase));

            if (!dryRun)
            {
                if (existingSource is not null)
                {
                    existingSource.CriteriaCount = mergeResult.Criteria.Count;
                    existingSource.ImportedAt = DateTime.UtcNow;
                    existingSource.SourceType = "import";
                }
                else
                {
                    criteriaIndex.Sources.Add(new CriteriaSource
                    {
                        File = importedRelFile,
                        SourceDoc = importPath,
                        SourceType = "import",
                        CriteriaCount = mergeResult.Criteria.Count,
                        ImportedAt = DateTime.UtcNow
                    });
                }

                var indexWriter = new CriteriaIndexWriter();
                await indexWriter.WriteAsync(criteriaIndexPath, criteriaIndex, ct);
            }

            // Build source breakdown
            var sourceBreakdown = imported
                .GroupBy(c => c.SourceType ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var totalCriteria = dryRun
                ? criteriaIndex.Sources.Sum(s => s.CriteriaCount) + mergeResult.NewCount
                : criteriaIndex.Sources.Sum(s => s.CriteriaCount);

            var relativeOutputPath = Path.GetRelativePath(currentDir, outputFilePath);

            // 11. Report results
            var importResult = new ImportCriteriaResult
            {
                Command = "import-criteria",
                Status = "completed",
                Message = dryRun ? "Dry run — no files written." : null,
                Imported = importedCount,
                Split = splitCount,
                Normalized = normalizedCount,
                Merged = mergeResult.MergedCount,
                New = mergeResult.NewCount,
                TotalCriteria = totalCriteria,
                File = relativeOutputPath,
                SourceBreakdown = sourceBreakdown.Count > 0 ? sourceBreakdown : null
            };
            importProgress.WriteResultOnly(importResult);

            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(importResult);
            }
            else
            {
                Console.WriteLine();
                if (dryRun) Console.WriteLine("Dry run — no files written.");
                Console.WriteLine($"Criteria import complete:");
                Console.WriteLine($"  Source file:       {importPath}");
                Console.WriteLine($"  Imported:          {importedCount}");
                if (normalizedCount > 0)
                {
                    Console.WriteLine($"  Compound split:    {normalizedCount} → {splitCount} criteria");
                }
                Console.WriteLine($"  Merged (updated):  {mergeResult.MergedCount}");
                Console.WriteLine($"  New:               {mergeResult.NewCount}");
                if (mergeResult.ReplacedCount > 0)
                    Console.WriteLine($"  Replaced:          {mergeResult.ReplacedCount}");
                Console.WriteLine($"  Total criteria:    {totalCriteria}");
                Console.WriteLine($"  Output file:       {relativeOutputPath}");

                if (sourceBreakdown.Count > 1)
                {
                    Console.WriteLine($"  Source breakdown:");
                    foreach (var (sourceType, count) in sourceBreakdown.OrderByDescending(kv => kv.Value))
                    {
                        Console.WriteLine($"    {sourceType}: {count}");
                    }
                }
            }

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            importProgress.Fail("Operation cancelled.");
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            importProgress.Fail(ex.Message);
            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ErrorResult
                {
                    Command = "import-criteria",
                    Status = "failed",
                    Error = ex.Message
                });
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (_verbosity >= VerbosityLevel.Detailed)
                    Console.Error.WriteLine(ex.StackTrace);
            }
            return ExitCodes.Error;
        }
    }

    /// <summary>
    /// Lists all acceptance criteria with optional filters and coverage status.
    /// </summary>
    public async Task<int> RunListCriteriaAsync(
        string? sourceType,
        string? component,
        string? priority,
        CancellationToken ct = default)
    {
        var listProgress = CreateResultOnlyProgress("list-criteria");
        listProgress.Reset();
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
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "list-criteria",
                        Status = "failed",
                        Error = "No spectra.config.json found. Run 'spectra init' first."
                    });
                }
                else
                {
                    Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
                }
                return ExitCodes.Error;
            }

            // Read criteria index
            var criteriaIndexPath = Path.Combine(currentDir, config.Coverage.CriteriaFile);
            var criteriaDir = Path.Combine(currentDir, config.Coverage.CriteriaDir);
            var indexReader = new CriteriaIndexReader();
            var criteriaIndex = await indexReader.ReadAsync(criteriaIndexPath, ct);

            // Read all criteria from all source files
            var fileReader = new CriteriaFileReader();
            var allCriteria = new List<AcceptanceCriterion>();

            foreach (var source in criteriaIndex.Sources)
            {
                var sourceFilePath = Path.IsPathRooted(source.File)
                    ? source.File
                    : Path.Combine(criteriaDir, source.File);

                var criteria = await fileReader.ReadAsync(sourceFilePath, ct);
                allCriteria.AddRange(criteria);
            }

            // Apply filters (case-insensitive)
            IEnumerable<AcceptanceCriterion> filtered = allCriteria;

            if (!string.IsNullOrEmpty(sourceType))
            {
                filtered = filtered.Where(c =>
                    string.Equals(c.SourceType, sourceType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(component))
            {
                filtered = filtered.Where(c =>
                    string.Equals(c.Component, component, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(priority))
            {
                filtered = filtered.Where(c =>
                    string.Equals(c.Priority, priority, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();

            // Load all tests to check coverage
            var testsDir = Path.Combine(currentDir, config.Tests?.Dir ?? "tests");
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

            // Build a map of criterion ID -> list of test IDs that cover it
            var coverageMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var test in allTests)
            {
                // Check both criteria and requirements fields
                var linkedIds = test.Criteria.Concat(test.Requirements);
                foreach (var criterionId in linkedIds)
                {
                    if (!coverageMap.TryGetValue(criterionId, out var testList))
                    {
                        testList = [];
                        coverageMap[criterionId] = testList;
                    }
                    testList.Add(test.Id);
                }
            }

            // Build result entries
            var entries = filteredList.Select(c =>
            {
                coverageMap.TryGetValue(c.Id, out var linkedTests);
                var testIds = linkedTests ?? [];
                // Also include linked_test_ids from the criterion itself
                var allLinkedTests = testIds
                    .Concat(c.LinkedTestIds ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ListCriterionEntry
                {
                    Id = c.Id,
                    Text = c.Text,
                    Rfc2119 = c.Rfc2119,
                    SourceType = c.SourceType,
                    SourceDoc = c.SourceDoc,
                    Component = c.Component,
                    Priority = c.Priority,
                    LinkedTests = allLinkedTests,
                    Covered = allLinkedTests.Count > 0
                };
            }).ToList();

            var coveredCount = entries.Count(e => e.Covered);
            var totalCount = entries.Count;
            var coveragePct = totalCount > 0
                ? Math.Round((decimal)coveredCount / totalCount * 100, 1)
                : 0m;

            var listResult = new ListCriteriaResult
            {
                Command = "list-criteria",
                Status = "completed",
                Criteria = entries,
                Total = totalCount,
                Covered = coveredCount,
                CoveragePct = coveragePct
            };
            listProgress.WriteResultOnly(listResult);

            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(listResult);
            }
            else
            {
                if (entries.Count == 0)
                {
                    Console.WriteLine("No criteria found matching the specified filters.");
                    return ExitCodes.Success;
                }

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("ID")
                    .AddColumn("Text")
                    .AddColumn("Source Type")
                    .AddColumn("Component")
                    .AddColumn("Priority")
                    .AddColumn("Covered");

                foreach (var entry in entries)
                {
                    var truncatedText = entry.Text.Length > 60
                        ? entry.Text[..57] + "..."
                        : entry.Text;
                    var coveredMark = entry.Covered ? "[green]✓[/]" : "[red]✗[/]";
                    table.AddRow(
                        Markup.Escape(entry.Id),
                        Markup.Escape(truncatedText),
                        Markup.Escape(entry.SourceType),
                        Markup.Escape(entry.Component ?? "-"),
                        Markup.Escape(entry.Priority),
                        coveredMark);
                }

                AnsiConsole.Write(table);
                Console.WriteLine();
                Console.WriteLine($"Total: {totalCount}  Covered: {coveredCount}  Coverage: {coveragePct}%");
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
            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ErrorResult
                {
                    Command = "list-criteria",
                    Status = "failed",
                    Error = ex.Message
                });
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (_verbosity >= VerbosityLevel.Detailed)
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

    /// <summary>
    /// Returns true if the document should be skipped during criteria extraction.
    /// Metadata files, criteria files, and index files are not source documentation.
    /// </summary>
    internal static bool ShouldSkipDocument(string fileName)
    {
        // Skip document index files — they are metadata, not source documentation
        if (fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("_index.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("_index.json", StringComparison.OrdinalIgnoreCase))
            return true;

        // Skip criteria files themselves (avoid circular extraction)
        if (fileName.EndsWith(".criteria.yaml", StringComparison.OrdinalIgnoreCase))
            return true;

        // Skip the criteria index
        if (fileName.Equals("_criteria_index.yaml", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Migrates the criteria folder from docs/requirements/ to docs/criteria/ if needed.
    /// Also cleans up _index.criteria.yaml if present.
    /// </summary>
    internal static async Task MigrateCriteriaFolderAsync(string basePath, VerbosityLevel verbosity)
    {
        var oldDir = Path.Combine(basePath, "docs", "requirements");
        var newDir = Path.Combine(basePath, "docs", "criteria");

        if (Directory.Exists(oldDir) && !Directory.Exists(newDir))
        {
            Directory.Move(oldDir, newDir);
            if (verbosity >= VerbosityLevel.Normal)
                Console.Error.WriteLine("Migrated criteria folder: docs/requirements/ → docs/criteria/");

            // Update spectra.config.json if it references old paths
            var configPath = Path.Combine(basePath, "spectra.config.json");
            if (File.Exists(configPath))
            {
                var configText = await File.ReadAllTextAsync(configPath);
                if (configText.Contains("docs/requirements"))
                {
                    configText = configText.Replace("docs/requirements", "docs/criteria");
                    await File.WriteAllTextAsync(configPath, configText);
                    if (verbosity >= VerbosityLevel.Normal)
                        Console.Error.WriteLine("Updated spectra.config.json: docs/requirements → docs/criteria");
                }
            }
        }
        else if (Directory.Exists(oldDir) && Directory.Exists(newDir))
        {
            if (verbosity >= VerbosityLevel.Normal)
                Console.Error.WriteLine("Warning: Both docs/requirements/ and docs/criteria/ exist. Skipping migration.");
        }

        // Clean up _index.criteria.yaml if present (should not exist)
        var indexCriteriaYaml = Path.Combine(newDir, "_index.criteria.yaml");
        if (File.Exists(indexCriteriaYaml))
        {
            File.Delete(indexCriteriaYaml);
            if (verbosity >= VerbosityLevel.Normal)
                Console.Error.WriteLine("Removed _index.criteria.yaml (metadata file should not be extracted)");
        }
    }
}
