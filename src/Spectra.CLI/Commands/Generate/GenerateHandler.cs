using System.Text.Json;
using Spectra.CLI.Agent;
using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Agent.Critic;
using Spectra.CLI.Commands.Auth;
using Spectra.CLI.Coverage;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Interactive;
using Spectra.CLI.IO;
using Spectra.CLI.Output;
using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Models.Profile;
using Spectre.Console;
using Spectra.CLI.Results;
using Spectra.CLI.Session;
using Spectra.CLI.Validation;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;
using Spectra.Core.Profile;
using ProfilePriority = Spectra.Core.Models.Profile.Priority;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Handles the generate command execution with direct and interactive modes.
/// </summary>
public sealed class GenerateHandler
{
    private const int BatchSize = 30;

    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly bool _noReview;
    private readonly bool _noInteraction;
    private readonly bool _skipCritic;
    private readonly OutputFormat _outputFormat;
    private readonly ProgressReporter _progress;
    private readonly ResultPresenter _results;
    private readonly GapPresenter _gapPresenter;
    private readonly VerificationPresenter _verification;

    public GenerateHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        bool noReview = false,
        bool noInteraction = false,
        bool skipCritic = false,
        OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _noReview = noReview;
        _noInteraction = noInteraction;
        _skipCritic = skipCritic;
        _outputFormat = outputFormat;
        _progress = new ProgressReporter(outputFormat: outputFormat);
        _results = new ResultPresenter(outputFormat: outputFormat);
        _gapPresenter = new GapPresenter();
        _verification = new VerificationPresenter(outputFormat);
    }

    public async Task<int> ExecuteAsync(
        string? suite,
        int? count,
        string? focus,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(suite, count, focus, null, null, null, false, false, ct);
    }

    public async Task<int> ExecuteAsync(
        string? suite,
        int? count,
        string? focus,
        string? fromSuggestions,
        string? fromDescription,
        string? descContext,
        bool autoComplete,
        bool analyzeOnly = false,
        CancellationToken ct = default)
    {
        // Handle --from-suggestions mode
        if (fromSuggestions is not null)
        {
            if (string.IsNullOrEmpty(suite))
            {
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "generate",
                        Status = "failed",
                        Error = "Missing required arguments: --from-suggestions requires <suite>",
                        MissingArguments = ["suite"]
                    });
                }
                else
                {
                    _progress.Error("--from-suggestions requires <suite>");
                }
                return ExitCodes.MissingArguments;
            }
            return await ExecuteFromSuggestionsAsync(suite, fromSuggestions, ct);
        }

        // Handle --from-description mode
        if (!string.IsNullOrEmpty(fromDescription))
        {
            if (string.IsNullOrEmpty(suite))
            {
                if (_outputFormat == OutputFormat.Json)
                {
                    JsonResultWriter.Write(new ErrorResult
                    {
                        Command = "generate",
                        Status = "failed",
                        Error = "Missing required arguments: --from-description requires <suite>",
                        MissingArguments = ["suite"]
                    });
                }
                else
                {
                    _progress.Error("--from-description requires <suite>");
                }
                return ExitCodes.MissingArguments;
            }
            return await ExecuteFromDescriptionAsync(suite, fromDescription, descContext, ct);
        }

        // Detect mode: direct if suite provided, interactive otherwise
        var isNonInteractive = _noInteraction ||
            Console.IsInputRedirected ||
            Console.IsOutputRedirected;

        var isDirectMode = !string.IsNullOrEmpty(suite);

        // Validate: --no-interaction requires --suite
        if (isNonInteractive && !isDirectMode)
        {
            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ErrorResult
                {
                    Command = "generate",
                    Status = "failed",
                    Error = "Missing required arguments in non-interactive mode",
                    MissingArguments = ["suite"]
                });
                return ExitCodes.MissingArguments;
            }

            _progress.Error("--suite is required when using --no-interaction");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: spectra ai generate <suite> [--focus <description>] [--no-interaction]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Run 'spectra ai generate --help' for more information.");
            return ExitCodes.MissingArguments;
        }

        if (isDirectMode)
        {
            return await ExecuteDirectModeAsync(suite!, count, focus, ct, autoComplete, analyzeOnly);
        }
        else
        {
            return await ExecuteInteractiveModeAsync(count, ct);
        }
    }

    private async Task<int> ExecuteDirectModeAsync(
        string suite,
        int? count,
        string? focus,
        CancellationToken ct,
        bool autoComplete = false,
        bool analyzeOnly = false)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config
        var config = await LoadConfigAsync(configPath, ct);
        if (config is null)
        {
            WriteErrorResultFile("No spectra.config.json found. Run 'spectra init' first.", suite);
            return ExitCodes.Error;
        }

        // Clear stale result/progress files from previous runs
        DeleteResultFile();

        // Mark result file as in-progress so agents know the command is running
        var progressStatus = analyzeOnly ? "analyzing" : "generating";
        WriteInProgressResultFile(suite, progressStatus, "Loading documentation...");

        // Open progress page in browser
        Progress.ProgressPageWriter.OpenInBrowser(_progressManager.ProgressPath);

        // Auto-refresh document index before generation
        var indexService = new DocumentIndexService();
        await indexService.EnsureIndexAsync(currentDir, config.Source, forceRebuild: false, ct);

        // Load source documents with full content for grounded generation
        UpdateProgress(suite, progressStatus, $"Loading {suite} documentation...");
        var documents = await _progress.StatusAsync(
            $"Loading {suite} suite...",
            async () =>
            {
                var docLoader = new SourceDocumentLoader(config.Source);
                return await docLoader.LoadAllAsync(currentDir, ct: ct);
            });

        if (documents.Count == 0)
        {
            _progress.Error("No source documentation found in docs/");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Please add documentation files or check your spectra.config.json.");
            WriteErrorResultFile("No source documentation found in docs/. Add documentation files or check spectra.config.json.", suite);
            return ExitCodes.Error;
        }

        // Also create document map for gap analysis (uses previews)
        var documentMap = await _progress.StatusAsync(
            "Analyzing coverage...",
            async () =>
            {
                var mapBuilder = new DocumentMapBuilder(config.Source);
                return await mapBuilder.BuildAsync(currentDir, ct);
            });

        // Load existing tests
        var testsDir = config.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);

        // Create suite directory if it doesn't exist
        if (!Directory.Exists(suitePath))
        {
            Directory.CreateDirectory(suitePath);
        }

        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        // Scan ALL suites for existing IDs to ensure global uniqueness
        var globalIdScanner = new GlobalIdScanner();
        var allExistingIds = await globalIdScanner.ScanAllIdsAsync(testsPath, ct);

        _progress.Success($"Loading {suite} suite... {existingTests.Count} existing tests");
        _progress.Success($"Scanning documentation... {documents.Count} relevant files");
        UpdateProgress(suite, progressStatus, $"Found {documents.Count} docs, {existingTests.Count} existing tests");

        if (allExistingIds.Count > existingTests.Count)
        {
            _progress.Info($"Global IDs: {allExistingIds.Count} (ensuring uniqueness across all suites)");
        }

        // Load profile
        var profileLoader = new ProfileLoader();
        var effectiveProfile = await profileLoader.LoadAsync(currentDir, suitePath, ct);

        // Smart test count: analyze documentation when --count is not specified
        BehaviorAnalysisResult? analysisResult = null;
        int effectiveCount;

        if (count.HasValue)
        {
            // --count explicitly provided: skip analysis, use as-is
            effectiveCount = count.Value;
        }
        else
        {
            // No --count: perform AI-powered behavior analysis
            UpdateProgress(suite, progressStatus, "Analyzing testable behaviors in documentation...");
            analysisResult = await _progress.StatusAsync(
                $"Analyzing {suite} documentation...",
                async () =>
                {
                    var provider = config.Ai.Providers?.FirstOrDefault(p => p.Enabled);
                    var analyzer = new BehaviorAnalyzer(provider, status =>
                    {
                        _progress.Info($"  {status}");
                        UpdateProgress(suite, progressStatus, status);
                    });
                    return await analyzer.AnalyzeAsync(documents, existingTests, focus, ct);
                });

            if (analysisResult is not null)
            {
                AnalysisPresenter.DisplayBreakdown(analysisResult, _outputFormat);

                if (analysisResult.RecommendedCount == 0)
                {
                    AnalysisPresenter.DisplayAllCovered(analysisResult.TotalBehaviors, _outputFormat);
                    var allCoveredResult = new GenerateResult
                    {
                        Command = "generate",
                        Status = "completed",
                        Suite = suite,
                        Message = "All behaviors already covered by existing tests",
                        Analysis = new GenerateAnalysis
                        {
                            TotalBehaviors = analysisResult.TotalBehaviors,
                            AlreadyCovered = analysisResult.AlreadyCovered,
                            Recommended = 0,
                            Breakdown = analysisResult.Breakdown?.ToDictionary(
                                kvp => kvp.Key.ToString(), kvp => kvp.Value)
                        },
                        Generation = new GenerateGeneration
                        {
                            TestsGenerated = 0,
                            TestsWritten = 0,
                            TestsRejectedByCritic = 0
                        },
                        FilesCreated = []
                    };
                    WriteResultFile(allCoveredResult);
                    if (_outputFormat == OutputFormat.Json)
                    {
                        JsonResultWriter.Write(allCoveredResult);
                    }
                    return ExitCodes.Success;
                }

                effectiveCount = analysisResult.RecommendedCount;
            }
            else
            {
                // Analysis failed — retry once before falling back
                _progress.Warning("Behavior analysis did not return results, retrying...");
                UpdateProgress(suite, progressStatus, "Retrying behavior analysis...");
                analysisResult = await _progress.StatusAsync(
                    $"Retrying {suite} analysis...",
                    async () =>
                    {
                        var provider = config.Ai.Providers?.FirstOrDefault(p => p.Enabled);
                        var analyzer = new BehaviorAnalyzer(provider, status =>
                        {
                            UpdateProgress(suite, progressStatus, status);
                        });
                        return await analyzer.AnalyzeAsync(documents, existingTests, focus, ct);
                    });

                if (analysisResult is not null)
                {
                    AnalysisPresenter.DisplayBreakdown(analysisResult, _outputFormat);
                    effectiveCount = analysisResult.RecommendedCount;
                }
                else
                {
                    // Both attempts failed — fall back to default
                    var defaultCount = config.Generation?.DefaultCount ?? 15;
                    effectiveCount = Math.Max(5, defaultCount - existingTests.Count);
                    _progress.Warning($"Behavior analysis unavailable after retry, using default count of {effectiveCount} (accounting for {existingTests.Count} existing tests)");
                }
            }
        }

        // --analyze-only: stop after analysis, output recommendation
        if (analyzeOnly)
        {
            var analyzeResult = new GenerateResult
            {
                Command = "generate",
                Status = "analyzed",
                Suite = suite,
                Analysis = analysisResult is not null ? new GenerateAnalysis
                {
                    TotalBehaviors = analysisResult.TotalBehaviors,
                    AlreadyCovered = analysisResult.AlreadyCovered,
                    Recommended = analysisResult.RecommendedCount,
                    Breakdown = analysisResult.Breakdown?.ToDictionary(
                        kvp => kvp.Key.ToString(), kvp => kvp.Value)
                } : new GenerateAnalysis
                {
                    TotalBehaviors = 0,
                    AlreadyCovered = existingTests.Count,
                    Recommended = effectiveCount
                },
                Generation = new GenerateGeneration
                {
                    TestsGenerated = 0,
                    TestsWritten = 0,
                    TestsRejectedByCritic = 0
                },
                FilesCreated = []
            };

            WriteResultFile(analyzeResult);

            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(analyzeResult);
            }
            else
            {
                _progress.BlankLine();
                _progress.Info($"Analysis complete for suite '{suite}':");
                _progress.Info($"  Total testable behaviors: {analyzeResult.Analysis!.TotalBehaviors}");
                _progress.Info($"  Already covered: {analyzeResult.Analysis.AlreadyCovered}");
                _progress.Info($"  Recommended new tests: {analyzeResult.Analysis.Recommended}");
                if (analyzeResult.Analysis.Breakdown is not null)
                {
                    foreach (var (category, categoryCount) in analyzeResult.Analysis.Breakdown)
                    {
                        _progress.Info($"    {category}: {categoryCount}");
                    }
                }
                _progress.BlankLine();
                _progress.Info($"To generate, run: spectra ai generate --suite {suite} --count {analyzeResult.Analysis.Recommended}");
            }

            Console.Out.Flush();
            Console.Error.Flush();
            return ExitCodes.Success;
        }

        // Analyze coverage gaps (filtered by suite)
        var gapAnalyzer = new GapAnalyzer();
        var gaps = gapAnalyzer.AnalyzeGaps(documentMap, existingTests, focus, suite);

        // Show existing tests matching focus if specified
        if (!string.IsNullOrEmpty(focus))
        {
            var matchingTests = FilterTestsByFocus(existingTests, focus);
            if (matchingTests.Count > 0)
            {
                _results.ShowExistingTests(matchingTests, focus);
            }

            if (gaps.Count == 0 && !count.HasValue)
            {
                // Only block generation when count was NOT explicitly requested.
                // When --count is provided, the user explicitly wants N tests with this focus —
                // the gap analyzer uses document-level source_refs which can't detect
                // category-level gaps (e.g., "security" tests vs general tests).
                _gapPresenter.ShowAllCovered(focus);
                WriteResultFile(new GenerateResult
                {
                    Command = "generate",
                    Status = "completed",
                    Suite = suite,
                    Message = $"No coverage gaps found for focus: {focus}",
                    Generation = new GenerateGeneration
                    {
                        TestsRequested = effectiveCount,
                        TestsGenerated = 0,
                        TestsWritten = 0,
                        TestsRejectedByCritic = 0
                    },
                    FilesCreated = []
                });
                return ExitCodes.Success;
            }

            if (gaps.Count > 0)
            {
                _gapPresenter.ShowUncoveredAreas(gaps);
            }
        }

        // Check duplicates status message
        await _progress.StatusAsync("Checking for duplicates...", async () =>
        {
            await Task.Delay(100, ct); // Small delay for UX
        });

        // Create agent using Copilot SDK
        UpdateProgress(suite, "generating", "Connecting to AI provider...");
        var createResult = await AgentFactory.CreateAgentAsync(
            config,
            currentDir,
            testsPath,
            status =>
            {
                _progress.Info($"  {status}");
                UpdateProgress(suite, "generating", status);
            },
            ct);

        if (!createResult.Success)
        {
            var authError = $"AI provider '{createResult.ProviderName}' authentication failed. Check your AI provider configuration in spectra.config.json.";
            AuthHandler.WriteAuthError(createResult.ProviderName!, createResult.AuthResult!);
            WriteErrorResultFile(authError, suite);
            return ExitCodes.Error;
        }

        var agent = createResult.Agent!;

        if (!await agent.IsAvailableAsync(ct))
        {
            _progress.Error($"AI provider '{agent.ProviderName}' is not available.");
            WriteErrorResultFile($"AI provider '{agent.ProviderName}' is not available. Check your network connection and API credentials.", suite);
            return ExitCodes.Error;
        }

        // Load criteria context for the suite
        var criteriaContext = await LoadCriteriaContextAsync(currentDir, suite, config, ct);

        // --- Batch generation loop ---
        var generatorModel = agent.ProviderName;
        var writer = new TestFileWriter();
        var allWrittenTests = new List<TestCase>();
        var allFilesCreated = new List<string>();
        var allVerificationResults = new List<(TestCase Test, VerificationResult Result)>();
        var allRejectedTests = new List<RejectedTest>();
        var allVerifiedTests = new List<VerifiedTest>();
        var totalGenerated = 0;
        var totalRejected = 0;
        var mutableExistingTests = new List<TestCase>(existingTests);
        var mutableExistingIds = new HashSet<string>(allExistingIds);
        var batchErrors = new List<string>();
        var batchesCompleted = 0;

        for (var batchNum = 1; totalGenerated < effectiveCount; batchNum++)
        {
            var remaining = effectiveCount - totalGenerated;
            var batchRequestCount = Math.Min(remaining, BatchSize);

            UpdateProgress(suite, "generating",
                $"Generating batch {batchNum}: {allWrittenTests.Count}/{effectiveCount} tests complete...");
            _progress.Info($"Batch {batchNum}: requesting {batchRequestCount} tests ({allWrittenTests.Count}/{effectiveCount} complete)");

            var prompt = BuildPrompt(suite, batchRequestCount, mutableExistingIds, effectiveProfile, focus);
            GenerationResult batchResult = null!;

            await _progress.StatusAsync($"Generating batch {batchNum}...", async () =>
            {
                batchResult = await agent.GenerateTestsAsync(
                    prompt, documents, mutableExistingTests, batchRequestCount, criteriaContext, ct);
            });

            // Handle batch failure — keep tests from prior batches
            if (!batchResult.IsSuccess)
            {
                batchErrors.AddRange(batchResult.Errors);
                _progress.Warning($"Batch {batchNum} failed: {string.Join("; ", batchResult.Errors)}");
                break;
            }

            // Handle empty batch — AI has nothing more to generate
            if (batchResult.Tests.Count == 0)
            {
                _progress.Info($"Batch {batchNum} returned 0 tests — generation complete.");
                break;
            }

            totalGenerated += batchResult.Tests.Count;
            _progress.Success($"Batch {batchNum}: generated {batchResult.Tests.Count} tests");
            _results.ShowGeneratedTests(batchResult.Tests);

            // Dry-run: show first batch only, don't write
            if (_dryRun)
            {
                _progress.BlankLine();
                _progress.Info("Dry run - no files written");
                break;
            }

            // Per-batch verification
            var batchTestsToWrite = batchResult.Tests.ToList();
            var batchVerificationResults = new List<(TestCase Test, VerificationResult Result)>();

            if (ShouldVerify(config.Ai.Critic))
            {
                UpdateProgress(suite, "verifying",
                    $"Verifying batch {batchNum} ({batchResult.Tests.Count} tests)...");

                batchVerificationResults = await VerifyTestsAsync(
                    batchResult.Tests, documentMap, config.Ai.Critic!, generatorModel, ct, suite,
                    onTestVerified: (test, result) =>
                    {
                        allVerifiedTests.Add(BuildVerifiedTest(test, result));
                        WriteVerificationProgress(suite, allVerifiedTests);
                    });

                _verification.ShowSummary(batchVerificationResults);
                allVerificationResults.AddRange(batchVerificationResults);

                batchTestsToWrite = batchVerificationResults
                    .Where(r => r.Result.Verdict != VerificationVerdict.Hallucinated)
                    .Select(r => r.Test)
                    .ToList();

                var batchRejected = batchVerificationResults
                    .Where(r => r.Result.Verdict == VerificationVerdict.Hallucinated);
                totalRejected += batchRejected.Count();
                allRejectedTests.AddRange(batchRejected.Select(r => BuildRejectedTest(r.Test, r.Result)));

                if (batchTestsToWrite.Count == 0)
                {
                    _progress.Warning($"Batch {batchNum}: all tests rejected by critic");
                    continue; // Try another batch
                }
            }
            else if (_skipCritic && batchNum == 1)
            {
                _verification.ShowSkippedNotice();
            }

            // Per-batch write
            UpdateProgress(suite, "generating",
                $"Writing batch {batchNum}: {batchTestsToWrite.Count} test files...");

            foreach (var test in batchTestsToWrite)
            {
                var filePath = TestFileWriter.GetFilePath(testsPath, suite, test.Id);
                var verification = batchVerificationResults.FirstOrDefault(r => r.Test.Id == test.Id);
                var testWithPath = CreateTestWithGrounding(
                    test, verification.Result, generatorModel, filePath, testsPath);
                await writer.WriteAsync(filePath, testWithPath, ct);

                allFilesCreated.Add(Path.GetRelativePath(currentDir, filePath));
                mutableExistingIds.Add(test.Id);
            }

            allWrittenTests.AddRange(batchTestsToWrite);
            mutableExistingTests.AddRange(batchTestsToWrite);
            batchesCompleted++;

            // Per-batch index update
            var allTestsForIndex = new List<TestCase>(existingTests);
            allTestsForIndex.AddRange(allWrittenTests);
            var indexGenerator = new IndexGenerator();
            var batchIndex = indexGenerator.Generate(suite, allTestsForIndex);
            var indexWriter = new IndexWriter();
            await indexWriter.WriteAsync(Path.Combine(suitePath, "_index.json"), batchIndex, ct);

            _progress.Success($"Progress: {allWrittenTests.Count}/{effectiveCount} tests written to disk");
        }
        // --- End batch loop ---

        if (_dryRun)
        {
            return ExitCodes.Success;
        }

        // Handle total failure (no tests from any batch)
        if (allWrittenTests.Count == 0 && batchErrors.Count > 0)
        {
            var errorMsg = string.Join("; ", batchErrors);
            WriteErrorResultFile(errorMsg, suite);
            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ErrorResult
                {
                    Command = "generate",
                    Status = "failed",
                    Error = errorMsg
                });
            }
            return ExitCodes.Error;
        }

        if (allWrittenTests.Count == 0)
        {
            _progress.Warning($"No tests generated (requested: {effectiveCount})");
            _progress.BlankLine();
            ShowCountMismatchReason(0, effectiveCount, documentMap.Documents.Count, existingTests.Count, focus);

            // Write completed result so progress page stops spinning
            WriteResultFile(new GenerateResult
            {
                Command = "generate",
                Status = "completed",
                Suite = suite,
                Message = $"No tests generated (requested: {effectiveCount})",
                Generation = new GenerateGeneration
                {
                    TestsRequested = effectiveCount,
                    TestsGenerated = 0,
                    TestsWritten = 0,
                    TestsRejectedByCritic = 0
                },
                FilesCreated = []
            });
            return ExitCodes.Success;
        }

        _progress.BlankLine();
        _results.ShowCompletion($"tests/{suite}/", allWrittenTests.Count);

        // Show remaining gaps
        var remainingGaps = gapAnalyzer.GetRemainingGaps(gaps, allWrittenTests);
        _gapPresenter.ShowRemainingGaps(remainingGaps);

        if (analysisResult is not null)
        {
            AnalysisPresenter.DisplayGapNotification(
                analysisResult, allWrittenTests.Count, suite, outputFormat: _outputFormat);
        }

        // Build final result from accumulated totals
        var finalGroundedCount = allVerificationResults.Count(r => r.Result?.Verdict == VerificationVerdict.Grounded);
        var finalPartialCount = allVerificationResults.Count(r => r.Result?.Verdict == VerificationVerdict.Partial);

        string? completionMessage = allWrittenTests.Count < effectiveCount
            ? $"Generated {allWrittenTests.Count} of {effectiveCount} requested across {batchesCompleted} batch(es)."
            : null;

        var generateResult = new GenerateResult
        {
            Command = "generate",
            Status = "completed",
            Suite = suite,
            Message = completionMessage,
            Analysis = analysisResult is not null ? new GenerateAnalysis
            {
                TotalBehaviors = analysisResult.TotalBehaviors,
                AlreadyCovered = analysisResult.AlreadyCovered,
                Recommended = analysisResult.RecommendedCount,
                Breakdown = analysisResult.Breakdown?.ToDictionary(
                    kvp => kvp.Key.ToString(), kvp => kvp.Value)
            } : null,
            Generation = new GenerateGeneration
            {
                TestsRequested = effectiveCount,
                TestsGenerated = totalGenerated,
                TestsWritten = allWrittenTests.Count,
                TestsRejectedByCritic = totalRejected,
                BatchesCompleted = batchesCompleted,
                Grounding = allVerificationResults.Count > 0 ? new GroundingCounts
                {
                    Grounded = finalGroundedCount,
                    Partial = finalPartialCount,
                    Hallucinated = totalRejected
                } : null
            },
            Verification = allVerifiedTests.Count > 0 ? allVerifiedTests : null,
            RejectedTests = allRejectedTests.Count > 0 ? allRejectedTests : null,
            FilesCreated = allFilesCreated
        };

        WriteResultFile(generateResult);

        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(generateResult);
        }

        Console.Out.Flush();
        Console.Error.Flush();
        NextStepHints.Print("generate", true, _verbosity, new HintContext { SuiteName = suite }, _outputFormat);
        return ExitCodes.Success;
    }

    private async Task<int> ExecuteInteractiveModeAsync(int? count, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config
        var config = await LoadConfigAsync(configPath, ct);
        if (config is null)
        {
            return ExitCodes.Error;
        }

        // Auto-refresh document index before generation
        var indexService = new DocumentIndexService();
        await indexService.EnsureIndexAsync(currentDir, config.Source, forceRebuild: false, ct);

        // Initialize session
        var session = new InteractiveSession { Mode = SessionMode.Generate };

        // Scan for suites
        var testsDir = config.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);

        var scanner = new SuiteScanner();
        var suites = await scanner.ScanSuitesAsync(testsPath, ct);

        // Select suite
        var suiteSelector = new SuiteSelector();
        var suiteResult = suiteSelector.SelectForGeneration(suites);

        string suiteName;
        string suitePath;

        if (suiteResult.IsCreateNew)
        {
            // Prompt for new suite name
            var prompt = new Spectre.Console.TextPrompt<string>("│  Suite name: ");
            prompt.PromptStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Cyan);
            var newSuiteName = AnsiConsole.Prompt(prompt);

            suiteName = newSuiteName.Trim().ToLowerInvariant().Replace(' ', '-');
            suitePath = Path.Combine(testsPath, suiteName);
            Directory.CreateDirectory(suitePath);
            _progress.Success($"Created new suite: {suiteName}");
        }
        else if (suiteResult.Suite is null)
        {
            _progress.Error("No suite selected.");
            return ExitCodes.Error;
        }
        else
        {
            suiteName = suiteResult.Suite.Name;
            suitePath = suiteResult.Suite.Path;
        }

        session.SetSuite(suiteName, suitePath);

        // Select test type
        var typeSelector = new TestTypeSelector();
        var testType = typeSelector.Select(suiteName);
        session.SetTestType(testType);

        // Get focus if needed
        string? focus = typeSelector.GetFocusDescription(testType);

        if (testType is TestTypeSelection.SpecificArea or TestTypeSelection.FreeDescription)
        {
            var focusDescriptor = new FocusDescriptor();
            focus = testType == TestTypeSelection.SpecificArea
                ? focusDescriptor.GetSpecificArea()
                : focusDescriptor.GetFocus();
        }

        session.SetFocus(focus);

        // Load source documents with full content for grounded generation
        var documents = await _progress.StatusAsync(
            $"Scanning documentation...",
            async () =>
            {
                var docLoader = new SourceDocumentLoader(config.Source);
                return await docLoader.LoadAllAsync(currentDir, ct: ct);
            });

        if (documents.Count == 0)
        {
            _progress.Error("No source documentation found in docs/");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Please add documentation files or check your spectra.config.json.");
            return ExitCodes.Error;
        }

        _progress.Success($"Scanning documentation... {documents.Count} files");

        // Also create document map for gap analysis
        var documentMap = await _progress.StatusAsync(
            "Analyzing coverage...",
            async () =>
            {
                var mapBuilder = new DocumentMapBuilder(config.Source);
                return await mapBuilder.BuildAsync(currentDir, ct);
            });

        // Load existing tests
        var existingTests = await LoadExistingTestsAsync(suitePath, testsPath, ct);

        // Scan ALL suites for existing IDs to ensure global uniqueness
        var globalIdScanner = new GlobalIdScanner();
        var allExistingIds = await globalIdScanner.ScanAllIdsAsync(testsPath, ct);

        _progress.Success($"Loading {suiteName} suite... {existingTests.Count} existing tests");

        if (allExistingIds.Count > existingTests.Count)
        {
            _progress.Info($"Global IDs: {allExistingIds.Count} (ensuring uniqueness across all suites)");
        }

        // Load profile
        var profileLoader = new ProfileLoader();
        var effectiveProfile = await profileLoader.LoadAsync(currentDir, suitePath, ct);

        // Analyze coverage gaps (filtered by suite)
        var gapAnalyzer = new GapAnalyzer();
        var gaps = gapAnalyzer.AnalyzeGaps(documentMap, existingTests, focus, suiteName);

        // Show existing tests matching focus if specified
        if (!string.IsNullOrEmpty(focus))
        {
            var matchingTests = FilterTestsByFocus(existingTests, focus);
            if (matchingTests.Count > 0)
            {
                _results.ShowExistingTests(matchingTests, focus);
            }

            if (gaps.Count == 0)
            {
                _gapPresenter.ShowAllCovered(focus);
                session.Complete();
                return ExitCodes.Success;
            }

            _gapPresenter.ShowUncoveredAreas(gaps);
        }

        // Smart test count: analyze documentation when --count is not specified
        BehaviorAnalysisResult? analysisResult = null;
        int effectiveCount;
        string? analysisAppendFocus = null;

        if (count.HasValue)
        {
            effectiveCount = count.Value;
        }
        else
        {
            // Perform AI-powered behavior analysis
            analysisResult = await _progress.StatusAsync(
                $"Analyzing {suiteName} documentation...",
                async () =>
                {
                    var provider = config.Ai.Providers?.FirstOrDefault(p => p.Enabled);
                    var analyzer = new BehaviorAnalyzer(provider);
                    return await analyzer.AnalyzeAsync(documents, existingTests, focus, ct);
                });

            if (analysisResult is not null)
            {
                AnalysisPresenter.DisplayBreakdown(analysisResult, _outputFormat);

                if (analysisResult.RecommendedCount == 0)
                {
                    AnalysisPresenter.DisplayAllCovered(analysisResult.TotalBehaviors, _outputFormat);
                    session.Complete();
                    WriteResultFile(new GenerateResult
                    {
                        Command = "generate",
                        Status = "completed",
                        Suite = suiteName,
                        Message = "All behaviors already covered by existing tests",
                        Generation = new GenerateGeneration
                        {
                            TestsGenerated = 0,
                            TestsWritten = 0,
                            TestsRejectedByCritic = 0
                        },
                        FilesCreated = []
                    });
                    return ExitCodes.Success;
                }

                // Show interactive count selection menu
                var countSelector = new CountSelector();
                var selection = countSelector.Select(analysisResult);
                effectiveCount = selection.Count;

                // If user selected specific categories, build focus from them
                if (selection.SelectedCategories is not null)
                {
                    var categoryNames = selection.SelectedCategories
                        .Select(c => c.ToString().ToLowerInvariant())
                        .ToList();
                    analysisAppendFocus = string.Join(", ", categoryNames);
                }

                if (selection.FreeTextDescription is not null)
                {
                    analysisAppendFocus = selection.FreeTextDescription;
                }
            }
            else
            {
                // Analysis failed — fall back to existing test type suggestion
                var suggestedCount = testType switch
                {
                    TestTypeSelection.FullCoverage => 30,
                    TestTypeSelection.NegativeOnly => 15,
                    _ => 20
                };

                AnsiConsole.MarkupLine("◆ How many test cases to generate?");
                var countPrompt = new Spectre.Console.TextPrompt<int>($"│  Number of tests [grey](default: {suggestedCount})[/]: ");
                countPrompt.PromptStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Cyan);
                countPrompt.AllowEmpty = true;
                var countInput = AnsiConsole.Prompt(countPrompt);
                effectiveCount = countInput > 0 ? countInput : suggestedCount;
                AnsiConsole.MarkupLine("└");
            }
        }

        // Combine focus with analysis category selection if applicable
        if (analysisAppendFocus is not null)
        {
            focus = string.IsNullOrEmpty(focus) ? analysisAppendFocus : $"{focus}, {analysisAppendFocus}";
        }

        // Interactive generation loop
        var allGeneratedTests = new List<TestCase>();
        var allRejectedTests = new List<RejectedTest>();
        var totalRejected = 0;
        var remainingGaps = gaps.ToList();

        while (!session.IsComplete)
        {
            session.StartGenerating();

            // Create agent using Copilot SDK
            var createResult = await AgentFactory.CreateAgentAsync(
                config,
                currentDir,
                testsPath,
                status => _progress.Info($"  {status}"),
                ct);

            if (!createResult.Success)
            {
                AuthHandler.WriteAuthError(createResult.ProviderName!, createResult.AuthResult!);
                return ExitCodes.Error;
            }

            var agent = createResult.Agent!;

            if (!await agent.IsAvailableAsync(ct))
            {
                _progress.Error($"AI provider '{agent.ProviderName}' is not available.");
                return ExitCodes.Error;
            }

            // Load criteria context for the suite
            var criteriaContext = await LoadCriteriaContextAsync(currentDir, suiteName, config, ct);

            // Generate tests
            var prompt = BuildPrompt(suiteName, effectiveCount, allExistingIds, effectiveProfile, focus);
            GenerationResult result = null!;
            await _progress.StatusAsync("Generating tests...", async () =>
            {
                result = await agent.GenerateTestsAsync(prompt, documents, existingTests, effectiveCount, criteriaContext, ct);
            });

            if (!result.IsSuccess)
            {
                _progress.Error("AI generation failed:");
                foreach (var error in result.Errors)
                {
                    Console.Error.WriteLine($"  {error}");
                }
                WriteErrorResultFile(string.Join("; ", result.Errors), suiteName);
                return ExitCodes.Error;
            }

            if (result.Tests.Count == 0)
            {
                _progress.Warning($"No tests generated (requested: {effectiveCount})");
                _progress.BlankLine();
                ShowCountMismatchReason(0, effectiveCount, documentMap.Documents.Count, existingTests.Count, focus);
                session.Complete();
                break;
            }

            // Display and write tests
            _progress.Success($"Generated {result.Tests.Count} tests:");
            _progress.BlankLine();
            _results.ShowGeneratedTests(result.Tests);

            // Warn if fewer tests than requested
            if (result.Tests.Count < effectiveCount)
            {
                _progress.BlankLine();
                _progress.Warning($"Generated {result.Tests.Count} of {effectiveCount} requested tests");
                ShowCountMismatchReason(result.Tests.Count, effectiveCount, documentMap.Documents.Count, existingTests.Count, focus);
            }

            if (!_dryRun)
            {
                // Verify tests against documentation if critic is configured
                var verificationResults = new List<(TestCase Test, VerificationResult Result)>();
                var testsToWrite = result.Tests.ToList();
                var generatorModel = agent.ProviderName;

                if (ShouldVerify(config.Ai.Critic))
                {
                    UpdateProgress(suiteName, "verifying",
                        $"Verifying {result.Tests.Count} tests...");

                    var interactiveVerifiedTests = new List<VerifiedTest>();
                    verificationResults = await VerifyTestsAsync(
                        result.Tests,
                        documentMap,
                        config.Ai.Critic!,
                        generatorModel,
                        ct,
                        suiteName,
                        onTestVerified: (test, vr) =>
                        {
                            interactiveVerifiedTests.Add(BuildVerifiedTest(test, vr));
                            WriteVerificationProgress(suiteName, interactiveVerifiedTests);
                        });

                    // Show verification summary
                    _verification.ShowSummary(verificationResults);
                    _verification.ShowPartialDetails(verificationResults);
                    _verification.ShowRejectedDetails(verificationResults);

                    // Filter out hallucinated tests
                    testsToWrite = verificationResults
                        .Where(r => r.Result.Verdict != VerificationVerdict.Hallucinated)
                        .Select(r => r.Test)
                        .ToList();

                    var rejected = verificationResults
                        .Where(r => r.Result.Verdict == VerificationVerdict.Hallucinated);
                    totalRejected += rejected.Count();
                    allRejectedTests.AddRange(rejected.Select(r => BuildRejectedTest(r.Test, r.Result)));

                    if (testsToWrite.Count == 0)
                    {
                        _progress.BlankLine();
                        _progress.Warning("All generated tests were rejected as hallucinated");
                        _progress.Info("Try adding more documentation or narrowing the focus");
                        session.Complete();
                        break;
                    }
                }
                else if (_skipCritic)
                {
                    _verification.ShowSkippedNotice();
                }

                var writer = new TestFileWriter();

                foreach (var test in testsToWrite)
                {
                    var filePath = TestFileWriter.GetFilePath(testsPath, suiteName, test.Id);

                    // Get verification result for this test if available
                    var verification = verificationResults.FirstOrDefault(r => r.Test.Id == test.Id);
                    var testWithPath = CreateTestWithGrounding(
                        test,
                        verification.Result,
                        generatorModel,
                        filePath,
                        testsPath);

                    await writer.WriteAsync(filePath, testWithPath, ct);
                    existingTests = [.. existingTests, testWithPath];
                    allExistingIds.Add(testWithPath.Id);
                }

                allGeneratedTests.AddRange(testsToWrite);

                _progress.BlankLine();
                _results.ShowCompletion($"tests/{suiteName}/", testsToWrite.Count);
            }

            // Update remaining gaps
            remainingGaps = gapAnalyzer.GetRemainingGaps(remainingGaps, result.Tests).ToList();
            session.RecordGeneration(result.Tests, remainingGaps);

            // Prompt for more if gaps remain
            if (remainingGaps.Count > 0 && !_dryRun)
            {
                _gapPresenter.ShowRemainingGaps(remainingGaps);

                var gapSelector = new GapSelector();
                var gapResult = gapSelector.PromptForMore(remainingGaps);

                switch (gapResult.Action)
                {
                    case GapAction.GenerateAll:
                        focus = null; // Generate for all remaining gaps
                        session.SelectGaps(remainingGaps);
                        break;

                    case GapAction.GenerateSelected:
                        // Build focus from selected gaps
                        focus = string.Join(", ", gapResult.SelectedGaps.Select(g => g.Suggestion ?? g.DocumentPath));
                        session.SelectGaps(gapResult.SelectedGaps);
                        break;

                    case GapAction.Done:
                    default:
                        session.Complete();
                        break;
                }
            }
            else
            {
                session.Complete();
            }
        }

        // Update index if we wrote any tests
        if (allGeneratedTests.Count > 0 && !_dryRun)
        {
            var indexGenerator = new IndexGenerator();
            var index = indexGenerator.Generate(suiteName, existingTests.ToList());

            var indexWriter = new IndexWriter();
            await indexWriter.WriteAsync(Path.Combine(suitePath, "_index.json"), index, ct);
        }

        // Track session stats
        var sessionSuites = new List<string> { suiteName };
        var sessionTotalTests = allGeneratedTests.Count;

        // Continuation loop — offer to continue with other suites
        if (!_noInteraction && !_dryRun)
        {
            while (true)
            {
                AnsiConsole.WriteLine();

                var continueChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What would you like to do next?")
                        .HighlightStyle(Style.Parse("cyan"))
                        .AddChoices(
                            $"Generate more for {suiteName}",
                            "Switch to a different suite",
                            "Create a new suite",
                            "Done — exit"));

                if (continueChoice == "Done — exit")
                    break;

                if (continueChoice == $"Generate more for {suiteName}")
                {
                    _progress.Info("Re-run 'spectra ai generate' to continue generating for this suite.");
                    break;
                }

                if (continueChoice == "Switch to a different suite")
                {
                    // Re-scan suites and let user pick
                    var freshSuites = await scanner.ScanSuitesAsync(testsPath, ct);
                    if (freshSuites.Count == 0)
                    {
                        _progress.Warning("No suites found.");
                        continue;
                    }

                    var suiteNames = freshSuites.Select(s => $"{s.Name} ({s.TestCount} tests)").ToList();
                    var selected = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select a suite:")
                            .HighlightStyle(Style.Parse("cyan"))
                            .AddChoices(suiteNames));

                    var selectedSuite = freshSuites[suiteNames.IndexOf(selected)];
                    sessionSuites.Add(selectedSuite.Name);
                    _progress.Info($"Switched to suite: {selectedSuite.Name}");
                    _progress.Info($"Run 'spectra ai generate {selectedSuite.Name}' to generate tests.");
                    break;
                }

                if (continueChoice == "Create a new suite")
                {
                    var newName = AnsiConsole.Prompt(
                        new TextPrompt<string>("Suite name:")
                            .PromptStyle(new Style(foreground: Color.Cyan)));

                    var sanitized = newName.Trim().ToLowerInvariant().Replace(' ', '-');
                    var newPath = Path.Combine(testsPath, sanitized);
                    Directory.CreateDirectory(newPath);
                    sessionSuites.Add(sanitized);
                    _progress.Success($"Created new suite: {sanitized}");
                    _progress.Info($"Run 'spectra ai generate {sanitized}' to generate tests for it.");
                    break;
                }
            }

            // Session summary if multiple suites were touched
            if (sessionSuites.Count > 1)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold]Session summary:[/] {sessionSuites.Count} suites, {sessionTotalTests} tests generated");
                foreach (var s in sessionSuites)
                {
                    AnsiConsole.MarkupLine($"  [grey]• {s}[/]");
                }
            }
        }

        // Smart count: show behavior gap notification if analysis was performed
        if (analysisResult is not null)
        {
            AnalysisPresenter.DisplayGapNotification(
                analysisResult, allGeneratedTests.Count, suiteName, outputFormat: _outputFormat);
        }

        // Write final result file so progress page shows completion
        WriteResultFile(new GenerateResult
        {
            Command = "generate",
            Status = "completed",
            Suite = suiteName,
            Message = allGeneratedTests.Count == 0
                ? $"No tests generated (requested: {effectiveCount})"
                : null,
            Analysis = analysisResult is not null ? new GenerateAnalysis
            {
                TotalBehaviors = analysisResult.TotalBehaviors,
                AlreadyCovered = analysisResult.AlreadyCovered,
                Recommended = analysisResult.RecommendedCount,
                Breakdown = analysisResult.Breakdown?.ToDictionary(
                    kvp => kvp.Key.ToString(), kvp => kvp.Value)
            } : null,
            Generation = new GenerateGeneration
            {
                TestsGenerated = allGeneratedTests.Count + totalRejected,
                TestsWritten = allGeneratedTests.Count,
                TestsRejectedByCritic = totalRejected
            },
            RejectedTests = allRejectedTests.Count > 0 ? allRejectedTests : null,
            FilesCreated = allGeneratedTests
                .Select(t => $"tests/{suiteName}/{t.Id}.md")
                .ToList()
        });

        NextStepHints.Print("generate", true, _verbosity, new HintContext { SuiteName = suiteName }, _outputFormat);
        return ExitCodes.Success;
    }

    /// <summary>
    /// Handles --from-suggestions: loads session, generates from pending suggestions.
    /// </summary>
    private async Task<int> ExecuteFromSuggestionsAsync(
        string suite,
        string suggestionsArg,
        CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var sessionStore = new SessionStore(currentDir);
        var session = await sessionStore.LoadAsync(suite, ct);

        if (session is null)
        {
            if (_outputFormat == OutputFormat.Json)
            {
                JsonResultWriter.Write(new ErrorResult
                {
                    Command = "generate",
                    Status = "failed",
                    Error = $"No active session found for suite '{suite}'. Run 'spectra ai generate {suite}' first."
                });
            }
            else
            {
                _progress.Error($"No active session found for suite '{suite}'. Run 'spectra ai generate {suite}' first.");
            }
            return ExitCodes.Error;
        }

        var pending = session.Suggestions.Where(s => s.Status == SuggestionStatus.Pending).ToList();

        // If specific indices provided, filter
        if (!string.IsNullOrEmpty(suggestionsArg))
        {
            var indices = suggestionsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : -1)
                .Where(n => n > 0)
                .ToHashSet();

            if (indices.Count > 0)
            {
                pending = pending.Where(s => indices.Contains(s.Index)).ToList();
            }
        }

        if (pending.Count == 0)
        {
            _progress.Info("No pending suggestions to generate.");
            return ExitCodes.Success;
        }

        // Generate tests for each pending suggestion using the focus approach
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        var config = await LoadConfigAsync(configPath, ct);
        if (config is null) return ExitCodes.Error;

        var testsDir = config.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);

        var focus = string.Join(", ", pending.Select(s => s.Title));
        var result = await ExecuteDirectModeAsync(suite, pending.Count, focus, ct);

        // Mark suggestions as generated
        foreach (var s in pending)
            s.Status = SuggestionStatus.Generated;

        await sessionStore.SaveAsync(session, ct);
        return result;
    }

    /// <summary>
    /// Handles --from-description: creates a test from a user description.
    /// </summary>
    private async Task<int> ExecuteFromDescriptionAsync(
        string suite,
        string description,
        string? context,
        CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        var config = await LoadConfigAsync(configPath, ct);
        if (config is null) return ExitCodes.Error;

        var testsDir = config.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);

        if (!Directory.Exists(suitePath))
            Directory.CreateDirectory(suitePath);

        // Scan existing IDs
        var globalIdScanner = new GlobalIdScanner();
        var allExistingIds = await globalIdScanner.ScanAllIdsAsync(testsPath, ct);

        var generator = new UserDescribedGenerator();
        _progress.Loading("Creating test case from your description...");

        // Best-effort: load matching documentation as formatting context (cap 3 docs × 8000 chars).
        string? docContext = null;
        IReadOnlyList<string> docPaths = [];
        try
        {
            var docLoader = new SourceDocumentLoader(config.Source);
            var allDocs = await docLoader.LoadAllAsync(currentDir, maxContentLengthPerDoc: 8000, ct: ct);
            var matchingDocs = FilterDocsForSuite(allDocs, suite).Take(3).ToList();
            if (matchingDocs.Count > 0)
            {
                docContext = FormatDocContext(matchingDocs);
                docPaths = matchingDocs.Select(d => d.Path).ToList();
            }
        }
        catch
        {
            // Best-effort: doc loading failures must not block from-description generation.
        }

        // Best-effort: load matching acceptance criteria as formatting context.
        string? criteriaContext = null;
        try
        {
            criteriaContext = await LoadCriteriaContextAsync(currentDir, suite, config, ct);
        }
        catch
        {
            // Best-effort: criteria loading failures must not block from-description generation.
        }

        var test = await generator.GenerateAsync(
            description, context, suite, allExistingIds,
            config, currentDir, testsPath,
            status => _progress.Info($"  {status}"),
            ct,
            documentContext: docContext,
            criteriaContext: criteriaContext,
            sourceRefPaths: docPaths);

        if (test is null)
        {
            _progress.Error("Failed to generate test from description.");
            return ExitCodes.Error;
        }

        // Write the test
        var writer = new TestFileWriter();
        var filePath = TestFileWriter.GetFilePath(testsPath, suite, test.Id);
        var testWithPath = new TestCase
        {
            Id = test.Id,
            Title = test.Title,
            Priority = test.Priority,
            Tags = test.Tags,
            Component = test.Component,
            Preconditions = test.Preconditions,
            Environment = test.Environment,
            EstimatedDuration = test.EstimatedDuration,
            DependsOn = test.DependsOn,
            SourceRefs = test.SourceRefs,
            RelatedWorkItems = test.RelatedWorkItems,
            Custom = test.Custom,
            Steps = test.Steps,
            ExpectedResult = test.ExpectedResult,
            TestData = test.TestData,
            Criteria = test.Criteria,
            FilePath = Path.GetRelativePath(testsPath, filePath),
            Grounding = test.Grounding
        };

        await writer.WriteAsync(filePath, testWithPath, ct);
        _progress.Success($"{test.Id} written to tests/{suite}/");

        // Update session
        var sessionStore = new SessionStore(currentDir);
        var session = await sessionStore.LoadAsync(suite, ct);
        if (session is not null)
        {
            session.UserDescribed.Add(test.Id);
            await sessionStore.SaveAsync(session, ct);
        }

        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(new GenerateResult
            {
                Command = "generate",
                Status = "completed",
                Suite = suite,
                Generation = new GenerateGeneration
                {
                    TestsGenerated = 1,
                    TestsWritten = 1,
                    TestsRejectedByCritic = 0
                },
                FilesCreated = [Path.GetRelativePath(currentDir, filePath)]
            });
        }

        return ExitCodes.Success;
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

    private async Task<List<TestCase>> LoadExistingTestsAsync(
        string suitePath,
        string testsPath,
        CancellationToken ct)
    {
        var tests = new List<TestCase>();

        if (!Directory.Exists(suitePath))
        {
            return tests;
        }

        var parser = new TestCaseParser();
        var files = Directory.GetFiles(suitePath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("_"));

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, ct);
            var relativePath = Path.GetRelativePath(testsPath, file);
            var result = parser.Parse(content, relativePath);

            if (result.IsSuccess)
            {
                tests.Add(result.Value!);
            }
        }

        return tests;
    }

    private static List<TestCase> FilterTestsByFocus(IReadOnlyList<TestCase> tests, string focus)
    {
        var lowerFocus = focus.ToLowerInvariant();

        return tests.Where(t =>
            t.Title.ToLowerInvariant().Contains(lowerFocus) ||
            t.Tags.Any(tag => tag.ToLowerInvariant().Contains(lowerFocus)) ||
            (t.Component?.ToLowerInvariant().Contains(lowerFocus) ?? false) ||
            t.Steps.Any(s => s.ToLowerInvariant().Contains(lowerFocus))
        ).ToList();
    }

    private static string BuildPrompt(
        string suite,
        int count,
        IReadOnlyCollection<string> allExistingIds,
        EffectiveProfile profile,
        string? focus)
    {
        var existingIds = string.Join(", ", allExistingIds);

        var profileContext = string.Empty;
        if (profile.Source.Type != SourceType.Default)
        {
            var contextBuilder = new ProfileContextBuilder();
            profileContext = contextBuilder.Build(profile);
        }

        var options = profile.Profile.Options;
        var defaultPriority = options.DefaultPriority switch
        {
            ProfilePriority.High => "high",
            ProfilePriority.Low => "low",
            _ => "medium"
        };

        var minNegative = options.MinNegativeScenarios;

        var focusInstruction = string.IsNullOrEmpty(focus)
            ? "Cover different aspects of the feature"
            : $"Focus on: {focus}";

        return $"""
            Generate {count} new manual test cases for the '{suite}' feature.

            {profileContext}
            Requirements:
            - Each test must have a unique ID in format TC-XXX (where XXX is a number)
            - Do not duplicate these existing test IDs: {existingIds}
            - {focusInstruction}
            - Include at least {minNegative} negative/error scenario test(s)
            - Default priority is {defaultPriority} unless the scenario clearly warrants different priority
            - Include clear steps and expected results for each test

            For each test, provide:
            - id: unique identifier
            - title: descriptive title
            - priority: high/medium/low
            - steps: list of actions
            - expected_result: what should happen
            """;
    }

    /// <summary>
    /// Verifies tests against documentation using the critic model.
    /// </summary>
    private async Task<List<(TestCase Test, VerificationResult Result)>> VerifyTestsAsync(
        IReadOnlyList<TestCase> tests,
        DocumentMap documentMap,
        CriticConfig criticConfig,
        string generatorModel,
        CancellationToken ct,
        string? suite = null,
        Action<TestCase, VerificationResult>? onTestVerified = null)
    {
        var results = new List<(TestCase Test, VerificationResult Result)>();

        var createResult = CriticFactory.TryCreate(criticConfig);
        if (!createResult.Success)
        {
            _verification.ShowCriticUnavailable(createResult.ErrorMessage ?? "Critic not available");

            // In non-interactive mode, proceed without verification
            if (_noInteraction)
            {
                foreach (var test in tests)
                {
                    results.Add((test, VerificationResult.Unverified(
                        "none", createResult.ErrorMessage ?? "Critic not configured")));
                }
                return results;
            }

            // In interactive mode, ask user
            var proceed = AnsiConsole.Confirm("Proceed without verification?", defaultValue: true);
            if (!proceed)
            {
                return results; // Return empty - will be handled by caller
            }

            foreach (var test in tests)
            {
                results.Add((test, VerificationResult.Unverified(
                    "none", createResult.ErrorMessage ?? "Critic not configured")));
            }
            return results;
        }

        var critic = createResult.Critic!;

        // Convert document map to source documents
        var sourceDocs = documentMap.Documents
            .Select(d => new SourceDocument
            {
                Path = d.Path,
                Title = d.Title,
                Content = d.Preview ?? ""
            })
            .ToList();

        // Verify each test
        for (var i = 0; i < tests.Count; i++)
        {
            var test = tests[i];

            // T013: Skip verification for tests with Manual verdict — pass through as-is
            if (test.Grounding is not null && test.Grounding.Verdict == VerificationVerdict.Manual)
            {
                results.Add((test, new VerificationResult
                {
                    Verdict = VerificationVerdict.Manual,
                    Score = 1.0,
                    Findings = [],
                    CriticModel = critic.ModelName
                }));
                continue;
            }

            if (suite is not null)
            {
                UpdateProgress(suite, "verifying",
                    $"Verifying test {i + 1}/{tests.Count}: {test.Id} ({critic.ModelName})...");
            }

            await _progress.StatusAsync($"Verifying {test.Id} ({critic.ModelName})...", async () =>
            {
                try
                {
                    var result = await critic.VerifyTestAsync(test, sourceDocs, ct);
                    results.Add((test, result));
                    onTestVerified?.Invoke(test, result);
                }
                catch (Exception ex)
                {
                    var unverified = VerificationResult.Unverified(critic.ModelName, ex.Message);
                    results.Add((test, unverified));
                    onTestVerified?.Invoke(test, unverified);
                }
            });
        }

        return results;
    }

    /// <summary>
    /// Creates a test with grounding metadata if verified.
    /// </summary>
    internal static TestCase CreateTestWithGrounding(
        TestCase test,
        VerificationResult? result,
        string generatorModel,
        string filePath,
        string testsPath)
    {
        // T010: Preserve existing Manual grounding metadata — do not overwrite with critic results
        if (test.Grounding is not null && test.Grounding.Verdict == VerificationVerdict.Manual)
        {
            return new TestCase
            {
                Id = test.Id,
                Title = test.Title,
                Priority = test.Priority,
                Tags = test.Tags,
                Component = test.Component,
                Preconditions = test.Preconditions,
                Environment = test.Environment,
                EstimatedDuration = test.EstimatedDuration,
                DependsOn = test.DependsOn,
                SourceRefs = test.SourceRefs,
                RelatedWorkItems = test.RelatedWorkItems,
                Custom = test.Custom,
                Steps = test.Steps,
                ExpectedResult = test.ExpectedResult,
                TestData = test.TestData,
                Criteria = test.Criteria,
                FilePath = Path.GetRelativePath(testsPath, filePath),
                Grounding = test.Grounding
            };
        }

        GroundingMetadata? grounding = null;

        if (result is not null && result.IsSuccess)
        {
            grounding = result.ToMetadata(generatorModel);
        }

        return new TestCase
        {
            Id = test.Id,
            Title = test.Title,
            Priority = test.Priority,
            Tags = test.Tags,
            Component = test.Component,
            Preconditions = test.Preconditions,
            Environment = test.Environment,
            EstimatedDuration = test.EstimatedDuration,
            DependsOn = test.DependsOn,
            SourceRefs = test.SourceRefs,
            RelatedWorkItems = test.RelatedWorkItems,
            Custom = test.Custom,
            Steps = test.Steps,
            ExpectedResult = test.ExpectedResult,
            TestData = test.TestData,
            Criteria = test.Criteria,
            FilePath = Path.GetRelativePath(testsPath, filePath),
            Grounding = grounding
        };
    }

    private static VerifiedTest BuildVerifiedTest(TestCase test, VerificationResult result)
    {
        var reason = result.Findings
            .Where(f => f.Status != FindingStatus.Grounded)
            .Select(f => f.Reason ?? f.Claim)
            .FirstOrDefault();

        return new VerifiedTest
        {
            Id = test.Id,
            Title = test.Title,
            Verdict = result.Verdict.ToString().ToLowerInvariant(),
            Score = result.Score,
            Reason = reason
        };
    }

    private static RejectedTest BuildRejectedTest(TestCase test, VerificationResult result)
    {
        var reason = result.Findings
            .Where(f => f.Status == FindingStatus.Hallucinated)
            .Select(f => f.Reason ?? f.Claim)
            .FirstOrDefault();

        return new RejectedTest
        {
            Id = test.Id,
            Title = test.Title,
            Verdict = result.Verdict.ToString().ToLowerInvariant(),
            Reason = reason
        };
    }

    /// <summary>
    /// Checks if critic verification should be performed.
    /// </summary>
    private bool ShouldVerify(CriticConfig? criticConfig)
    {
        // Skip if --skip-critic flag is set
        if (_skipCritic)
            return false;

        // Skip if critic not configured or disabled
        if (criticConfig is null || !criticConfig.Enabled)
            return false;

        return true;
    }

    private static readonly Progress.ProgressManager _progressManager =
        new("generate", Progress.ProgressPhases.Generate, title: "Test Generation");

    private static void DeleteResultFile()
    {
        _progressManager.Reset();
    }

    private static void WriteResultFile(GenerateResult result)
    {
        if (result.Status is "completed" or "failed")
            _progressManager.Complete(result);
        else
            _progressManager.Update(result);
    }

    private static void WriteInProgressResultFile(string suite, string status = "generating", string? message = null)
    {
        var result = new GenerateResult
        {
            Command = "generate",
            Status = status,
            Suite = suite,
            Message = message,
            Generation = new GenerateGeneration
            {
                TestsGenerated = 0,
                TestsWritten = 0,
                TestsRejectedByCritic = 0
            },
            FilesCreated = []
        };
        _progressManager.Update(result);
    }

    private static void UpdateProgress(string suite, string status, string message)
    {
        WriteInProgressResultFile(suite, status, message);
    }

    private static void WriteVerificationProgress(string suite, List<VerifiedTest> verifiedTests)
    {
        var grounded = verifiedTests.Count(v => v.Verdict == "grounded");
        var partial = verifiedTests.Count(v => v.Verdict == "partial");
        var hallucinated = verifiedTests.Count(v => v.Verdict == "hallucinated");
        var message = $"Verified {verifiedTests.Count} tests: {grounded} grounded, {partial} partial, {hallucinated} hallucinated";

        var result = new GenerateResult
        {
            Command = "generate",
            Status = "verifying",
            Suite = suite,
            Message = message,
            Generation = new GenerateGeneration
            {
                TestsGenerated = 0,
                TestsWritten = 0,
                TestsRejectedByCritic = hallucinated
            },
            Verification = verifiedTests,
            FilesCreated = []
        };
        _progressManager.Update(result);
    }

    private static void WriteErrorResultFile(string error, string? suite = null)
    {
        _progressManager.Fail(error);
    }

    /// <summary>
    /// Shows explanation for why fewer tests were generated than requested.
    /// </summary>
    private void ShowCountMismatchReason(
        int actual,
        int requested,
        int docCount,
        int existingTestCount,
        string? focus)
    {
        var reasons = new List<string>();

        // Analyze possible reasons
        if (docCount == 0)
        {
            reasons.Add("No documentation found - add files to docs/ folder");
        }
        else if (docCount < 3)
        {
            reasons.Add($"Limited documentation ({docCount} files) - AI may not find enough distinct scenarios");
        }

        if (existingTestCount > 0 && actual == 0)
        {
            reasons.Add($"Existing tests ({existingTestCount}) may already cover most scenarios");
        }

        if (!string.IsNullOrEmpty(focus))
        {
            reasons.Add($"Focus filter '{focus}' may be too narrow - try broadening or removing --focus");
        }

        if (requested > 100)
        {
            reasons.Add($"Very high count ({requested}) - try generating in batches of 50 or less");
        }

        if (reasons.Count == 0)
        {
            // Generic fallback
            reasons.Add("AI determined fewer unique test scenarios were available");
            reasons.Add("Try: different --focus, more documentation, or lower --count");
        }

        _progress.Info("Possible reasons:");
        foreach (var reason in reasons)
        {
            Console.WriteLine($"  - {reason}");
        }
    }

    /// <summary>
    /// Loads acceptance criteria relevant to the target suite from .criteria.yaml files.
    /// Matches by document name and by component field.
    /// </summary>
    private static async Task<string?> LoadCriteriaContextAsync(
        string basePath,
        string suiteName,
        SpectraConfig config,
        CancellationToken ct)
    {
        var criteriaDir = Path.Combine(basePath, config.Coverage?.CriteriaDir ?? "docs/criteria");
        if (!Directory.Exists(criteriaDir))
            return null;

        var reader = new CriteriaFileReader();
        var allCriteria = new List<AcceptanceCriterion>();

        // Load criteria from all .criteria.yaml files in the criteria directory
        var criteriaFiles = Directory.GetFiles(criteriaDir, "*.criteria.yaml", SearchOption.AllDirectories);
        foreach (var file in criteriaFiles)
        {
            var criteria = await reader.ReadAsync(file, ct);
            allCriteria.AddRange(criteria);
        }

        if (allCriteria.Count == 0)
            return null;

        // Filter: criteria matching suite name (exact or partial match on component, source doc, or file name)
        var relevant = allCriteria.Where(c =>
            // Exact match by component
            (c.Component != null && c.Component.Equals(suiteName, StringComparison.OrdinalIgnoreCase)) ||
            // Component contains suite name (e.g., suite "reporting" matches component "reporting-analytics")
            (c.Component != null && c.Component.Contains(suiteName, StringComparison.OrdinalIgnoreCase)) ||
            // Suite contains component (e.g., suite "reporting-analytics" matches component "reporting")
            (c.Component != null && suiteName.Contains(c.Component, StringComparison.OrdinalIgnoreCase)) ||
            // Source doc file name contains suite name
            (c.SourceDoc != null && Path.GetFileNameWithoutExtension(c.SourceDoc)
                .Contains(suiteName, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // Also include criteria from files whose name starts with the suite name
        if (relevant.Count == 0)
        {
            var matchingFiles = criteriaFiles.Where(f =>
                Path.GetFileNameWithoutExtension(f).Replace(".criteria", "")
                    .Contains(suiteName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingFiles.Count > 0)
            {
                var matchingFileSet = new HashSet<string>(matchingFiles, StringComparer.OrdinalIgnoreCase);
                // Reload only from matching files
                relevant = new List<AcceptanceCriterion>();
                foreach (var file in matchingFiles)
                {
                    var fileCriteria = await reader.ReadAsync(file, ct);
                    relevant.AddRange(fileCriteria);
                }
            }
        }

        // Last resort: use all criteria (better than none, but may be noisy)
        if (relevant.Count == 0)
            relevant = allCriteria;

        // Format as context string
        var sb = new System.Text.StringBuilder();
        foreach (var criterion in relevant)
        {
            sb.Append($"- **{criterion.Id}**");
            if (!string.IsNullOrEmpty(criterion.Rfc2119))
                sb.Append($" [{criterion.Rfc2119}]");
            sb.AppendLine($" {criterion.Text}");
            if (!string.IsNullOrEmpty(criterion.Component))
                sb.AppendLine($"  Component: {criterion.Component}");
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    /// <summary>
    /// Filters loaded source documents to those most relevant to the target suite.
    /// Match strategy: case-insensitive contains on filename, title, or any section heading.
    /// Used by the from-description flow as best-effort context selection.
    /// </summary>
    private static IEnumerable<SourceDocument> FilterDocsForSuite(
        IReadOnlyList<SourceDocument> docs,
        string suite)
    {
        if (string.IsNullOrWhiteSpace(suite))
            return [];

        bool Matches(SourceDocument d)
        {
            var fileName = Path.GetFileNameWithoutExtension(d.Path);
            if (fileName.Contains(suite, StringComparison.OrdinalIgnoreCase))
                return true;
            if (d.Title.Contains(suite, StringComparison.OrdinalIgnoreCase))
                return true;
            if (d.Sections.Any(s => s.Contains(suite, StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        return docs.Where(Matches);
    }

    /// <summary>
    /// Formats a small set of source documents into a single context string for the AI prompt.
    /// </summary>
    private static string FormatDocContext(IReadOnlyList<SourceDocument> docs)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var doc in docs)
        {
            sb.AppendLine($"### {doc.Title} ({doc.Path})");
            sb.AppendLine();
            sb.AppendLine(doc.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
