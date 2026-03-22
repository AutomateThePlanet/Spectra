using Spectra.Core.BugReporting;

namespace Spectra.Core.Tests.BugReporting;

public class BugReportWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BugReportWriter _writer = new();

    public BugReportWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "spectra-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_CreatesFile()
    {
        var path = await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-001", "TC-101", "# Bug report content");

        Assert.True(File.Exists(path));
        Assert.Equal("# Bug report content", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_CreatesCorrectPath()
    {
        var path = await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-001", "TC-101", "content");

        var expected = Path.Combine(_tempDir, "run-001", "bugs", "BUG-TC-101.md");
        Assert.Equal(expected, path);
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_CreatesBugsDirectory()
    {
        await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-002", "TC-101", "content");

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "run-002", "bugs")));
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_CopiesAttachments()
    {
        // Create a fake screenshot
        var screenshotDir = Path.Combine(_tempDir, "screenshots");
        Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir, "step3.png");
        await File.WriteAllTextAsync(screenshotPath, "fake-image-data");

        await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-003", "TC-101", "content",
            [screenshotPath]);

        var attachmentPath = Path.Combine(_tempDir, "run-003", "bugs", "attachments", "step3.png");
        Assert.True(File.Exists(attachmentPath));
        Assert.Equal("fake-image-data", await File.ReadAllTextAsync(attachmentPath));
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_SkipsMissingAttachments()
    {
        await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-004", "TC-101", "content",
            ["/nonexistent/file.png"]);

        // Should not throw, attachments dir may not be created
        var bugsDir = Path.Combine(_tempDir, "run-004", "bugs");
        Assert.True(Directory.Exists(bugsDir));
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_HandlesDuplicateFileNames()
    {
        var path1 = await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-005", "TC-101", "first bug");
        var path2 = await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-005", "TC-101", "second bug");

        Assert.NotEqual(path1, path2);
        Assert.Contains("BUG-TC-101.md", path1);
        Assert.Contains("BUG-TC-101-2.md", path2);
    }

    [Fact]
    public async Task WriteLocalBugReportAsync_NullAttachments_NoError()
    {
        var path = await _writer.WriteLocalBugReportAsync(
            _tempDir, "run-006", "TC-101", "content", null);

        Assert.True(File.Exists(path));
    }
}
