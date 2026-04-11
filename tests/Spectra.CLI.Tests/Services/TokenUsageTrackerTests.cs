using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Services;

public class TokenUsageTrackerTests
{
    [Fact]
    public void Record_SingleCall_TracksCorrectly()
    {
        var t = new TokenUsageTracker();
        t.Record("generation", "gpt-4o", "github-models", 100, 50, TimeSpan.FromSeconds(3));

        var summary = t.GetSummary();
        Assert.Single(summary);
        Assert.Equal("generation", summary[0].Phase);
        Assert.Equal(1, summary[0].Calls);
        Assert.Equal(100, summary[0].TokensIn);
        Assert.Equal(50, summary[0].TokensOut);
        Assert.Equal(150, summary[0].TotalTokens);
        Assert.Equal(TimeSpan.FromSeconds(3), summary[0].Elapsed);
    }

    [Fact]
    public void Record_MultipleCalls_AggregatesByPhase()
    {
        var t = new TokenUsageTracker();
        t.Record("generation", "gpt-4o", "github-models", 100, 50, TimeSpan.FromSeconds(3));
        t.Record("generation", "gpt-4o", "github-models", 200, 80, TimeSpan.FromSeconds(4));
        t.Record("critic", "gpt-4o-mini", "github-models", 30, 10, TimeSpan.FromSeconds(1));

        var summary = t.GetSummary();
        Assert.Equal(2, summary.Count);

        var gen = summary.First(p => p.Phase == "generation");
        Assert.Equal(2, gen.Calls);
        Assert.Equal(300, gen.TokensIn);
        Assert.Equal(130, gen.TokensOut);
        Assert.Equal(TimeSpan.FromSeconds(7), gen.Elapsed);

        var critic = summary.First(p => p.Phase == "critic");
        Assert.Equal(1, critic.Calls);
        Assert.Equal(40, critic.TotalTokens);
    }

    [Fact]
    public void Record_DifferentModels_SeparateEntries()
    {
        var t = new TokenUsageTracker();
        t.Record("generation", "gpt-4o", "github-models", 100, 50, TimeSpan.FromSeconds(3));
        t.Record("generation", "gpt-4o-mini", "github-models", 200, 80, TimeSpan.FromSeconds(4));

        var summary = t.GetSummary();
        Assert.Equal(2, summary.Count);
        Assert.Contains(summary, p => p.Model == "gpt-4o");
        Assert.Contains(summary, p => p.Model == "gpt-4o-mini");
    }

    [Fact]
    public void GetTotal_SumsAllPhases()
    {
        var t = new TokenUsageTracker();
        t.Record("analysis", "gpt-4o", "github-models", 100, 50, TimeSpan.FromSeconds(2));
        t.Record("generation", "gpt-4o", "github-models", 200, 100, TimeSpan.FromSeconds(5));
        t.Record("critic", "gpt-4o-mini", "github-models", 50, 20, TimeSpan.FromSeconds(1));

        var total = t.GetTotal();
        Assert.Equal(3, total.Calls);
        Assert.Equal(350, total.TokensIn);
        Assert.Equal(170, total.TokensOut);
        Assert.Equal(520, total.TotalTokens);
        Assert.Equal(TimeSpan.FromSeconds(8), total.Elapsed);
        Assert.Equal("TOTAL", total.Phase);
    }

    [Fact]
    public void Record_NullTokens_StillIncrementsCalls()
    {
        var t = new TokenUsageTracker();
        t.Record("generation", "gpt-4o", "github-models", null, null, TimeSpan.FromSeconds(3));

        var summary = t.GetSummary();
        Assert.Single(summary);
        Assert.Equal(1, summary[0].Calls);
        Assert.Equal(0, summary[0].TokensIn);
        Assert.Equal(0, summary[0].TokensOut);
        Assert.Equal(TimeSpan.FromSeconds(3), summary[0].Elapsed);
    }

    [Fact]
    public void ThreadSafety_ConcurrentRecords()
    {
        var t = new TokenUsageTracker();
        const int callsPerThread = 100;
        const int threads = 8;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < callsPerThread; i++)
            {
                t.Record("generation", "gpt-4o", "github-models", 10, 5, TimeSpan.FromMilliseconds(10));
            }
        });

        var summary = t.GetSummary();
        Assert.Single(summary);
        Assert.Equal(threads * callsPerThread, summary[0].Calls);
        Assert.Equal(threads * callsPerThread * 10, summary[0].TokensIn);
    }

    [Fact]
    public void HasData_EmptyTracker_False()
    {
        var t = new TokenUsageTracker();
        Assert.False(t.HasData());
    }

    [Fact]
    public void HasData_AfterRecord_True()
    {
        var t = new TokenUsageTracker();
        t.Record("analysis", "gpt-4o", "github-models", null, null, TimeSpan.Zero);
        Assert.True(t.HasData());
    }

    [Fact]
    public void Record_Estimated_AggregatesFlag()
    {
        var t = new TokenUsageTracker();
        t.Record("generation", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(1), estimated: false);
        t.Record("generation", "gpt-4.1", "github-models", 200, 80, TimeSpan.FromSeconds(2), estimated: true);

        var summary = t.GetSummary();
        Assert.Single(summary);
        Assert.True(summary[0].Estimated);
        Assert.Equal(300, summary[0].TokensIn);
        Assert.Equal(130, summary[0].TokensOut);
    }

    [Fact]
    public void Record_NotEstimated_AggregateFalse()
    {
        var t = new TokenUsageTracker();
        t.Record("generation", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(1));
        t.Record("generation", "gpt-4.1", "github-models", 200, 80, TimeSpan.FromSeconds(2));

        var summary = t.GetSummary();
        Assert.Single(summary);
        Assert.False(summary[0].Estimated);
    }

    [Fact]
    public void GetTotal_MixedEstimated_TotalEstimatedTrue()
    {
        var t = new TokenUsageTracker();
        t.Record("analysis", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(1), estimated: false);
        t.Record("generation", "gpt-4.1", "github-models", 200, 80, TimeSpan.FromSeconds(2), estimated: true);

        var total = t.GetTotal();
        Assert.True(total.Estimated);
    }

    [Fact]
    public void GetTotal_AllNonEstimated_TotalEstimatedFalse()
    {
        var t = new TokenUsageTracker();
        t.Record("analysis", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(1));
        t.Record("generation", "gpt-4.1", "github-models", 200, 80, TimeSpan.FromSeconds(2));

        var total = t.GetTotal();
        Assert.False(total.Estimated);
    }
}
