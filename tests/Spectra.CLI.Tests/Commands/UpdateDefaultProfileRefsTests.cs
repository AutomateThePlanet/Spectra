using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 078 (US2, SC-003/SC-004): <c>spectra ai update</c> resolves the refs the default generation profile
/// writes (<c>docs/&lt;file&gt;.md#&lt;Section&gt;</c>) plus backslash and legacy bare refs. Checked through
/// the orphan marks <c>--no-review</c> writes, read as raw text so this does not depend on the parser.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class UpdateDefaultProfileRefsTests : IDisposable
{
    private const string Suite = "checkout";
    private readonly UpdateWorkspace _ws = new();

    public UpdateDefaultProfileRefsTests()
    {
        _ws.WriteDoc("docs/checkout.md", UpdateWorkspace.MatchingDoc);
        _ws.WriteDoc("docs/login.md", UpdateWorkspace.MatchingDoc.Replace("# Checkout", "# Login"));
        SeedTests();
    }

    public void Dispose() => _ws.Dispose();

    private void SeedTests()
    {
        _ws.WriteTest(Suite, "TC-001", UpdateWorkspace.SimpleTest("TC-001", "docs/checkout.md#Payment"));
        _ws.WriteTest(Suite, "TC-002", UpdateWorkspace.SimpleTest("TC-002", "docs\\checkout.md"));
        _ws.WriteTest(Suite, "TC-003", UpdateWorkspace.SimpleTest("TC-003", "checkout.md"));
        _ws.WriteTest(Suite, "TC-004", UpdateWorkspace.SimpleTest("TC-004", "docs/login.md#Form", "docs/checkout.md#Payment"));
    }

    private bool IsOrphaned(string id) =>
        File.ReadAllText(_ws.PathOf($"test-cases/{Suite}/{id}.md")).Contains("status: orphaned");

    [Fact]
    public async Task DefaultProfileRefs_ToExistingDocs_AreNotOrphaned()
    {
        var (exit, output) = await _ws.RunAsync("ai", "update", Suite, "--no-review");

        Assert.True(exit == 0, output);
        Assert.DoesNotContain("Source document not found", output);
        Assert.All(["TC-001", "TC-002", "TC-003", "TC-004"], id => Assert.False(IsOrphaned(id), $"{id} was orphaned"));
    }

    [Fact]
    public async Task DeletingTheOnlyReferencedDoc_OrphansExactlyThoseTests()
    {
        File.Delete(_ws.PathOf("docs/checkout.md"));

        var (exit, output) = await _ws.RunAsync("ai", "update", Suite, "--no-review");

        Assert.True(exit == 0, output);
        Assert.True(IsOrphaned("TC-001"));
        Assert.True(IsOrphaned("TC-002"));
        Assert.True(IsOrphaned("TC-003"));
        Assert.False(IsOrphaned("TC-004"), "TC-004 still has docs/login.md");
    }
}
