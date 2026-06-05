# Contract: single critic-model selector + dead/duplicate-code removal (FR-004, FR-008)

## `CriticModelResolver` — the single source of truth

```csharp
namespace Spectra.CLI.Agent.Critic;

public static class CriticModelResolver
{
    // §32 same-family direction; target value Sonnet 4.6. The single default.
    public const string DefaultCriticModel = /* same-family default */;

    /// config.Model wins when set; otherwise the single same-family default.
    /// No provider-keyed branch — ai.critic.model is the only selector.
    public static string Resolve(CriticConfig? config);
}
```

**Behavior**

| `config.Model` | Result |
|----------------|--------|
| non-empty | that value (verbatim) |
| null/empty/whitespace | `DefaultCriticModel` (single same-family default) |
| any provider | irrelevant — provider no longer selects the default |

## Call sites collapsed onto the resolver

| Site | Before | After |
|------|--------|-------|
| `CopilotCritic.GetEffectiveModel` (`GroundingAgent.cs:192`) | provider→default switch (anthropic→haiku, openai→gpt-5-mini, …) | `=> CriticModelResolver.Resolve(config)` |
| `CopilotService.GetCriticModel` (`CopilotService.cs:319`) | duplicate of the same switch | `=> CriticModelResolver.Resolve(criticConfig)` |

After this change the provider→default switch exists in **zero** places (FR-008); `ai.critic.model`
is the single selector (FR-004). The `ai.critic.model` override path is unchanged.

## Comment corrections (FR-004)

| Site | Stale (cross-architecture) | Corrected (§32 same-family) |
|------|----------------------------|------------------------------|
| `GroundingAgent.cs:197` | "Cross-architecture when possible so the critic catches hallucinations the generator can't see." | Same-family critic per §32; cross-family was a means, not the end. Model is config-driven via `ai.critic.model`. |
| `CopilotService.cs:324` | "favor cross-architecture verification (GPT critic for a Claude generator …)" | Same as above. |

## Dead code removal (FR-008)

- **Delete** `CopilotCriticFactory` (`GroundingAgent.cs:226`) — unreferenced outside its own file
  (investigation F-1); the live factory is `CriticFactory`. Remove its tests (they cover dead
  code).

## Test contract

- `Resolve(config with Model="X")` → `"X"`.
- `Resolve(config with no Model, provider in {anthropic, openai, azure-*, github-models, …})` →
  `DefaultCriticModel` for **every** provider (no provider branch reachable).
- A repo search confirms zero remaining provider→default-model switches and no `CopilotCriticFactory`.

## Constraints (additive scope)

The retained in-process critic path keeps working: `GetEffectiveModel` still returns a valid model
string (now via the resolver). This is a model-*default* change (same-family), not a removal of the
in-process call — the call at `GroundingAgent.cs:124` stays until the subsequent wiring spec.
