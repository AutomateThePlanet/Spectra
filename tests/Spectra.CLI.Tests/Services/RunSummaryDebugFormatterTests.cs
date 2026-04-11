using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Services;

public class RunSummaryDebugFormatterTests
{
    [Fact]
    public void FormatRunTotal_GenerateWithPhases_MatchesExpectedFormat()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("analysis", "gpt-4.1", "github-models", 8900, 6200, TimeSpan.FromSeconds(18.5));
        tracker.Record("generation", "gpt-4.1", "github-models", 4000, 3000, TimeSpan.FromSeconds(20));
        tracker.Record("generation", "gpt-4.1", "github-models", 4000, 3000, TimeSpan.FromSeconds(20));
        tracker.Record("generation", "gpt-4.1", "github-models", 5580, 5240, TimeSpan.FromSeconds(12.3));
        for (int i = 0; i < 20; i++)
        {
            tracker.Record("critic", "gpt-4.1", "github-models", 2100, 340, TimeSpan.FromSeconds(4.7));
        }

        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", "checkout", tracker, TimeSpan.FromSeconds(165));

        Assert.StartsWith("RUN TOTAL command=generate suite=checkout", line);
        Assert.Contains("calls=24", line);
        Assert.Contains("tokens_in=64480", line);
        Assert.Contains("tokens_out=24240", line);
        Assert.DoesNotContain("~tokens_in", line);
        Assert.DoesNotContain("~tokens_out", line);
        Assert.Contains("elapsed=2m45s", line);
        Assert.Contains("phases=analysis:1/18.5s,generation:3/52.3s,critic:20/1m34s", line);
    }

    [Fact]
    public void FormatRunTotal_EmptyTracker_EmitsZeroCallsLine()
    {
        var tracker = new TokenUsageTracker();
        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "update", "checkout", tracker, TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(12)));

        Assert.Contains("command=update", line);
        Assert.Contains("calls=0", line);
        Assert.Contains("tokens_in=0", line);
        Assert.Contains("tokens_out=0", line);
        Assert.Contains("elapsed=4m12s", line);
        Assert.EndsWith("phases=", line);
    }

    [Fact]
    public void FormatRunTotal_AnyPhaseEstimated_PrefixesTildesOnTotals()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("analysis", "gpt-4.1", "github-models", 1000, 500, TimeSpan.FromSeconds(5), estimated: false);
        tracker.Record("generation", "gpt-4.1", "github-models", 2000, 1000, TimeSpan.FromSeconds(10), estimated: true);

        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", "checkout", tracker, TimeSpan.FromSeconds(20));

        Assert.Contains("~tokens_in=3000", line);
        Assert.Contains("~tokens_out=1500", line);
    }

    [Fact]
    public void FormatRunTotal_NullSuite_RendersDashPlaceholder()
    {
        var tracker = new TokenUsageTracker();
        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", null, tracker, TimeSpan.FromSeconds(1));
        Assert.Contains("suite=-", line);
    }

    [Fact]
    public void FormatRunTotal_EmptySuite_RendersDashPlaceholder()
    {
        var tracker = new TokenUsageTracker();
        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", "", tracker, TimeSpan.FromSeconds(1));
        Assert.Contains("suite=-", line);
    }

    [Fact]
    public void FormatRunTotal_SubsecondPhase_FormatsAsDecimalSeconds()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("analysis", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromMilliseconds(800));

        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", "x", tracker, TimeSpan.FromMilliseconds(800));

        Assert.Contains("analysis:1/0.8s", line);
    }

    [Fact]
    public void FormatRunTotal_HourLongPhase_FormatsHourMinSec()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("generation", "gpt-4.1", "github-models", 100, 50,
            new TimeSpan(1, 3, 42));

        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", "x", tracker, new TimeSpan(1, 3, 42));

        Assert.Contains("generation:1/1h03m42s", line);
        Assert.Contains("elapsed=1h03m42s", line);
    }

    [Fact]
    public void FormatRunTotal_MultiplePhases_CommaSeparated()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("analysis", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(2));
        tracker.Record("generation", "gpt-4.1", "github-models", 200, 100, TimeSpan.FromSeconds(5));

        var line = RunSummaryDebugFormatter.FormatRunTotal(
            "generate", "x", tracker, TimeSpan.FromSeconds(10));

        Assert.Contains("phases=analysis:1/2.0s,generation:1/5.0s", line);
    }
}
