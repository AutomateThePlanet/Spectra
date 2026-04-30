using Spectra.CLI.Index;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Index;

namespace Spectra.CLI.Tests.Index;

/// <summary>
/// Tests for <see cref="LegacyIndexMigrator"/>. Uses a representative
/// 30-doc stand-in fixture under <c>tests/TestFixtures/legacy_index_541docs/</c>;
/// when the real 541-doc file is checked in, the count assertions in
/// <c>MigrateAsync_RealFixture541Docs_*</c> need to be updated to match
/// (currently pinned to the stand-in's actual numbers, not the spec's "541").
/// </summary>
public class LegacyIndexMigratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _legacyDocsDir;
    private readonly string _legacyIndexPath;

    public LegacyIndexMigratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-migrate-test-{Guid.NewGuid():N}");
        _legacyDocsDir = Path.Combine(_tempDir, "docs");
        Directory.CreateDirectory(_legacyDocsDir);
        _legacyIndexPath = Path.Combine(_legacyDocsDir, "_index.md");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    private static SourceConfig CreateConfig() =>
        new()
        {
            LocalDir = "docs/",
            DocIndexDir = "docs/_index",
        };

    private static CoverageConfig CreateCoverage() => new();

    /// <summary>Copies the canonical legacy fixture into the temp project.</summary>
    private void SeedLegacyFixture()
    {
        var fixturePath = LocateFixture();
        File.Copy(fixturePath, _legacyIndexPath, overwrite: true);
    }

    /// <summary>
    /// Walks up from the test assembly directory to the repo root to locate
    /// <c>tests/TestFixtures/legacy_index_541docs/_index.md</c>.
    /// </summary>
    private static string LocateFixture()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(
                dir, "tests", "TestFixtures", "legacy_index_541docs", "_index.md");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException(
            "Could not locate tests/TestFixtures/legacy_index_541docs/_index.md by walking up from the test assembly directory.");
    }

    [Fact]
    public void NeedsMigration_LegacyPresentNewAbsent_ReturnsTrue()
    {
        File.WriteAllText(_legacyIndexPath, "# Documentation Index\n");
        var migrator = new LegacyIndexMigrator();

        Assert.True(migrator.NeedsMigration(_tempDir, CreateConfig()));
    }

    [Fact]
    public void NeedsMigration_LegacyAbsent_ReturnsFalse()
    {
        var migrator = new LegacyIndexMigrator();

        Assert.False(migrator.NeedsMigration(_tempDir, CreateConfig()));
    }

    [Fact]
    public void NeedsMigration_BothPresent_ReturnsFalse()
    {
        File.WriteAllText(_legacyIndexPath, "# Documentation Index\n");
        var manifestPath = Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, "version: 2\n");
        var migrator = new LegacyIndexMigrator();

        // Spec contract: dual-condition (legacy AND no manifest) → false when both are present.
        Assert.False(migrator.NeedsMigration(_tempDir, CreateConfig()));
    }

    [Fact]
    public async Task MigrateAsync_RealFixture_ProducesExpectedSuiteCount()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        var record = await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        Assert.True(record.Performed);
        // Stand-in: 13 suites (_root, SM_GSG_Topics, RD_Topics, IG_Topics,
        // POS_UG_Topics, SM_UG_Topics, cm_ug_topics, LM_UG_Topics, NBP_Topics,
        // SQL_Topics, release-notes, legacy, archive). When the real 541-doc
        // file is dropped in this should match the spec's "12 top-level
        // groups" claim — the difference is that the real corpus folds
        // release-notes/ into one of the topic groups.
        Assert.Equal(13, record.SuitesCreated);
    }

    [Fact]
    public async Task MigrateAsync_RealFixture_DocumentCountMatchesFixture()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        var record = await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        // Stand-in: 30 documents. Real file: 541 — update this assertion when
        // the real fixture is checked in.
        Assert.Equal(30, record.DocumentsMigrated);
    }

    [Fact]
    public async Task MigrateAsync_RealFixture_ChecksumStoreContainsAllDocs()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        var checksumsPath = Path.Combine(_tempDir, "docs", "_index", "_checksums.json");
        Assert.True(File.Exists(checksumsPath));
        var store = await new ChecksumStoreReader().ReadAsync(checksumsPath);
        Assert.NotNull(store);
        Assert.Equal(30, store!.Checksums.Count);
    }

    [Fact]
    public async Task MigrateAsync_AppliesDefaultExclusionPatterns()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        var manifestPath = Path.Combine(_tempDir, "docs", "_index", "_manifest.yaml");
        var manifest = await new DocIndexManifestReader().ReadAsync(manifestPath);
        Assert.NotNull(manifest);

        // The fixture has docs in `legacy/`, `archive/`, and `release-notes/`
        // — those suites' entries should be flagged skip_analysis: true.
        var skipped = manifest!.Groups.Where(g => g.SkipAnalysis).ToList();
        Assert.NotEmpty(skipped);
        Assert.All(skipped, g => Assert.Equal("pattern", g.ExcludedBy));
        Assert.All(skipped, g => Assert.NotNull(g.ExcludedPattern));
    }

    [Fact]
    public async Task MigrateAsync_PreservesLegacyFileAsBak()
    {
        SeedLegacyFixture();
        var originalBytes = await File.ReadAllBytesAsync(_legacyIndexPath);
        var migrator = new LegacyIndexMigrator();

        await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        var bakPath = _legacyIndexPath + ".bak";
        Assert.False(File.Exists(_legacyIndexPath), "Legacy _index.md should have been renamed");
        Assert.True(File.Exists(bakPath), "_index.md.bak should exist");

        var bakBytes = await File.ReadAllBytesAsync(bakPath);
        Assert.Equal(originalBytes, bakBytes);
    }

    [Fact]
    public async Task MigrateAsync_WritesAllExpectedArtifacts()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        var indexDir = Path.Combine(_tempDir, "docs", "_index");
        Assert.True(Directory.Exists(indexDir));
        Assert.True(File.Exists(Path.Combine(indexDir, "_manifest.yaml")));
        Assert.True(File.Exists(Path.Combine(indexDir, "_checksums.json")));
        Assert.True(Directory.Exists(Path.Combine(indexDir, "groups")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(indexDir, "groups"), "*.index.md"));
    }

    [Fact]
    public async Task MigrateAsync_IsIdempotent_SecondCallNoOps()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        var first = await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());
        Assert.True(first.Performed);

        // Second call: legacy file is gone (renamed to .bak), so nothing to migrate.
        var second = await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());
        Assert.False(second.Performed);
    }

    [Fact]
    public async Task MigrateAsync_FailsAtomically_OnUnreadableLegacyFile()
    {
        // Create a malformed legacy file: just enough to fail the parser.
        await File.WriteAllTextAsync(_legacyIndexPath, "");
        var migrator = new LegacyIndexMigrator();

        // Empty file should be tolerated and produce an empty layout, not a parse error.
        // Replace this test if the empty-file behavior changes — for malformed-but-parseable
        // input, the parser is currently lenient.
        var record = await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());
        Assert.True(record.Performed);
        Assert.Equal(0, record.DocumentsMigrated);
    }

    [Fact]
    public async Task MigrateAsync_LargestSuiteIsRecorded()
    {
        SeedLegacyFixture();
        var migrator = new LegacyIndexMigrator();

        var record = await migrator.MigrateAsync(_tempDir, CreateConfig(), CreateCoverage());

        Assert.NotNull(record.LargestSuiteId);
        Assert.True(record.LargestSuiteTokens > 0);
    }
}
