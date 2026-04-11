using System.Text.Json;
using Spectra.CLI.Results;
using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Results;

public class GenerateResultTokenUsageTests
{
    [Fact]
    public void Serialization_TokenUsage_UsesSnakeCaseFields()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("analysis", "gpt-4.1", "github-models", 1000, 500, TimeSpan.FromSeconds(10));
        tracker.Record("generation", "gpt-4.1", "github-models", 2000, 1500, TimeSpan.FromSeconds(20));

        var report = TokenUsageReport.FromTracker(tracker);

        var result = new GenerateResult
        {
            Command = "generate",
            Status = "completed",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsRequested = 8,
                TestsGenerated = 8,
                TestsWritten = 8,
                TestsRejectedByCritic = 0
            },
            FilesCreated = Array.Empty<string>(),
            RunSummary = new RunSummary
            {
                DocumentsProcessed = 3,
                BehaviorsIdentified = 20,
                TestsGenerated = 8,
                BatchSize = 8,
                Batches = 1,
                DurationSeconds = 35.0
            },
            TokenUsage = report
        };

        var json = JsonSerializer.Serialize(result);

        // Spec 040 JSON contract: snake_case field names.
        Assert.Contains("\"run_summary\"", json);
        Assert.Contains("\"token_usage\"", json);
        Assert.Contains("\"documents_processed\":3", json);
        Assert.Contains("\"behaviors_identified\":20", json);
        Assert.Contains("\"duration_seconds\":35", json);
        Assert.Contains("\"phases\"", json);
        Assert.Contains("\"total\"", json);
        Assert.Contains("\"tokens_in\":", json);
        Assert.Contains("\"tokens_out\":", json);
        Assert.Contains("\"total_tokens\":", json);
        Assert.Contains("\"elapsed_seconds\":", json);
        Assert.Contains("\"estimated_cost_usd\":null", json); // github-models
        Assert.Contains("\"cost_display\":\"Included in Copilot plan", json);
    }

    [Fact]
    public void Deserialization_TokenUsage_RoundTrip()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record("generation", "gpt-4.1-mini", "azure-openai", 1000, 500, TimeSpan.FromSeconds(5));
        var original = TokenUsageReport.FromTracker(tracker);

        var json = JsonSerializer.Serialize(original);
        var round = JsonSerializer.Deserialize<TokenUsageReport>(json);

        Assert.NotNull(round);
        Assert.Single(round!.Phases);
        Assert.Equal("generation", round.Phases[0].Phase);
        Assert.Equal("gpt-4.1-mini", round.Phases[0].Model);
        Assert.Equal("azure-openai", round.Phases[0].Provider);
        Assert.Equal(1000, round.Phases[0].TokensIn);
        Assert.Equal(500, round.Phases[0].TokensOut);
        Assert.Equal(1500, round.Phases[0].TotalTokens);
        Assert.Equal(1, round.Total.Calls);
        Assert.NotNull(round.EstimatedCostUsd);
    }
}
