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
    /// Spec 058: the in-process critic is retired. Verification of record runs as the
    /// <c>spectra-critic</c> subagent (Spec 055), and the shipped generation skill always generates
    /// with <c>--skip-critic</c>. This factory no longer constructs an in-process critic — it always
    /// reports the in-process critic unavailable so callers proceed without it. (The factory type is
    /// retained so the dead-but-compiling verification scaffolding in <c>GenerateHandler</c> still
    /// builds; that scaffolding is removed with the generation inversion in Spec 059.)
    /// </summary>
    public static CriticCreateResult TryCreate(
        CriticConfig? config,
        TokenUsageTracker? tracker = null,
        RunErrorTracker? errorTracker = null)
    {
        _ = (config, tracker, errorTracker);
        return CriticCreateResult.Failed(
            "subagent",
            "In-process critic retired (Spec 058); verification runs as the spectra-critic subagent.");
    }

    /// <summary>
    /// Spec 058: see <see cref="TryCreate"/>. Always reports the in-process critic unavailable.
    /// </summary>
    public static Task<CriticCreateResult> TryCreateAsync(
        CriticConfig? config,
        CancellationToken ct = default,
        TokenUsageTracker? tracker = null,
        RunErrorTracker? errorTracker = null)
    {
        _ = (config, ct, tracker, errorTracker);
        return Task.FromResult(TryCreate(config, tracker, errorTracker));
    }

    /// <summary>
    /// Checks if a provider name is in the (legacy) canonical critic set. Retained for the existing
    /// provider-name tests; no longer gates critic creation (Spec 058).
    /// </summary>
    public static bool IsSupported(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return false;
        var normalized = provider.Trim().ToLowerInvariant();
        if (HardErrorProviders.Contains(normalized)) return false;
        if (LegacyAliases.ContainsKey(normalized)) return true;
        return SupportedProviders.Contains(normalized);
    }
}
