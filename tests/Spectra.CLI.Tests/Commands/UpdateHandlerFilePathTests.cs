using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 080 A1 (FR-001, SC-001): <c>ai update</c> writes orphan marks to, and deletes, the test's own file, even
/// when the file name is not <c>{id}.md</c>. It never creates a second file with the same ID.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class UpdateHandlerFilePathTests : IDisposable
{
    private const string Suite = "checkout";
    private readonly UpdateWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private string WriteNamed(string fileName, string id)
    {
        var path = _ws.PathOf($"test-cases/{Suite}/{fileName}");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, UpdateWorkspace.SimpleTest(id, "docs/gone.md"));
        return path;
    }

    private string[] SuiteFiles() =>
        Directory.GetFiles(_ws.PathOf($"test-cases/{Suite}"), "*.md")
            .Select(Path.GetFileName)
            .Where(n => !n!.StartsWith('_'))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray()!;

    [Fact]
    public async Task OrphanMark_GoesIntoTheTestsOwnFile()
    {
        var own = WriteNamed("TC-100-login.md", "TC-100");

        var (exit, output) = await _ws.RunAsync("ai", "update", Suite, "--no-interaction", "--no-review");

        Assert.True(exit == 0, output);
        Assert.Contains("status: orphaned", File.ReadAllText(own));
        Assert.Equal(new[] { "TC-100-login.md" }, SuiteFiles());
    }

    [Fact]
    public async Task DeleteOrphaned_DeletesTheTestsOwnFile()
    {
        WriteNamed("TC-100-login.md", "TC-100");
        WriteNamed("TC-102.md", "TC-102");

        var (exit, output) = await _ws.RunAsync(
            "ai", "update", Suite, "--no-interaction", "--no-review", "--delete-orphaned");

        Assert.True(exit == 0, output);
        Assert.Empty(SuiteFiles());
    }

    [Fact]
    public async Task FileNamedAfterItsId_BehavesAsBefore()
    {
        var own = WriteNamed("TC-102.md", "TC-102");

        var (exit, output) = await _ws.RunAsync("ai", "update", Suite, "--no-interaction", "--no-review");

        Assert.True(exit == 0, output);
        Assert.Contains("status: orphaned", File.ReadAllText(own));
        Assert.Equal(new[] { "TC-102.md" }, SuiteFiles());
    }

    [Fact]
    public async Task OrphanedTwice_LeavesTheFileSetAndTheDateAlone()
    {
        var own = WriteNamed("TC-100-login.md", "TC-100");
        var original = SuiteFiles();

        var (exit1, output1) = await _ws.RunAsync("ai", "update", Suite, "--no-interaction", "--no-review");
        Assert.True(exit1 == 0, output1);
        var afterFirst = File.ReadAllBytes(own);

        var (exit2, output2) = await _ws.RunAsync("ai", "update", Suite, "--no-interaction", "--no-review");
        Assert.True(exit2 == 0, output2);

        Assert.Equal(original, SuiteFiles());
        Assert.Equal(afterFirst, File.ReadAllBytes(own));
    }
}
