namespace Spectra.CLI.Commands.Init;

/// <summary>
/// Spec 041: pre-defined generator + critic model pairing offered during
/// <c>spectra init -i</c>. Not configurable by the user — just a convenience
/// that writes both <c>ai.providers[0]</c> and <c>ai.critic</c> in one step.
/// </summary>
public sealed record ModelPreset(
    string Name,
    string Description,
    string GeneratorProvider,
    string GeneratorModel,
    string CriticProvider,
    string CriticModel,
    string PlanNote)
{
    /// <summary>
    /// Spec 041 preset menu (stable order — the first entry is the default).
    /// All presets use <c>github-models</c> so they work out of the box on any
    /// paid Copilot plan without extra API keys.
    /// </summary>
    public static IReadOnlyList<ModelPreset> All { get; } =
    [
        new ModelPreset(
            Name: "GPT-4.1 + GPT-5 mini critic (free, unlimited)",
            Description: "Best for daily usage, no cost concerns",
            GeneratorProvider: "github-models",
            GeneratorModel: "gpt-4.1",
            CriticProvider: "github-models",
            CriticModel: "gpt-5-mini",
            PlanNote: "any paid plan (Pro, Pro+, Business, Enterprise)"),

        new ModelPreset(
            Name: "Claude Sonnet 4.5 + GPT-4.1 critic (premium, high quality)",
            Description: "Best for complex business logic and maximum test quality",
            GeneratorProvider: "github-models",
            GeneratorModel: "claude-sonnet-4.5",
            CriticProvider: "github-models",
            CriticModel: "gpt-4.1",
            PlanNote: "Pro+ recommended (1× per call; critic is free)"),

        new ModelPreset(
            Name: "GPT-4.1 + Claude Haiku 4.5 critic (free gen, cross-family critic)",
            Description: "Free generation with cross-architecture verification",
            GeneratorProvider: "github-models",
            GeneratorModel: "gpt-4.1",
            CriticProvider: "github-models",
            CriticModel: "claude-haiku-4.5",
            PlanNote: "any paid plan (critic is 0.33× per call)"),

        new ModelPreset(
            Name: "Custom — I'll configure manually",
            Description: "Writes preset 1 defaults; edit spectra.config.json after init",
            GeneratorProvider: "github-models",
            GeneratorModel: "gpt-4.1",
            CriticProvider: "github-models",
            CriticModel: "gpt-5-mini",
            PlanNote: "edit config manually for BYOK providers or custom models"),
    ];

    public bool IsCustom => Name.StartsWith("Custom");
}
