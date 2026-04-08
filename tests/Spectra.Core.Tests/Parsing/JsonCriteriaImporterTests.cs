using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class JsonCriteriaImporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonCriteriaImporter _importer;

    public JsonCriteriaImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"json-importer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _importer = new JsonCriteriaImporter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ImportAsync_ValidJson_ParsesAllFields()
    {
        var path = Path.Combine(_tempDir, "full.json");
        await File.WriteAllTextAsync(path, """
        {
            "criteria": [
                {
                    "id": "REQ-001",
                    "text": "User must authenticate via SSO",
                    "rfc2119": "MUST",
                    "source": "login-spec",
                    "source_type": "jira",
                    "component": "auth",
                    "priority": "high",
                    "tags": ["security", "login"]
                },
                {
                    "id": "REQ-002",
                    "text": "Session expires after 30 minutes",
                    "rfc2119": "SHOULD",
                    "source": "session-spec",
                    "source_type": "confluence",
                    "component": "session",
                    "priority": "medium",
                    "tags": ["timeout"]
                }
            ]
        }
        """);

        var result = await _importer.ImportAsync(path);

        Assert.Equal(2, result.Count);

        Assert.Equal("REQ-001", result[0].Id);
        Assert.Equal("User must authenticate via SSO", result[0].Text);
        Assert.Equal("MUST", result[0].Rfc2119);
        Assert.Equal("login-spec", result[0].Source);
        Assert.Equal("jira", result[0].SourceType);
        Assert.Equal("auth", result[0].Component);
        Assert.Equal("high", result[0].Priority);
        Assert.Equal(["security", "login"], result[0].Tags);

        Assert.Equal("REQ-002", result[1].Id);
        Assert.Equal("Session expires after 30 minutes", result[1].Text);
    }

    [Fact]
    public async Task ImportAsync_MinimalFields_UsesDefaults()
    {
        var path = Path.Combine(_tempDir, "minimal.json");
        await File.WriteAllTextAsync(path, """
        {
            "criteria": [
                { "text": "Basic requirement" }
            ]
        }
        """);

        var result = await _importer.ImportAsync(path, "manual");

        Assert.Single(result);
        Assert.Equal("Basic requirement", result[0].Text);
        Assert.Equal(string.Empty, result[0].Id);
        Assert.Equal("manual", result[0].SourceType);
        Assert.Equal("medium", result[0].Priority);
        Assert.Null(result[0].Source);
        Assert.Null(result[0].Component);
        Assert.Null(result[0].Rfc2119);
        Assert.Empty(result[0].Tags);
    }

    [Fact]
    public async Task ImportAsync_InvalidSchema_ThrowsInvalidOperation()
    {
        var path = Path.Combine(_tempDir, "invalid.json");
        // Malformed JSON triggers JsonException which is wrapped as InvalidOperationException
        await File.WriteAllTextAsync(path, """[not valid json{{{""");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _importer.ImportAsync(path));
    }

    [Fact]
    public async Task ImportAsync_EmptyArray_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "empty.json");
        await File.WriteAllTextAsync(path, """{ "criteria": [] }""");

        var result = await _importer.ImportAsync(path);

        Assert.Empty(result);
    }
}
