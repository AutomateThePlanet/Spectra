using System.Text.Json;
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
using Spectra.Core.Update;

namespace Spectra.CLI.Commands.Update;

/// <summary>
/// Handles the update command execution with direct and interactive modes.
/// </summary>
public sealed class UpdateHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly bool _noReview;
    private readonly bool _noInteraction;
    private readonly ProgressReporter _progress;
    private readonly ClassificationPresenter _classificationPresenter;

    public UpdateHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        bool noReview = false,
        bool noInteraction = false)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _noReview = noReview;
        _noInteraction = noInteraction;
        _progress = new ProgressReporter();
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

        // Classify tests using the classifier directly for better presentation
        var classifier = new TestClassifier();
        var classificationResults = await _progress.StatusAsync(
            "Classifying tests...",
            () => Task.FromResult(classifier.ClassifyBatch(readResult.Tests, sourceContents)));

        // Show classification summary
        _classificationPresenter.ShowSummary(classificationResults);

        // Show detailed results by classification type
        _classificationPresenter.ShowOutdated(classificationResults);
        _classificationPresenter.ShowOrphaned(classificationResults);
        _classificationPresenter.ShowRedundant(classificationResults);

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
        if (!_dryRun)
        {
            await ApplyChangesAsync(suitePath, reviewResult, ct);
        }
        else
        {
            _progress.Info("Dry run - no changes were made.");
        }

        // Update index
        if (!_dryRun && (reviewResult.ToUpdate.Count > 0 || reviewResult.ToDelete.Count > 0))
        {
            // Re-read all tests to rebuild index
            var rereadResult = await batchReader.ExecuteAsync(suitePath, ct: ct);
            if (rereadResult.Success && rereadResult.Tests is not null)
            {
                var indexGenerator = new IndexGenerator();
                var index = indexGenerator.Generate(suite, rereadResult.Tests);
                var indexWriter = new IndexWriter();
                await indexWriter.WriteAsync(Path.Combine(suitePath, "_index.json"), index, ct);
            }
        }

        // Summary
        _classificationPresenter.ShowUpdateComplete(
            reviewResult.ToUpdate.Count,
            0, // marked orphaned count (we update in place)
            reviewResult.ToDelete.Count);

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

    private static async Task ApplyChangesAsync(
        string suitePath,
        UpdateReviewResult result,
        CancellationToken ct)
    {
        var writer = new TestFileWriter();

        // Apply updates
        foreach (var proposal in result.ToUpdate)
        {
            if (proposal.ProposedTest is not null)
            {
                var path = Path.Combine(suitePath, $"{proposal.OriginalTest.Id}.md");
                await writer.WriteAsync(path, proposal.ProposedTest, ct);
                Console.WriteLine($"  Updated: {proposal.OriginalTest.Id}");
            }
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
        }
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
