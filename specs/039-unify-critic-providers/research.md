# Research: Unified Critic Provider List

**Feature**: 039-unify-critic-providers
**Date**: 2026-04-11
**Status**: Complete

## Decision 1: Canonical critic provider set is exactly the generator set

**Decision**: `github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`. No more, no less.

**Rationale**:
- The whole point of spec 009 (Copilot SDK consolidation) is that one runtime serves both generator and critic. Two divergent provider lists is a leftover bug, not a feature.
- The current `CriticFactory.SupportedProviders` includes `azure-deepseek` and `google` — but `azure-deepseek` isn't in the generator set, and `google` cannot be routed by the Copilot SDK. Both are removed.

**Alternatives considered**:
- *Keep `azure-deepseek` in the critic set*: rejected — would re-introduce the same drift the spec is fixing. If azure-deepseek becomes a generator option later, add it to both lists at the same time.
- *Keep `google` as a "best effort" route*: rejected — silent failure or surprising cross-vendor behavior.

## Decision 2: Soft mapping for `github`, hard error for `google`

**Decision**: `github` → `github-models` with a one-line stderr deprecation warning. `google` → hard error with the supported list.

**Rationale**:
- `github` and `github-models` represent the same intent — the user just used the shorthand. Silently rewriting is safe and helpful.
- `google` represents a different intent (route to Google Gemini). The runtime cannot honor it. Silently mapping to OpenAI or any other provider would mislead the user about which model graded their tests. Hard error is the only honest choice.

**Alternatives considered**:
- *Hard error for both*: rejected — too aggressive for the harmless `github` case.
- *Soft map `google` to `openai`*: rejected — see "honest" point above.

## Decision 3: Validation lives in `CriticFactory.TryCreate`, not `CriticConfig.IsValid`

**Decision**: Add the validation + alias logic inside `CriticFactory.TryCreate` (and `TryCreateAsync`). `CriticConfig.IsValid` remains a thin "is the section non-null and provider non-empty" check.

**Rationale**:
- `CriticConfig` is a Core model — it should not know about the alias table or stderr warnings (which are CLI-side concerns).
- `CriticFactory` is the single chokepoint. Both `TryCreate` (sync) and `TryCreateAsync` route through a shared private helper.
- Keeping the validation here means the existing `TryCreate` callsites get the new behavior automatically without needing to plumb error messages through `CriticConfig`.

**Alternatives considered**:
- *Validation in `CriticConfig.IsValid`*: rejected — `CriticConfig` is in `Spectra.Core` and shouldn't carry CLI-side concerns like stderr warnings.
- *Validation in a new `CriticProviderValidator` class*: rejected — over-engineered for a 5-entry lookup.

## Decision 4: Case-insensitive matching, lowercase normalization

**Decision**: Lowercase the user's input before lookup. `"Azure-OpenAI"` is accepted as `"azure-openai"`. The downstream `CopilotCritic` receives the normalized form.

**Rationale**:
- Provider names are stable identifiers, not user-facing display strings. Case sensitivity is a footgun.
- Mirrors how `GetEffectiveModel` and `GetDefaultApiKeyEnv` already use `Provider?.ToLowerInvariant()`.

**Alternatives considered**:
- *Strict case match*: rejected — needless friction.

## Decision 5: Empty/missing provider → use default, not error

**Decision**: If `Enabled = true` but `Provider` is empty/null/whitespace, fall back to the default `github-models`. Do NOT raise an error.

**Rationale**:
- The current `CriticConfig.IsValid()` already requires a non-empty Provider when enabled, so this case is mostly hypothetical. If config loading allows it through, the safest behavior is the same default the rest of the codebase already uses.

**Alternatives considered**:
- *Hard error*: rejected — would surprise users whose config-management tooling drops empty fields.

## Decision 6: Stale switch-statements in `CriticConfig.GetEffectiveModel` / `GetDefaultApiKeyEnv`

**Decision**: Update both switch statements to cover the canonical 5 providers. Keep `github` and `google` cases for backward read-side safety (in case any caller passes the legacy value before factory normalization runs), but the canonical-set entries are the source of truth.

**Rationale**:
- These methods are called from places that may not always go through `CriticFactory.TryCreate`. Defensive entries don't hurt and protect against the edge case.
- The `GOOGLE_API_KEY` and `gemini-2.0-flash` defaults remain in those legacy cases — they're harmless because if anyone hits them, the factory will already have hard-errored.

**Alternatives considered**:
- *Remove the legacy cases entirely*: rejected — riskier; some test fixture or external caller might still pass them.

## Open Questions

None.
