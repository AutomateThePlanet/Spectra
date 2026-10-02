using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>Spec 084 User Story 3 (FR-024, FR-025, FR-026a): <c>spectra ai update-ack</c>.</summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateAckCommandTests : IDisposable
{
    private const string StorageDoc =
        "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n\n## Sharing\n\nFiles can be shared by link.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public UpdateAckCommandTests()
    {
        _ws.WriteDoc("docs/storage.md", StorageDoc);
        _ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-001", "Each user MUST get 10GB"));
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], ["AC-STORAGE-001"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-103", ["docs/storage.md#Sharing"]);
    }

    public void Dispose() => _ws.Dispose();

    private void EditDoc(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    private async Task<(int Exit, JsonElement Failure)> FailAsync(params string[] args)
    {
        var (exit, output, error) = await _ws.RunAsync([.. args, "--output-format", "json"]);
        Assert.True(exit != 0, $"expected a failure: {output}");
        return (exit, BaselineTestWorkspace.Json(error.Trim().Split('\n').Last()));
    }

    private static string Code(JsonElement failure) => failure.GetProperty("error_code").GetString()!;

    [Fact]
    public async Task Ids_RefreshRefsAndCriteria_TestFilesUntouched_NextTriageUnchanged()
    {
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");
        _ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-001", "Each user MUST get 50GB"));
        var tests = _ws.TestFiles();

        var result = await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101,TC-102");

        Assert.Equal(["TC-101", "TC-102"], result.GetProperty("tests").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(2, result.GetProperty("baseline").GetProperty("updated").GetInt32());
        Assert.Equal(tests, _ws.TestFiles());
        var triage = await _ws.TriageAsync();
        Assert.Equal(3, BaselineTestWorkspace.Count(triage, "storage", "unchanged"));
    }

    [Fact]
    public async Task Group_AcksAllMembers()
    {
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");
        var triage = await _ws.TriageAsync();
        var group = BaselineTestWorkspace.GroupOf(triage, "TC-102");

        await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--group", group.GetProperty("id").GetString()!);

        var next = await _ws.TriageAsync();
        Assert.Equal(JsonValueKind.Undefined, BaselineTestWorkspace.GroupOf(next, "TC-102").ValueKind);
    }

    [Fact]
    public async Task Group_ChangedSinceTriage_IsStale_AndNothingIsWritten()
    {
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-102").GetProperty("id").GetString()!;
        EditDoc("50GB", "80GB");
        var before = _ws.ReadBaseline("storage");

        var (exit, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--group", group);

        Assert.Equal(4, exit);
        Assert.Equal("TRIAGE_STALE", Code(failure));
        Assert.Equal(before, _ws.ReadBaseline("storage"));
    }

    [Fact]
    public async Task Group_WithoutATriage_IsTriageMissing()
    {
        await _ws.InitBaselineAsync();

        var (exit, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--group", "G1");

        Assert.Equal(4, exit);
        Assert.Equal("TRIAGE_MISSING", Code(failure));
    }

    [Fact]
    public async Task Group_FromATriageOfAnotherSuite_IsTriageMissing()
    {
        _ws.WriteTest("billing", "TC-201", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        await _ws.TriageAsync("billing");

        var (_, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--group", "G1");

        Assert.Equal("TRIAGE_MISSING", Code(failure));
    }

    [Fact]
    public async Task UnknownGroup_OrAGroupOfAnotherSuite_IsGroupNotFound()
    {
        _ws.WriteTest("billing", "TC-201", ["docs/storage.md#Quotas"]);
        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        EditDoc("10GB", "50GB");
        var triage = await _ws.RunJsonAsync("ai", "update-triage", "--all-suites");
        var billingGroup = BaselineTestWorkspace.GroupOf(triage, "TC-201").GetProperty("id").GetString()!;

        Assert.Equal("GROUP_NOT_FOUND", Code((await FailAsync("ai", "update-ack", "--suite", "storage", "--group", "G99")).Failure));
        Assert.Equal("GROUP_NOT_FOUND", Code((await FailAsync("ai", "update-ack", "--suite", "storage", "--group", billingGroup)).Failure));
    }

    [Fact]
    public async Task UnknownId_IsTestNotFound_AndNothingIsWritten()
    {
        await _ws.InitBaselineAsync();
        var before = _ws.Snapshot();

        var (exit, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101,TC-999");

        Assert.Equal(4, exit);
        Assert.Equal("TEST_NOT_FOUND", Code(failure));
        Assert.Contains("TC-999", failure.GetProperty("errors")[0].GetString());
        Assert.Equal(before, _ws.Snapshot());
    }

    [Theory]
    [InlineData("--suite storage")]
    [InlineData("--suite storage --ids TC-101 --group G1")]
    [InlineData("--ids TC-101")]
    public async Task BothOrNeitherOfIdsAndGroup_IsInvalidArguments(string args)
    {
        var (exit, failure) = await FailAsync(["ai", "update-ack", .. args.Split(' ')]);

        Assert.Equal(4, exit);
        Assert.Equal("INVALID_ARGUMENTS", Code(failure));
    }

    [Fact]
    public async Task RemovedRef_IsRefused_AndStaysRemoved()
    {
        await _ws.InitBaselineAsync();
        EditDoc("## Quotas", "## Storage quotas");
        var before = _ws.ReadBaseline("storage");

        var (exit, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101");

        Assert.Equal(4, exit);
        Assert.Equal("REF_REMOVED", Code(failure));
        Assert.Contains("TC-101: docs/storage.md#Quotas", failure.GetProperty("errors")[0].GetString());
        Assert.Equal(before, _ws.ReadBaseline("storage"));
        Assert.Equal("REMOVED", BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-101").GetProperty("bucket").GetString());
    }

    [Fact]
    public async Task RemovedCriterion_IsRefused()
    {
        await _ws.InitBaselineAsync();
        _ws.WriteCriteria("docs/storage.criteria.yaml", ("AC-STORAGE-009", "Something else"));

        var (_, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101");

        Assert.Equal("CRITERION_REMOVED", Code(failure));
    }

    [Fact]
    public async Task RemovedGroup_IsRefused()
    {
        await _ws.InitBaselineAsync();
        EditDoc("## Quotas", "## Storage quotas");
        var group = BaselineTestWorkspace.GroupOf(await _ws.TriageAsync(), "TC-102").GetProperty("id").GetString()!;

        var (_, failure) = await FailAsync("ai", "update-ack", "--suite", "storage", "--group", group);

        Assert.Equal("REF_REMOVED", Code(failure));
    }

    [Fact]
    public async Task HandFixedRemovedRef_ClearsTheTest_ThenAckRecordsTheNewRef()
    {
        await _ws.InitBaselineAsync();
        EditDoc("## Quotas", "## Storage quotas");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Storage-quotas"], ["AC-STORAGE-001"]); // fixed by hand

        var triage = await _ws.TriageAsync();
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "partial"));
        Assert.DoesNotContain(triage.GetProperty("removed").EnumerateArray(), r => r.GetProperty("test").GetString() == "TC-101");

        await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101");

        var refs = _ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-101").GetProperty("refs");
        Assert.Equal(["docs/storage.md#Storage-quotas"], refs.EnumerateObject().Select(p => p.Name));
        var next = await _ws.TriageAsync();
        Assert.Equal(0, BaselineTestWorkspace.Count(next, "storage", "partial"));
        Assert.DoesNotContain(next.GetProperty("removed").EnumerateArray(), r => r.GetProperty("test").GetString() == "TC-101");
    }

    [Fact]
    public async Task Ids_DropAStaleKey()
    {
        await _ws.InitBaselineAsync();
        _ws.WriteTest("storage", "TC-103", []); // Sharing dropped by hand

        await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-103");

        Assert.Empty(_ws.BaselineJson("storage").GetProperty("tests").GetProperty("TC-103").GetProperty("refs").EnumerateObject());
    }

    [Fact]
    public async Task DryRun_WritesNothing()
    {
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");
        var before = _ws.Snapshot();

        var result = await _ws.RunJsonAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101", "--dry-run");

        Assert.Equal(before, _ws.Snapshot());
        Assert.Equal(1, result.GetProperty("baseline").GetProperty("updated").GetInt32());
    }

    [Fact]
    public async Task UnacknowledgedChange_IsReportedAgain()
    {
        await _ws.InitBaselineAsync();
        EditDoc("10GB", "50GB");

        var first = await _ws.TriageAsync();
        var second = await _ws.TriageAsync();

        Assert.Equal(BaselineTestWorkspace.GroupOf(first, "TC-101").GetRawText(), BaselineTestWorkspace.GroupOf(second, "TC-101").GetRawText());
    }
}
