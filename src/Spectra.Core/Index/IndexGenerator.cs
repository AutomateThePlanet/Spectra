using Spectra.Core.Models;

namespace Spectra.Core.Index;

/// <summary>
/// Generates metadata indexes from test cases.
/// </summary>
public sealed class IndexGenerator
{
    /// <summary>
    /// Generates a MetadataIndex from a collection of test cases.
    /// </summary>
    public MetadataIndex Generate(string suiteName, IEnumerable<TestCase> testCases)
    {
        ArgumentNullException.ThrowIfNull(testCases);

        var entries = testCases
            .OrderBy(t => t.Id)
            .Select(CreateEntry)
            .ToList();

        return new MetadataIndex
        {
            Suite = suiteName,
            GeneratedAt = DateTime.UtcNow,
            Tests = entries
        };
    }

    /// <summary>
    /// Creates an index entry from a test case.
    /// </summary>
    public TestIndexEntry CreateEntry(TestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        return new TestIndexEntry
        {
            Id = testCase.Id,
            File = testCase.FilePath,
            Title = testCase.Title,
            Description = testCase.Description,
            Priority = testCase.Priority.ToString().ToLowerInvariant(),
            Tags = testCase.Tags.ToList(),
            Component = testCase.Component,
            EstimatedDuration = testCase.EstimatedDuration?.TotalMinutes switch
            {
                null => null,
                < 1 => $"{(int)testCase.EstimatedDuration.Value.TotalSeconds}s",
                < 60 => $"{(int)testCase.EstimatedDuration.Value.TotalMinutes}m",
                _ => $"{(int)testCase.EstimatedDuration.Value.TotalHours}h {testCase.EstimatedDuration.Value.Minutes}m"
            },
            DependsOn = testCase.DependsOn,
            SourceRefs = testCase.SourceRefs.ToList(),
            AutomatedBy = testCase.AutomatedBy,
            Requirements = testCase.Requirements
        };
    }

    /// <summary>
    /// Updates an existing index with new test cases.
    /// </summary>
    public MetadataIndex Update(MetadataIndex existing, IEnumerable<TestCase> newTests)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(newTests);

        var existingIds = new HashSet<string>(existing.Tests.Select(t => t.Id));
        var newEntries = new List<TestIndexEntry>(existing.Tests);

        foreach (var test in newTests)
        {
            if (existingIds.Contains(test.Id))
            {
                // Update existing entry
                var index = newEntries.FindIndex(e => e.Id == test.Id);
                if (index >= 0)
                {
                    newEntries[index] = CreateEntry(test);
                }
            }
            else
            {
                // Add new entry
                newEntries.Add(CreateEntry(test));
            }
        }

        return new MetadataIndex
        {
            Suite = existing.Suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = newEntries.OrderBy(e => e.Id).ToList()
        };
    }

    /// <summary>
    /// Removes orphaned entries (tests that no longer exist).
    /// </summary>
    public MetadataIndex RemoveOrphans(MetadataIndex existing, IEnumerable<string> validIds)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(validIds);

        var validIdSet = new HashSet<string>(validIds);
        var filteredEntries = existing.Tests
            .Where(e => validIdSet.Contains(e.Id))
            .ToList();

        return new MetadataIndex
        {
            Suite = existing.Suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = filteredEntries
        };
    }
}
