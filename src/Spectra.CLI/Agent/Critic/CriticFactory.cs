using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Services;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Result of attempting to create a critic.
/// </summary>
public sealed record CriticCreateResult
{
    /// <summary>
    /// Gets whether the critic was created successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the critic if creation was successful.
    /// </summary>
    public ICriticRuntime? Critic { get; init; }

    /// <summary>
    /// Gets the provider name that was attempted (after legacy-alias resolution).
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Gets the error message if creation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets authentication help instructions.
    /// </summary>
    public IReadOnlyList<string> HelpInstructions { get; init; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CriticCreateResult Succeeded(ICriticRuntime critic, string providerName) => new()
    {
        Success = true,
        Critic = critic,
        ProviderName = providerName
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static CriticCreateResult Failed(string providerName, string error, params string[] help) => new()
    {
        Success = false,
        ProviderName = providerName,
        ErrorMessage = error,
        HelpInstructions = help
    };
}

/// <summary>
/// Factory for creating critic runtime instances using the Copilot SDK.
///
/// Spec 039: the canonical critic provider set is identical to the generator
/// provider set — <c>github-models</c>, <c>azure-openai</c>, <c>azure-anthropic</c>,
/// <c>openai</c>, <c>anthropic</c>. Legacy aliases are accepted with a
/// deprecation warning (<c>github</c> → <c>github-models</c>) or rejected
/// outright (<c>google</c>, since the runtime cannot route to it).
/// </summary>
public static class CriticFactory
{
    /// <summary>
    /// Default provider used when the user enables the critic without
    /// specifying a provider, or when an empty/whitespace value is given.
    /// </summary>
    public const string DefaultProvider = "github-models";

    /// <summary>
    /// Canonical set of supported critic providers — same as the generator.
    /// </summary>
    public static IReadOnlyList<string> SupportedProviders { get; } =
        ["github-models", "azure-openai", "azure-anthropic", "openai", "anthropic"];

    /// <summary>
    /// Soft aliases — accepted with a deprecation warning then rewritten to
    /// the canonical name. Spec 039 FR-005.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LegacyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["github"] = "github-models"
        };

    /// <summary>
    /// Provider names that used to be valid but are now hard-rejected.
    /// Spec 039 FR-006: <c>google</c> cannot be honored by the Copilot SDK
    /// runtime, so silent fallback would mislead the user.
    /// </summary>
    private static readonly HashSet<string> HardErrorProviders =
        new(StringComparer.OrdinalIgnoreCase) { "google" };

    /// <summary>
    /// Tries to create a critic from configuration using the Copilot SDK.
    /// </summary>
    public static CriticCreateResult TryCreate(CriticConfig? config, TokenUsageTracker? tracker = null)
    {
        if (config is null || !config.Enabled)
        {
            return CriticCreateResult.Failed("none", "Critic not configured or disabled");
        }

        var (resolved, error) = ResolveProvider(config.Provider);
        if (resolved is null)
        {
            return CriticCreateResult.Failed(config.Provider ?? "", error ?? "Invalid critic provider");
        }

        // Spec 039: construct CopilotCritic with the resolved (normalized) name.
        // The original config is preserved so model/api_key_env/base_url still apply.
        var critic = new CopilotCritic(config, tracker);
        return CriticCreateResult.Succeeded(critic, resolved);
    }

    /// <summary>
    /// Tries to create a critic asynchronously with availability check.
    /// </summary>
    public static async Task<CriticCreateResult> TryCreateAsync(
        CriticConfig? config,
        CancellationToken ct = default,
        TokenUsageTracker? tracker = null)
    {
        if (config is null || !config.Enabled)
        {
            return CriticCreateResult.Failed("none", "Critic not configured or disabled");
        }

        // Spec 039: validate the provider BEFORE the Copilot availability check
        // so unknown providers fail fast with the actionable error message.
        var (resolved, error) = ResolveProvider(config.Provider);
        if (resolved is null)
        {
            return CriticCreateResult.Failed(config.Provider ?? "", error ?? "Invalid critic provider");
        }

        // Check Copilot availability
        var (available, sdkError) = await CopilotService.CheckAvailabilityAsync(ct);
        if (!available)
        {
            return CriticCreateResult.Failed("copilot-sdk", sdkError ?? "Copilot SDK not available");
        }

        var critic = new CopilotCritic(config, tracker);
        return CriticCreateResult.Succeeded(critic, resolved);
    }

    /// <summary>
    /// Checks if a provider is supported. Returns true for canonical names
    /// AND legacy aliases (e.g. <c>github</c>). Returns false for
    /// hard-rejected legacy values (e.g. <c>google</c>) and unknowns.
    /// </summary>
    public static bool IsSupported(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return false;
        var normalized = provider.Trim().ToLowerInvariant();
        if (HardErrorProviders.Contains(normalized)) return false;
        if (LegacyAliases.ContainsKey(normalized)) return true;
        return SupportedProviders.Contains(normalized);
    }

    /// <summary>
    /// Spec 039: resolves a user-supplied provider name to the canonical form.
    /// Returns (resolved, null) on success and (null, errorMessage) on failure.
    /// Emits a one-line stderr deprecation warning when a legacy alias is used.
    /// Empty/whitespace input falls back to <see cref="DefaultProvider"/>.
    /// </summary>
    internal static (string? resolved, string? error) ResolveProvider(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (DefaultProvider, null);
        }

        var normalized = raw.Trim().ToLowerInvariant();

        if (HardErrorProviders.Contains(normalized))
        {
            return (null, BuildUnsupportedError(raw));
        }

        if (LegacyAliases.TryGetValue(normalized, out var canonical))
        {
            // Soft alias: warn once and continue.
            try
            {
                Console.Error.WriteLine(
                    $"⚠ Critic provider '{raw}' is deprecated. Use '{canonical}' instead.");
            }
            catch
            {
                // Stderr write failure must not block critic creation.
            }
            return (canonical, null);
        }

        if (SupportedProviders.Contains(normalized))
        {
            return (normalized, null);
        }

        return (null, BuildUnsupportedError(raw));
    }

    private static string BuildUnsupportedError(string raw) =>
        $"Critic provider '{raw}' is not supported. " +
        $"Supported providers: {string.Join(", ", SupportedProviders)}.";
}
