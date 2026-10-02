using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.IdAllocation;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 084 (FR-030, FR-031, contract error table): every failure of the three commands is one JSON document on stderr
/// in the shared shape, with the named code and its exit code; human mode ends with <c>[CODE]</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateTriageErrorTests : IDisposable
{
    private static readonly string[] Triage = ["ai", "update-triage", "--suite", "storage"];
    private static readonly string[] Ack = ["ai", "update-ack", "--suite", "storage", "--ids", "TC-101"];
    private static readonly string[] Init = ["ai", "source-baseline", "--init", "--suite", "storage"];

    private readonly BaselineTestWorkspace _ws = new();

    public UpdateTriageErrorTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
    }

    public void Dispose()
    {
        BaselineStore.LockTimeoutOverride = null;
        _ws.Dispose();
    }

    public static TheoryData<string> Commands => new() { "triage", "ack", "init" };

    private static string[] Args(string command) => command switch
    {
        "triage" => Triage,
        "ack" => Ack,
        _ => Init
    };

    private async Task AssertFailure(string[] args, string code, int exit, string command)
    {
        var (actualExit, output, error) = await _ws.RunAsync([.. args, "--output-format", "json"]);

        Assert.True(actualExit == exit, $"exit {actualExit}, stdout {output}, stderr {error}");
        Assert.Equal("", output);
        var document = BaselineTestWorkspace.Json(error.Trim().Split('\n').Last());
        Assert.Equal(["command", "success", "error_code", "errors", "warnings"], document.EnumerateObject().Select(p => p.Name));
        Assert.Equal(command, document.GetProperty("command").GetString());
        Assert.False(document.GetProperty("success").GetBoolean());
        Assert.Equal(code, document.GetProperty("error_code").GetString());

        var human = await _ws.RunAsync(args);
        Assert.Equal(exit, human.Exit);
        Assert.EndsWith($"[{code}]", human.Err.Trim());
    }

    private static string CommandName(string command) => command switch
    {
        "triage" => "update-triage",
        "ack" => "update-ack",
        _ => "source-baseline"
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task NoConfig_IsConfigMissing(string command)
    {
        File.Delete(_ws.PathOf("spectra.config.json"));
        await AssertFailure(Args(command), "CONFIG_MISSING", 1, CommandName(command));
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task MalformedConfig_IsConfigInvalid(string command)
    {
        File.WriteAllText(_ws.PathOf("spectra.config.json"), "{\"source\": {");
        await AssertFailure(Args(command), "CONFIG_INVALID", 1, CommandName(command));
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task MalformedBaseline_IsBaselineUnreadable_NamingTheFile(string command)
    {
        File.WriteAllText(_ws.BaselinePath("storage"), "{\"version\":1,\"tests\":[]}");
        await AssertFailure(Args(command), "BASELINE_UNREADABLE", 1, CommandName(command));

        var (_, _, error) = await _ws.RunAsync([.. Args(command), "--output-format", "json"]);
        Assert.Contains("test-cases/storage/_source-baseline.json", error);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task HeldLock_IsLockTimeout(string command)
    {
        BaselineStore.LockTimeoutOverride = TimeSpan.FromMilliseconds(200);
        using var held = await FileLockHandle.AcquireAsync(_ws.PathOf(".spectra/baseline.lock"), TimeSpan.FromSeconds(5));

        await AssertFailure(Args(command), "LOCK_TIMEOUT", 1, CommandName(command));
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task UnknownSuite_IsSuiteNotFound(string command)
    {
        var args = Args(command).Select(a => a == "storage" ? "nope" : a).ToArray();
        await AssertFailure(args, "SUITE_NOT_FOUND", 4, CommandName(command));
    }

    [Theory]
    [InlineData("ai update-triage", "update-triage")]
    [InlineData("ai update-triage --suite storage --all-suites", "update-triage")]
    [InlineData("ai source-baseline --suite storage", "source-baseline")]
    [InlineData("ai source-baseline --init", "source-baseline")]
    [InlineData("ai update-ack --suite storage", "update-ack")]
    public async Task BadArgumentCombinations_AreInvalidArguments(string args, string command) =>
        await AssertFailure(args.Split(' '), "INVALID_ARGUMENTS", 4, command);

    [Fact]
    public async Task IngestTests_HeldLock_IsLockTimeout_AndPersistsNothing()
    {
        BaselineStore.LockTimeoutOverride = TimeSpan.FromMilliseconds(200);
        using var held = await FileLockHandle.AcquireAsync(_ws.PathOf(".spectra/baseline.lock"), TimeSpan.FromSeconds(5));

        var (exit, _, error) = await _ws.RunAsync("ai", "ingest-tests", "storage", "--from",
            _ws.GeneratedPayload(("TC-150", ["docs/storage.md#Quotas"], [])), "--output-format", "json");

        Assert.Equal(1, exit);
        Assert.Equal("LOCK_TIMEOUT", BaselineTestWorkspace.Json(error.Trim()).GetProperty("error_code").GetString());
        Assert.False(File.Exists(_ws.PathOf("test-cases/storage/TC-150.md")));
    }
}
