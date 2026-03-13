using Spectra.Core.Models;

namespace Spectra.CLI.Agent;

/// <summary>
/// Mock agent for development and testing.
/// Generates placeholder test cases without calling an AI service.
/// </summary>
public sealed class MockAgent : IAgentRuntime
{
    public string ProviderName => "mock";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests,
        CancellationToken ct = default)
    {
        var tests = new List<TestCase>();
        var existingIds = new HashSet<string>(existingTests.Select(t => t.Id));

        // Generate placeholder tests based on document entries
        var testNumber = GetNextTestNumber(existingTests);

        foreach (var entry in documentMap.Documents.Take(3)) // Limit to 3 per run
        {
            var id = $"TC-{testNumber:D3}";
            if (existingIds.Contains(id))
            {
                testNumber++;
                continue;
            }

            tests.Add(new TestCase
            {
                Id = id,
                Title = $"Test {entry.Title}",
                Priority = Priority.Medium,
                Steps =
                [
                    "Navigate to the feature",
                    "Perform the main action",
                    "Verify the expected behavior"
                ],
                ExpectedResult = $"The {entry.Title.ToLowerInvariant()} feature works as documented",
                FilePath = $"{id}.md",
                SourceRefs = [entry.Path],
                Tags = ["generated", "mock"]
            });

            testNumber++;
        }

        return Task.FromResult(new GenerationResult
        {
            Tests = tests,
            TokenUsage = new TokenUsage(100, 200, 300)
        });
    }

    private static int GetNextTestNumber(IReadOnlyList<TestCase> existingTests)
    {
        if (existingTests.Count == 0)
        {
            return 100;
        }

        var maxNumber = existingTests
            .Select(t => t.Id)
            .Where(id => id.StartsWith("TC-"))
            .Select(id =>
            {
                var numPart = id[3..];
                return int.TryParse(numPart, out var n) ? n : 0;
            })
            .DefaultIfEmpty(99)
            .Max();

        return maxNumber + 1;
    }
}
