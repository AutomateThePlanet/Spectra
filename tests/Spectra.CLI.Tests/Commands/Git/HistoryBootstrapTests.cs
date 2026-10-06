using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T5 (FR-015–FR-018, SC-005): <c>source-baseline --init --from-date</c> / <c>--from-ref --owner</c> fill
/// missing entries from the docs as they were, following renames back, and never change an existing fingerprint.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class HistoryBootstrapTests : IDisposable
{
    private static readonly DateTimeOffset Day1 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day3 = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);
    private const string Between = "2026-03-15";

    private readonly BaselineTestWorkspace _ws = new();

    public HistoryBootstrapTests()
    {
        if (GitAvailability.IsAvailable) _ws.InitGit();
    }

    public void Dispose() => _ws.Dispose();

    private Task<JsonElement> FromDateAsync(params string[] extra) =>
        _ws.InitBaselineAsync("storage", ["--from-date", Between, .. extra]);

    private string Fingerprint(string id, string sourceRef) =>
        _ws.BaselineJson("storage").GetProperty("tests").GetProperty(id).GetProperty("refs").GetProperty(sourceRef).GetString()!;

    private static JsonElement Unbaselined(JsonElement result) =>
        result.GetProperty("suites")[0].GetProperty("unbaselined");

    /// <summary>docs/a.md with 10GB on Day1; 50GB on Day3 (optionally renamed to docs/guides/a.md in the same commit).</summary>
    private void ChangedAfterTheDate(bool rename = false)
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.Commit("one", Day1);
        var path = "docs/a.md";
        if (rename)
        {
            _ws.Move("docs/a.md", "docs/guides/a.md");
            path = "docs/guides/a.md";
        }

        _ws.Edit(path, "10GB", "50GB");
        _ws.Commit("change", Day3);
    }

    private async Task AssertNumberChangeAsync(string id = "TC-101")
    {
        var triage = await _ws.TriageAsync();
        var group = Group(triage, id);
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());
        Assert.Contains("numbers: 10gb→50gb", Strings(group.GetProperty("why")));
    }

    [GitFact]
    public async Task FromDate_RecordsTheOldContent_SoTheLaterChangeIsLoad()
    {
        ChangedAfterTheDate();
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);

        var result = await FromDateAsync();

        Assert.Equal(Between, result.GetProperty("from").GetProperty("date").GetString());
        Assert.Empty(Unbaselined(result).EnumerateArray());
        await AssertNumberChangeAsync();
    }

    [GitFact]
    public async Task FromDate_FollowsRenamesBack_ToTheOldPath()
    {
        ChangedAfterTheDate(rename: true);
        _ws.WriteTest("storage", "TC-101", ["docs/guides/a.md#Quotas"]);

        await FromDateAsync();

        await AssertNumberChangeAsync();
    }

    [GitFact]
    public async Task FromDate_ARefThatDidNotExistThen_IsLeftUnbaselinedWithTheReason()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        var first = _ws.Commit("one", Day1);
        _ws.WriteDoc("docs/new.md", Doc("New", ("Intro", "Added later.")));
        _ws.Edit("docs/a.md", "## Sharing", "## Limits\n\nTen files.\n\n## Sharing");
        _ws.Commit("later", Day3);
        _ws.WriteTest("storage", "TC-101", ["docs/new.md#Intro", "docs/a.md#Limits", "docs/a.md#Quotas"]);

        var result = await FromDateAsync();

        var reasons = Unbaselined(result).EnumerateArray().ToDictionary(u => u.GetProperty("ref").GetString()!, u => u.GetProperty("reason").GetString());
        Assert.Equal(2, reasons.Count);
        Assert.Equal($"file did not exist at {first[..7]}", reasons["docs/new.md#Intro"]);
        Assert.Equal($"anchor did not exist at {first[..7]}", reasons["docs/a.md#Limits"]);
        var refs = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.Equal(["docs/a.md#Quotas"], refs.EnumerateObject().Select(p => p.Name));
    }

    [GitFact]
    public async Task FromDate_NeverChangesAnExistingEntry()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.Commit("one", Day1);
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        _ws.Edit("docs/a.md", "10GB", "50GB");
        _ws.Commit("change", Day3);
        await _ws.InitBaselineAsync(); // current content (50GB)
        var before = _ws.ReadBaseline("storage");

        await FromDateAsync();

        Assert.Equal(before, _ws.ReadBaseline("storage"));
    }

    [GitFact]
    public async Task FromDate_PartialEntry_FillsOnlyTheMissingRef_TheOtherFingerprintByteIdentical()
    {
        // Analysis C1.
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteDoc("docs/b.md", Doc("B", ("Limits", "Ten files of 5MB.")));
        _ws.Commit("one", Day1);
        _ws.Edit("docs/a.md", "10GB", "50GB");
        _ws.Edit("docs/b.md", "5MB", "8MB");
        _ws.Commit("change", Day3);
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        var baselined = Fingerprint("TC-101", "docs/a.md#Quotas");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/b.md#Limits"]);

        var result = await FromDateAsync();

        Assert.Equal(1, result.GetProperty("suites")[0].GetProperty("completed").GetInt32());
        Assert.Equal(baselined, Fingerprint("TC-101", "docs/a.md#Quotas"));
        var triage = await _ws.TriageAsync();
        Assert.Contains("numbers: 5mb→8mb", Strings(Group(triage, "TC-101").GetProperty("why")));
        Assert.DoesNotContain(Strings(Group(triage, "TC-101").GetProperty("why")), w => w.Contains("10gb"));
    }

    [GitFact]
    public async Task FromRef_PinsTheOwner_OtherOwnersUseCurrentContentWithAWarning()
    {
        var child = _ws.Root + "-child";
        try
        {
            GitFixture.Init(child);
            File.WriteAllText(Path.Combine(child, "guide.md"), Doc("Guide", ("Setup", "Install it.")));
            GitFixture.Commit(child, "child", Day1);
            _ws.WriteDoc("docs/a.md", StorageDoc());
            GitFixture.AddSubmodule(_ws.Root, child, "docs/sub");
            var point = _ws.Commit("one", Day1);
            _ws.Edit("docs/a.md", "10GB", "50GB");
            _ws.Commit("change", Day3);
            _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
            _ws.WriteTest("storage", "TC-102", ["docs/sub/guide.md#Setup"]);

            var result = await _ws.InitBaselineAsync("storage", "--from-ref", point, "--owner", ".");

            Assert.Equal(point, result.GetProperty("from").GetProperty("ref").GetString());
            var warning = Assert.Single(result.GetProperty("warnings").EnumerateArray()).GetString();
            Assert.Contains("docs/sub/guide.md", warning);
            await AssertNumberChangeAsync();
            Assert.Equal(1, BaselineTestWorkspace.Count(await _ws.TriageAsync(), "storage", "unchanged"));
        }
        finally
        {
            GitFixture.DeleteTree(child);
        }
    }

    [GitTheory]
    [InlineData("INVALID_ARGUMENTS", "--init", "--from-date", Between, "--force")]
    [InlineData("INVALID_ARGUMENTS", "--init", "--from-ref", "HEAD")]
    [InlineData("INVALID_ARGUMENTS", "--init", "--owner", ".")]
    [InlineData("INVALID_ARGUMENTS", "--from-date", Between)]
    [InlineData("INVALID_ARGUMENTS", "--from-ref", "HEAD", "--owner", ".")]
    [InlineData("INVALID_ARGUMENTS", "--init", "--from-date", Between, "--from-ref", "HEAD", "--owner", ".")]
    [InlineData("INVALID_ARGUMENTS", "--init", "--from-date", "15/03/2026")]
    [InlineData("GIT_REF_UNAVAILABLE", "--init", "--from-ref", "no-such-ref", "--owner", ".")]
    [InlineData("GIT_REF_UNAVAILABLE", "--init", "--from-ref", "HEAD", "--owner", "@outside")]
    public async Task Refusals_WriteNothing(string code, params string[] args)
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.Commit("one", Day1);
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        var outside = Path.Combine(Path.GetTempPath(), $"spectra-086-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            var before = _ws.Snapshot();
            var argv = args.Select(a => a == "@outside" ? outside : a).ToArray();

            var (exit, output, error) = await _ws.RunAsync(["ai", "source-baseline", "--suite", "storage", .. argv, "--output-format", "json"]);

            Assert.Equal(4, exit);
            Assert.Empty(output);
            Assert.Equal(code, BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
            Assert.Equal(before, _ws.Snapshot());
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [GitFact]
    public async Task DryRun_WritesNothing()
    {
        ChangedAfterTheDate();
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        var before = _ws.Snapshot();

        var result = await FromDateAsync("--dry-run");

        Assert.Equal(1, result.GetProperty("suites")[0].GetProperty("created").GetInt32());
        Assert.Equal(before, _ws.Snapshot());
    }
}
