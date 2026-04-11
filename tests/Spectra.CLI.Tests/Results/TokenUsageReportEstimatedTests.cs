using System.Text.Json;
using Spectra.CLI.Results;
using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Results;

public class TokenUsageReportEstimatedTests
{
    [Fact]
    public void FromTracker_NoEstimatedCalls_ReportEstimatedFalse()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("analysis", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(1), estimated: false);

        var report = TokenUsageReport.FromTracker(tracker);
        Assert.False(report.Estimated);
        Assert.False(report.Total.Estimated);
        Assert.All(report.Phases, p => Assert.False(p.Estimated));
    }

    [Fact]
    public void FromTracker_OneEstimatedCall_ReportEstimatedTrue()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("generation", "gpt-4.1", "github-models", 1000, 500, TimeSpan.FromSeconds(5), estimated: true);

        var report = TokenUsageReport.FromTracker(tracker);
        Assert.True(report.Estimated);
        Assert.True(report.Total.Estimated);
        Assert.Single(report.Phases);
        Assert.True(report.Phases[0].Estimated);
    }

    [Fact]
    public void FromTracker_MixedCalls_PerPhaseEstimatedFlagCorrect()
    {
        var tracker = new TokenUsageTracker();
        // Non-estimated analysis phase
        tracker.Record("analysis", "gpt-4.1", "github-models", 100, 50, TimeSpan.FromSeconds(1), estimated: false);
        // Estimated generation phase
        tracker.Record("generation", "gpt-4.1", "github-models", 2000, 1000, TimeSpan.FromSeconds(10), estimated: true);
        // Non-estimated critic phase
        tracker.Record("critic", "gpt-4.1", "github-models", 300, 150, TimeSpan.FromSeconds(2), estimated: false);

        var report = TokenUsageReport.FromTracker(tracker);
        Assert.True(report.Estimated); // any phase estimated → report estimated

        var analysis = report.Phases.First(p => p.Phase == "analysis");
        var generation = report.Phases.First(p => p.Phase == "generation");
        var critic = report.Phases.First(p => p.Phase == "critic");

        Assert.False(analysis.Estimated);
        Assert.True(generation.Estimated);
        Assert.False(critic.Estimated);
    }

    [Fact]
    public void FromTracker_EstimatedAndNonEstimatedSamePhase_AggregateEstimated()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("generation", "gpt-4.1", "github-models", 1000, 500, TimeSpan.FromSeconds(5), estimated: false);
        tracker.Record("generation", "gpt-4.1", "github-models", 2000, 1000, TimeSpan.FromSeconds(10), estimated: true);

        var report = TokenUsageReport.FromTracker(tracker);
        Assert.Single(report.Phases);
        Assert.True(report.Phases[0].Estimated); // logical OR
        Assert.Equal(2, report.Phases[0].Calls);
        Assert.Equal(3000, report.Phases[0].TokensIn);
    }

    [Fact]
    public void Serialization_EstimatedField_SnakeCase()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("generation", "gpt-4.1", "github-models", 1000, 500, TimeSpan.FromSeconds(5), estimated: true);

        var report = TokenUsageReport.FromTracker(tracker);
        var json = JsonSerializer.Serialize(report);

        Assert.Contains("\"estimated\":true", json);
        // Per-phase estimated flag also present
        var firstPhaseBlock = json.Substring(json.IndexOf("\"phases\""));
        Assert.Contains("\"estimated\":true", firstPhaseBlock);
    }
}
