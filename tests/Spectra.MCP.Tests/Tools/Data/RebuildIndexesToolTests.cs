using System.Text.Json;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools.Data;

public class RebuildIndexesToolTests : IDisposable
{
    private readonly string _testDir;

    public RebuildIndexesToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_NoTestsDir_ReturnsError()
    {
        var tool = new RebuildIndexesTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("TESTS_DIR_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_SuiteNotFound_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

        var tool = new RebuildIndexesTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"nonexistent\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("SUITE_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_CreatesIndexFile()
    {
        var suiteDir = Path.Combine(_testDir, "tests", "auth");
        Directory.CreateDirectory(suiteDir);

        var testContent = """
            ---
            id: TC-001
            priority: high
            tags: [smoke]
            component: auth
            ---

            # Login test

            ## Steps

            1. Test step

            ## Expected Result

            Expected result
            """;

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "tc-001.md"), testContent);

        var tool = new RebuildIndexesTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"auth\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(1, response.GetProperty("data").GetProperty("suites_processed").GetInt32());
        Assert.Equal(1, response.GetProperty("data").GetProperty("tests_indexed").GetInt32());

        // Verify index file was created
        var indexPath = Path.Combine(suiteDir, "_index.json");
        Assert.True(File.Exists(indexPath));
    }

    [Fact]
    public async Task Execute_ReturnsCorrectAddedCount()
    {
        var suiteDir = Path.Combine(_testDir, "tests", "auth");
        Directory.CreateDirectory(suiteDir);

        var testContent = """
            ---
            id: TC-001
            priority: high
            ---

            # Test

            ## Steps

            1. Step

            ## Expected Result

            Result
            """;

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "tc-001.md"), testContent);

        var tool = new RebuildIndexesTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"auth\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(1, response.GetProperty("data").GetProperty("files_added").GetInt32());
        Assert.Equal(0, response.GetProperty("data").GetProperty("files_removed").GetInt32());
    }

    [Fact]
    public async Task Execute_RebuildsAllSuites_WhenNoSuiteSpecified()
    {
        var auth = Path.Combine(_testDir, "tests", "auth");
        var checkout = Path.Combine(_testDir, "tests", "checkout");
        Directory.CreateDirectory(auth);
        Directory.CreateDirectory(checkout);

        var testContent = """
            ---
            id: TC-001
            priority: high
            ---

            # Test

            ## Steps

            1. Step

            ## Expected Result

            Result
            """;

        await File.WriteAllTextAsync(Path.Combine(auth, "tc-001.md"), testContent);
        await File.WriteAllTextAsync(Path.Combine(checkout, "tc-002.md"), testContent.Replace("TC-001", "TC-002"));

        var tool = new RebuildIndexesTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(2, response.GetProperty("data").GetProperty("suites_processed").GetInt32());
        Assert.Equal(2, response.GetProperty("data").GetProperty("tests_indexed").GetInt32());
    }

    [Fact]
    public async Task Execute_ReturnsIndexPaths()
    {
        var suiteDir = Path.Combine(_testDir, "tests", "auth");
        Directory.CreateDirectory(suiteDir);

        var testContent = """
            ---
            id: TC-001
            priority: high
            ---

            # Test

            ## Steps

            1. Step

            ## Expected Result

            Result
            """;

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "tc-001.md"), testContent);

        var tool = new RebuildIndexesTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"auth\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var indexPaths = response.GetProperty("data").GetProperty("index_paths");
        Assert.Equal(1, indexPaths.GetArrayLength());
        Assert.Contains("_index.json", indexPaths[0].GetString());
    }
}
