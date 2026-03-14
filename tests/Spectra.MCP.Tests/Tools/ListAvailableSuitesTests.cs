using System.Text.Json;
using Spectra.Core.Models;
using Spectra.MCP.Tools.RunManagement;

namespace Spectra.MCP.Tests.Tools;

public class ListAvailableSuitesTests : IDisposable
{
    private readonly string _testDir;

    public ListAvailableSuitesTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task Execute_NoSuites_ReturnsError()
    {
        var tool = new ListAvailableSuitesTool(_ => []);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("NO_SUITES_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_WithSuites_ReturnsSuiteList()
    {
        var suites = new List<SuiteInfo>
        {
            new("checkout", 15, "tests/checkout"),
            new("auth", 8, "tests/auth"),
            new("payment", 12, "tests/payment")
        };

        var tool = new ListAvailableSuitesTool(_ => suites);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.TryGetProperty("data", out var data));
        var suitesArray = data.GetProperty("suites");
        Assert.Equal(3, suitesArray.GetArrayLength());
    }

    [Fact]
    public async Task Execute_IncludesTestCounts()
    {
        var suites = new List<SuiteInfo>
        {
            new("checkout", 15, "tests/checkout")
        };

        var tool = new ListAvailableSuitesTool(_ => suites);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var suiteInfo = response.GetProperty("data").GetProperty("suites")[0];
        Assert.Equal("checkout", suiteInfo.GetProperty("name").GetString());
        Assert.Equal(15, suiteInfo.GetProperty("test_count").GetInt32());
        Assert.Equal("tests/checkout", suiteInfo.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Execute_ReturnsNextExpectedAction()
    {
        var suites = new List<SuiteInfo>
        {
            new("checkout", 15, "tests/checkout")
        };

        var tool = new ListAvailableSuitesTool(_ => suites);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("start_execution_run", response.GetProperty("next_expected_action").GetString());
    }

    [Fact]
    public async Task Execute_IndexStale_ReturnsWarning()
    {
        var suites = new List<SuiteInfo>
        {
            new("checkout", 15, "tests/checkout", IsStale: true)
        };

        var tool = new ListAvailableSuitesTool(_ => suites);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        var suiteInfo = response.GetProperty("data").GetProperty("suites")[0];
        Assert.True(suiteInfo.GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task Execute_ReturnsTotalTestCount()
    {
        var suites = new List<SuiteInfo>
        {
            new("checkout", 15, "tests/checkout"),
            new("auth", 8, "tests/auth")
        };

        var tool = new ListAvailableSuitesTool(_ => suites);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal(23, response.GetProperty("data").GetProperty("total_tests").GetInt32());
    }
}
