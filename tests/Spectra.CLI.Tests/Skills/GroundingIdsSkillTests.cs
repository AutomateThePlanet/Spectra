using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 097 F1 (FR-001–FR-003; research R1, R9): the generate skill's critic step (8a) and the update skill's
/// re-verify step (5a) write grounding blocks only for the tests they confirmed — <c>ingest-grounding --all --ids</c>,
/// skipped when there are none. On 807047c both called <c>ingest-grounding --all</c> without ids, so a test reported
/// unverified still got a block and leftover verdicts inflated <c>written</c>. The 075 guard text and the NON-STOP
/// contract are pinned verbatim (copies of the 807047c text; these rows pass on both).
/// </summary>
public sealed class GroundingIdsSkillTests
{
    private const string Anomaly807047c = """
          > GROUNDING-INGEST ANOMALY: `ingest-grounding --all` returned `written: 0` but {kept-grounded} tests were marked grounded in the critic loop. This means the grounding blocks were not written. Likely causes: `_index.json` file paths are suite-prefixed (path-doubling bug) or the suite directory could not be resolved. **STOP — do not proceed to 8b, do not edit `.md` files by hand, do not rewrite verdict fields by hand.** Run `spectra ai audit-grounding --suite {suite}` to diagnose the current grounding state, then report the anomaly to the user and halt.
        """;

    private const string Warning807047c = """
          > GROUNDING-INGEST WARNING: expected {kept-grounded} grounding blocks written, got {written}. Continuing to 8b — run `spectra ai audit-grounding --suite {suite}` to inspect which tests are missing their grounding block.
        """;

    private const string NonStop807047c = """
        **NON-STOP CONTRACT (restated):** A broken or zero-result CLI verb is a STOP signal — it is NEVER a license to do the work by hand. Manual editing of `.md` files, rewriting of `grounding:` frontmatter, and synthesizing verdict fields are PROHIBITED regardless of how blocked you are. If a CLI verb returns an anomaly, stop and report. Do not improvise. Exit `8` from `ingest-update --all` is neither broken nor zero-result: the batch completed and `results` lists the failed files — retry them as 8b describes.
        """;

    private static string Generate => SkillContent.Generate.Replace("\r\n", "\n");

    private static string Update => SkillContent.Update.Replace("\r\n", "\n");

    [Fact]
    public void Generate8a_NamesTheKeptGroundedTests()
    {
        Assert.Contains("spectra ai ingest-grounding --suite {suite} --all --ids {kept-grounded ids} --output-format json", Generate);
        Assert.DoesNotContain("spectra ai ingest-grounding --suite {suite} --all --output-format json", Generate);
    }

    [Fact]
    public void Generate8a_SkipsTheCall_WhenNoTestWasKeptAsGrounded()
    {
        var step = Generate[Generate.IndexOf("--all --ids {kept-grounded ids}", StringComparison.Ordinal)..];
        Assert.Contains("when no test was kept as grounded", step[..Math.Min(step.Length, 600)]);
    }

    [Fact]
    public void Update5a_NamesTheGroundedTests_AfterTheRouting()
    {
        const string call = "spectra ai ingest-grounding --suite <suite> --all --ids <grounded ids> --output-format json";
        Assert.Contains(call, Update);
        Assert.DoesNotContain("spectra ai ingest-grounding --suite <suite> --all --output-format json", Update);
        Assert.True(
            Update.IndexOf(call, StringComparison.Ordinal) > Update.IndexOf("Route each test by its entry", StringComparison.Ordinal),
            "the grounding call comes after the routing by results");
        var at = Update.IndexOf(call, StringComparison.Ordinal);
        var around = Update[Math.Max(0, at - 300)..Math.Min(Update.Length, at + call.Length + 300)];
        Assert.Contains("no test of the group is", around.Replace('\n', ' '));
    }

    [Theory]
    [InlineData("anomaly")]
    [InlineData("warning")]
    [InlineData("nonstop")]
    public void The075GuardAndTheNonStopContract_StayVerbatim(string which)
    {
        var expected = which switch
        {
            "anomaly" => Anomaly807047c,
            "warning" => Warning807047c,
            _ => NonStop807047c,
        };

        Assert.Contains(expected.Trim(), Generate);
    }
}
