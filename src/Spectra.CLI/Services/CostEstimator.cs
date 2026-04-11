using System.Globalization;

namespace Spectra.CLI.Services;

/// <summary>
/// USD cost estimation for token usage (Spec 040). Maintains a hardcoded
/// per-1M-token rate table for BYOK providers. Updated manually when
/// rates change.
/// </summary>
public static class CostEstimator
{
    private const string GitHubModels = "github-models";

    /// <summary>
    /// Hardcoded per-1M-token rates keyed by model name (case-insensitive).
    /// </summary>
    private static readonly Dictionary<string, (decimal InputPer1M, decimal OutputPer1M)>
        KnownRates = new(StringComparer.OrdinalIgnoreCase)
        {
            // OpenAI / Azure OpenAI
            ["gpt-4o"] = (2.50m, 10.00m),
            ["gpt-4o-mini"] = (0.15m, 0.60m),
            ["gpt-4.1"] = (2.00m, 8.00m),
            ["gpt-4.1-mini"] = (0.40m, 1.60m),
            ["gpt-4.1-nano"] = (0.10m, 0.40m),

            // Anthropic
            ["claude-sonnet-4-20250514"] = (3.00m, 15.00m),
            ["claude-3-5-haiku-latest"] = (0.80m, 4.00m),

            // DeepSeek
            ["deepseek-v3.2"] = (0.30m, 0.88m),
        };

    /// <summary>
    /// Estimate USD cost across all phases. Returns <c>null</c> amount when
    /// cost cannot be estimated (github-models, any unknown model).
    /// </summary>
    public static (decimal? UsdAmount, string DisplayMessage) Estimate(
        IReadOnlyList<PhaseUsage> phases)
    {
        if (phases is null || phases.Count == 0)
        {
            return (null, "No AI calls recorded.");
        }

        // github-models special case: if ANY phase is github-models, the whole
        // run is considered Copilot-plan billing and we don't show dollars.
        foreach (var p in phases)
        {
            if (string.Equals(p.Provider, GitHubModels, StringComparison.OrdinalIgnoreCase))
            {
                return (null, "Included in Copilot plan (rate limits apply)");
            }
        }

        decimal total = 0m;
        string? primaryProvider = null;
        foreach (var p in phases)
        {
            if (!KnownRates.TryGetValue(p.Model, out var rate))
            {
                return (null, $"Cost estimate unavailable for {p.Model}");
            }

            total += (p.TokensIn / 1_000_000m) * rate.InputPer1M;
            total += (p.TokensOut / 1_000_000m) * rate.OutputPer1M;
            primaryProvider ??= p.Provider;
        }

        var providerLabel = string.IsNullOrEmpty(primaryProvider) ? "rates" : $"{primaryProvider} rates";
        var display = total < 0.01m
            ? $"< $0.01 ({providerLabel})"
            : $"${total.ToString("F2", CultureInfo.InvariantCulture)} ({providerLabel})";

        return (total, display);
    }
}
