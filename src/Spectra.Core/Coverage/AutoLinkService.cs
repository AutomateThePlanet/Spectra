namespace Spectra.Core.Coverage;

/// <summary>
/// Matches automation scan results to tests, producing auto-link suggestions.
/// Does NOT write files — the CLI layer handles I/O.
/// </summary>
public sealed class AutoLinkService
{
    /// <summary>
    /// Generates auto-link results from scanner output and test metadata.
    /// Returns a list of (testId, suite, testFilePath, automationFilePath) tuples.
    /// </summary>
    public IReadOnlyList<AutoLinkResult> GenerateLinks(
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles,
        IReadOnlyDictionary<string, (string Suite, string FilePath)> testFileMap)
    {
        var results = new List<AutoLinkResult>();
        var seen = new HashSet<(string TestId, string AutoFile)>(
            new AutoLinkComparer());

        foreach (var (autoFile, info) in automationFiles)
        {
            foreach (var testId in info.ReferencedTestIds)
            {
                if (!testFileMap.TryGetValue(testId, out var testInfo))
                {
                    continue;
                }

                var key = (testId, autoFile);
                if (!seen.Add(key))
                {
                    continue;
                }

                results.Add(new AutoLinkResult(
                    testId,
                    testInfo.Suite,
                    testInfo.FilePath,
                    autoFile));
            }
        }

        return results.OrderBy(r => r.TestId).ToList();
    }

    private sealed class AutoLinkComparer
        : IEqualityComparer<(string TestId, string AutoFile)>
    {
        public bool Equals((string TestId, string AutoFile) x, (string TestId, string AutoFile) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.TestId, y.TestId) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.AutoFile, y.AutoFile);

        public int GetHashCode((string TestId, string AutoFile) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TestId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AutoFile));
    }
}

/// <summary>
/// Result of auto-linking a test to an automation file.
/// </summary>
public sealed record AutoLinkResult(
    string TestId,
    string Suite,
    string TestFilePath,
    string AutomationFilePath);
