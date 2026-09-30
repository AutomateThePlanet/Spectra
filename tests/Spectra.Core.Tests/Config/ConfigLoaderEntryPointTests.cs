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

    [Fact]
    public async Task Write_MissingSectionItReads_IsAnError_NamingTheSection()
    {
        WriteConfig("""{ "source": { "local_dir": "docs/" }, "ai": {} }""");

        var result = await ConfigLoader.LoadForWriteAsync(_root, ConfigSections.Tests);

        Assert.Null(result.Config);
        Assert.Equal("spectra.config.json is invalid: missing 'tests' section", result.Error);
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
}
