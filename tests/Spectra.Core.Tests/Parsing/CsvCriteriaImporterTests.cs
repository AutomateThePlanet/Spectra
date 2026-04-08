using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class CsvCriteriaImporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CsvCriteriaImporter _importer;

    public CsvCriteriaImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"csv-importer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _importer = new CsvCriteriaImporter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ImportAsync_StandardColumns_ParsesCorrectly()
    {
        var path = Path.Combine(_tempDir, "standard.csv");
        await File.WriteAllTextAsync(path,
            "id,text,source,component,priority\n" +
            "REQ-001,User can log in,login-spec,auth,high\n" +
            "REQ-002,User can log out,logout-spec,auth,low\n");

        var result = await _importer.ImportAsync(path);

        Assert.Equal(2, result.Count);

        Assert.Equal("User can log in", result[0].Text);
        Assert.Equal("login-spec", result[0].Source);
        Assert.Equal("auth", result[0].Component);
        Assert.Equal("high", result[0].Priority);

        Assert.Equal("User can log out", result[1].Text);
        Assert.Equal("logout-spec", result[1].Source);
        Assert.Equal("auth", result[1].Component);
        Assert.Equal("low", result[1].Priority);
    }

    [Fact]
    public async Task ImportAsync_JiraColumns_AutoMaps()
    {
        var path = Path.Combine(_tempDir, "jira.csv");
        await File.WriteAllTextAsync(path,
            "Key,Summary,Acceptance Criteria\n" +
            "PROJ-100,Login flow,User must authenticate with SSO\n");

        var result = await _importer.ImportAsync(path);

        Assert.Single(result);
        // "Summary" maps to text column, "Key" maps to source column
        Assert.Equal("Login flow", result[0].Text);
        Assert.Equal("PROJ-100", result[0].Source);
    }

    [Fact]
    public async Task ImportAsync_AdoColumns_AutoMaps()
    {
        var path = Path.Combine(_tempDir, "ado.csv");
        await File.WriteAllTextAsync(path,
            "Work Item ID,Title,Area Path\n" +
            "12345,Payment validation,Billing\\Payments\n");

        var result = await _importer.ImportAsync(path);

        Assert.Single(result);
        // "Title" maps to text, "Work Item ID" maps to source, "Area Path" maps to component
        Assert.Equal("Payment validation", result[0].Text);
        Assert.Equal("12345", result[0].Source);
        Assert.Equal("Billing\\Payments", result[0].Component);
    }

    [Fact]
    public async Task ImportAsync_MissingTextColumn_ThrowsInvalidOperation()
    {
        var path = Path.Combine(_tempDir, "notext.csv");
        await File.WriteAllTextAsync(path,
            "id,status,owner\n" +
            "1,open,alice\n");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _importer.ImportAsync(path));
    }

    [Fact]
    public async Task ImportAsync_EmptyRows_Skipped()
    {
        var path = Path.Combine(_tempDir, "empty-rows.csv");
        await File.WriteAllTextAsync(path,
            "text,source\n" +
            "First criterion,src-1\n" +
            ",src-2\n" +
            "  ,src-3\n" +
            "Third criterion,src-4\n");

        var result = await _importer.ImportAsync(path);

        Assert.Equal(2, result.Count);
        Assert.Equal("First criterion", result[0].Text);
        Assert.Equal("Third criterion", result[1].Text);
    }

    [Fact]
    public async Task ImportAsync_QuotedFieldsWithCommas_ParsesCorrectly()
    {
        var path = Path.Combine(_tempDir, "quoted.csv");
        await File.WriteAllTextAsync(path,
            "text,source,priority\n" +
            "\"Given a user, when they log in, then they see dashboard\",login-spec,high\n");

        var result = await _importer.ImportAsync(path);

        Assert.Single(result);
        Assert.Equal("Given a user, when they log in, then they see dashboard", result[0].Text);
        Assert.Equal("login-spec", result[0].Source);
        Assert.Equal("high", result[0].Priority);
    }
}
