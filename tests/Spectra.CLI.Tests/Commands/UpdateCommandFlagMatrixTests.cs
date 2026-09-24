using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 078 (US3, FR-008–FR-010): the <c>spectra ai update</c> flag matrix in
/// specs/078-lossless-test-writes/contracts/ai-update-cli.md. Classify-only runs write nothing (run-status
/// files excepted), always print the classification tables, always write <c>.spectra-result.json</c>, and exit
/// with the same code as an applying run.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class UpdateCommandFlagMatrixTests : IDisposable
{
    private const string Suite = "smoke";
    private readonly UpdateWorkspace _ws = new();

    public UpdateCommandFlagMatrixTests()
    {
        _ws.WriteDoc("docs/checkout.md", UpdateWorkspace.MatchingDoc);
        _ws.WriteTest(Suite, "TC-001", UpdateWorkspace.SimpleTest("TC-001", "docs/checkout.md#Payment"));
        _ws.WriteTest(Suite, "TC-002", UpdateWorkspace.SimpleTest("TC-002", "docs/removed.md#Gone"));
    }

    public void Dispose() => _ws.Dispose();

    public static TheoryData<string[]> ClassifyOnlyRows => new()
    {
        new[] { "--diff", "--dry-run", "--no-interaction" },
        new[] { "--diff", "--dry-run", "--no-review" },
        new[] { "--diff", "--no-interaction" },
        new[] { "--dry-run" },                                   // no --diff: must not reach the interactive reviewer
        new[] { "--dry-run", "--no-interaction" },
        new[] { "--no-interaction", "--delete-orphaned" },       // must not delete
        new[] { "--dry-run", "--no-review", "--delete-orphaned" }
    };

    public static TheoryData<string[]> ApplyingRows => new()
    {
        new[] { "--diff", "--no-interaction", "--no-review" },
        new[] { "--diff", "--no-review" },
        new[] { "--no-review" }
    };

    private Task<(int Exit, string Output)> RunUpdateAsync(string[] flags, string outputFormat = "json") =>
        _ws.RunAsync(["ai", "update", Suite, .. flags, "--output-format", outputFormat, "--verbosity", "quiet"]);

    [Theory]
    [MemberData(nameof(ClassifyOnlyRows))]
    public async Task ClassifyOnly_WritesNothing_ButReports(string[] flags)
    {
        var before = _ws.Snapshot();

        var (exit, output) = await RunUpdateAsync(flags);

        Assert.True(exit == 0, output);
        Assert.Equal(before, _ws.Snapshot());
        Assert.Contains("Classification Results", output);
        Assert.Contains("Orphaned", output);

        var result = _ws.ReadResult();
        Assert.False(result.GetProperty("applied").GetBoolean());
        Assert.Equal(1, result.GetProperty("classification").GetProperty("orphaned").GetInt32());
        Assert.Equal(0, result.GetProperty("testsRemoved").GetInt32());
    }

    [Theory]
    [MemberData(nameof(ApplyingRows))]
    public async Task Applying_MarksOrphans_AndReportsApplied(string[] flags)
    {
        var before = _ws.Snapshot();

        var (exit, output) = await RunUpdateAsync(flags);

        Assert.True(exit == 0, output);
        Assert.NotEqual(before, _ws.Snapshot());
        Assert.Contains("status: orphaned", File.ReadAllText(_ws.PathOf($"test-cases/{Suite}/TC-002.md")));
        Assert.DoesNotContain("status: orphaned", File.ReadAllText(_ws.PathOf($"test-cases/{Suite}/TC-001.md")));

        var result = _ws.ReadResult();
        Assert.True(result.GetProperty("applied").GetBoolean());
        Assert.Equal(1, result.GetProperty("classification").GetProperty("orphaned").GetInt32());
    }

    [Fact]
    public async Task NoReviewWithDeleteOrphaned_DeletesTheOrphan()
    {
        var (exit, output) = await RunUpdateAsync(["--no-review", "--delete-orphaned"]);

        Assert.True(exit == 0, output);
        Assert.False(File.Exists(_ws.PathOf($"test-cases/{Suite}/TC-002.md")));
        Assert.True(File.Exists(_ws.PathOf($"test-cases/{Suite}/TC-001.md")));
    }

    [Fact]
    public async Task NoInteraction_PrintsApplyHint_InHumanOutput()
    {
        var (exit, output) = await RunUpdateAsync(["--no-interaction"], outputFormat: "human");

        Assert.Equal(0, exit);
        Assert.Contains("Pass --no-review to apply.", output);
    }

    [Fact]
    public async Task DryRun_DoesNotPrintApplyHint()
    {
        var (_, output) = await RunUpdateAsync(["--dry-run", "--no-interaction"], outputFormat: "human");

        Assert.Contains("Dry run - no changes were made.", output);
        Assert.DoesNotContain("Pass --no-review to apply.", output);
    }

    [Fact]
    public async Task ClassifyOnlyAndApplying_ReturnTheSameExitCode()
    {
        var (classifyExit, _) = await RunUpdateAsync(["--no-interaction"]);
        var (applyExit, _) = await RunUpdateAsync(["--no-interaction", "--no-review"]);

        Assert.Equal(applyExit, classifyExit);
    }
}
