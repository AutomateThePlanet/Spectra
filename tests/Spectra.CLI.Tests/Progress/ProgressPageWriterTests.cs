using Spectra.CLI.Progress;

namespace Spectra.CLI.Tests.Progress;

public class ProgressPageWriterTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string CreateTempHtmlPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}.html");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".tmp"); } catch { }
        }
    }

    [Fact]
    public void WriteProgressPage_CreatesHtmlFile()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "analyzing", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("<!DOCTYPE html>", content);
    }

    [Fact]
    public void WriteProgressPage_AlwaysContainsAutoRefreshScript()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "analyzing", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("setInterval", content);
        Assert.Contains("Date.now()", content);
    }

    [Fact]
    public void WriteProgressPage_AlwaysHasAutoRefresh_EvenWhenTerminal()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "completed", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: true);

        var content = File.ReadAllText(path);
        Assert.Contains("setInterval", content);
        Assert.Contains("Date.now()", content);
    }

    [Fact]
    public void WriteProgressPage_EmbedsJsonData()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "analyzed", "suite": "login", "analysis": {"total_behaviors": 10, "already_covered": 3, "recommended": 7}}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        // The JSON values are parsed and rendered server-side, so individual values appear in the HTML
        Assert.Contains("login", content);
    }

    [Fact]
    public void WriteProgressPage_ShowsAnalyzingStatus()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "analyzing", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("Analyzing Documentation", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteProgressPage_ShowsCompletedStatus()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "completed", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: true);

        var content = File.ReadAllText(path);
        Assert.Contains("Complete", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteProgressPage_ShowsFilesCreated()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "completed", "suite": "checkout", "files_created": ["tests/checkout/TC-001.md", "tests/checkout/TC-002.md"]}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: true);

        var content = File.ReadAllText(path);
        Assert.Contains("tests/checkout/TC-001.md", content);
        Assert.Contains("tests/checkout/TC-002.md", content);
        Assert.Contains("vscode://file/", content);
    }

    [Fact]
    public void WriteProgressPage_ShowsGenerationCards()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "generating", "suite": "checkout", "generation": {"tests_written": 5, "tests_generated": 7, "tests_requested": 10}}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains(">5<", content);
        Assert.Contains("Written", content, StringComparison.OrdinalIgnoreCase);
    }

    // === New phase tests for spec 025 ===

    [Fact]
    public void WriteProgressPage_ShowsClassifyingStatus()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "classifying", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("Classifying Tests", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteProgressPage_ShowsScanningTestsStatus()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "scanning-tests"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("Scanning Test Suites", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteProgressPage_ShowsCollectingDataStatus()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "collecting-data"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("Collecting Dashboard Data", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteProgressPage_ShowsExtractingStatus()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "extracting"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("Extracting Acceptance Criteria", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteProgressPage_ShowsCoverageCards()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "completed", "documentationCoverage": {"percentage": 67.5, "covered": 8, "total": 12}, "acceptanceCriteriaCoverage": {"percentage": 74.0, "covered": 37, "total": 50}, "automationCoverage": {"percentage": 12.0, "covered": 31, "total": 259}}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: true);

        var content = File.ReadAllText(path);
        Assert.Contains("Doc Coverage", content);
        Assert.Contains("Criteria Coverage", content);
        Assert.Contains("Automation", content);
    }

    [Fact]
    public void WriteProgressPage_ShowsUpdateCards()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "completed", "testsUpdated": 3, "testsRemoved": 1, "testsUnchanged": 12}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: true);

        var content = File.ReadAllText(path);
        Assert.Contains("Updated", content);
        Assert.Contains("Unchanged", content);
    }

    [Fact]
    public void WriteProgressPage_WithTitle_ShowsTitleInHeader()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "scanning-tests"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false, title: "Coverage Analysis");

        var content = File.ReadAllText(path);
        Assert.Contains("Coverage Analysis", content);
    }

    [Fact]
    public void WriteProgressPage_WithoutTitle_ShowsProgressAsDefault()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "analyzing"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("<title>SPECTRA", content);
    }

    [Fact]
    public void WriteProgressPage_AllNewStatuses_ShowSpinner()
    {
        var statuses = new[] { "classifying", "updating", "verifying", "scanning-tests",
            "analyzing-docs", "analyzing-criteria", "analyzing-automation",
            "scanning-docs", "extracting", "building-index", "collecting-data", "generating-html" };

        foreach (var status in statuses)
        {
            var path = CreateTempHtmlPath();
            var json = $$$"""{"status": "{{{status}}}"}""";

            ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

            var content = File.ReadAllText(path);
            Assert.Contains("spinner", content);
        }
    }
}
