using System.Text.Json;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Review;

/// <summary>
/// Spec 071 FR6: Core logic for reviewing flagged (still-partial-after-repair) tests.
/// Handles scanning, accepting, and deleting. Retry-repair is delegated to the
/// spectra-review-flagged skill (requires agent inference).
/// </summary>
public sealed class ReviewFlaggedHandler
{
    public sealed record FlaggedTest
    {
        public required string Id { get; init; }
        public required string Suite { get; init; }
        public required string Title { get; init; }
        public required string FilePath { get; init; }
        public required double Score { get; init; }
        public required int RepairAttempts { get; init; }
        public required IReadOnlyList<Core.Models.Grounding.CondensedFinding> CondensedFindings { get; init; }
    }

    private readonly string _currentDir;
    private readonly string _testsDir;

    public ReviewFlaggedHandler(string currentDir, string testsDir)
    {
        _currentDir = currentDir;
        _testsDir = testsDir;
    }

    /// <summary>
    /// Scans all suites (or one suite) for tests with flagged_for_review: true.
    /// </summary>
    public async Task<List<FlaggedTest>> FindFlaggedAsync(string? suiteFilter, CancellationToken ct)
    {
        var results = new List<FlaggedTest>();
        var testsPath = Path.Combine(_currentDir, _testsDir);
        if (!Directory.Exists(testsPath)) return results;

        IEnumerable<string> suiteDirs;
        if (!string.IsNullOrWhiteSpace(suiteFilter))
        {
            var specific = Path.Combine(testsPath, suiteFilter);
            suiteDirs = Directory.Exists(specific) ? [specific] : [];
        }
        else
        {
            suiteDirs = Directory.GetDirectories(testsPath);
        }

        var parser = new TestCaseParser();
        foreach (var suiteDir in suiteDirs)
        {
            var suiteName = Path.GetFileName(suiteDir);
            var mdFiles = Directory.GetFiles(suiteDir, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith('_'));

            foreach (var file in mdFiles)
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var rel = Path.GetRelativePath(testsPath, file);
                var parsed = parser.Parse(content, rel);
                if (!parsed.IsSuccess || parsed.Value is null) continue;

                var tc = parsed.Value;
                if (tc.Grounding is { FlaggedForReview: true })
                {
                    results.Add(new FlaggedTest
                    {
                        Id = tc.Id,
                        Suite = suiteName,
                        Title = tc.Title,
                        FilePath = file,
                        Score = tc.Grounding.Score,
                        RepairAttempts = tc.Grounding.RepairAttempts,
                        CondensedFindings = tc.Grounding.CondensedFindings
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Accept a flagged test as-is: clears flagged_for_review while keeping the partial verdict.
    /// </summary>
    public async Task<bool> AcceptAsync(FlaggedTest test, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(test.FilePath, ct);
        var rel = Path.GetRelativePath(Path.Combine(_currentDir, _testsDir), test.FilePath);
        var parsed = new TestCaseParser().Parse(content, rel);
        if (!parsed.IsSuccess || parsed.Value is null) return false;

        var tc = parsed.Value;
        if (tc.Grounding is null) return false;

        // Clear the flagged_for_review flag (GroundingMetadata is a record — with-expression works)
        var updatedGrounding = tc.Grounding with { FlaggedForReview = false };
        var updated = CopyWithGrounding(tc, updatedGrounding);

        await new TestFileWriter().WriteAsync(test.FilePath, updated, ct);
        return true;
    }

    /// <summary>
    /// Delete a flagged test: record drop trail then three-phase clean delete.
    /// </summary>
    public async Task<bool> DeleteAsync(FlaggedTest test, CancellationToken ct)
    {
        var trail = new DroppedTestsTrail(_currentDir);
        var entry = new DroppedTestEntry
        {
            Id = test.Id,
            Suite = test.Suite,
            Title = test.Title,
            DropReason = "user_decided",
            ContradictingClaim = null,
            DocRef = null,
            CriticModel = null,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Source = "review"
        };

        try
        {
            await trail.AppendAsync(entry, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: failed to write drop trail for {test.Id}: {ex.Message}");
            // Do not abort — log and continue to delete
        }

        // Three-phase delete via DeleteHandler
        var testsDir = Path.Combine(_currentDir, _testsDir);
        var suitePath = Path.Combine(testsDir, test.Suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);
        var indexWriter = new IndexWriter();
        var index = await indexWriter.ReadAsync(indexPath, ct);

        if (index is not null)
        {
            var updated = new MetadataIndex
            {
                Suite = index.Suite,
                GeneratedAt = DateTime.UtcNow,
                Tests = index.Tests.Where(t => !string.Equals(t.Id, test.Id, StringComparison.OrdinalIgnoreCase)).ToList()
            };
            await indexWriter.WriteAsync(indexPath, updated, ct);
        }

        if (File.Exists(test.FilePath))
            File.Delete(test.FilePath);

        return true;
    }

    private static TestCase CopyWithGrounding(TestCase original, Core.Models.Grounding.GroundingMetadata grounding) => new()
    {
        Id = original.Id,
        FilePath = original.FilePath,
        Priority = original.Priority,
        Tags = original.Tags,
        Component = original.Component,
        Description = original.Description,
        Preconditions = original.Preconditions,
        Environment = original.Environment,
        EstimatedDuration = original.EstimatedDuration,
        DependsOn = original.DependsOn,
        SourceRefs = original.SourceRefs,
        ScenarioFromDoc = original.ScenarioFromDoc,
        RelatedWorkItems = original.RelatedWorkItems,
        Custom = original.Custom,
        AutomatedBy = original.AutomatedBy,
        Requirements = original.Requirements,
        Criteria = original.Criteria,
        Bugs = original.Bugs,
        Status = original.Status,
        OrphanedReason = original.OrphanedReason,
        OrphanedDate = original.OrphanedDate,
        Title = original.Title,
        Steps = original.Steps,
        ExpectedResult = original.ExpectedResult,
        TestData = original.TestData,
        Grounding = grounding
    };

    public static async Task<string> ResolveTestsDirAsync(string currentDir, CancellationToken ct)
    {
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath)) return "test-cases";
        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SpectraConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Tests?.Dir ?? "test-cases";
        }
        catch { return "test-cases"; }
    }
}
