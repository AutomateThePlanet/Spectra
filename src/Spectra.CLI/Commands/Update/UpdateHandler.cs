using System.Text.Json;
using Spectra.CLI.Agent.Tools;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Review;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Update;

namespace Spectra.CLI.Commands.Update;

/// <summary>
/// Handles the update command execution.
/// </summary>
public sealed class UpdateHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly bool _dryRun;
    private readonly bool _noReview;

    public UpdateHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        bool dryRun = false,
        bool noReview = false)
    {
        _verbosity = verbosity;
        _dryRun = dryRun;
        _noReview = noReview;
    }

    /// <summary>
    /// Executes the update command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string suite,
        bool showDiff = false,
        bool deleteOrphaned = false,
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
                try
                {
                    var configJson = await File.ReadAllTextAsync(configPath, ct);
                    config = JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Error reading config: {ex.Message}");
                    return ExitCodes.Error;
                }
            }

            if (config is null)
            {
                Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
                return ExitCodes.Error;
            }

            // Determine suite path
            var testsDir = Path.Combine(basePath, config.Tests.Dir);
            var suitePath = Path.Combine(testsDir, suite);

            if (!Directory.Exists(suitePath))
            {
                Console.Error.WriteLine($"Error: Suite not found: {suite}");
                return ExitCodes.Error;
            }

            // Read existing tests
            var batchReader = new BatchReadTestsTool();
            var readResult = await batchReader.ExecuteAsync(suitePath, ct: ct);

            if (!readResult.Success || readResult.Tests is null)
            {
                Console.Error.WriteLine($"Error: Could not read tests: {readResult.Error}");
                return ExitCodes.Error;
            }

            if (readResult.Tests.Count == 0)
            {
                Console.WriteLine("No tests found in suite.");
                return ExitCodes.Success;
            }

            Console.WriteLine($"Found {readResult.Tests.Count} tests in suite '{suite}'");

            // Load source documents
            var sourceDir = Path.Combine(basePath, config.Source.LocalDir ?? "docs");
            var sourceContents = await LoadSourceDocumentsAsync(sourceDir, readResult.Tests, ct);

            // Classify tests
            var classifier = new TestClassifier();
            var proposeTool = new BatchProposeUpdatesTool(classifier);
            var proposeResult = proposeTool.Execute(readResult.Tests, sourceContents);

            if (!proposeResult.Success || proposeResult.Proposals is null)
            {
                Console.Error.WriteLine($"Error: Could not analyze tests: {proposeResult.Error}");
                return ExitCodes.Error;
            }

            // Convert to UpdateProposals for the reviewer
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
                    return ExitCodes.Success;
                }
            }

            // Review or auto-approve
            UpdateReviewResult reviewResult;

            if (_noReview)
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
                Console.WriteLine("Update cancelled.");
                return ExitCodes.Cancelled;
            }

            // Apply changes
            if (!_dryRun)
            {
                await ApplyChangesAsync(suitePath, reviewResult, ct);
            }
            else
            {
                Console.WriteLine("[Dry run] No changes were made.");
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
                    Console.WriteLine("Updated _index.json");
                }
            }

            // Summary
            Console.WriteLine($"\nUpdate complete:");
            Console.WriteLine($"  Updated: {reviewResult.ToUpdate.Count}");
            Console.WriteLine($"  Deleted: {reviewResult.ToDelete.Count}");
            Console.WriteLine($"  Skipped: {reviewResult.Skipped.Count}");
            Console.WriteLine($"  Up to date: {reviewResult.UpToDateCount}");

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
}
