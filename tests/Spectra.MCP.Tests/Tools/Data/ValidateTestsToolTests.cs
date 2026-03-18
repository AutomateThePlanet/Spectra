using System.Text.Json;
using Spectra.MCP.Tools.Data;

namespace Spectra.MCP.Tests.Tools.Data;

public class ValidateTestsToolTests : IDisposable
{
    private readonly string _testDir;

    public ValidateTestsToolTests()
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
        var tool = new ValidateTestsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("TESTS_DIR_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_SuiteNotFound_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "tests"));

        var tool = new ValidateTestsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"nonexistent\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.Equal("SUITE_NOT_FOUND", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ValidTestFile_ReturnsSuccess()
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

            # Login with valid credentials

            ## Steps

            1. Navigate to login page
            2. Enter valid credentials
            3. Click submit

            ## Expected Result

            User is logged in successfully
            """;

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "tc-001.md"), testContent);

        var tool = new ValidateTestsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"auth\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.GetProperty("data").GetProperty("is_valid").GetBoolean());
        Assert.Equal(1, response.GetProperty("data").GetProperty("total_files").GetInt32());
        Assert.Equal(1, response.GetProperty("data").GetProperty("valid_files").GetInt32());
    }

    [Fact]
    public async Task Execute_MissingId_ReturnsValidationError()
    {
        var suiteDir = Path.Combine(_testDir, "tests", "auth");
        Directory.CreateDirectory(suiteDir);

        var testContent = """
            ---
            priority: high
            ---

            # Login test

            ## Steps

            1. Test step

            ## Expected Result

            Expected outcome
            """;

        await File.WriteAllTextAsync(Path.Combine(suiteDir, "tc-001.md"), testContent);

        var tool = new ValidateTestsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"auth\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.False(response.GetProperty("data").GetProperty("is_valid").GetBoolean());
        var errors = response.GetProperty("data").GetProperty("errors");
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal("MISSING_ID", errors[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Execute_ValidatesAllSuites_WhenNoSuiteSpecified()
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

        var tool = new ValidateTestsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.GetProperty("data").GetProperty("is_valid").GetBoolean());
        Assert.Equal(2, response.GetProperty("data").GetProperty("total_files").GetInt32());
    }

    [Fact]
    public async Task Execute_SkipsIndexFiles()
    {
        var suiteDir = Path.Combine(_testDir, "tests", "auth");
        Directory.CreateDirectory(suiteDir);

        // Create index file that should be skipped
        await File.WriteAllTextAsync(Path.Combine(suiteDir, "_index.json"), "{}");

        var tool = new ValidateTestsTool(_testDir);

        var result = await tool.ExecuteAsync(JsonDocument.Parse("{\"suite\":\"auth\"}").RootElement);
        var response = JsonDocument.Parse(result).RootElement;

        Assert.True(response.GetProperty("data").GetProperty("is_valid").GetBoolean());
        Assert.Equal(0, response.GetProperty("data").GetProperty("total_files").GetInt32());
    }
}
