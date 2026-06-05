using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Single source of truth for the critic model (Spec 055 FR-004/FR-008). <c>ai.critic.model</c> is
/// the only selector: when set it wins; otherwise a single same-family default applies. There is no
/// provider-keyed default switch — the previously duplicated switches in
/// <c>CopilotCritic.GetEffectiveModel</c> and <c>CopilotService.GetCriticModel</c> both delegate
/// here, so the default cannot drift between two places.
///
/// Direction (ARCHITECTURE-v2 §32): the critic reads as more useful when it is the same family as
/// the generator (Sonnet 4.6); "different families" was a means, not the end. The config keeps that
/// decision open for the post-migration bake-off — flipping the model is a config change, not a code
/// change.
/// </summary>
public static class CriticModelResolver
{
    /// <summary>
    /// The single same-family default (§32 direction; target Sonnet 4.6). Applied only when
    /// <c>ai.critic.model</c> is unset.
    /// </summary>
    public const string DefaultCriticModel = "claude-sonnet-4-6";

    /// <summary>
    /// Resolves the critic model: <c>config.Model</c> when set, otherwise
    /// <see cref="DefaultCriticModel"/>. No provider influences the result.
    /// </summary>
    public static string Resolve(CriticConfig? config) =>
        string.IsNullOrWhiteSpace(config?.Model) ? DefaultCriticModel : config.Model;
}
