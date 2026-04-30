using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// Builds a <see cref="CoverageSnapshot"/> from the suite's _index.json,
/// .criteria.yaml files, and docs/_index.md. Each data source is independent —
/// missing sources produce partial snapshots without errors.
/// </summary>
public sealed class CoverageSnapshotBuilder
{
    private readonly string _basePath;

    public CoverageSnapshotBuilder(string basePath)
    {
        _basePath = basePath;
    }

    /// <summary>
    /// Builds a coverage snapshot for the given suite.
    /// </summary>
    /// <param name="suite">Suite name (directory under test-cases/).</param>
    /// <param name="testsDir">Root test-cases directory.</param>
    /// <param name="criteriaDir">Criteria directory (e.g., docs/criteria).</param>
    /// <param name="criteriaIndexFile">Path to _criteria_index.yaml.</param>
    /// <param name="docIndexFile">Path to docs/_index.md.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<CoverageSnapshot> BuildAsync(
        string suite,
        string testsDir,
        string criteriaDir,
        string criteriaIndexFile,
        string docIndexFile,
        CancellationToken ct = default)
    {
        // Read all three data sources concurrently
        var indexTask = ReadSuiteIndexAsync(suite, testsDir, ct);
        var criteriaTask = ReadAllCriteriaAsync(criteriaDir, criteriaIndexFile, ct);
        var docRefsTask = ReadDocSectionRefsAsync(docIndexFile, ct);

        await Task.WhenAll(indexTask, criteriaTask, docRefsTask);

        var (testTitles, testCriteriaIds, testSourceRefs) = indexTask.Result;
        var allCriteria = criteriaTask.Result;
        var allDocRefs = docRefsTask.Result;

        // Cross-reference criteria
        var coveredCriteriaIds = testCriteriaIds;
        var uncoveredCriteria = allCriteria
            .Where(c => !coveredCriteriaIds.Contains(c.Id))
            .Select(c => new UncoveredCriterion(c.Id, c.Text, c.SourceDoc ?? c.Source, c.Priority))
            .ToList();

        // Cross-reference source refs
        var coveredSourceRefs = testSourceRefs;
        var uncoveredSourceRefs = allDocRefs
            .Where(r => !coveredSourceRefs.Contains(r))
            .ToList();

        return new CoverageSnapshot
        {
            ExistingTestCount = testTitles.Count,
            ExistingTestTitles = testTitles,
            CoveredCriteriaIds = coveredCriteriaIds,
            UncoveredCriteria = uncoveredCriteria,
            CoveredSourceRefs = coveredSourceRefs,
            UncoveredSourceRefs = uncoveredSourceRefs,
            TotalCriteriaCount = allCriteria.Count
        };
    }

    /// <summary>
    /// Reads _index.json for the suite. Returns test titles, covered criteria IDs,
    /// and covered source refs. Returns empty data if index doesn't exist.
    /// </summary>
    internal static async Task<(List<string> titles, HashSet<string> criteriaIds, HashSet<string> sourceRefs)>
        ReadSuiteIndexAsync(string suite, string testsDir, CancellationToken ct)
    {
        var titles = new List<string>();
        var criteriaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var indexPath = Path.Combine(testsDir, suite, "_index.json");
            var indexWriter = new IndexWriter();
            var index = await indexWriter.ReadAsync(indexPath, ct);
            if (index is null)
                return (titles, criteriaIds, sourceRefs);

            foreach (var test in index.Tests)
            {
                titles.Add(test.Title);

                foreach (var criterionId in test.Criteria)
                    criteriaIds.Add(criterionId);

                foreach (var sourceRef in test.SourceRefs)
                    sourceRefs.Add(sourceRef);
            }
        }
        catch
        {
            // Graceful fallback — return empty data
        }

        return (titles, criteriaIds, sourceRefs);
    }

    /// <summary>
    /// Reads all acceptance criteria from .criteria.yaml files in the criteria directory.
    /// Returns empty list if directory or files don't exist.
    /// </summary>
    internal static async Task<List<AcceptanceCriterion>> ReadAllCriteriaAsync(
        string criteriaDir,
        string criteriaIndexFile,
        CancellationToken ct)
    {
        var allCriteria = new List<AcceptanceCriterion>();

        try
        {
            if (!Directory.Exists(criteriaDir))
                return allCriteria;

            var reader = new CriteriaFileReader();
            var criteriaFiles = Directory.GetFiles(criteriaDir, "*.criteria.yaml", SearchOption.AllDirectories);
            foreach (var file in criteriaFiles)
            {
                var criteria = await reader.ReadAsync(file, ct);
                allCriteria.AddRange(criteria);
            }
        }
        catch
        {
            // Graceful fallback — return whatever was loaded
        }

        return allCriteria;
    }

    /// <summary>
    /// Reads doc section references from the document index.
    /// Returns section paths/headings that can be cross-referenced against test source_refs.
    /// Returns empty list if index doesn't exist.
    /// </summary>
    internal static async Task<List<string>> ReadDocSectionRefsAsync(
        string docIndexFile, CancellationToken ct)
    {
        var refs = new List<string>();

        try
        {
            // Spec 040 Phase 4: prefer the v2 manifest layout. Walk every
            // suite's index file (regardless of skip_analysis — coverage
            // considers all documents per FR-018). Falls back to the legacy
            // single-file path when the manifest is absent.
            var indexDir = Path.GetDirectoryName(docIndexFile);
            var v2Manifest = string.IsNullOrEmpty(indexDir)
                ? null
                : Path.Combine(indexDir, "_index", "_manifest.yaml");

            if (!string.IsNullOrEmpty(v2Manifest) && File.Exists(v2Manifest))
            {
                var manifest = await new DocIndexManifestReader().ReadAsync(v2Manifest, ct);
                if (manifest is not null)
                {
                    var v2IndexDir = Path.GetDirectoryName(v2Manifest)!;
                    var suiteReader = new SuiteIndexFileReader();
                    foreach (var group in manifest.Groups)
                    {
                        var suiteFile = await suiteReader.ReadAsync(
                            Path.Combine(v2IndexDir, group.IndexFile),
                            group.Id,
                            ct);
                        if (suiteFile is null) continue;
                        foreach (var entry in suiteFile.Entries)
                        {
                            refs.Add(entry.Path);
                            foreach (var section in entry.Sections)
                            {
                                refs.Add($"{entry.Path}#{section.Heading}");
                            }
                        }
                    }
                    return refs;
                }
            }

            if (!File.Exists(docIndexFile))
                return refs;

            var content = await File.ReadAllTextAsync(docIndexFile, ct);
            var docIndex = DocumentIndexReader.ParseFull(content);
            if (docIndex is null)
                return refs;

            foreach (var entry in docIndex.Entries)
            {
                // Add the document path itself as a source ref
                refs.Add(entry.Path);

                // Add section headings qualified by document path
                foreach (var section in entry.Sections)
                {
                    refs.Add($"{entry.Path}#{section.Heading}");
                }
            }
        }
        catch
        {
            // Graceful fallback — return whatever was loaded
        }

        return refs;
    }
}
