using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Services;

public class CostEstimatorTests
{
    [Fact]
    public void EstimateCost_KnownModel_ReturnsValue()
    {
        var phases = new[]
        {
            new PhaseUsage("generation", "gpt-4.1-mini", "azure-openai", 1,
                1_000_000, 1_000_000, TimeSpan.FromSeconds(10))
        };

        var (cost, display) = CostEstimator.Estimate(phases);
        // gpt-4.1-mini = 0.40 input + 1.60 output = 2.00 per 1M in + 1M out
        Assert.NotNull(cost);
        Assert.Equal(2.00m, cost!.Value);
        Assert.Contains("azure-openai", display);
        Assert.StartsWith("$", display);
    }

    [Fact]
    public void EstimateCost_UnknownModel_ReturnsNull()
    {
        var phases = new[]
        {
            new PhaseUsage("generation", "unknown-model-xyz", "azure-openai", 1, 1000, 500, TimeSpan.Zero)
        };

        var (cost, display) = CostEstimator.Estimate(phases);
        Assert.Null(cost);
        Assert.Contains("unavailable", display, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unknown-model-xyz", display);
    }

    [Fact]
    public void EstimateCost_GitHubModels_ReturnsNull()
    {
        var phases = new[]
        {
            new PhaseUsage("generation", "gpt-4o", "github-models", 1, 5000, 2000, TimeSpan.Zero)
        };

        var (cost, display) = CostEstimator.Estimate(phases);
        Assert.Null(cost);
        Assert.Contains("Copilot plan", display);
    }

    [Fact]
    public void EstimateCost_GitHubModelsAnyPhase_SuppressesDollars()
    {
        // Even if most phases are BYOK, a single github-models phase flips
        // the whole run to "Included in Copilot plan" (the critic is a common
        // reason why this mixed state would occur).
        var phases = new[]
        {
            new PhaseUsage("generation", "gpt-4.1", "azure-openai", 1, 10_000, 5_000, TimeSpan.Zero),
            new PhaseUsage("critic", "gpt-4o-mini", "github-models", 20, 50_000, 10_000, TimeSpan.Zero)
        };

        var (cost, display) = CostEstimator.Estimate(phases);
        Assert.Null(cost);
        Assert.Contains("Copilot plan", display);
    }

    [Fact]
    public void EstimateCost_MultiplePhases_SumsCorrectly()
    {
        var phases = new[]
        {
            new PhaseUsage("generation", "gpt-4.1-mini", "azure-openai", 1, 1_000_000, 0, TimeSpan.Zero), // 0.40
            new PhaseUsage("critic", "gpt-4o-mini", "azure-openai", 1, 1_000_000, 0, TimeSpan.Zero)       // 0.15
        };

        var (cost, _) = CostEstimator.Estimate(phases);
        Assert.NotNull(cost);
        Assert.Equal(0.55m, cost!.Value);
    }

    [Fact]
    public void EstimateCost_CaseInsensitiveModelLookup()
    {
        var phases = new[]
        {
            new PhaseUsage("generation", "GPT-4O-MINI", "azure-openai", 1, 1_000_000, 1_000_000, TimeSpan.Zero)
        };

        var (cost, _) = CostEstimator.Estimate(phases);
        Assert.NotNull(cost);
        // 0.15 + 0.60 = 0.75
        Assert.Equal(0.75m, cost!.Value);
    }

    [Fact]
    public void EstimateCost_EmptyPhases_ReturnsNullAmount()
    {
        var (cost, display) = CostEstimator.Estimate(Array.Empty<PhaseUsage>());
        Assert.Null(cost);
        Assert.Contains("No AI calls", display);
    }
}
