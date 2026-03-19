using Spectra.Core.Models;

namespace Spectra.Core.Index;

/// <summary>
/// Scans all test suites to collect existing test IDs globally.
/// Ensures new test IDs are unique across the entire repository.
/// </summary>
public sealed class GlobalIdScanner
{
    private readonly IndexWriter _indexWriter = new();

    /// <summary>
    /// Scans all _index.json files in the tests directory and returns all existing test IDs.
    /// </summary>
    /// <param name="testsPath">Path to the tests/ directory</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Set of all existing test IDs across all suites</returns>
    public async Task<HashSet<string>> ScanAllIdsAsync(string testsPath, CancellationToken ct = default)
    {
        var allIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(testsPath))
        {
            return allIds;
        }

        // Find all _index.json files
        var indexFiles = Directory.GetFiles(testsPath, "_index.json", SearchOption.AllDirectories);

        foreach (var indexFile in indexFiles)
        {
            var index = await _indexWriter.ReadAsync(indexFile, ct);
            if (index?.Tests is null)
            {
                continue;
            }

            foreach (var test in index.Tests)
            {
                if (!string.IsNullOrEmpty(test.Id))
                {
                    allIds.Add(test.Id);
                }
            }
        }

        return allIds;
    }

    /// <summary>
    /// Creates a TestIdAllocator initialized with all existing IDs from all suites.
    /// </summary>
    /// <param name="testsPath">Path to the tests/ directory</param>
    /// <param name="prefix">ID prefix (default: "TC")</param>
    /// <param name="startNumber">Starting number for new IDs (default: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>TestIdAllocator with all existing IDs reserved</returns>
    public async Task<TestIdAllocator> CreateGlobalAllocatorAsync(
        string testsPath,
        string prefix = "TC",
        int startNumber = 100,
        CancellationToken ct = default)
    {
        var existingIds = await ScanAllIdsAsync(testsPath, ct);
        return TestIdAllocator.FromExistingIds(existingIds, prefix, startNumber);
    }

    /// <summary>
    /// Gets the maximum test ID number across all suites.
    /// </summary>
    /// <param name="testsPath">Path to the tests/ directory</param>
    /// <param name="prefix">ID prefix to match (default: "TC")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Maximum ID number found, or 0 if none found</returns>
    public async Task<int> GetMaxIdNumberAsync(
        string testsPath,
        string prefix = "TC",
        CancellationToken ct = default)
    {
        var allIds = await ScanAllIdsAsync(testsPath, ct);
        var maxNumber = 0;

        foreach (var id in allIds)
        {
            var number = ExtractNumber(id, prefix);
            if (number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return maxNumber;
    }

    private static int ExtractNumber(string id, string prefix)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        // Handle IDs like "TC-123" or "TEST-001"
        var dashIndex = id.LastIndexOf('-');
        if (dashIndex < 0 || dashIndex >= id.Length - 1)
        {
            return 0;
        }

        // Check if prefix matches (case-insensitive)
        var idPrefix = id[..dashIndex];
        if (!idPrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var numberPart = id[(dashIndex + 1)..];
        return int.TryParse(numberPart, out var number) ? number : 0;
    }
}
