using Spectra.Core.Config;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Tests.Config;

/// <summary>
/// Spec 080 A5: the two config entry points. Read commands run on defaults when the config is missing; write
/// commands refuse. A malformed or invalid config is an error for both, never a silent fallback to defaults.
/// </summary>
public sealed class ConfigLoaderEntryPointTests : IDisposable
{
    private const string FullConfig = """
        {
          "source": { "local_dir": "product-docs/" },
          "tests": { "dir": "specs/" },
          "ai": { }
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "spectra-cfg-" + Guid.NewGuid().ToString("N"));

    public ConfigLoaderEntryPointTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_root, ConfigLoader.ConfigFileName), json);

    [Fact]
    public async Task LoadForRead_MissingConfig_ReturnsDefaults()
    {
        var result = await ConfigLoader.LoadForReadAsync(_root);

        Assert.Null(result.Error);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Config);
        Assert.Equal(SpectraConfig.Default.Source.LocalDir, result.Config!.Source.LocalDir);
        Assert.Equal(SpectraConfig.Default.Tests.Dir, result.Config.Tests.Dir);
    }

    [Fact]
    public async Task LoadForWrite_MissingConfig_ReturnsMissingMessage()
    {
        var result = await ConfigLoader.LoadForWriteAsync(_root);

        Assert.Null(result.Config);
        Assert.False(result.IsSuccess);
        Assert.Equal(ConfigLoader.MissingConfigMessage, result.Error);
        Assert.Equal("No spectra.config.json found. Run 'spectra init' first.", result.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MalformedJson_IsAnError_ForBothEntryPoints(bool forWrite)
    {
        WriteConfig("{\"source\": {");

        var result = forWrite ? await ConfigLoader.LoadForWriteAsync(_root) : await ConfigLoader.LoadForReadAsync(_root);

        Assert.Null(result.Config);
        Assert.NotNull(result.Error);
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, result.Error);
        Assert.StartsWith("spectra.config.json is invalid: ", result.Error);
    }

    // Spec 081 C2: replaces 080's MissingAiSection_IsAnError test, which encoded the defect — no command reads `ai`.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingAiSection_IsAccepted_ForBothEntryPoints(bool forWrite)
    {
        WriteConfig("""{ "source": { "local_dir": "docs/" }, "tests": { "dir": "test-cases/" } }""");

        var result = forWrite ? await ConfigLoader.LoadForWriteAsync(_root) : await ConfigLoader.LoadForReadAsync(_root);

        Assert.Null(result.Error);
        Assert.NotNull(result.Config);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Read_MissingSectionItReads_UsesDefaults_AndWarns()
    {
        WriteConfig("""{ "source": { "local_dir": "docs/" }, "ai": {} }""");

        var result = await ConfigLoader.LoadForReadAsync(_root, ConfigSections.Tests);

        Assert.Null(result.Error);
        Assert.Equal("test-cases/", result.Config!.Tests.Dir);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("'tests'", warning);
    }

    // Spec 083: no section is ever required. (082's Write_MissingRequiredSection_IsAnError is removed — requiring
    // `coverage` broke every workspace created by `spectra init`, whose template had no `coverage`.) `reads:` is always
    // passed by name so the call means the same on 3594d62, where the second positional parameter was `requires`.
    [Fact]
    public async Task Write_MissingSectionItReads_Warns()
    {
        WriteConfig("""{ "source": {}, "coverage": { "criteria_dir": "req/" }, "ai": {} }""");

        var result = await ConfigLoader.LoadForWriteAsync(_root, reads: ConfigSections.Tests);

        Assert.Null(result.Error);
        Assert.Equal("test-cases/", result.Config!.Tests.Dir);
        Assert.Equal("req/", result.Config.Coverage.CriteriaDir);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("'tests'", warning);
    }

    // Spec 083: a missing `coverage` gives its built-in defaults silently, for every command (replaces 082's
    // Read_MissingCoverage_UsesDefaults_AndWarns — pre-083 workspaces have no `coverage`, so it warned on every run).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingCoverage_UsesDefaults_WithoutWarning(bool forWrite)
    {
        WriteConfig("""{ "source": {}, "tests": {}, "ai": {} }""");

        var result = forWrite
            ? await ConfigLoader.LoadForWriteAsync(_root, reads: ConfigSections.Source | ConfigSections.Tests)
            : await ConfigLoader.LoadForReadAsync(_root, reads: ConfigSections.Source | ConfigSections.Tests);

        Assert.Null(result.Error);
        Assert.Equal("docs/criteria", result.Config!.Coverage.CriteriaDir);
        Assert.Equal("docs/criteria/_criteria_index.yaml", result.Config.Coverage.CriteriaFile);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingSectionsItDoesNotRead_AreIgnored(bool forWrite)
    {
        WriteConfig("""{ "coverage": {} }""");

        var result = forWrite
            ? await ConfigLoader.LoadForWriteAsync(_root, ConfigSections.None)
            : await ConfigLoader.LoadForReadAsync(_root, ConfigSections.None);

        Assert.Null(result.Error);
        Assert.Empty(result.Warnings);
        Assert.Equal("docs/", result.Config!.Source.LocalDir);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrongSectionType_IsStillInvalid(bool forWrite)
    {
        WriteConfig("""{ "source": 5, "tests": {}, "ai": {} }""");

        var result = forWrite ? await ConfigLoader.LoadForWriteAsync(_root) : await ConfigLoader.LoadForReadAsync(_root);

        Assert.Null(result.Config);
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, result.Error);
    }

    [Fact]
    public async Task SectionKeys_MatchCaseInsensitively_AndCommentsAndTrailingCommasAreAllowed()
    {
        WriteConfig("""
            {
              // a comment
              "Source": { "local_dir": "product-docs/" },
              "TESTS": { "dir": "specs/" },
            }
            """);

        var result = await ConfigLoader.LoadForWriteAsync(_root, ConfigSections.Source | ConfigSections.Tests);

        Assert.Null(result.Error);
        Assert.Equal("product-docs/", result.Config!.Source.LocalDir);
        Assert.Equal("specs/", result.Config.Tests.Dir);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidConfig_IsLoaded_ForBothEntryPoints(bool forWrite)
    {
        WriteConfig(FullConfig);

        var result = forWrite ? await ConfigLoader.LoadForWriteAsync(_root) : await ConfigLoader.LoadForReadAsync(_root);

        Assert.Null(result.Error);
        Assert.Equal("product-docs/", result.Config!.Source.LocalDir);
        Assert.Equal("specs/", result.Config.Tests.Dir);
    }

    // ---------- Spec 082 A3: a section set to null is a missing section ----------

    private static readonly Dictionary<string, string> Sections = new()
    {
        ["source"] = "\"source\": { \"local_dir\": \"product-docs/\" }",
        ["tests"] = "\"tests\": { \"dir\": \"specs/\" }",
        ["coverage"] = "\"coverage\": { \"criteria_dir\": \"req/\" }",
        ["ai"] = "\"ai\": {}"
    };

    private static string ConfigWith(string section, string? value) =>
        "{ " + string.Join(", ", Sections
            .Where(kv => kv.Key != section || value is not null)
            .Select(kv => kv.Key == section ? $"\"{section}\": {value}" : kv.Value)) + " }";

    private async Task<(ConfigLoadResult Read, ConfigLoadResult Write)> LoadBothAsync(string json)
    {
        WriteConfig(json);
        return (await ConfigLoader.LoadForReadAsync(_root, reads: ConfigSections.Source | ConfigSections.Tests),
            await ConfigLoader.LoadForWriteAsync(_root, reads: ConfigSections.Tests));
    }

    private static void AssertSameOutcome(ConfigLoadResult missing, ConfigLoadResult nulled)
    {
        Assert.Equal(missing.Error, nulled.Error);
        Assert.Equal(missing.Warnings, nulled.Warnings);
        if (missing.Config is null)
        {
            Assert.Null(nulled.Config);
            return;
        }

        Assert.NotNull(nulled.Config);
        Assert.NotNull(nulled.Config!.Coverage);
        Assert.Equal(missing.Config.Source.LocalDir, nulled.Config.Source.LocalDir);
        Assert.Equal(missing.Config.Tests.Dir, nulled.Config.Tests.Dir);
        Assert.Equal(missing.Config.Coverage.CriteriaDir, nulled.Config.Coverage.CriteriaDir);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("tests")]
    [InlineData("coverage")]
    [InlineData("ai")]
    public async Task NullSection_BehavesLikeAMissingSection(string section)
    {
        var missing = await LoadBothAsync(ConfigWith(section, null));
        var nulled = await LoadBothAsync(ConfigWith(section, "null"));

        AssertSameOutcome(missing.Read, nulled.Read);
        AssertSameOutcome(missing.Write, nulled.Write);

        // Spec 083: only `source` and `tests` (when read) warn; `coverage` and `ai` are filled silently.
        var expected = section is "source" or "tests" ? 1 : 0;
        Assert.Equal(expected, nulled.Read.Warnings.Count);
        Assert.Null(nulled.Read.Error);
        Assert.Null(nulled.Write.Error);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    public async Task TopLevelNull_And_EmptyFile_AreInvalid(string content)
    {
        WriteConfig(content);

        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, (await ConfigLoader.LoadForReadAsync(_root)).Error);
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, (await ConfigLoader.LoadForWriteAsync(_root)).Error);
    }

    // ---------- Spec 082 A4: named config error codes ----------

    [Fact]
    public async Task ErrorCodes_NameEachConfigProblem()
    {
        Assert.Equal(ConfigLoader.ErrorConfigMissing, (await ConfigLoader.LoadForWriteAsync(_root)).ErrorCode);
        Assert.Equal("CONFIG_MISSING", ConfigLoader.ErrorConfigMissing);

        foreach (var content in new[] { "{\"source\": {", "{ \"source\": 5 }", "null", "" })
        {
            WriteConfig(content);
            Assert.Equal("CONFIG_INVALID", (await ConfigLoader.LoadForReadAsync(_root)).ErrorCode);
            Assert.Equal("CONFIG_INVALID", (await ConfigLoader.LoadForWriteAsync(_root)).ErrorCode);
        }

        // Spec 083: CONFIG_SECTION_MISSING no longer exists — a config without `coverage` loads for writes too.
        WriteConfig("{ \"source\": {}, \"tests\": {} }");
        var ok = await ConfigLoader.LoadForWriteAsync(_root, reads: ConfigSections.Tests);
        Assert.Null(ok.ErrorCode);
        Assert.Null(ok.Error);
    }

    // ---------- Spec 083 F3: duplicate top-level keys ----------

    [Theory]
    [InlineData("{ \"tests\": {}, \"Tests\": null }", "tests", "Tests")]
    [InlineData("{ \"generation\": {}, \"Generation\": {} }", "generation", "Generation")]
    public async Task CaseVariantDuplicateKeys_AreInvalid_NamingTheKey(string json, string first, string second)
    {
        WriteConfig(json);

        foreach (var result in new[] { await ConfigLoader.LoadForReadAsync(_root), await ConfigLoader.LoadForWriteAsync(_root) })
        {
            Assert.Null(result.Config);
            Assert.Equal("CONFIG_INVALID", result.ErrorCode);
            Assert.Contains($"'{first}'", result.Error);
            Assert.Contains($"'{second}'", result.Error);
        }
    }

    [Fact]
    public async Task ExactDuplicateKey_IsInvalid_NotAnException()
    {
        WriteConfig("{ \"source\": {}, \"source\": {} }");

        var read = await ConfigLoader.LoadForReadAsync(_root);
        var write = await ConfigLoader.LoadForWriteAsync(_root);

        Assert.Equal("CONFIG_INVALID", read.ErrorCode);
        Assert.Equal("CONFIG_INVALID", write.ErrorCode);
        Assert.StartsWith(ConfigLoader.InvalidConfigPrefix, read.Error);
    }

    // ---- Spec 084: the optional update-triage keys (FR-029) ----

    [Theory]
    [InlineData("")]
    [InlineData(", \"update\": null")]
    [InlineData(", \"update\": {}")]
    [InlineData(", \"update\": { \"group_size\": null, \"light_threshold_words\": null }")]
    [InlineData(", \"update\": { \"chunk_size\": 5 }")]
    public async Task Update_MissingOrNull_UsesTheDefaults_WithoutWarning(string update)
    {
        WriteConfig("{ \"source\": {}, \"tests\": {}, \"ai\": {}" + update + " }");

        var result = await ConfigLoader.LoadForWriteAsync(_root, reads: ConfigSections.Source | ConfigSections.Tests);

        Assert.Null(result.Error);
        Assert.Equal(10, result.Config!.Update.EffectiveGroupSize);
        Assert.Equal(12, result.Config.Update.EffectiveLightThresholdWords);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Update_Values_AreUsed()
    {
        WriteConfig("""{ "source": {}, "tests": {}, "update": { "group_size": 4, "light_threshold_words": 0 } }""");

        var result = await ConfigLoader.LoadForReadAsync(_root, reads: ConfigSections.None);

        Assert.Equal(4, result.Config!.Update.EffectiveGroupSize);
        Assert.Equal(0, result.Config.Update.EffectiveLightThresholdWords);
    }

    [Theory]
    [InlineData("{ \"group_size\": 0 }", "update.group_size")]
    [InlineData("{ \"group_size\": -3 }", "update.group_size")]
    [InlineData("{ \"group_size\": \"x\" }", "group_size")]
    [InlineData("{ \"group_size\": 1.5 }", "group_size")]
    [InlineData("{ \"light_threshold_words\": -1 }", "update.light_threshold_words")]
    public async Task Update_OutOfRangeOrWrongType_IsInvalid_NamingTheKey(string update, string key)
    {
        WriteConfig("{ \"source\": {}, \"tests\": {}, \"update\": " + update + " }");

        var result = await ConfigLoader.LoadForWriteAsync(_root, reads: ConfigSections.None);

        Assert.Equal(ConfigLoader.ErrorConfigInvalid, result.ErrorCode);
        Assert.Contains(key, result.Error);
    }
}
