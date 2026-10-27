using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Git;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 087 T5 (F5, FR-015–FR-017): the history bootstrap takes <c>--min-similarity</c> like <c>relink-sources</c>, and a
/// failed "renames since" read leaves the refs unbaselined with the reason instead of reading them from the wrong path.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BootstrapThresholdTests : IDisposable
{
    private static readonly DateTimeOffset Day1 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day3 = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);
    private const string Between = "2026-03-15";

    // git scores this rename 67% (probed on 2.53): below 90, above 60. The fixture asserts the range itself.
    private const string Before = "# Storage\n\n## Quotas\n\nEach user gets 10GB of storage space.\nUsers must log in first.\n"
        + "Quotas reset monthly.\nAdmins can raise a quota.\nFiles over the quota are rejected.\nThe archive tier holds old files.\n";
    private const string After = "# Storage\n\n## Quotas\n\nEach user gets 50GB of storage space.\nMembers must sign in first.\n"
        + "Quotas reset monthly.\nAdmins can raise a quota.\nFiles over the quota are rejected.\nThe archive tier holds old files.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public BootstrapThresholdTests()
    {
        if (GitAvailability.IsAvailable) _ws.InitGit();
    }

    public void Dispose()
    {
        GitAccess.RunnerOverride = null;
        _ws.Dispose();
    }

    /// <summary>docs/a.md on Day1; renamed to docs/g/a.md with heavy edits on Day3. Returns the Day1 commit.</summary>
    private string HeavilyEditedRename()
    {
        _ws.WriteDoc("docs/a.md", Before);
        var day1 = _ws.Commit("one", Day1);
        _ws.Move("docs/a.md", "docs/g/a.md");
        _ws.WriteDoc("docs/g/a.md", After);
        _ws.Commit("rename with heavy edits", Day3);
        _ws.WriteTest("storage", "TC-101", ["docs/g/a.md#Quotas"]);

        var line = GitFixture.Git(_ws.Root, "log", "-M60%", "--diff-filter=R", "--name-status", "--format=", "-1").Trim();
        var score = int.Parse(line[1..line.IndexOf('\t')]);
        Assert.InRange(score, 60, 89);
        return day1;
    }

    private static JsonElement Unbaselined(JsonElement result) => result.GetProperty("suites")[0].GetProperty("unbaselined");

    [GitFact]
    public async Task AHeavilyEditedRename_IsFollowedBack_WithMinSimilarity60()
    {
        var day1 = HeavilyEditedRename();

        var at90 = await _ws.InitBaselineAsync("storage", "--from-date", Between, "--dry-run");
        Assert.Equal($"file did not exist at {day1[..7]}", Unbaselined(at90)[0].GetProperty("reason").GetString());
        Assert.Equal(90, at90.GetProperty("from").GetProperty("min_similarity").GetInt32());

        var at60 = await _ws.InitBaselineAsync("storage", "--from-date", Between, "--min-similarity", "60");

        Assert.Empty(Unbaselined(at60).EnumerateArray());
        Assert.Equal(60, at60.GetProperty("from").GetProperty("min_similarity").GetInt32());
        var group = Group(await _ws.TriageAsync(), "TC-101");
        Assert.Equal("LOAD", group.GetProperty("bucket").GetString());
        Assert.Contains("numbers: 10gb→50gb", Strings(group.GetProperty("why")));
    }

    [GitFact]
    public async Task AFailedHistoryRead_LeavesTheRefsUnbaselined_WithTheReason()
    {
        HeavilyEditedRename();
        GitAccess.RunnerOverride = new FakeGitRunner((_, args) =>
            args.Contains("log") && args[^1].EndsWith("..HEAD", StringComparison.Ordinal)
                ? new GitProcessResult(128, [], "fatal: bad object\n", false)
                : null);

        var result = await _ws.InitBaselineAsync("storage", "--from-date", Between);

        var reason = Unbaselined(result)[0].GetProperty("reason").GetString();
        Assert.StartsWith("git history unavailable: git failed: fatal: bad object", reason);
    }

    [GitTheory]
    [InlineData("49")]
    [InlineData("101")]
    public async Task OutOfRange_IsRefused_ExactlyLikeRelinkSources(string value)
    {
        HeavilyEditedRename();
        var before = _ws.Snapshot();

        var (exit, output, error) = await _ws.RunAsync("ai", "source-baseline", "--init", "--suite", "storage",
            "--from-date", Between, "--min-similarity", value, "--output-format", "json");
        var (_, _, relinkError) = await _ws.RunAsync("ai", "relink-sources", "--suite", "storage",
            "--min-similarity", value, "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Empty(output);
        var failure = BaselineTestWorkspace.Json(error);
        Assert.Equal("INVALID_ARGUMENTS", failure.GetProperty("error_code").GetString());
        Assert.Equal(BaselineTestWorkspace.Json(relinkError).GetProperty("errors")[0].GetString(), failure.GetProperty("errors")[0].GetString());
        Assert.Equal($"--min-similarity must be between 50 and 100 (got {value}).", failure.GetProperty("errors")[0].GetString());
        Assert.Equal(before, _ws.Snapshot());
    }

    [GitFact]
    public async Task WithoutFromDateOrFromRef_IsRefused()
    {
        HeavilyEditedRename();
        var before = _ws.Snapshot();

        var (exit, output, error) = await _ws.RunAsync("ai", "source-baseline", "--init", "--suite", "storage",
            "--min-similarity", "60", "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Empty(output);
        var failure = BaselineTestWorkspace.Json(error);
        Assert.Equal("INVALID_ARGUMENTS", failure.GetProperty("error_code").GetString());
        Assert.Equal("--min-similarity is only used with --from-date or --from-ref.", failure.GetProperty("errors")[0].GetString());
        Assert.Equal(before, _ws.Snapshot());
    }
}
