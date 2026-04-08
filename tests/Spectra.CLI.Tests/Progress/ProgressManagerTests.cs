using Spectra.CLI.Progress;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Progress;

public class ProgressManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _resultPath;
    private readonly string _progressPath;

    public ProgressManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-pm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _resultPath = Path.Combine(_tempDir, ".spectra-result.json");
        _progressPath = Path.Combine(_tempDir, ".spectra-progress.html");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private ProgressManager CreateManager(string command = "test-command", string[]? phases = null, string? title = null)
    {
        return new ProgressManager(
            command,
            phases ?? ["phase-1", "phase-2", "completed"],
            _resultPath,
            _progressPath,
            title);
    }

    [Fact]
    public void Reset_DeletesExistingFiles()
    {
        File.WriteAllText(_resultPath, "stale");
        File.WriteAllText(_progressPath, "stale");

        var pm = CreateManager();
        pm.Reset();

        Assert.False(File.Exists(_resultPath));
        Assert.False(File.Exists(_progressPath));
    }

    [Fact]
    public void Reset_NoFilesExist_DoesNotThrow()
    {
        var pm = CreateManager();
        pm.Reset(); // Should not throw
    }

    [Fact]
    public void Start_CreatesProgressHtml()
    {
        var pm = CreateManager();
        pm.Start("Starting...");

        Assert.True(File.Exists(_progressPath));
        var html = File.ReadAllText(_progressPath);
        Assert.Contains("<!DOCTYPE html>", html);
    }

    [Fact]
    public void Start_WritesResultJson()
    {
        var pm = CreateManager();
        pm.Start("Starting...");

        Assert.True(File.Exists(_resultPath));
        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"command\"", json);
        Assert.Contains("\"status\"", json);
    }

    [Fact]
    public void Start_UsesFirstPhaseAsStatus()
    {
        var pm = CreateManager(phases: ["scanning", "indexing", "completed"]);
        pm.Start();

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"scanning\"", json);
    }

    [Fact]
    public void Update_WritesResultJsonWithCurrentPhase()
    {
        var pm = CreateManager();
        pm.Update("phase-2", "Processing...");

        Assert.True(File.Exists(_resultPath));
        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"phase-2\"", json);
        Assert.Contains("Processing...", json);
    }

    [Fact]
    public void Update_UpdatesProgressHtml()
    {
        var pm = CreateManager();
        pm.Update("phase-1", "Working...");

        Assert.True(File.Exists(_progressPath));
        var html = File.ReadAllText(_progressPath);
        Assert.Contains("<!DOCTYPE html>", html);
    }

    [Fact]
    public void Update_WithTypedResult_WritesAllFields()
    {
        var pm = CreateManager(command: "dashboard");
        var result = new DashboardResult
        {
            Command = "dashboard",
            Status = "collecting-data",
            Message = "Loading suites...",
            OutputPath = "./site",
            SuitesIncluded = 3,
            TestsIncluded = 42
        };

        pm.Update(result);

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"collecting-data\"", json);
        Assert.Contains("\"suites_included\"", json);
    }

    [Fact]
    public void Complete_WritesResultJsonWithFinalData()
    {
        var pm = CreateManager(command: "validate");
        var result = new ValidateResult
        {
            Command = "validate",
            Status = "completed",
            TotalFiles = 50,
            Valid = 48,
            Errors = []
        };

        pm.Complete(result);

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"completed\"", json);
        Assert.Contains("\"total_files\"", json);
    }

    [Fact]
    public void Complete_RemovesAutoRefreshFromHtml()
    {
        var pm = CreateManager();

        // First write in-progress (has auto-refresh)
        pm.Update("phase-1", "Working...");
        var htmlBefore = File.ReadAllText(_progressPath);
        Assert.Contains("refresh", htmlBefore);

        // Complete (should remove auto-refresh)
        pm.Complete(new CommandResult
        {
            Command = "test-command",
            Status = "completed"
        });

        var htmlAfter = File.ReadAllText(_progressPath);
        Assert.DoesNotContain("""http-equiv="refresh""", htmlAfter);
    }

    [Fact]
    public void Fail_WritesErrorResult()
    {
        var pm = CreateManager();
        pm.Fail("Something went wrong");

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"failed\"", json);
        Assert.Contains("Something went wrong", json);
    }

    [Fact]
    public void Fail_RemovesAutoRefreshFromHtml()
    {
        var pm = CreateManager();
        pm.Update("phase-1", "Working...");
        pm.Fail("Crashed");

        var html = File.ReadAllText(_progressPath);
        Assert.DoesNotContain("""http-equiv="refresh""", html);
    }

    [Fact]
    public void Fail_WithPartialResult_WritesPartialData()
    {
        var pm = CreateManager(command: "generate");
        var partial = new GenerateResult
        {
            Command = "generate",
            Status = "failed",
            Suite = "checkout",
            FilesCreated = [],
            Generation = new GenerateGeneration { TestsGenerated = 0, TestsWritten = 0, TestsRejectedByCritic = 0 }
        };

        pm.Fail("AI provider unavailable", partial);

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"failed\"", json);
        Assert.Contains("checkout", json);
    }

    [Fact]
    public void WriteResultOnly_WritesJsonButNoHtml()
    {
        var pm = CreateManager();
        var result = new ValidateResult
        {
            Command = "validate",
            Status = "completed",
            TotalFiles = 10,
            Valid = 10,
            Errors = []
        };

        pm.WriteResultOnly(result);

        Assert.True(File.Exists(_resultPath));
        Assert.False(File.Exists(_progressPath));
    }

    [Fact]
    public void ProgressHtml_HasAutoRefreshTag_DuringExecution()
    {
        var pm = CreateManager();
        pm.Update("phase-1", "In progress...");

        var html = File.ReadAllText(_progressPath);
        Assert.Contains("""<meta http-equiv="refresh" content="2">""", html);
    }

    [Fact]
    public void ProgressHtml_ShowsTitle_WhenProvided()
    {
        var pm = CreateManager(title: "Coverage Analysis");
        pm.Start("Starting...");

        var html = File.ReadAllText(_progressPath);
        Assert.Contains("Coverage Analysis", html);
    }

    [Fact]
    public void ProgressHtml_ShowsCommandAsTitle_WhenNoTitleProvided()
    {
        var pm = CreateManager(command: "my-command");
        pm.Start("Starting...");

        var html = File.ReadAllText(_progressPath);
        Assert.Contains("my-command", html);
    }

    [Fact]
    public void ResultPath_Property_ReturnsConfiguredPath()
    {
        var pm = CreateManager();
        Assert.Equal(_resultPath, pm.ResultPath);
    }

    [Fact]
    public void ProgressPath_Property_ReturnsConfiguredPath()
    {
        var pm = CreateManager();
        Assert.Equal(_progressPath, pm.ProgressPath);
    }
}
