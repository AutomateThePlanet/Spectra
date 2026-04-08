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
    public void WriteProgressPage_ContainsMetaRefresh_WhenNotTerminal()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "analyzing", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false);

        var content = File.ReadAllText(path);
        Assert.Contains("""<meta http-equiv="refresh" content="2">""", content);
    }

    [Fact]
    public void WriteProgressPage_OmitsMetaRefresh_WhenTerminal()
    {
        var path = CreateTempHtmlPath();
        var json = """{"status": "completed", "suite": "checkout"}""";

        ProgressPageWriter.WriteProgressPage(path, json, isTerminal: true);

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("""http-equiv="refresh""", content);
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
        Assert.Contains("Generation Complete", content, StringComparison.OrdinalIgnoreCase);
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
}
