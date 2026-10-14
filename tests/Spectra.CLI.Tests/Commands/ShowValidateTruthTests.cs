using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 096 F3, F4 (FR-006, FR-007): <c>spectra show</c> finds a test through its suite folder plus the index's bare file
/// name (the legacy <c>suite/file</c> form still works), and <c>spectra validate</c> fails when a test doesn't parse. On
/// 23aa08c <c>show</c> joined the bare name to the tests folder (not found) and <c>validate</c> exited 0 on parse errors.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ShowValidateTruthTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public ShowValidateTruthTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 30GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], title: "Quota is enforced");
    }

    public void Dispose() => _ws.Dispose();

    private string IndexPath => _ws.PathOf("test-cases/storage/_index.json");

    [Fact]
    public async Task Show_FindsATestIndexedWithItsBareFileName()
    {
        var file = JsonNode.Parse(File.ReadAllText(IndexPath))!["tests"]![0]!["file"]!.GetValue<string>();
        Assert.Equal("TC-101.md", file);

        var (exit, output, error) = await _ws.RunAsync("show", "TC-101", "--output-format", "json");

        Assert.True(exit == 0, error);
        Assert.Contains("Quota is enforced", output);
    }

    [Fact]
    public async Task Show_StillFindsALegacySuitePrefixedFileName()
    {
        var index = JsonNode.Parse(File.ReadAllText(IndexPath))!;
        index["tests"]![0]!["file"] = "storage/TC-101.md";
        File.WriteAllText(IndexPath, index.ToJsonString());

        var (exit, output, error) = await _ws.RunAsync("show", "TC-101", "--output-format", "json");

        Assert.True(exit == 0, error);
        Assert.Contains("Quota is enforced", output);
    }

    private void BreakATest() =>
        File.WriteAllText(_ws.PathOf("test-cases/storage/TC-102.md"), "---\nid: TC-102\npriority: [unclosed\n---\n\n# Broken\n");

    [Fact]
    public async Task Validate_FailsInJsonMode_WhenATestDoesNotParse()
    {
        BreakATest();

        var (exit, output, _) = await _ws.RunAsync("validate", "--output-format", "json");

        Assert.Equal(1, exit);
        var result = BaselineTestWorkspace.Json(output);
        Assert.Equal("failed", result.GetProperty("status").GetString());
        Assert.Contains("error(s) found", result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Validate_FailsInHumanMode_WhenATestDoesNotParse()
    {
        BreakATest();

        var (exit, output, error) = await _ws.RunAsync("validate");

        Assert.Equal(1, exit);
        Assert.DoesNotContain("Validation passed", output + error);
    }

    [Fact]
    public async Task Validate_PassesOnACleanSuite()
    {
        var (exit, _, error) = await _ws.RunAsync("validate", "--output-format", "json");

        Assert.True(exit == 0, error);
    }
}
