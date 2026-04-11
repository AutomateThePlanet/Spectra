using System.Text.Json.Serialization;
using Spectra.CLI.Services;

namespace Spectra.CLI.Results;

/// <summary>
/// Serialized token usage shown in the Run Summary and included in
/// --output-format json and .spectra-result.json (Spec 040).
/// </summary>
public sealed class TokenUsageReport
{
    [JsonPropertyName("phases")]
    public IReadOnlyList<PhaseUsageDto> Phases { get; init; } = [];

    [JsonPropertyName("total")]
    public PhaseUsageDto Total { get; init; } = new();

    [JsonPropertyName("estimated_cost_usd")]
    public decimal? EstimatedCostUsd { get; init; }

    [JsonPropertyName("cost_display")]
    public string CostDisplay { get; init; } = "";

    /// <summary>
    /// Spec 040 follow-up: true when any phase in the report came from the
    /// <see cref="TokenEstimator"/> fallback (text.Length / 4) rather than
    /// provider-reported <c>AssistantUsageEvent</c> counts. Consumers
    /// (dashboards, SKILLs) use this to tell honest/approximate numbers
    /// apart.
    /// </summary>
    [JsonPropertyName("estimated")]
    public bool Estimated { get; init; }

    /// <summary>
    /// Builds a report snapshot from a live <see cref="TokenUsageTracker"/>.
    /// Safe to call at any time; if the tracker has no data, returns an
    /// empty report with zero totals.
    /// </summary>
    public static TokenUsageReport FromTracker(TokenUsageTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        var summary = tracker.GetSummary();
        var total = tracker.GetTotal();
        var (cost, display) = CostEstimator.Estimate(summary);

        return new TokenUsageReport
        {
            Phases = summary.Select(PhaseUsageDto.From).ToList(),
            Total = PhaseUsageDto.From(total),
            EstimatedCostUsd = cost,
            CostDisplay = display,
            Estimated = total.Estimated
        };
    }
}

public sealed class PhaseUsageDto
{
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "";

    [JsonPropertyName("model")]
    public string Model { get; init; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "";

    [JsonPropertyName("calls")]
    public int Calls { get; init; }

    [JsonPropertyName("tokens_in")]
    public int TokensIn { get; init; }

    [JsonPropertyName("tokens_out")]
    public int TokensOut { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("elapsed_seconds")]
    public double ElapsedSeconds { get; init; }

    /// <summary>
    /// Spec 040 follow-up: true when any call aggregated into this phase
    /// used the <see cref="TokenEstimator"/> fallback.
    /// </summary>
    [JsonPropertyName("estimated")]
    public bool Estimated { get; init; }

    public static PhaseUsageDto From(PhaseUsage p) => new()
    {
        Phase = p.Phase,
        Model = p.Model,
        Provider = p.Provider,
        Calls = p.Calls,
        TokensIn = p.TokensIn,
        TokensOut = p.TokensOut,
        TotalTokens = p.TotalTokens,
        ElapsedSeconds = Math.Round(p.Elapsed.TotalSeconds, 2),
        Estimated = p.Estimated
    };
}
