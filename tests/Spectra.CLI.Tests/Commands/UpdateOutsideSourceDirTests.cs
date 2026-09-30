using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 080 A2 (FR-002, SC-002): with <c>source.local_dir</c> pointing outside the repository
/// (<c>../product-docs/docs</c>), tests that reference docs there are not classified ORPHANED.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class UpdateOutsideSourceDirTests : IDisposable
{
    private const string Suite = "checkout";
    private readonly UpdateWorkspace _ws = new();
    private readonly string _sibling;

    public UpdateOutsideSourceDirTests()
    {
        var name = "product-docs-" + Guid.NewGuid().ToString("N");
        _sibling = Path.GetFullPath(Path.Combine(_ws.Root, "..", name));
        Directory.CreateDirectory(Path.Combine(_sibling, "docs"));
        File.WriteAllText(Path.Combine(_sibling, "docs", "checkout.md"), UpdateWorkspace.MatchingDoc);

        var config = new
        {
            source = new { local_dir = $"../{name}/docs", include_patterns = new[] { "**/*.md" } },
            tests = new { dir = "test-cases/" },
            ai = new { }
        };
        File.WriteAllText(_ws.PathOf("spectra.config.json"), JsonSerializer.Serialize(config));
    }

    public void Dispose()
    {
        _ws.Dispose();
        try { Directory.Delete(_sibling, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task TestReferencingAnOutOfRepoDoc_IsNotOrphaned()
    {
        _ws.WriteTest(Suite, "TC-100", UpdateWorkspace.SimpleTest("TC-100", "checkout.md#Payment"));
        var before = _ws.Snapshot();

        var (exit, output) = await _ws.RunAsync("ai", "update", Suite, "--dry-run", "--output-format", "json");

        Assert.True(exit == 0, output);
        var result = JsonDocument.Parse(File.ReadAllText(_ws.PathOf(".spectra-result.json"))).RootElement;
        Assert.Equal(0, result.GetProperty("classification").GetProperty("orphaned").GetInt32());
        Assert.Equal(before, _ws.Snapshot());
    }
}
