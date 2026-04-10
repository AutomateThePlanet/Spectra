using Spectra.CLI.Profile;

namespace Spectra.CLI.Tests.Profile;

public class ProfileFormatLoaderTests : IDisposable
{
    private readonly string _testDir;

    public ProfileFormatLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "spectra-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void LoadFormat_ReturnsEmbeddedDefault_WhenNoFileExists()
    {
        var format = ProfileFormatLoader.LoadFormat(_testDir);

        Assert.False(string.IsNullOrWhiteSpace(format));
        // Embedded default contains the canonical fields used by SPECTRA test cases.
        Assert.Contains("\"id\"", format);
        Assert.Contains("\"steps\"", format);
        Assert.Contains("\"expected_result\"", format);
    }

    [Fact]
    public void LoadFormat_ReturnsFileContent_WhenDefaultFileExists()
    {
        var profilesDir = Path.Combine(_testDir, "profiles");
        Directory.CreateDirectory(profilesDir);

        const string customYaml = """
            format: |
              [
                {
                  "id": "TC-CUSTOM",
                  "title": "custom test",
                  "custom_field": "custom_value"
                }
              ]
            """;
        File.WriteAllText(Path.Combine(profilesDir, "_default.yaml"), customYaml);

        var format = ProfileFormatLoader.LoadFormat(_testDir);

        Assert.Contains("TC-CUSTOM", format);
        Assert.Contains("custom_field", format);
        // Should NOT contain the default schema's distinct fields when overridden.
        Assert.DoesNotContain("scenario_from_doc", format);
    }

    [Fact]
    public void LoadFormat_FallsBackToEmbedded_WhenFileIsMalformed()
    {
        var profilesDir = Path.Combine(_testDir, "profiles");
        Directory.CreateDirectory(profilesDir);

        // Invalid YAML — bad indentation that breaks the parser.
        File.WriteAllText(
            Path.Combine(profilesDir, "_default.yaml"),
            "format:\n  - bad\n bad_indent: [unclosed");

        var format = ProfileFormatLoader.LoadFormat(_testDir);

        // Falls back to embedded default; never throws.
        Assert.False(string.IsNullOrWhiteSpace(format));
        Assert.Contains("scenario_from_doc", format);
    }

    [Fact]
    public void LoadFormat_FallsBackToEmbedded_WhenFormatFieldMissing()
    {
        var profilesDir = Path.Combine(_testDir, "profiles");
        Directory.CreateDirectory(profilesDir);

        // Valid YAML, but no top-level `format` key.
        File.WriteAllText(
            Path.Combine(profilesDir, "_default.yaml"),
            "fields:\n  id:\n    description: foo\n");

        var format = ProfileFormatLoader.LoadFormat(_testDir);

        Assert.False(string.IsNullOrWhiteSpace(format));
        Assert.Contains("scenario_from_doc", format);
    }

    [Fact]
    public void LoadEmbeddedDefaultYaml_ContainsFormatField()
    {
        var content = ProfileFormatLoader.LoadEmbeddedDefaultYaml();

        Assert.Contains("format:", content);
        Assert.Contains("fields:", content);
    }

    [Fact]
    public void LoadEmbeddedCustomizationGuide_IsNonEmptyMarkdown()
    {
        var content = ProfileFormatLoader.LoadEmbeddedCustomizationGuide();

        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("# SPECTRA Customization Guide", content);
        Assert.Contains("profiles/_default.yaml", content);
    }
}
