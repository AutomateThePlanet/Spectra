using Spectra.Core.Models;
using CoverageModels = Spectra.Core.Models.Coverage;

namespace Spectra.Core.Coverage;

/// <summary>
/// Reconciles links between tests and automation files.
/// Detects unlinked tests, orphaned automation, broken links, and mismatches.
/// </summary>
public sealed class LinkReconciler
{
    /// <summary>
    /// Reconciles test-to-automation links using bidirectional analysis.
    /// </summary>
    public ReconciliationResult Reconcile(
        IReadOnlyDictionary<string, MetadataIndex> suiteIndexes,
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles)
    {
        // Build test → automation map (from automated_by field in tests)
        var testToAutomation = BuildTestToAutomationMap(suiteIndexes);

        // Build automation → test map (from attribute patterns in automation files)
        var automationToTests = BuildAutomationToTestsMap(automationFiles);

        // All known test IDs
        var allTestIds = new HashSet<string>(
            suiteIndexes.Values.SelectMany(idx => idx.Tests.Select(t => t.Id)),
            StringComparer.OrdinalIgnoreCase);

        // Detect issues
        var unlinkedTests = FindUnlinkedTests(suiteIndexes, testToAutomation);
        var orphanedAutomation = FindOrphanedAutomation(automationFiles, allTestIds);
        var brokenLinks = FindBrokenLinks(suiteIndexes, automationFiles);
        var mismatches = FindMismatches(testToAutomation, automationToTests, allTestIds);

        // Build valid links
        var validLinks = BuildValidLinks(testToAutomation, automationToTests, allTestIds, automationFiles);

        return new ReconciliationResult(
            testToAutomation,
            automationToTests,
            validLinks,
            unlinkedTests,
            orphanedAutomation,
            brokenLinks,
            mismatches);
    }

    /// <summary>
    /// Builds a map from test ID to automation file path.
    /// Prefers AutomatedBy field; falls back to SourceRefs + IsAutomationFile heuristic.
    /// </summary>
    private static Dictionary<string, string> BuildTestToAutomationMap(
        IReadOnlyDictionary<string, MetadataIndex> suiteIndexes)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var index in suiteIndexes.Values)
        {
            foreach (var test in index.Tests)
            {
                // Prefer automated_by field if present
                if (test.AutomatedBy.Count > 0)
                {
                    map[test.Id] = NormalizePath(test.AutomatedBy[0]);
                    continue;
                }

                // Fall back to source_refs with automation file heuristic
                var automationRef = test.SourceRefs
                    .FirstOrDefault(r => IsAutomationFile(r));

                if (!string.IsNullOrEmpty(automationRef))
                {
                    map[test.Id] = NormalizePath(automationRef);
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Builds a map from automation file to test IDs it references.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> BuildAutomationToTestsMap(
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles)
    {
        return automationFiles.ToDictionary(
            kvp => NormalizePath(kvp.Key),
            kvp => kvp.Value.ReferencedTestIds,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds tests without any automation link.
    /// </summary>
    private static List<CoverageModels.UnlinkedTest> FindUnlinkedTests(
        IReadOnlyDictionary<string, MetadataIndex> suiteIndexes,
        Dictionary<string, string> testToAutomation)
    {
        var unlinked = new List<CoverageModels.UnlinkedTest>();

        foreach (var (suite, index) in suiteIndexes)
        {
            foreach (var test in index.Tests)
            {
                if (!testToAutomation.ContainsKey(test.Id))
                {
                    unlinked.Add(new CoverageModels.UnlinkedTest
                    {
                        TestId = test.Id,
                        Suite = suite,
                        Title = test.Title,
                        Priority = test.Priority
                    });
                }
            }
        }

        return unlinked.OrderBy(t => t.Suite).ThenBy(t => t.TestId).ToList();
    }

    /// <summary>
    /// Finds automation files that reference non-existent tests.
    /// </summary>
    private static List<CoverageModels.OrphanedAutomation> FindOrphanedAutomation(
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles,
        HashSet<string> allTestIds)
    {
        var orphaned = new List<CoverageModels.OrphanedAutomation>();

        foreach (var (file, info) in automationFiles)
        {
            var missingIds = info.ReferencedTestIds
                .Where(id => !allTestIds.Contains(id))
                .ToList();

            if (missingIds.Count > 0)
            {
                var lineNumbers = info.References
                    .Where(r => missingIds.Contains(r.TestId))
                    .Select(r => r.LineNumber)
                    .Distinct()
                    .ToList();

                orphaned.Add(new CoverageModels.OrphanedAutomation
                {
                    File = file,
                    ReferencedIds = missingIds,
                    LineNumbers = lineNumbers
                });
            }
        }

        return orphaned.OrderBy(o => o.File).ToList();
    }

    /// <summary>
    /// Finds tests with automated_by pointing to non-existent files.
    /// </summary>
    private static List<CoverageModels.BrokenLink> FindBrokenLinks(
        IReadOnlyDictionary<string, MetadataIndex> suiteIndexes,
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles)
    {
        // Normalize automation file keys for cross-platform comparison
        var normalizedAutomationFiles = automationFiles
            .ToDictionary(kvp => NormalizePath(kvp.Key), kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        var broken = new List<CoverageModels.BrokenLink>();

        foreach (var index in suiteIndexes.Values)
        {
            foreach (var test in index.Tests)
            {
                // Check automated_by field first
                foreach (var autoRef in test.AutomatedBy)
                {
                    if (!normalizedAutomationFiles.ContainsKey(NormalizePath(autoRef)))
                    {
                        broken.Add(new CoverageModels.BrokenLink
                        {
                            TestId = test.Id,
                            AutomatedBy = autoRef,
                            Reason = "File not found"
                        });
                    }
                }

                // Fall back to source_refs heuristic if no automated_by
                if (test.AutomatedBy.Count == 0)
                {
                    var automationRef = test.SourceRefs
                        .FirstOrDefault(r => IsAutomationFile(r));

                    if (!string.IsNullOrEmpty(automationRef) &&
                        !normalizedAutomationFiles.ContainsKey(NormalizePath(automationRef)))
                    {
                        broken.Add(new CoverageModels.BrokenLink
                        {
                            TestId = test.Id,
                            AutomatedBy = automationRef,
                            Reason = "File not found"
                        });
                    }
                }
            }
        }

        return broken.OrderBy(b => b.TestId).ToList();
    }

    /// <summary>
    /// Finds mismatched bidirectional links.
    /// </summary>
    private static List<CoverageModels.LinkMismatch> FindMismatches(
        Dictionary<string, string> testToAutomation,
        Dictionary<string, IReadOnlyList<string>> automationToTests,
        HashSet<string> allTestIds)
    {
        var mismatches = new List<CoverageModels.LinkMismatch>();

        // Check: test points to automation, but automation doesn't reference test
        foreach (var (testId, automationFile) in testToAutomation)
        {
            if (automationToTests.TryGetValue(automationFile, out var referencedTests))
            {
                if (!referencedTests.Contains(testId, StringComparer.OrdinalIgnoreCase))
                {
                    mismatches.Add(new CoverageModels.LinkMismatch
                    {
                        TestId = testId,
                        TestAutomatedBy = automationFile,
                        AutomationFile = automationFile,
                        Issue = "Test references automation file, but file doesn't reference test"
                    });
                }
            }
        }

        // Check: automation references test, but test doesn't point to automation
        foreach (var (automationFile, referencedTests) in automationToTests)
        {
            foreach (var testId in referencedTests)
            {
                if (allTestIds.Contains(testId) &&
                    (!testToAutomation.TryGetValue(testId, out var pointedFile) ||
                     !pointedFile.Equals(automationFile, StringComparison.OrdinalIgnoreCase)))
                {
                    mismatches.Add(new CoverageModels.LinkMismatch
                    {
                        TestId = testId,
                        TestAutomatedBy = testToAutomation.GetValueOrDefault(testId),
                        AutomationFile = automationFile,
                        Issue = "Automation references test, but test points elsewhere or nowhere"
                    });
                }
            }
        }

        return mismatches
            .DistinctBy(m => (m.TestId, m.AutomationFile))
            .OrderBy(m => m.TestId)
            .ToList();
    }

    /// <summary>
    /// Builds list of valid bidirectional links.
    /// </summary>
    private static List<CoverageModels.CoverageLink> BuildValidLinks(
        Dictionary<string, string> testToAutomation,
        Dictionary<string, IReadOnlyList<string>> automationToTests,
        HashSet<string> allTestIds,
        IReadOnlyDictionary<string, AutomationFileInfo> automationFiles)
    {
        // Normalize automation file keys for cross-platform comparison
        var normalizedAutomationFiles = automationFiles
            .ToDictionary(kvp => NormalizePath(kvp.Key), kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        var links = new List<CoverageModels.CoverageLink>();

        foreach (var (testId, automationFile) in testToAutomation)
        {
            var status = CoverageModels.LinkStatus.Valid;

            // Check if automation file exists
            if (!normalizedAutomationFiles.ContainsKey(automationFile))
            {
                status = CoverageModels.LinkStatus.Broken;
            }
            // Check if automation references test back
            else if (!automationToTests.TryGetValue(automationFile, out var refs) ||
                     !refs.Contains(testId, StringComparer.OrdinalIgnoreCase))
            {
                status = CoverageModels.LinkStatus.Mismatch;
            }

            links.Add(new CoverageModels.CoverageLink
            {
                Source = testId,
                Target = automationFile,
                Type = CoverageModels.LinkType.TestToAutomation,
                Status = status
            });
        }

        return links;
    }

    /// <summary>
    /// Checks if a reference looks like an automation file path.
    /// </summary>
    private static bool IsAutomationFile(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return false;

        var ext = Path.GetExtension(reference).ToLowerInvariant();
        return ext is ".cs" or ".ts" or ".js" or ".py" or ".java" or ".rb" or ".go";
    }

    /// <summary>
    /// Normalizes file path for cross-platform comparison.
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

/// <summary>
/// Result of link reconciliation.
/// </summary>
public sealed record ReconciliationResult(
    IReadOnlyDictionary<string, string> TestToAutomation,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AutomationToTests,
    IReadOnlyList<CoverageModels.CoverageLink> ValidLinks,
    IReadOnlyList<CoverageModels.UnlinkedTest> UnlinkedTests,
    IReadOnlyList<CoverageModels.OrphanedAutomation> OrphanedAutomation,
    IReadOnlyList<CoverageModels.BrokenLink> BrokenLinks,
    IReadOnlyList<CoverageModels.LinkMismatch> Mismatches)
{
    /// <summary>
    /// Total number of tests with valid automation links.
    /// </summary>
    public int AutomatedCount => ValidLinks.Count(l => l.Status == CoverageModels.LinkStatus.Valid);
}
