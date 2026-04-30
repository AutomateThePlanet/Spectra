using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Spectra.CLI.Agent.Tools;
using Spectra.Core.IdAllocation;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Validation;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// AIFunction definitions for test index access tools.
/// Provides test deduplication and ID allocation capabilities for the agent loop.
/// </summary>
public sealed class TestIndexTools
{
    private readonly ToolRegistry _registry;
    private readonly string _testsPath;
    private readonly PersistentTestIdAllocator _idAllocator;
    private readonly int _idStart;
    private readonly IReadOnlyList<TestCase> _existingTests;

    public TestIndexTools(
        ToolRegistry registry,
        string testsPath,
        PersistentTestIdAllocator idAllocator,
        int idStart,
        IReadOnlyList<TestCase> existingTests)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _testsPath = testsPath ?? throw new ArgumentNullException(nameof(testsPath));
        _idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
        _idStart = idStart;
        _existingTests = existingTests ?? throw new ArgumentNullException(nameof(existingTests));
    }

    /// <summary>
    /// Creates AIFunctions for test index access.
    /// </summary>
    public IEnumerable<AIFunction> CreateFunctions()
    {
        yield return AIFunctionFactory.Create(
            ReadTestIndex,
            nameof(ReadTestIndex),
            "Reads the test index for a suite, showing all existing tests with their IDs, titles, and coverage. " +
            "Use this to avoid generating duplicate tests.");

        yield return AIFunctionFactory.Create(
            CheckDuplicates,
            nameof(CheckDuplicates),
            "Checks if proposed test titles are semantically similar to existing tests. " +
            "Returns similarity scores to help avoid duplicates. " +
            "Use this before finalizing test generation.");

        yield return AIFunctionFactory.Create(
            GetNextTestIds,
            nameof(GetNextTestIds),
            "Allocates the next available test IDs in TC-XXX format. " +
            "Ensures globally unique IDs across all test suites. " +
            "Call this to get IDs for new tests you're about to generate.");

        yield return AIFunctionFactory.Create(
            GetExistingTestDetails,
            nameof(GetExistingTestDetails),
            "Gets detailed information about existing tests including steps, expected results, and source references. " +
            "Use this for semantic comparison when checking for duplicates.");
    }

    /// <summary>
    /// Reads the test index for a suite.
    /// </summary>
    [Description("Reads the test index for a suite, showing all existing tests.")]
    public async Task<string> ReadTestIndex(
        [Description("Name of the test suite (e.g., 'checkout', 'authentication')")] string suiteName,
        CancellationToken ct = default)
    {
        var suitePath = Path.Combine(_testsPath, suiteName);
        var result = await _registry.ReadTestIndex.ExecuteAsync(suitePath, ct);

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error ?? "Failed to read test index"
            });
        }

        // Use the correct property names: Tests instead of Index
        return JsonSerializer.Serialize(new
        {
            success = true,
            suite = suiteName,
            index_exists = result.IndexExists,
            suite_name = result.SuiteName,
            total_tests = result.Tests?.Count ?? 0,
            tests = result.Tests?.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                priority = t.Priority,
                tags = t.Tags,
                source_refs = t.SourceRefs,
                file_path = t.FilePath
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Checks proposed tests for duplicates against existing tests.
    /// </summary>
    [Description("Checks if proposed test titles are semantically similar to existing tests.")]
    public Task<string> CheckDuplicates(
        [Description("Array of proposed test titles to check")] string[] proposedTitles,
        CancellationToken ct = default)
    {
        var results = new List<object>();

        foreach (var title in proposedTitles)
        {
            var duplicates = new List<object>();

            foreach (var existing in _existingTests)
            {
                var similarity = CalculateSimilarity(title, existing.Title);
                if (similarity > 0.6) // 60% threshold
                {
                    duplicates.Add(new
                    {
                        existing_id = existing.Id,
                        existing_title = existing.Title,
                        similarity_score = Math.Round(similarity, 2),
                        is_likely_duplicate = similarity > 0.8
                    });
                }
            }

            results.Add(new
            {
                proposed_title = title,
                has_duplicates = duplicates.Count > 0,
                potential_duplicates = duplicates.OrderByDescending(d =>
                    ((dynamic)d).similarity_score).Take(3)
            });
        }

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            success = true,
            checks = results
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Allocates the next available test IDs.
    /// </summary>
    [Description("Allocates the next available test IDs in TC-XXX format.")]
    public async Task<string> GetNextTestIds(
        [Description("Number of IDs to allocate (1-100)")] int count = 1,
        CancellationToken ct = default)
    {
        if (count < 1 || count > 100)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Count must be between 1 and 100"
            });
        }

        // Spec 040: persistent allocator owns the cross-process file lock and
        // high-water-mark, so two concurrent generation runs cannot allocate
        // overlapping ID ranges. The lock is held only for the duration of
        // this call, not the whole generation pass.
        var ids = await _idAllocator.AllocateAsync(count, "TC", _idStart, "ai generate", ct).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            success = true,
            allocated_ids = ids,
            count = ids.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Gets detailed information about existing tests.
    /// </summary>
    [Description("Gets detailed information about existing tests for semantic comparison.")]
    public Task<string> GetExistingTestDetails(
        [Description("Filter by component name (optional)")] string? component = null,
        [Description("Maximum number of tests to return")] int limit = 50,
        CancellationToken ct = default)
    {
        var tests = _existingTests.AsEnumerable();

        if (!string.IsNullOrEmpty(component))
        {
            tests = tests.Where(t =>
                t.Component?.Contains(component, StringComparison.OrdinalIgnoreCase) == true);
        }

        var details = tests.Take(limit).Select(t => new
        {
            id = t.Id,
            title = t.Title,
            priority = t.Priority.ToString().ToLowerInvariant(),
            component = t.Component,
            tags = t.Tags,
            steps = t.Steps,
            expected_result = t.ExpectedResult,
            source_refs = t.SourceRefs,
            scenario_from_doc = t.ScenarioFromDoc
        });

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            success = true,
            total_in_suite = _existingTests.Count,
            returned = details.Count(),
            tests = details
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Factory method to create TestIndexTools with required dependencies.
    /// </summary>
    /// <remarks>
    /// Spec 040: <paramref name="basePath"/> is the workspace root. The
    /// persistent allocator owns the cross-process file lock and HWM so two
    /// concurrent generation runs cannot allocate overlapping ID ranges.
    /// </remarks>
    public static TestIndexTools Create(
        string basePath,
        string testsPath,
        SpectraConfig config,
        IReadOnlyList<TestCase> existingTests,
        Action<string>? logInfo = null)
    {
        var registry = new ToolRegistry(basePath, config);
        var idAllocator = new PersistentTestIdAllocator(basePath, logInfo);
        return new TestIndexTools(registry, testsPath, idAllocator, config.Tests.IdStart, existingTests);
    }

    /// <summary>
    /// Calculates similarity between two strings using a simple word overlap algorithm.
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0;

        var words1 = Tokenize(s1);
        var words2 = Tokenize(s2);

        if (words1.Count == 0 || words2.Count == 0)
            return 0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0;
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', '-', '_', '.', ',', ':', ';', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Skip short words
            .ToHashSet();
    }
}

// Spec 040: legacy in-file `TestIdAllocator` removed — replaced by
// Spectra.Core.IdAllocation.PersistentTestIdAllocator which owns the
// cross-process file lock and high-water-mark so concurrent generation
// runs cannot allocate overlapping ID ranges.
