using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.Commands;

[Collection("WorkingDirectory")]
public class InitHandlerBugTrackingTests : IDisposable
{
    private readonly string _testDir;
    private readonly ILogger<InitHandler> _logger;

    public InitHandlerBugTrackingTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "spectra-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        var loggerFactory = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet);
        _logger = loggerFactory.CreateLogger<InitHandler>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task HandleAsync_CreatesBugReportTemplate()
    {
        var handler = new InitHandler(_logger, _testDir);

        var exitCode = await handler.HandleAsync(force: false);

        Assert.Equal(ExitCodes.Success, exitCode);

        var templatePath = Path.Combine(_testDir, "templates", "bug-report.md");
        Assert.True(File.Exists(templatePath), "Bug report template should exist");

        var content = await File.ReadAllTextAsync(templatePath);
        Assert.Contains("{{test_id}}", content);
        Assert.Contains("{{severity}}", content);
        Assert.Contains("{{failed_steps}}", content);
        Assert.Contains("{{attachments}}", content);
    }

    [Fact]
    public async Task HandleAsync_CreatesTemplatesDirectory()
    {
        var handler = new InitHandler(_logger, _testDir);

        await handler.HandleAsync(force: false);

        Assert.True(Directory.Exists(Path.Combine(_testDir, "templates")));
    }

    [Fact]
    public async Task HandleAsync_SkipsExistingTemplate()
    {
        var templateDir = Path.Combine(_testDir, "templates");
        Directory.CreateDirectory(templateDir);
        var templatePath = Path.Combine(templateDir, "bug-report.md");
        await File.WriteAllTextAsync(templatePath, "# Custom template");

        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: true);

        var content = await File.ReadAllTextAsync(templatePath);
        Assert.Equal("# Custom template", content);
    }

    [Fact]
    public async Task HandleAsync_ConfigIncludesBugTrackingSection()
    {
        var handler = new InitHandler(_logger, _testDir);

        await handler.HandleAsync(force: false);

        var configPath = Path.Combine(_testDir, "spectra.config.json");
        var configJson = await File.ReadAllTextAsync(configPath);
        using var doc = JsonDocument.Parse(configJson);

        Assert.True(doc.RootElement.TryGetProperty("bug_tracking", out var bugTracking),
            "Config should have bug_tracking section");
        Assert.Equal("auto", bugTracking.GetProperty("provider").GetString());
        Assert.Equal("templates/bug-report.md", bugTracking.GetProperty("template").GetString());
        Assert.Equal("medium", bugTracking.GetProperty("default_severity").GetString());
        Assert.True(bugTracking.GetProperty("auto_attach_screenshots").GetBoolean());
        Assert.True(bugTracking.GetProperty("auto_prompt_on_failure").GetBoolean());
    }
}
