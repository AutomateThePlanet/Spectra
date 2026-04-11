using System.Diagnostics;
using System.Text.Json;
using Spectra.CLI.Agent;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Classification;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Interactive;
using Spectra.CLI.IO;
using Spectra.CLI.Output;
using Spectra.CLI.Review;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Update;

namespace Spectra.CLI.Commands.Update;

/// <summary>
/// Handles the update command execution with direct and interactive modes.
/// </summary>
public sealed class UpdateHandler
{
    private static readonly Progress.ProgressManager _progressManager =
        new("update", Progress.ProgressPhases.Update, title: "Test Update");

    // Spec 041: in-flight progress snapshot for the current update run.
    // Mutated by the proposal apply loop and serialized into .spectra-result.json
    // via _progressManager.Update(updateResult).
    private static Progress.ProgressSnapshot? _currentProgress;

    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly bool _noReview;
    private readonly bool _noInteraction;
    private readonly OutputFormat _outputFormat;
    private readonly ProgressReporter _progress;
    private readonly ClassificationPresenter _classificationPresenter;
    // Spec 043: per-run error counters surfaced in run summary.
    private readonly Spectra.CLI.Services.RunErrorTracker _errorTracker = new();

    public UpdateHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        bool noReview = false,
        bool noInteraction = false,
        OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _noReview = noReview;
        _noInteraction = noInteraction;
        _outputFormat = outputFormat;
        _progress = new ProgressReporter(outputFormat: outputFormat, verbosity: verbosity);
        _classificationPresenter = new ClassificationPresenter();
    }

    /// <summary>
    /// Executes the update command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? suite,
        bool showDiff = false,
        bool deleteOrphaned = false,
        CancellationToken ct = default)
    {
        _progressManager.Reset();

        // Detect mode: direct if suite provided, interactive otherwise
        var isNonInteractive = _noInteraction ||
            Console.IsInputRedirected ||
            !Console.IsOutputRedirected;

        var isDirectMode = !string.IsNullOrEmpty(suite);

        // Validate: --no-interaction requires --suite
        if (isNonInteractive && !isDirectMode)
        {
            _progress.Error("--suite is required when using --no-interaction");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: spectra ai update <suite> [--no-interaction]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Run 'spectra ai update --help' for more information.");
            return ExitCodes.Error;
        }

        if (isDirectMode)
        {
            return await ExecuteDirectModeAsync(suite!, showDiff, deleteOrphaned, ct);
        }
        else
        {
            return await ExecuteInteractiveModeAsync(showDiff, deleteOrphaned, ct);
        }
    }

    private async Task<int> ExecuteDirectModeAsync(
        string suite,
        bool showDiff,
        bool deleteOrphaned,
        CancellationToken ct)
    {
        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(basePath, "spectra.config.json");

            // Load configuration
            var config = await LoadConfigAsync(configPath, ct);
            if (config is null)
            {
                return ExitCodes.Error;
            }

            // Determine suite path
            var testsDir = Path.Combine(basePath, config.Tests.Dir);
            var suitePath = Path.Combine(testsDir, suite);

            if (!Directory.Exists(suitePath))
            {
                _progress.Error($"Suite not found: {suite}");
                return ExitCodes.Error;
            }

            return await ProcessSuiteAsync(basePath, config, suite, suitePath, showDiff, deleteOrphaned, ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            // Spec 043: capture handler-level failure to error log.
            _errorTracker.Record(ex);
            Spectra.CLI.Infrastructure.ErrorLogger.Write("update", $"suite={suite}", ex);
            _progress.Error(ex.Message);
            return ExitCodes.Error;
        }
    }

    private async Task<int> ExecuteInteractiveModeAsync(
        bool showDiff,
        bool deleteOrphaned,
        CancellationToken ct)
    {
        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(basePath, "spectra.config.json");

            // Load configuration
            var config = await LoadConfigAsync(configPath, ct);
            if (config is null)
            {
                return ExitCodes.Error;
            }

            // Scan for suites
            var testsDir = Path.Combine(basePath, config.Tests.Dir);
            var scanner = new SuiteScanner();
            var suites = await scanner.ScanSuitesAsync(testsDir, ct);

            if (suites.Count == 0)
            {
                _progress.Error("No test suites found.");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Run 'spectra ai generate' first to create tests.");
                return ExitCodes.Error;
            }

            // Select suite
            var suiteSelector = new SuiteSelector();
            var selectedSuite = suiteSelector.SelectForUpdate(suites);

            if (selectedSuite is null)
            {
                // Update all suites
                foreach (var suiteInfo in suites)
                {
                    _progress.Info($"Processing suite: {suiteInfo.Name}");
                    var result = await ProcessSuiteAsync(
                        basePath, config, suiteInfo.Name, suiteInfo.Path,
                        showDiff, deleteOrphaned, ct);

                    if (result != ExitCodes.Success)
                    {
                        return result;
                    }
                }

                return ExitCodes.Success;
            }

            return await ProcessSuiteAsync(
                basePath, config, selectedSuite.Name, selectedSuite.Path,
                showDiff, deleteOrphaned, ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            _progress.Error(ex.Message);
            return ExitCodes.Error;
        }
    }

    private async Task<int> ProcessSuiteAsync(
        string basePath,
        SpectraConfig config,
        string suite,
        string suitePath,
        bool showDiff,
        bool deleteOrphaned,
        CancellationToken ct)
    {
        // Spec 040: configure the shared debug logger from config.debug.enabled
        // with a --verbosity diagnostic override. (Update currently makes no
        // AI calls, so this only matters if DebugLogger.Append is called from
        // other components invoked downstream.)
        Spectra.CLI.Infrastructure.DebugLogger.Enabled =
            config.Debug.Enabled || _verbosity == VerbosityLevel.Diagnostic;
        Spectra.CLI.Infrastructure.DebugLogger.LogFile = config.Debug.LogFile;
        Spectra.CLI.Infrastructure.DebugLogger.Mode = config.Debug.Mode;
        Spectra.CLI.Infrastructure.DebugLogger.BeginRun();

        // Spec 043: error log mirrors debug log enable/mode but writes to a
        // separate file (created lazily on first error).
        Spectra.CLI.Infrastructure.ErrorLogger.Enabled =
            config.Debug.Enabled || _verbosity == VerbosityLevel.Diagnostic;
        Spectra.CLI.Infrastructure.ErrorLogger.LogFile = config.Debug.ErrorLogFile;
        Spectra.CLI.Infrastructure.ErrorLogger.Mode = config.Debug.Mode;
        Spectra.CLI.Infrastructure.ErrorLogger.BeginRun();

        var sw = Stopwatch.StartNew();
        _progressManager.Update("classifying", $"Analyzing changes in {suite} suite...");

        // Read existing tests
        var batchReader = new BatchReadTestsTool();
        var readResult = await _progress.StatusAsync(
            $"Loading {suite} suite...",
            async () => await batchReader.ExecuteAsync(suitePath, ct: ct));

        if (!readResult.Success || readResult.Tests is null)
        {
            _progress.Error($"Could not read tests: {readResult.Error}");
            return ExitCodes.Error;
        }

        if (readResult.Tests.Count == 0)
        {
            _progress.Info("No tests found in suite.");
            return ExitCodes.Success;
        }

        _progress.Success($"Loading {suite} suite... {readResult.Tests.Count} tests");

        // Load source documents
        var sourceDir = Path.Combine(basePath, config.Source.LocalDir ?? "docs");
        var sourceContents = await _progress.StatusAsync(
            "Loading documentation...",
            async () => await LoadSourceDocumentsAsync(sourceDir, readResult.Tests, ct));

        _progress.Success($"Loading documentation... {sourceContents.Count} files");

        // Load acceptance criteria for the suite
        IReadOnlyList<AcceptanceCriterion>? suiteCriteria = null;
        try
        {
            var criteriaDir = Path.Combine(basePath, config.Coverage?.CriteriaDir ?? "docs/criteria");
            var criteriaFile = Path.Combine(basePath, config.Coverage?.CriteriaFile ?? "docs/criteria/_criteria_index.yaml");
            suiteCriteria = await GroundedPromptBuilder.LoadRelatedCriteriaAsync(
                criteriaDir, criteriaFile, suite, ct);
        }
        catch
        {
            // Criteria loading is best-effort; continue without criteria
        }

        // Classify tests using the classifier directly for better presentation
        var classifier = new TestClassifier();
        var classificationResults = await _progress.StatusAsync(
            "Classifying tests...",
            () => Task.FromResult(classifier.ClassifyBatch(readResult.Tests, sourceContents, suiteCriteria)));

        // Show classification summary
        _classificationPresenter.ShowSummary(classificationResults);

        // Show detailed results by classification type
        _classificationPresenter.ShowOutdated(classificationResults);
        _classificationPresenter.ShowOrphaned(classificationResults);
        _classificationPresenter.ShowRedundant(classificationResults);

        // Check for unlinked acceptance criteria and show suggestion
        if (suiteCriteria is not null && suiteCriteria.Count > 0 && _outputFormat == OutputFormat.Human)
        {
            var linkedCriteriaIds = readResult.Tests
                .SelectMany(t => t.Criteria)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unlinkedCriteria = suiteCriteria
                .Where(c => !linkedCriteriaIds.Contains(c.Id))
                .ToList();

            if (unlinkedCriteria.Count > 0)
            {
                _progress.Warning(
                    $"{unlinkedCriteria.Count} acceptance criteria for '{suite}' have no linked tests: " +
                    string.Join(", ", unlinkedCriteria.Select(c => c.Id)));
                _progress.Info(
                    $"Run 'spectra ai generate {suite}' to generate tests covering these criteria.");
            }
        }

        // Convert to UpdateProposals for the reviewer
        var proposeTool = new BatchProposeUpdatesTool(classifier);
        var proposeResult = proposeTool.Execute(readResult.Tests, sourceContents);

        if (!proposeResult.Success || proposeResult.Proposals is null)
        {
            _progress.Error($"Could not analyze tests: {proposeResult.Error}");
            return ExitCodes.Error;
        }

        var proposals = ConvertToProposals(readResult.Tests, proposeResult.Proposals);

        // Show diff if requested
        if (showDiff)
        {
            var diffPresenter = new DiffPresenter();
            diffPresenter.ShowSummary(proposals);

            foreach (var proposal in proposals.Where(p => p.Classification != UpdateClassification.UpToDate))
            {
                diffPresenter.ShowProposal(proposal);
            }

            if (_dryRun)
            {
                _progress.Info("Dry run - no changes were made.");
                return ExitCodes.Success;
            }
        }

        // Review or auto-approve
        UpdateReviewResult reviewResult;

        if (_noReview || _noInteraction)
        {
            var reviewer = new UpdateReviewer();
            reviewResult = reviewer.AutoReview(
                proposals,
                applyOutdated: true,
                deleteOrphaned: deleteOrphaned,
                deleteRedundant: false);
        }
        else
        {
            var reviewer = new UpdateReviewer();
            reviewResult = reviewer.Review(proposals);
        }

        if (!reviewResult.Completed)
        {
            _progress.Info("Update cancelled.");
            return ExitCodes.Cancelled;
        }

        // Apply changes
        _progressManager.Update("updating", $"Applying changes to {suite}...");
        var orphanedCount = 0;
        if (!_dryRun)
        {
            // Spec 041: per-proposal progress bar. Total counts every proposal
            // that ApplyChangesAsync will touch (updates + orphaned marks +
            // deletes). The snapshot mirrors the bar so the progress page
            // can render it.
            var orphanedToMark = reviewResult.Skipped.Count(p => p.Classification == UpdateClassification.Orphaned);
            var totalProposals = reviewResult.ToUpdate.Count + orphanedToMark + reviewResult.ToDelete.Count;

            _currentProgress = new Progress.ProgressSnapshot
            {
                Phase = Progress.ProgressPhase.Updating,
                TestsTarget = totalProposals,
                TotalBatches = totalProposals,
                CurrentBatch = 0,
                TestsGenerated = 0,
                TestsVerified = 0
            };

            await _progress.ProgressAsync("Updating tests", Math.Max(1, totalProposals), async increment =>
            {
                orphanedCount = await ApplyChangesAsync(suitePath, reviewResult, ct,
                    onProposalApplied: testId =>
                    {
                        increment(1);
                        if (_currentProgress is not null)
                        {
                            _currentProgress.CurrentBatch++;
                            _currentProgress.LastTestId = testId;
                            WriteUpdateProgress(suite, _currentProgress);
                        }
                    });
            });
        }
        else
        {
            _progress.Info("Dry run - no changes were made.");
        }

        // Update index with redundant test info
        if (!_dryRun && (reviewResult.ToUpdate.Count > 0 || reviewResult.ToDelete.Count > 0 || orphanedCount > 0))
        {
            // Re-read all tests to rebuild index
            var rereadResult = await batchReader.ExecuteAsync(suitePath, ct: ct);
            if (rereadResult.Success && rereadResult.Tests is not null)
            {
                var indexGenerator = new IndexGenerator();
                var index = indexGenerator.Generate(suite, rereadResult.Tests);

                // Add redundant test info to index entries
                var redundantTests = classificationResults
                    .Where(r => r.Classification == UpdateClassification.Redundant)
                    .ToDictionary(r => r.Test.Id);

                var updatedEntries = index.Tests.Select(entry =>
                {
                    if (redundantTests.TryGetValue(entry.Id, out var result))
                    {
                        return new TestIndexEntry
                        {
                            Id = entry.Id,
                            File = entry.File,
                            Title = entry.Title,
                            Description = entry.Description,
                            Priority = entry.Priority,
                            Tags = entry.Tags,
                            Component = entry.Component,
                            EstimatedDuration = entry.EstimatedDuration,
                            DependsOn = entry.DependsOn,
                            SourceRefs = entry.SourceRefs,
                            AutomatedBy = entry.AutomatedBy,
                            Requirements = entry.Requirements,
                            RedundantOf = result.RelatedTestId,
                            RedundantReason = $"{result.Confidence:P0} content similarity"
                        };
                    }
                    return entry;
                }).ToList();

                var updatedIndex = new MetadataIndex
                {
                    Suite = index.Suite,
                    GeneratedAt = index.GeneratedAt,
                    Tests = updatedEntries
                };

                var indexWriter = new IndexWriter();
                await indexWriter.WriteAsync(Path.Combine(suitePath, "_index.json"), updatedIndex, ct);
            }
        }

        // Summary
        _classificationPresenter.ShowUpdateComplete(
            reviewResult.ToUpdate.Count,
            orphanedCount,
            reviewResult.ToDelete.Count);

        sw.Stop();

        var flaggedProposals = proposals
            .Where(p => p.Classification is UpdateClassification.Orphaned or UpdateClassification.Redundant)
            .ToList();

        // Spec 040: build RunSummary for the terminal panel and .spectra-result.json.
        var runSummary = new Results.RunSummary
        {
            TestsScanned = proposals.Count,
            TestsUpdated = reviewResult.ToUpdate.Count,
            TestsUnchanged = proposals.Count(p => p.Classification == UpdateClassification.UpToDate),
            Classifications = new Dictionary<string, int>
            {
                ["up_to_date"] = proposals.Count(p => p.Classification == UpdateClassification.UpToDate),
                ["outdated"] = proposals.Count(p => p.Classification == UpdateClassification.Outdated),
                ["orphaned"] = proposals.Count(p => p.Classification == UpdateClassification.Orphaned),
                ["redundant"] = proposals.Count(p => p.Classification == UpdateClassification.Redundant)
            },
            DurationSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2)
        };

        var updateResult = new Results.UpdateResult
        {
            Command = "update",
            Status = "completed",
            Success = true,
            Suite = suite,
            TotalTests = proposals.Count,
            TestsUpdated = reviewResult.ToUpdate.Count,
            TestsRemoved = orphanedCount + reviewResult.ToDelete.Count,
            TestsUnchanged = proposals.Count(p => p.Classification == UpdateClassification.UpToDate),
            Classification = new Results.UpdateClassificationCounts
            {
                UpToDate = proposals.Count(p => p.Classification == UpdateClassification.UpToDate),
                Outdated = proposals.Count(p => p.Classification == UpdateClassification.Outdated),
                Orphaned = proposals.Count(p => p.Classification == UpdateClassification.Orphaned),
                Redundant = proposals.Count(p => p.Classification == UpdateClassification.Redundant)
            },
            TestsFlagged = flaggedProposals.Count,
            FlaggedTests = flaggedProposals.Count > 0
                ? flaggedProposals.Select(p => new Results.FlaggedTestEntry
                {
                    Id = p.OriginalTest.Id,
                    Title = p.OriginalTest.Title,
                    Classification = p.Classification.ToString().ToUpperInvariant(),
                    Reason = p.Reason
                }).ToList()
                : null,
            Duration = sw.Elapsed.ToString(@"hh\:mm\:ss"),
            RunSummary = runSummary,
            // TokenUsage intentionally null — update flow currently performs no
            // AI calls (classification is purely local heuristics in TestClassifier).
            TokenUsage = null
        };
        _progressManager.Complete(updateResult);

        // Spec 040 follow-up: emit RUN TOTAL line to .spectra-debug.log. The
        // update flow has no AI calls today, so the tracker is empty and the
        // line renders `calls=0 tokens_in=0 tokens_out=0 phases=`. Still
        // useful as a run-boundary marker when grepping the log.
        var emptyTracker = new Spectra.CLI.Services.TokenUsageTracker();
        Spectra.CLI.Infrastructure.DebugLogger.Append(
            "summary ",
            Spectra.CLI.Services.RunSummaryDebugFormatter.FormatRunTotal(
                "update", suite, emptyTracker, sw.Elapsed, _errorTracker));

        // Spec 040: render Run Summary panel unless JSON mode.
        if (_outputFormat != OutputFormat.Json)
        {
            Spectra.CLI.Output.RunSummaryPresenter.Render(
                runSummary, tokenUsage: null, _verbosity,
                errorTracker: _errorTracker);
        }

        return ExitCodes.Success;
    }

    private static async Task<Dictionary<string, string>> LoadSourceDocumentsAsync(
        string sourceDir,
        IReadOnlyList<TestCase> tests,
        CancellationToken ct)
    {
        var contents = new Dictionary<string, string>();

        if (!Directory.Exists(sourceDir))
        {
            return contents;
        }

        // Collect all source refs
        var sourceRefs = tests
            .SelectMany(t => t.SourceRefs)
            .Distinct()
            .ToList();

        foreach (var sourceRef in sourceRefs)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(sourceDir, sourceRef);
            if (File.Exists(fullPath))
            {
                contents[sourceRef] = await File.ReadAllTextAsync(fullPath, ct);
            }
        }

        // Also load all markdown files in the source directory
        foreach (var file in Directory.GetFiles(sourceDir, "*.md", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
            if (!contents.ContainsKey(relativePath))
            {
                contents[relativePath] = await File.ReadAllTextAsync(file, ct);
            }
        }

        return contents;
    }

    private static List<UpdateProposal> ConvertToProposals(
        IReadOnlyList<TestCase> tests,
        IReadOnlyList<UpdateProposalItem> items)
    {
        var proposals = new List<UpdateProposal>();
        var testDict = tests.ToDictionary(t => t.Id);

        foreach (var item in items)
        {
            if (!testDict.TryGetValue(item.TestId, out var test))
            {
                continue;
            }

            var classification = Enum.Parse<UpdateClassification>(item.Classification);

            proposals.Add(new UpdateProposal
            {
                OriginalTest = test,
                Classification = classification,
                Reason = item.Reason,
                Confidence = item.Confidence
            });
        }

        return proposals;
    }

    private static async Task<int> ApplyChangesAsync(
        string suitePath,
        UpdateReviewResult result,
        CancellationToken ct,
        Action<string>? onProposalApplied = null)
    {
        var writer = new TestFileWriter();
        var orphanedCount = 0;

        // Apply updates
        foreach (var proposal in result.ToUpdate)
        {
            if (proposal.ProposedTest is not null)
            {
                var path = Path.Combine(suitePath, $"{proposal.OriginalTest.Id}.md");
                await writer.WriteAsync(path, proposal.ProposedTest, ct);
                Console.WriteLine($"  Updated: {proposal.OriginalTest.Id}");
            }
            onProposalApplied?.Invoke(proposal.OriginalTest.Id);
        }

        // Mark orphaned tests (in skipped list) with status
        foreach (var proposal in result.Skipped.Where(p => p.Classification == UpdateClassification.Orphaned))
        {
            var originalTest = proposal.OriginalTest;
            var orphanedTest = new TestCase
            {
                Id = originalTest.Id,
                FilePath = originalTest.FilePath,
                Priority = originalTest.Priority,
                Tags = originalTest.Tags,
                Component = originalTest.Component,
                Preconditions = originalTest.Preconditions,
                Environment = originalTest.Environment,
                EstimatedDuration = originalTest.EstimatedDuration,
                DependsOn = originalTest.DependsOn,
                SourceRefs = originalTest.SourceRefs,
                RelatedWorkItems = originalTest.RelatedWorkItems,
                Custom = originalTest.Custom,
                Title = originalTest.Title,
                Steps = originalTest.Steps,
                ExpectedResult = originalTest.ExpectedResult,
                TestData = originalTest.TestData,
                Status = "orphaned",
                OrphanedReason = proposal.Reason ?? "Source documentation no longer exists",
                OrphanedDate = DateTimeOffset.UtcNow
            };

            var path = Path.Combine(suitePath, $"{originalTest.Id}.md");
            await writer.WriteAsync(path, orphanedTest, ct);
            Console.WriteLine($"  Marked orphaned: {originalTest.Id}");
            orphanedCount++;
            onProposalApplied?.Invoke(originalTest.Id);
        }

        // Delete tests
        foreach (var proposal in result.ToDelete)
        {
            var path = Path.Combine(suitePath, $"{proposal.OriginalTest.Id}.md");
            if (File.Exists(path))
            {
                File.Delete(path);
                Console.WriteLine($"  Deleted: {proposal.OriginalTest.Id}");
            }
            onProposalApplied?.Invoke(proposal.OriginalTest.Id);
        }

        return orphanedCount;
    }

    /// <summary>
    /// Spec 041: writes an in-flight update result with the current progress
    /// snapshot attached, so the progress page picks it up on next refresh.
    /// </summary>
    private static void WriteUpdateProgress(string suite, Progress.ProgressSnapshot snapshot)
    {
        var partial = new Results.UpdateResult
        {
            Command = "update",
            Status = "updating",
            Success = false,
            Suite = suite,
            TotalTests = snapshot.TestsTarget,
            TestsUpdated = snapshot.CurrentBatch,
            Progress = snapshot
        };
        _progressManager.Update(partial);
    }

    private async Task<SpectraConfig?> LoadConfigAsync(string configPath, CancellationToken ct)
    {
        if (!File.Exists(configPath))
        {
            _progress.Error("No spectra.config.json found. Run 'spectra init' first.");
            return null;
        }

        try
        {
            var configJson = await File.ReadAllTextAsync(configPath, ct);
            return JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _progress.Error($"Error reading config: {ex.Message}");
            return null;
        }
    }
}
