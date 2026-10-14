using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Suite;

/// <summary>
/// Spec 098 F3 (FR-009, SC-003): <c>suite delete</c> reports every removed file once. Verdict and record files are
/// removed by the deletion service and listed under <c>verdict_files_removed</c>; <c>leftovers</c> /
/// <c>leftovers_removed</c> list only the suite's staging folders and generation plan. On 986da01 the verdict files
/// were also added to the leftovers (deleted a second time, a no-op) and listed and counted there.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SuiteDeleteReportTests : IDisposable
{
    private static readonly string[] VerdictFiles =
    [
        ".spectra/verdicts/critic-verdict-TC-101.json",
        ".spectra/verdicts/critic-input-TC-101.json",
        ".spectra/verdicts/critic-verdict-TC-102.json",
    ];

    private readonly BaselineTestWorkspace _ws = new();

    public SuiteDeleteReportTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 30GB.\n");
        _ws.WriteTest("a", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("a", "TC-102", ["docs/storage.md#Quotas"]);
        foreach (var file in VerdictFiles.Concat([".spectra/updates/a/updated-TC-101.json", ".spectra/generation-plan/a.json"]))
        {
            _ws.WriteDoc(file, "{}");
        }
    }

    public void Dispose() => _ws.Dispose();

    private async Task<(JsonElement Result, string Err)> SuiteDeleteAsync(params string[] extra)
    {
        var (exit, output, error) = await _ws.RunAsync(["suite", "delete", "a", .. extra, "--force", "--no-interaction", "--output-format", "json"]);
        Assert.True(exit == 0, error);
        return (BaselineTestWorkspace.Json(output), error);
    }

    private static string[] Strings(JsonElement result, string property) =>
        result.TryGetProperty(property, out var array) ? array.EnumerateArray().Select(e => e.GetString()!).ToArray() : [];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerdictFiles_AreReportedOnce_UnderTheirOwnList(bool dryRun)
    {
        var (result, error) = await SuiteDeleteAsync(dryRun ? ["--dry-run"] : []);

        Assert.Equal(VerdictFiles.OrderBy(f => f, StringComparer.Ordinal),
            Strings(result, "verdict_files_removed").OrderBy(f => f, StringComparer.Ordinal));
        var leftovers = Strings(result, dryRun ? "leftovers" : "leftovers_removed");
        Assert.Equal([".spectra/generation-plan/a.json", ".spectra/updates/a/"], leftovers.OrderBy(f => f, StringComparer.Ordinal));

        // The human summary (stderr in JSON mode) names each verdict file once and counts 2 leftovers.
        foreach (var file in VerdictFiles)
        {
            Assert.Equal(1, UpdatePromptWorkspace.Count(error, file));
        }

        Assert.Contains("2 leftover(s)", error);
    }

    [Fact]
    public async Task TheVerdictFiles_AreGone()
    {
        await SuiteDeleteAsync();

        foreach (var file in VerdictFiles)
        {
            Assert.False(File.Exists(_ws.PathOf(file)), file);
        }
    }
}
