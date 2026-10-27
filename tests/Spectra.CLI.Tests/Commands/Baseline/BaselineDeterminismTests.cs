using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>Spec 084 T9 (FR-007, FR-023): the same workspace gives byte-identical outputs and files.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BaselineDeterminismTests : IDisposable
{
    private const string StorageDoc =
        "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Über → Sharing\n\nFiles can be shared by link.\n";

    private readonly List<BaselineTestWorkspace> _workspaces = [];

    public void Dispose()
    {
        foreach (var ws in _workspaces) ws.Dispose();
    }

    private BaselineTestWorkspace NewWorkspace()
    {
        var ws = new BaselineTestWorkspace();
        _workspaces.Add(ws);
        ws.WriteDoc("docs/storage.md", StorageDoc);
        ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-002", "B"), ("AC-STORAGE-001", "A"));
        // Written in non-sorted order on purpose.
        ws.WriteTest("storage", "TC-110", ["docs/storage.md#Quotas"], ["AC-STORAGE-002", "AC-STORAGE-001"]);
        ws.WriteTest("storage", "TC-102", ["docs/storage.md#Über-→-Sharing", "docs/storage.md#Quotas"]);
        ws.WriteTest("storage", "TC-101", ["docs/storage.md"]);
        ws.WriteTest("billing", "TC-201", ["docs/storage.md#Quotas"]);
        return ws;
    }

    private static IReadOnlyDictionary<string, string> Snapshots(BaselineTestWorkspace ws) =>
        ws.Snapshot().Where(kv => kv.Key.StartsWith(".spectra/source-snapshots/", StringComparison.Ordinal)).ToDictionary(kv => kv.Key, kv => kv.Value);

    [Fact]
    public async Task RepeatedTriage_IsByteIdentical()
    {
        var ws = NewWorkspace();
        await ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        ws.WriteDoc("docs/storage.md", StorageDoc.Replace("10GB", "50GB").Replace("by link", "by **link**"));

        var outputs = new List<string>();
        var details = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var (exit, output, _) = await ws.RunAsync("ai", "update-triage", "--all-suites", "--no-auto-ack", "--output-format", "json");
            Assert.Equal(0, exit);
            outputs.Add(output);
            details.Add(File.ReadAllText(ws.PathOf(".spectra/update-triage.json")));
        }

        Assert.All(outputs, o => Assert.Equal(outputs[0], o));
        Assert.All(details, d => Assert.Equal(details[0], d));

        // After auto-ack settles, repeated runs are identical too.
        await ws.RunAsync("ai", "update-triage", "--all-suites", "--output-format", "json");
        var settled = (await ws.RunAsync("ai", "update-triage", "--all-suites", "--output-format", "json")).Out;
        Assert.Equal(settled, (await ws.RunAsync("ai", "update-triage", "--all-suites", "--output-format", "json")).Out);
    }

    [Fact]
    public async Task TwoCopiesOfAWorkspace_GiveIdenticalBaselinesAndSnapshots_WithNoPaths()
    {
        var a = NewWorkspace();
        var b = NewWorkspace();

        await a.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        await b.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");

        Assert.Equal(a.ReadBaseline("storage"), b.ReadBaseline("storage"));
        Assert.Equal(a.ReadBaseline("billing"), b.ReadBaseline("billing"));
        Assert.Equal(Snapshots(a), Snapshots(b));

        a.WriteDoc("docs/storage.md", StorageDoc.Replace("10GB", "50GB"));
        b.WriteDoc("docs/storage.md", StorageDoc.Replace("10GB", "50GB"));
        var triageA = (await a.RunAsync("ai", "update-triage", "--all-suites", "--output-format", "json")).Out;
        var triageB = (await b.RunAsync("ai", "update-triage", "--all-suites", "--output-format", "json")).Out;
        Assert.Equal(triageA, triageB);
        Assert.Equal(File.ReadAllText(a.PathOf(".spectra/update-triage.json")), File.ReadAllText(b.PathOf(".spectra/update-triage.json")));

        foreach (var text in new[] { triageA, File.ReadAllText(a.PathOf(".spectra/update-triage.json")), a.ReadBaseline("storage")! })
        {
            Assert.DoesNotContain(a.Root, text);
            Assert.DoesNotContain(a.Root.Replace('\\', '/'), text);
            Assert.DoesNotContain(Path.GetTempPath().TrimEnd('\\', '/'), text);
        }
    }

    [Fact]
    public async Task WrittenFiles_HaveNoCarriageReturns()
    {
        var ws = NewWorkspace();
        await ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        await ws.TriageAsync();

        foreach (var file in new[] { ws.BaselinePath("storage"), ws.PathOf(".spectra/update-triage.json") }
                     .Concat(ws.SnapshotFiles().Select(f => ws.PathOf(".spectra/source-snapshots/" + f))))
        {
            Assert.DoesNotContain((byte)'\r', File.ReadAllBytes(file));
        }
    }

    [Fact]
    public async Task OneTestsChange_IsALocalDiffInTheBaseline()
    {
        var ws = NewWorkspace();
        await ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        var before = ws.ReadBaseline("storage")!.Split('\n');
        ws.WriteDoc("docs/storage.md", StorageDoc.Replace("by link", "by email"));
        ws.WriteTest("storage", "TC-102", ["docs/storage.md#Über-→-Sharing"]); // only TC-102 changes

        await ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-102");

        var after = ws.ReadBaseline("storage")!.Split('\n');

        Assert.NotEqual(Block(before, "TC-102"), Block(after, "TC-102"));
        Assert.Equal(WithoutBlock(before, "TC-102"), WithoutBlock(after, "TC-102"));
    }

    /// <summary>The lines of one test's entry: from <c>"id": {</c> to its closing brace at the same indent.</summary>
    private static string[] Block(string[] lines, string id)
    {
        var start = Array.FindIndex(lines, l => l.TrimStart().StartsWith($"\"{id}\": {{", StringComparison.Ordinal));
        var indent = lines[start].Length - lines[start].TrimStart().Length;
        var end = Array.FindIndex(lines, start + 1, l => l.Length - l.TrimStart().Length == indent && l.TrimStart().StartsWith('}'));
        return lines[start..(end + 1)];
    }

    private static string[] WithoutBlock(string[] lines, string id)
    {
        var block = Block(lines, id);
        var start = Array.IndexOf(lines, block[0]);
        return [.. lines[..start], .. lines[(start + block.Length)..]];
    }
}
