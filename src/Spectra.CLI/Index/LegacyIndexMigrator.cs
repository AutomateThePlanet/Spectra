using Spectra.CLI.Results;
using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Index;

/// <summary>
/// One-time migration from the legacy single-file <c>docs/_index.md</c>
/// (Spec 010) to the v2 layout (Spec 040 §3.8).
/// </summary>
/// <remarks>
/// <para>Detection is dual-conditional: legacy file present AND v2 manifest
/// absent. Migration is idempotent on success — subsequent runs short-circuit
/// because the legacy file has been renamed to <c>_index.md.bak</c>.</para>
/// <para>Atomicity: writes the new layout under a sibling <c>_index.tmp/</c>
/// directory first; only after every artifact has landed is the directory
/// promoted via <see cref="Directory.Move"/> and the legacy file renamed to
/// <c>.bak</c>. Failure at any earlier step deletes the tmp directory and
/// leaves the legacy file byte-identical to its pre-run state.</para>
/// </remarks>
public sealed class LegacyIndexMigrator
{
    private const string TempDirSuffix = ".tmp";
    private const string LegacyBackupSuffix = ".bak";

    private static readonly DocumentIndexReader LegacyReader = new();
    private static readonly DocIndexManifestWriter ManifestWriter = new();
    private static readonly ChecksumStoreWriter ChecksumWriter = new();
    private static readonly SuiteIndexFileWriter SuiteWriter = new();

    /// <summary>
    /// True iff a legacy <c>_index.md</c> exists at the conventional location
    /// AND no <c>_manifest.yaml</c> exists in the v2 directory. Both conditions
    /// must hold; partial / mixed states return false (and the caller is
    /// expected to log a warning).
    /// </summary>
    public bool NeedsMigration(string basePath, SourceConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(config);

        var legacyPath = ResolveLegacyIndexPath(basePath, config);
        var manifestPath = ResolveManifestPath(basePath, config);

        return File.Exists(legacyPath) && !File.Exists(manifestPath);
    }

    /// <summary>
    /// Performs the migration. Throws <see cref="InvalidOperationException"/> if
    /// the legacy file is unreadable, with the legacy file untouched and no
    /// partial v2 layout left on disk.
    /// </summary>
    public async Task<MigrationRecord> MigrateAsync(
        string basePath,
        SourceConfig sourceConfig,
        CoverageConfig coverageConfig,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(sourceConfig);
        ArgumentNullException.ThrowIfNull(coverageConfig);

        var legacyPath = ResolveLegacyIndexPath(basePath, sourceConfig);
        if (!File.Exists(legacyPath))
        {
            return new MigrationRecord { Performed = false };
        }

        var indexDir = ResolveIndexDir(basePath, sourceConfig);

        // Parse legacy file.
        var legacyIndex = await LegacyReader.ReadFullAsync(legacyPath, ct)
            ?? throw new InvalidOperationException(
                $"Legacy documentation index at '{legacyPath}' could not be parsed. " +
                "Migration aborted; legacy file untouched.");

        if (legacyIndex.Entries.Count == 0)
        {
            // Empty legacy file → write an empty v2 layout and rename.
            return await WriteEmptyLayoutAndBackupAsync(
                basePath, legacyPath, indexDir, ct);
        }

        // Resolve suites for every entry (frontmatter values are not preserved
        // through the legacy index — only the relative path matters here).
        var resolver = new SuiteResolver();
        var discovered = legacyIndex.Entries
            .Select(e => new DiscoveredDoc(e.Path))
            .ToList();
        var resolution = resolver.Resolve(discovered, sourceConfig);

        var warnings = new List<string>();
        if (resolution.Errors.Count > 0)
        {
            warnings.AddRange(resolution.Errors);
        }

        // Group entries by suite.
        var entriesBySuite = new Dictionary<string, List<DocumentIndexEntry>>(StringComparer.Ordinal);
        foreach (var entry in legacyIndex.Entries)
        {
            var suiteId = resolution.Assignments[entry.Path];
            if (!entriesBySuite.TryGetValue(suiteId, out var list))
            {
                list = new List<DocumentIndexEntry>();
                entriesBySuite[suiteId] = list;
            }
            list.Add(entry);
        }

        // Build manifest entries.
        var generatedAt = DateTimeOffset.UtcNow;
        var manifestGroups = new List<DocSuiteEntry>();
        foreach (var (suiteId, entries) in entriesBySuite.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var suiteTokens = entries.Sum(e => e.EstimatedTokens);
            var (excludedBy, excludedPattern) = ClassifyExclusion(
                entries[0].Path, suiteId, coverageConfig.AnalysisExcludePatterns);

            var skipAnalysis = excludedBy != "none";

            manifestGroups.Add(new DocSuiteEntry
            {
                Id = suiteId,
                Title = DeriveTitle(suiteId),
                Path = DerivePath(entries),
                DocumentCount = entries.Count,
                TokensEstimated = suiteTokens,
                SkipAnalysis = skipAnalysis,
                ExcludedBy = excludedBy,
                ExcludedPattern = excludedPattern,
                IndexFile = $"groups/{suiteId}.index.md",
            });
        }

        var manifest = new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = generatedAt,
            TotalDocuments = legacyIndex.Entries.Count,
            TotalWords = legacyIndex.TotalWordCount,
            TotalTokensEstimated = legacyIndex.TotalEstimatedTokens,
            Groups = manifestGroups,
        };

        // Build checksum store from legacy entries' ContentHash.
        var checksums = new ChecksumStore
        {
            Version = 2,
            GeneratedAt = generatedAt,
            Checksums = new Dictionary<string, string>(StringComparer.Ordinal),
        };
        var missingHashCount = 0;
        foreach (var entry in legacyIndex.Entries)
        {
            if (string.IsNullOrEmpty(entry.ContentHash))
            {
                // Legacy file may be missing checksums for some entries; record but proceed.
                missingHashCount++;
                continue;
            }
            checksums.Checksums[entry.Path] = entry.ContentHash;
        }
        if (missingHashCount > 0)
        {
            warnings.Add(
                $"{missingHashCount} legacy entries had no checksum; they will be re-hashed on next 'spectra docs index' run.");
        }

        // Build per-suite SuiteIndexFile models.
        var suiteFiles = new Dictionary<string, SuiteIndexFile>(StringComparer.Ordinal);
        foreach (var (suiteId, entries) in entriesBySuite)
        {
            var suiteTokens = entries.Sum(e => e.EstimatedTokens);
            suiteFiles[suiteId] = new SuiteIndexFile
            {
                SuiteId = suiteId,
                GeneratedAt = generatedAt,
                DocumentCount = entries.Count,
                TokensEstimated = suiteTokens,
                Entries = entries,
            };
        }

        // ── Atomic write phase: write all artifacts to a sibling tmp directory,
        //    then promote it via Directory.Move, then rename the legacy file.
        var tmpDir = indexDir + TempDirSuffix;
        try
        {
            CleanDirectory(tmpDir);
            Directory.CreateDirectory(tmpDir);
            Directory.CreateDirectory(Path.Combine(tmpDir, "groups"));

            var manifestPath = Path.Combine(tmpDir, "_manifest.yaml");
            await ManifestWriter.WriteAsync(manifestPath, manifest, ct);

            var checksumsPath = Path.Combine(tmpDir, "_checksums.json");
            await ChecksumWriter.WriteAsync(checksumsPath, checksums, ct);

            foreach (var (suiteId, suiteFile) in suiteFiles)
            {
                var suitePath = Path.Combine(tmpDir, "groups", $"{suiteId}.index.md");
                await SuiteWriter.WriteAsync(suitePath, suiteFile, ct);
            }

            // Promote tmp dir to final location.
            if (Directory.Exists(indexDir))
            {
                // Defensive: should not happen because NeedsMigration guards on
                // manifest absence. Treat as a programming error.
                throw new InvalidOperationException(
                    $"Refusing to overwrite existing v2 index directory '{indexDir}'.");
            }
            Directory.Move(tmpDir, indexDir);
        }
        catch
        {
            // Best-effort cleanup; swallow secondary errors.
            try { CleanDirectory(tmpDir); } catch { }
            throw;
        }

        // Rename the legacy file last. If this fails after promotion, the v2
        // layout is in place — re-running the indexer will detect and either
        // reconcile or warn.
        var backupPath = legacyPath + LegacyBackupSuffix;
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
        File.Move(legacyPath, backupPath);

        // Largest suite by tokens.
        DocSuiteEntry? largest = null;
        foreach (var group in manifestGroups)
        {
            if (largest is null || group.TokensEstimated > largest.TokensEstimated)
            {
                largest = group;
            }
        }

        return new MigrationRecord
        {
            Performed = true,
            LegacyFile = ToRelative(basePath, backupPath),
            SuitesCreated = manifestGroups.Count,
            DocumentsMigrated = legacyIndex.Entries.Count,
            LargestSuiteId = largest?.Id,
            LargestSuiteTokens = largest?.TokensEstimated ?? 0,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Convenience accessor used by handlers that want to surface the
    /// resolved paths without re-deriving them.
    /// </summary>
    public static string ResolveLegacyIndexPath(string basePath, SourceConfig config)
    {
        if (!string.IsNullOrEmpty(config.DocIndex))
        {
            return Path.IsPathRooted(config.DocIndex)
                ? config.DocIndex
                : Path.Combine(basePath, config.DocIndex);
        }
        return Path.Combine(basePath, config.LocalDir.TrimEnd('/', '\\'), "_index.md");
    }

    public static string ResolveIndexDir(string basePath, SourceConfig config)
    {
        var dir = string.IsNullOrEmpty(config.DocIndexDir) ? "docs/_index" : config.DocIndexDir;
        return Path.IsPathRooted(dir) ? dir : Path.Combine(basePath, dir);
    }

    public static string ResolveManifestPath(string basePath, SourceConfig config) =>
        Path.Combine(ResolveIndexDir(basePath, config), "_manifest.yaml");

    private async Task<MigrationRecord> WriteEmptyLayoutAndBackupAsync(
        string basePath,
        string legacyPath,
        string indexDir,
        CancellationToken ct)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var manifest = new DocIndexManifest
        {
            Version = 2,
            GeneratedAt = generatedAt,
            TotalDocuments = 0,
            TotalWords = 0,
            TotalTokensEstimated = 0,
            Groups = new List<DocSuiteEntry>(),
        };

        var checksums = new ChecksumStore
        {
            Version = 2,
            GeneratedAt = generatedAt,
            Checksums = new Dictionary<string, string>(),
        };

        var tmpDir = indexDir + TempDirSuffix;
        try
        {
            CleanDirectory(tmpDir);
            Directory.CreateDirectory(tmpDir);
            Directory.CreateDirectory(Path.Combine(tmpDir, "groups"));
            await ManifestWriter.WriteAsync(Path.Combine(tmpDir, "_manifest.yaml"), manifest, ct);
            await ChecksumWriter.WriteAsync(Path.Combine(tmpDir, "_checksums.json"), checksums, ct);

            if (Directory.Exists(indexDir))
            {
                throw new InvalidOperationException(
                    $"Refusing to overwrite existing v2 index directory '{indexDir}'.");
            }
            Directory.Move(tmpDir, indexDir);
        }
        catch
        {
            try { CleanDirectory(tmpDir); } catch { }
            throw;
        }

        var backupPath = legacyPath + LegacyBackupSuffix;
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
        File.Move(legacyPath, backupPath);

        return new MigrationRecord
        {
            Performed = true,
            LegacyFile = ToRelative(basePath, backupPath),
            SuitesCreated = 0,
            DocumentsMigrated = 0,
        };
    }

    /// <summary>
    /// Phase-5: real glob matching via <see cref="ExclusionPatternMatcher"/>.
    /// </summary>
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

    private static string DeriveTitle(string suiteId)
    {
        if (suiteId == "_root") return "Root";
        // Replace underscores/dashes with spaces for legibility; keep casing.
        return suiteId.Replace('_', ' ').Replace('-', ' ').Trim();
    }

    private static string DerivePath(IReadOnlyList<DocumentIndexEntry> entries)
    {
        if (entries.Count == 0) return string.Empty;
        // Common parent directory of the entries.
        var firstPath = entries[0].Path.Replace('\\', '/');
        var lastSlash = firstPath.LastIndexOf('/');
        return lastSlash >= 0 ? firstPath[..lastSlash] : string.Empty;
    }

    private static string ToRelative(string basePath, string fullPath)
    {
        return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
    }

    private static void CleanDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
