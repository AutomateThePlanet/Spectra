using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T4 and T7 (FR-008–FR-011, FR-022, SC-003/SC-004): <c>spectra ai relink-sources</c> end to end in a real
/// repository — only ref values change, the index and baseline follow, orphan status clears only when everything
/// resolves, <c>--dry-run</c> writes nothing, and the output is deterministic.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class RelinkSourcesTests : IDisposable
{
    private static readonly DateTimeOffset Day1 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly BaselineTestWorkspace _ws = new();
    private readonly List<BaselineTestWorkspace> _extra = [];

    public RelinkSourcesTests()
    {
        if (GitAvailability.IsAvailable) _ws.InitGit();
    }

    public void Dispose()
    {
        _ws.Dispose();
        foreach (var ws in _extra) ws.Dispose();
    }

    private string TestPath(string id = "TC-101") => _ws.PathOf($"test-cases/storage/{id}.md");

    private Task<JsonElement> RelinkAsync(BaselineTestWorkspace? ws = null, params string[] extra) =>
        (ws ?? _ws).RunJsonAsync(["ai", "relink-sources", "--suite", "storage", .. extra]);

    private static string Fingerprint(BaselineTestWorkspace ws, string id, string sourceRef) =>
        ws.BaselineJson("storage").GetProperty("tests").GetProperty(id).GetProperty("refs").GetProperty(sourceRef).GetString()!;

    [GitFact]
    public async Task OnlyTheRefValuesChange_InTheirOwnQuotingStyle()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteDoc("docs/b.md", Doc("B", ("X", "Text of b.")));
        _ws.WriteDoc("docs/c.md", Doc("C", ("Y", "Text of c.")));
        _ws.WriteDoc("docs/keep.md", Doc("Keep", ("Z", "Text of keep.")));
        const string original =
            "---\n# header comment\nid: TC-101\npriority: high\nsource_refs:\n"
            + "  - docs/a.md#Quotas\n  - \"docs/b.md#X\"\n  # a comment inside the list\n  - 'docs/c.md'\n  - docs/keep.md\n"
            + "tags: [smoke]\ncustom_key: kept\n---\n\n# Title\n\n## Steps\n\n1. Do it\n\n## Expected Result\n\nIt works\n\n## Notes\n\nKept.\n";
        Directory.CreateDirectory(_ws.PathOf("test-cases/storage"));
        File.WriteAllText(TestPath(), original);
        _ws.RebuildIndex("storage");
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Move("docs/b.md", "docs/guides/b.md");
        _ws.Move("docs/c.md", "docs/guides/c.md");
        _ws.Commit("restructure");

        var result = await RelinkAsync();

        var expected = original.Replace("  - docs/a.md#Quotas\n", "  - docs/guides/a.md#Quotas\n")
            .Replace("\"docs/b.md#X\"", "\"docs/guides/b.md#X\"").Replace("'docs/c.md'", "'docs/guides/c.md'");
        Assert.Equal(expected, File.ReadAllText(TestPath()));
        Assert.Equal(3, result.GetProperty("counts").GetProperty("refs_relinked").GetInt32());
        Assert.Empty(result.GetProperty("warnings").EnumerateArray());

        var index = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf("test-cases/storage/_index.json")));
        var entry = index.GetProperty("tests").EnumerateArray().Single(t => t.GetProperty("id").GetString() == "TC-101");
        Assert.Equal(["docs/guides/a.md#Quotas", "docs/guides/b.md#X", "docs/guides/c.md", "docs/keep.md"],
            Strings(entry.GetProperty("source_refs")));
    }

    [GitTheory]
    [InlineData(true, "LOAD")]
    [InlineData(false, "UNCHANGED")]
    public async Task TheBaselineKeyMovesWithItsOldFingerprint(bool editInTheRenameCommit, string nextState)
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        var old = Fingerprint(_ws, "TC-101", "docs/a.md#Quotas");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        if (editInTheRenameCommit) _ws.Edit("docs/guides/a.md", "10GB", "50GB");
        _ws.Commit("move");

        await RelinkAsync();

        var refs = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.False(refs.TryGetProperty("docs/a.md#Quotas", out _));
        Assert.Equal(old, refs.GetProperty("docs/guides/a.md#Quotas").GetString());

        var triage = await _ws.TriageAsync();
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", nextState.ToLowerInvariant()));
        if (editInTheRenameCommit)
        {
            Assert.Contains("numbers: 10gb→50gb", Strings(Group(triage, "TC-101").GetProperty("why")));
        }
    }

    [GitFact]
    public async Task TheTestAlreadyListsTheTarget_TheOldRefIsDropped_TheTargetEntryKept()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/guides/a.md#Quotas"]);
        await _ws.InitBaselineAsync(); // completes the new key from current content
        var target = Fingerprint(_ws, "TC-101", "docs/guides/a.md#Quotas");

        await RelinkAsync();

        Assert.Equal(BaselineTestWorkspace.TestMarkdown("TC-101", ["docs/guides/a.md#Quotas"]), File.ReadAllText(TestPath()));
        var refs = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.Equal(target, refs.GetProperty("docs/guides/a.md#Quotas").GetString());
        Assert.False(refs.TryGetProperty("docs/a.md#Quotas", out _));
    }

    [GitTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrphanStatus_IsClearedOnlyWhenEveryRefResolves(bool otherRefResolves)
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteDoc("docs/b.md", Doc("B", ("Limits", "Ten files.")));
        var markdown = BaselineTestWorkspace.TestMarkdown("TC-101", ["docs/a.md#Quotas", "docs/b.md#Limits"])
            .Replace("priority: high\n", "priority: high\nstatus: orphaned\norphaned_reason: \"Source doc gone\"\norphaned_date: 2026-01-02\n");
        Directory.CreateDirectory(_ws.PathOf("test-cases/storage"));
        File.WriteAllText(TestPath(), markdown);
        _ws.RebuildIndex("storage");
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        if (!otherRefResolves) _ws.Edit("docs/b.md", "## Limits", "## Caps"); // a heading rename: stays REMOVED (D5)
        _ws.Commit("move");

        var result = await RelinkAsync();

        var text = File.ReadAllText(TestPath());
        var relinked = Assert.Single(result.GetProperty("relinked").EnumerateArray());
        Assert.Equal(otherRefResolves, relinked.GetProperty("orphan_cleared").GetBoolean());
        Assert.Contains("docs/guides/a.md#Quotas", text);
        if (otherRefResolves)
        {
            Assert.Equal(markdown.Replace("status: orphaned\norphaned_reason: \"Source doc gone\"\norphaned_date: 2026-01-02\n", "")
                .Replace("docs/a.md#Quotas", "docs/guides/a.md#Quotas"), text);
        }
        else
        {
            Assert.Contains("status: orphaned", text);
            Assert.Contains("orphaned_date: 2026-01-02", text);
        }
    }

    [GitFact]
    public async Task DryRun_WritesNothingAnywhere()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move");
        var before = _ws.Snapshot();

        var result = await RelinkAsync(null, "--dry-run");

        Assert.Equal("docs/guides/a.md#Quotas", RelinkedTo(result, "TC-101"));
        Assert.Equal(before, _ws.Snapshot());
        Assert.False(File.Exists(_ws.PathOf(".spectra/baseline.lock")));
    }

    [GitTheory]
    [InlineData("49")]
    [InlineData("101")]
    public async Task MinSimilarityOutOfRange_IsRefused(string value)
    {
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);

        var (exit, output, error) = await _ws.RunAsync("ai", "relink-sources", "--suite", "storage", "--min-similarity", value, "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Equal("INVALID_ARGUMENTS", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
    }

    [GitFact]
    public async Task TheHumanSummary_IsOnStderrInJsonMode()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move");

        var (exit, output, error) = await _ws.RunAsync("ai", "relink-sources", "--suite", "storage", "--dry-run", "--output-format", "json");

        Assert.Equal(0, exit);
        Assert.Equal("relink-sources", BaselineTestWorkspace.Json(output).GetProperty("command").GetString());
        Assert.Contains("storage/TC-101  docs/a.md#Quotas → docs/guides/a.md#Quotas", error);
    }

    // ---- analysis H1: refs and tests without a baseline entry ----

    [GitFact]
    public async Task NeverBaselinedRef_IsRewritten_NoKeyIsCreated()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteDoc("docs/b.md", Doc("B", ("Limits", "Ten files.")));
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move before any baseline");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/b.md#Limits"]);
        await _ws.InitBaselineAsync();
        var before = _ws.ReadBaseline("storage");

        await RelinkAsync();

        Assert.Contains("docs/guides/a.md#Quotas", File.ReadAllText(TestPath()));
        Assert.Equal(before, _ws.ReadBaseline("storage"));
        var triage = await _ws.TriageAsync();
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "partial"));
        Assert.Equal(["docs/guides/a.md#Quotas"], Strings(_ws.DetailOf("TC-101").GetProperty("unbaselined_refs")));
    }

    [GitFact]
    public async Task TestWithNoEntry_IsRewritten_NoEntryIsCreated()
    {
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move before any baseline");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);

        await RelinkAsync();

        Assert.Equal(BaselineTestWorkspace.TestMarkdown("TC-101", ["docs/guides/a.md#Quotas"]), File.ReadAllText(TestPath()));
        Assert.Null(_ws.ReadBaseline("storage"));
    }

    // ---- T7: determinism ----

    [GitFact]
    public async Task SameRepositoryState_GivesByteIdenticalOutput()
    {
        var second = new BaselineTestWorkspace();
        _extra.Add(second);
        var outputs = new List<(string Relink, string Triage, Dictionary<string, string> Files)>();
        foreach (var ws in new[] { _ws, second })
        {
            if (ws != _ws) ws.InitGit();
            ws.WriteDoc("docs/a.md", StorageDoc());
            ws.WriteDoc("docs/b.md", Doc("B", ("Limits", "Ten files.")));
            ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
            ws.WriteTest("storage", "TC-102", ["'docs/b.md'", "docs/a.md"]);
            await ws.InitBaselineAsync();
            ws.Commit("one", Day1);
            ws.Move("docs/a.md", "docs/guides/a.md");
            ws.Move("docs/b.md", "docs/guides/b.md");
            ws.Edit("docs/guides/a.md", "10GB", "50GB");
            ws.Commit("move", Day2);

            var (_, relink, _) = await ws.RunAsync("ai", "relink-sources", "--suite", "storage", "--output-format", "json");
            var (_, triage, _) = await ws.RunAsync("ai", "update-triage", "--suite", "storage", "--dry-run", "--output-format", "json");
            var files = new Dictionary<string, string>
            {
                ["TC-101"] = File.ReadAllText(ws.PathOf("test-cases/storage/TC-101.md")),
                ["TC-102"] = File.ReadAllText(ws.PathOf("test-cases/storage/TC-102.md")),
                ["baseline"] = ws.ReadBaseline("storage")!,
                ["index"] = WithoutGeneratedAt(File.ReadAllText(ws.PathOf("test-cases/storage/_index.json")))
            };
            outputs.Add((relink, triage, files));
        }

        Assert.Equal(outputs[0].Relink, outputs[1].Relink);
        Assert.Equal(outputs[0].Triage, outputs[1].Triage);
        Assert.Equal(outputs[0].Files, outputs[1].Files);
        Assert.Contains("docs/guides/a.md#Quotas", outputs[0].Relink);
    }

    /// <summary>The index carries its generation time; everything else must match.</summary>
    private static string WithoutGeneratedAt(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("generated_at");
        return node.ToJsonString();
    }
}
