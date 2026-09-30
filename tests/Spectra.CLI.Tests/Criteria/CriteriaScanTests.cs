using System.Reflection;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Criteria;

/// <summary>
/// Spec 080 B8 (FR-013): <c>docs changed</c> and the criteria store find criteria files through one shared scan.
/// B8 is a pure refactor, so the regression guard is structural (no second scan exists), with a behavioural check
/// that a nested file is seen the same way by both.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CriteriaScanTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void DocsChanged_HasNoScanOfItsOwn_AndTheStoreExposesTheSharedOne()
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        Assert.Null(typeof(DocsChangedHandler).GetMethod("ScanDuplicatesAsync", All));
        var scan = typeof(CriteriaStore).GetMethod("ScanFilesAsync", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(scan);
    }

    [Fact]
    public async Task NestedCriteriaFile_IsSeenByDocsChangedAndByIngest()
    {
        _ws.WriteDoc("docs/checkout.md", "# Checkout\n");
        _ws.WriteCriteriaFile("x.criteria.yaml", "criteria:\n- id: AC-X-001\n  text: top\n");
        _ws.WriteCriteriaFile("sub/deeper/x.criteria.yaml", "criteria:\n- id: AC-X-001\n  text: nested\n");

        var changed = new StringWriter();
        await new DocsChangedHandler(OutputFormat.Json, false, false).ExecuteAsync(_ws.Root, changed);
        var (exit, ingest, err) = await _ws.IngestAsync("docs/checkout.md", new { text = "System MUST pay", rfc2119 = "MUST" });

        Assert.True(exit == 0, err);
        var fromChanged = Assert.Single(CriteriaTestWorkspace.Json(changed.ToString()).GetProperty("duplicate_ids").EnumerateArray());
        var fromIngest = Assert.Single(CriteriaTestWorkspace.Json(ingest).GetProperty("duplicate_ids").EnumerateArray());
        Assert.Equal("AC-X-001", fromChanged.GetProperty("id").GetString());
        Assert.Equal(
            fromChanged.GetProperty("files").EnumerateArray().Select(f => f.GetString()),
            fromIngest.GetProperty("files").EnumerateArray().Select(f => f.GetString()));
        Assert.Contains("sub/deeper/x.criteria.yaml", fromChanged.GetProperty("files").EnumerateArray().Select(f => f.GetString()));
    }
}
