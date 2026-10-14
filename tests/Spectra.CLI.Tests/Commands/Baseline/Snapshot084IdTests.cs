using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Baseline;
using Spectra.Core.Text;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 098 F6 (FR-015), command level: a section baselined by 084 while its heading had <c>{#quotas}</c> keeps that
/// block in its stored snapshot (084 normalised without removing it). When only the id is later removed, triage reports
/// the section as COSMETIC (auto-acknowledged), not a doc change. On 986da01 it was LIGHT, once.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class Snapshot084IdTests : IDisposable
{
    private const string WithId = "# Storage\n\n## Quotas {#quotas}\n\nEach user gets 30GB.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    /// <summary>Rewrites the snapshot of the current Quotas section the way 084 stored it (attribute block kept).</summary>
    private void MakeSnapshot084()
    {
        var raw = SectionExtractor.Extract(WithId, "quotas").RawText!;
        var path = Path.Combine(BaselineStore.SnapshotDir(_ws.Root), SourceFingerprint.Hex(SourceFingerprint.OfSection(raw)) + ".txt");
        Assert.True(File.Exists(path), path);
        File.WriteAllText(path, TextNormalizer.Normalize(raw));
    }

    [Theory]
    [InlineData("# Storage\n\n## Quotas\n\nEach user gets 30GB.\n")]
    [InlineData("# Storage\n\n## Quotas {#user-quotas}\n\nEach user gets 30GB.\n")]
    public async Task AnIdOnlyHeadingChange_AgainstAn084Snapshot_IsCosmetic(string after)
    {
        _ws.WriteDoc("docs/storage.md", WithId);
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#quotas"]);
        await _ws.InitBaselineAsync();
        MakeSnapshot084();
        _ws.WriteDoc("docs/storage.md", after);

        var triage = await _ws.TriageAsync("storage", "--dry-run");

        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "cosmetic"));
        Assert.Equal(0, BaselineTestWorkspace.Count(triage, "storage", "light"));
    }

    [Fact]
    public async Task ARealEdit_AgainstAn084Snapshot_IsStillAChange()
    {
        _ws.WriteDoc("docs/storage.md", WithId);
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#quotas"]);
        await _ws.InitBaselineAsync();
        MakeSnapshot084();
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 30GB of fast storage.\n");

        var triage = await _ws.TriageAsync("storage", "--dry-run");

        Assert.Equal(0, BaselineTestWorkspace.Count(triage, "storage", "cosmetic"));
    }
}
