using Spectra.CLI.Index;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Index;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Source;

/// <summary>
/// Service for building and maintaining the document index.
/// </summary>
public sealed class DocumentIndexService
{
    private readonly DocumentIndexExtractor _extractor = new();
    private readonly DocIndexManifestReader _manifestReader = new();
    private readonly DocIndexManifestWriter _manifestWriter = new();
    private readonly ChecksumStoreReader _checksumReader = new();
    private readonly ChecksumStoreWriter _checksumWriter = new();
    private readonly SuiteIndexFileWriter _suiteWriter = new();

    /// <summary>
    /// Gets the number of changed files vs the v2 checksum store.
    /// Returns (total, changed) counts useful for reporting (e.g., dry-run).
    /// </summary>
    public async Task<(int Total, int Changed)> GetUpdateStatsAsync(
        string basePath,
        SourceConfig config,
        CancellationToken ct = default)
    {
        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(basePath, config);
        var checksumsPath = Path.Combine(
            LegacyIndexMigrator.ResolveIndexDir(basePath, config),
            "_checksums.json");

        var files = DiscoverFiles(basePath, config, manifestPath);

        var storedChecksums = await _checksumReader.ReadAsync(checksumsPath, ct);
        if (storedChecksums is null)
        {
            return (files.Count, files.Count);
        }

        var changedCount = 0;
        foreach (var (absolutePath, relativePath) in files)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(absolutePath, ct);
            var hash = DocumentIndexExtractor.ComputeHash(content);

            if (!storedChecksums.Checksums.TryGetValue(relativePath, out var storedHash) || hash != storedHash)
            {
                changedCount++;
            }
        }

        return (files.Count, changedCount);
    }

    /// <summary>
    /// Projects a DocumentIndex to a DocumentMap for backward compatibility.
    /// </summary>
    public static DocumentMap ToDocumentMap(DocumentIndex index)
    {
        var entries = index.Entries.Select(e => new DocumentEntry
        {
            Path = e.Path,
            Title = e.Title,
            SizeKb = e.SizeKb,
            Headings = e.Sections.Select(s => s.Heading).ToList(),
            Preview = e.Sections.Count > 0
                ? e.Sections[0].Summary
                : ""
        }).ToList();

        return new DocumentMap
        {
            TotalSizeKb = entries.Sum(e => e.SizeKb),
            Documents = entries
        };
    }

    /// <summary>
    /// Resolves the index file path from config.
    /// </summary>
    public static string ResolveIndexPath(string basePath, SourceConfig config)
    {
        if (!string.IsNullOrEmpty(config.DocIndex))
        {
            return Path.IsPathRooted(config.DocIndex)
                ? config.DocIndex
                : Path.Combine(basePath, config.DocIndex);
        }

        return Path.Combine(basePath, config.LocalDir.TrimEnd('/', '\\'), "_index.md");
    }

    private static List<(string AbsolutePath, string RelativePath)> DiscoverFiles(
        string basePath, SourceConfig config, string indexPath)
    {
        var indexRelative = Path.GetRelativePath(basePath, indexPath).Replace('\\', '/');
        // Spec 040: also exclude every artifact produced by the v2 layout
        // (manifest, checksum store, and per-suite index files under groups/)
        // so the indexer does not recurse over its own output.
        var docIndexDir = LegacyIndexMigrator.ResolveIndexDir(basePath, config);
        var docIndexDirRelative = Path.GetRelativePath(basePath, docIndexDir)
            .Replace('\\', '/')
            .TrimEnd('/');
        var docIndexDirPrefix = docIndexDirRelative + "/";

        var discovery = new SourceDiscovery(config);
        return discovery.DiscoverWithRelativePaths(basePath)
            .Where(f =>
                !string.Equals(f.RelativePath, indexRelative, StringComparison.OrdinalIgnoreCase) &&
                !f.RelativePath.StartsWith(docIndexDirPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ──────────────────────────────────────────────────────────────────────
    // v2 layout (Spec 040)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds or refreshes the v2 documentation index layout
    /// (<c>_manifest.yaml</c> + <c>groups/{suite}.index.md</c> +
    /// <c>_checksums.json</c>) under <paramref name="sourceConfig"/>'s
    /// <see cref="SourceConfig.DocIndexDir"/>. Incremental: reuses existing
    /// per-doc entries whose checksum is unchanged.
    /// </summary>
    /// <param name="suiteFilter">When non-null, only suites whose IDs appear in
    /// this list are rewritten; other suite files are left untouched on disk
    /// (their entries are still represented in the manifest, sourced from the
    /// existing manifest). Pass null for a full rebuild of all suites.</param>
    public async Task<NewLayoutResult> EnsureNewLayoutAsync(
        string basePath,
        SourceConfig sourceConfig,
        CoverageConfig coverageConfig,
        bool forceRebuild = false,
        IReadOnlyList<string>? suiteFilter = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(sourceConfig);
        ArgumentNullException.ThrowIfNull(coverageConfig);

        var indexDir = LegacyIndexMigrator.ResolveIndexDir(basePath, sourceConfig);
        var manifestPath = LegacyIndexMigrator.ResolveManifestPath(basePath, sourceConfig);
        var checksumsPath = Path.Combine(indexDir, "_checksums.json");
        var groupsDir = Path.Combine(indexDir, "groups");

        Directory.CreateDirectory(groupsDir);

        var legacyResolved = ResolveIndexPath(basePath, sourceConfig);
        var files = DiscoverFiles(basePath, sourceConfig, legacyResolved);

        // Read existing manifest/checksums for incremental detection.
        DocIndexManifest? existingManifest = null;
        ChecksumStore? existingChecksums = null;
        if (!forceRebuild)
        {
            existingManifest = await _manifestReader.ReadAsync(manifestPath, ct);
            existingChecksums = await _checksumReader.ReadAsync(checksumsPath, ct);
        }

        // Compute current hashes + extract frontmatter for change detection
        // and per-doc suite override (Spec 040 §3.5 step 1, Phase 5).
        var currentHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var frontmatterSuites = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (absolutePath, relativePath) in files)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(absolutePath, ct);
            currentHashes[relativePath] = DocumentIndexExtractor.ComputeHash(content);

            var fmSuite = FrontmatterReader.ReadSuite(content);
            if (!string.IsNullOrEmpty(fmSuite))
            {
                frontmatterSuites[relativePath] = fmSuite;
            }
        }

        // Reuse unchanged entries from existing per-suite files.
        var reusableEntries = new Dictionary<string, DocumentIndexEntry>(StringComparer.Ordinal);
        if (existingManifest is not null && existingChecksums is not null)
        {
            var suiteFileReader = new SuiteIndexFileReader();
            foreach (var group in existingManifest.Groups)
            {
                var suiteFilePath = Path.Combine(indexDir, group.IndexFile);
                var suiteFile = await suiteFileReader.ReadAsync(suiteFilePath, group.Id, ct);
                if (suiteFile is null) continue;
                foreach (var entry in suiteFile.Entries)
                {
                    if (existingChecksums.Checksums.TryGetValue(entry.Path, out var oldHash) &&
                        currentHashes.TryGetValue(entry.Path, out var newHash) &&
                        string.Equals(oldHash, newHash, StringComparison.Ordinal))
                    {
                        // DocumentIndexEntry is a class with init-only properties; we
                        // can reuse the parsed entry as-is. Hashes are tracked in
                        // _checksums.json (currentHashes); the per-suite file no
                        // longer carries them.
                        reusableEntries[entry.Path] = entry;
                    }
                }
            }
        }

        // Build per-doc entries.
        var entries = new List<DocumentIndexEntry>(files.Count);
        foreach (var (absolutePath, relativePath) in files)
        {
            ct.ThrowIfCancellationRequested();
            if (reusableEntries.TryGetValue(relativePath, out var reused))
            {
                entries.Add(reused);
            }
            else
            {
                var entry = await _extractor.ExtractFromFileAsync(absolutePath, relativePath, ct);
                entries.Add(entry);
            }
        }
        entries.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

        // Resolve suites.
        var resolver = new SuiteResolver();
        var discovered = entries.Select(e => new DiscoveredDoc(
            RelativePath: e.Path,
            FrontmatterSuite: frontmatterSuites.TryGetValue(e.Path, out var fmSuite) ? fmSuite : null
        )).ToList();
        var resolution = resolver.Resolve(discovered, sourceConfig);

        // Group entries by suite.
        var entriesBySuite = new Dictionary<string, List<DocumentIndexEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var suiteId = resolution.Assignments[entry.Path];
            if (!entriesBySuite.TryGetValue(suiteId, out var list))
            {
                list = new List<DocumentIndexEntry>();
                entriesBySuite[suiteId] = list;
            }
            list.Add(entry);
        }

        // Build manifest groups + write per-suite files.
        var generatedAt = DateTimeOffset.UtcNow;
        var manifestGroups = new List<DocSuiteEntry>();
        var rewrittenSuites = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (suiteId, suiteEntries) in entriesBySuite.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var suiteTokens = suiteEntries.Sum(e => e.EstimatedTokens);
            var (excludedBy, excludedPattern) = ClassifyExclusion(
                suiteEntries[0].Path, suiteId, coverageConfig.AnalysisExcludePatterns);
            var skipAnalysis = excludedBy != "none";

            // Phase 5 / Spec 040 §3.7: when a suite's tokens exceed the
            // configured threshold, write per-doc spillover files so consumers
            // (Spec 041 iterative analyzer) can load fine-grained slices.
            List<string>? spilloverPaths = null;
            if (suiteTokens > coverageConfig.MaxSuiteTokens && coverageConfig.MaxSuiteTokens > 0)
            {
                spilloverPaths = await WriteSpilloverFilesAsync(
                    suiteId, suiteEntries, generatedAt, indexDir, ct);
            }

            manifestGroups.Add(new DocSuiteEntry
            {
                Id = suiteId,
                Title = DeriveTitle(suiteId),
                Path = DerivePath(suiteEntries),
                DocumentCount = suiteEntries.Count,
                TokensEstimated = suiteTokens,
                SkipAnalysis = skipAnalysis,
                ExcludedBy = excludedBy,
                ExcludedPattern = excludedPattern,
                IndexFile = $"groups/{suiteId}.index.md",
                SpilloverFiles = spilloverPaths,
            });

            // Honor --suites filter: only rewrite the named suites; others stay
            // on disk untouched.
            if (suiteFilter is not null && !suiteFilter.Contains(suiteId, StringComparer.Ordinal))
            {
                continue;
            }

            var suiteFile = new SuiteIndexFile
            {
                SuiteId = suiteId,
                GeneratedAt = generatedAt,
                DocumentCount = suiteEntries.Count,
                TokensEstimated = suiteTokens,
                Entries = suiteEntries,
            };
            await _suiteWriter.WriteAsync(
                Path.Combine(indexDir, "groups", $"{suiteId}.index.md"),
                suiteFile,
                ct);
            rewrittenSuites.Add(suiteId);
        }

        // Build manifest.
        var manifest = new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = generatedAt,
            TotalDocuments = entries.Count,
            TotalWords = entries.Sum(e => e.WordCount),
            TotalTokensEstimated = entries.Sum(e => e.EstimatedTokens),
            Groups = manifestGroups,
        };

        await _manifestWriter.WriteAsync(manifestPath, manifest, ct);

        // Build checksum store from current hashes.
        var checksums = new ChecksumStore
        {
            Version = 2,
            GeneratedAt = generatedAt,
            Checksums = new Dictionary<string, string>(currentHashes, StringComparer.Ordinal),
        };
        await _checksumWriter.WriteAsync(checksumsPath, checksums, ct);

        // Compute incremental stats vs existing checksums.
        var (newDocs, changedDocs, unchangedDocs) = ComputeChangeStats(currentHashes, existingChecksums);

        return new NewLayoutResult(
            ManifestPath: manifestPath,
            Manifest: manifest,
            ChecksumsPath: checksumsPath,
            IndexDir: indexDir,
            NewDocuments: newDocs,
            ChangedDocuments: changedDocs,
            UnchangedDocuments: unchangedDocs,
            SkippedDocuments: manifestGroups
                .Where(g => g.SkipAnalysis)
                .Sum(g => g.DocumentCount),
            ResolutionWarnings: resolution.Errors);
    }

    private static (int New, int Changed, int Unchanged) ComputeChangeStats(
        IReadOnlyDictionary<string, string> currentHashes,
        ChecksumStore? existing)
    {
        if (existing is null)
        {
            return (currentHashes.Count, 0, 0);
        }

        var newCount = 0;
        var changedCount = 0;
        var unchangedCount = 0;
        foreach (var (path, hash) in currentHashes)
        {
            if (!existing.Checksums.TryGetValue(path, out var oldHash))
            {
                newCount++;
            }
            else if (!string.Equals(oldHash, hash, StringComparison.Ordinal))
            {
                changedCount++;
            }
            else
            {
                unchangedCount++;
            }
        }
        return (newCount, changedCount, unchangedCount);
    }

    private static (string ExcludedBy, string? ExcludedPattern) ClassifyExclusion(
        string docPath,
        string suiteId,
        IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0) return ("none", null);
        var matcher = new ExclusionPatternMatcher(patterns);
        return matcher.IsExcluded(docPath, out var matchedPattern)
            ? ("pattern", matchedPattern)
            : ("none", null);
    }

    /// <summary>
    /// Writes per-doc spillover files for a suite that exceeds the configured
    /// <c>coverage.max_suite_tokens</c> threshold (Spec 040 §3.7 / R-007).
    /// Each spillover file lives at
    /// <c>docs/_index/docs/&lt;sanitized-source-path&gt;.index.md</c> and contains
    /// a single-document <see cref="SuiteIndexFile"/>. Returns the list of
    /// source-document paths recorded into the manifest's
    /// <see cref="DocSuiteEntry.SpilloverFiles"/> field.
    /// </summary>
    private async Task<List<string>> WriteSpilloverFilesAsync(
        string suiteId,
        IReadOnlyList<DocumentIndexEntry> entries,
        DateTimeOffset generatedAt,
        string indexDir,
        CancellationToken ct)
    {
        var spilloverDir = Path.Combine(indexDir, "docs");
        Directory.CreateDirectory(spilloverDir);
        var paths = new List<string>(entries.Count);

        foreach (var entry in entries)
        {
            var sanitized = SanitizeSpilloverFileName(entry.Path);
            var spilloverPath = Path.Combine(spilloverDir, $"{sanitized}.index.md");
            await _suiteWriter.WriteAsync(
                spilloverPath,
                new SuiteIndexFile
                {
                    SuiteId = suiteId,
                    GeneratedAt = generatedAt,
                    DocumentCount = 1,
                    TokensEstimated = entry.EstimatedTokens,
                    Entries = new[] { entry },
                },
                ct);
            paths.Add(entry.Path);
        }

        return paths;
    }

    /// <summary>
    /// Sanitizes a repo-relative document path into a single filesystem-safe
    /// segment by replacing path separators with double underscores and
    /// stripping the <c>.md</c> extension. Reversible enough for debugging.
    /// </summary>
    internal static string SanitizeSpilloverFileName(string repoRelativePath)
    {
        var normalized = repoRelativePath.Replace('\\', '/');
        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }
        return normalized.Replace("/", "__");
    }

    private static string DeriveTitle(string suiteId)
    {
        if (suiteId == "_root") return "Root";
        return suiteId.Replace('_', ' ').Replace('-', ' ').Trim();
    }

    private static string DerivePath(IReadOnlyList<DocumentIndexEntry> entries)
    {
        if (entries.Count == 0) return string.Empty;
        var firstPath = entries[0].Path.Replace('\\', '/');
        var lastSlash = firstPath.LastIndexOf('/');
        return lastSlash >= 0 ? firstPath[..lastSlash] : string.Empty;
    }
}

/// <summary>
/// Result of <see cref="DocumentIndexService.EnsureNewLayoutAsync"/>.
/// </summary>
public sealed record NewLayoutResult(
    string ManifestPath,
    DocIndexManifest Manifest,
    string ChecksumsPath,
    string IndexDir,
    int NewDocuments,
    int ChangedDocuments,
    int UnchangedDocuments,
    int SkippedDocuments,
    IReadOnlyList<string> ResolutionWarnings);
