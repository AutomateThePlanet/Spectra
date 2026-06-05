# Data Model: Config shape before/after

**Feature**: `058-provider-retirement` | **Date**: 2026-06-05

This spec changes only the **config schema** (no runtime entities, no DB). Below: the `ai` block and
its critic sub-block, before and after the collapse. Surviving sections (`source`, `tests`,
`analysis`, `debug`, `coverage`, `execution`, …) are unchanged.

## `AiConfig` (`src/Spectra.Core/Models/Config/AiConfig.cs`)

| JSON key | Before | After | Change |
|---|---|---|---|
| `providers` | `required IReadOnlyList<ProviderConfig>` | — | **Removed.** Generator provider/model is selected by the interactive Claude Code session, not config. |
| `fallback_strategy` | `string = "auto"` (display-only) | — | **Removed.** No programmatic provider fallback when a human drives. |
| `critic` | `CriticConfig?` | `CriticConfig?` | Unchanged (sub-block collapses — below). |
| `generation_timeout_minutes` | `int = 5` | `int = 5` | Unchanged. |
| `analysis_timeout_minutes` | `int = 2` | `int = 2` | Unchanged. |
| `generation_batch_size` | `int = 30` | `int = 30` | Unchanged. |

## `CriticConfig` (`src/Spectra.Core/Models/Config/CriticConfig.cs`)

| JSON key | Before | After | Change |
|---|---|---|---|
| `enabled` | `bool = false` | `bool = false` | Unchanged. |
| `model` | `string?` | `string?` | **Survives as the single selector.** Resolved by `CriticModelResolver.Resolve` (unset → `claude-sonnet-4-6`). |
| `provider` | `string?` | — | **Removed.** |
| `api_key_env` | `string?` | — | **Removed.** |
| `base_url` | `string?` | — | **Removed.** |
| `timeout_seconds` | `int = 120` | `int = 120` | Unchanged. |
| `max_concurrent` | `int = 1` (clamp [1,20]) | `int = 1` (clamp [1,20]) | Unchanged. |
| **methods** | `GetEffectiveModel()` (per-provider switch), `GetDefaultApiKeyEnv()`, `IsValid()` (requires `Provider` when enabled) | `IsValid()` relaxed (no `Provider` requirement) | Per-provider switches **removed** — superseded by `CriticModelResolver`. |

## `ProviderConfig` (`src/Spectra.Core/Models/Config/ProviderConfig.cs`)

**Deleted.** Was the element type of `ai.providers[]` (`name`, `model`, `enabled`, `priority`,
`api_key_env`, `base_url`). Orphaned once `Providers` is removed.

## Surviving (regression net — unchanged, do not edit their tests)

| Entity | Key | Role |
|---|---|---|
| `AnalysisConfig` | `analysis.max_prompt_tokens` (default 96 000) | Pre-flight token budget; exceeding → exit code 4 (`PreFlightTokenChecker`). The cost lever. |
| `DebugConfig` | `debug.enabled` (+ `log_file`, `mode`, `error_log_file`) | Token/error telemetry path. |
| `ExecutionConfig` | (emptied of `copilot_space*` in Spec 057) | Confirms schema not left half-cleaned. |

## Validation rules (after)

- A config **without** `ai.providers` validates (the `MISSING_PROVIDERS` rule in `ConfigLoader` is
  removed). `ai` itself remains required (`MISSING_AI` unchanged); `source`/`tests` remain
  `[JsonRequired]`.
- A config **with** dead keys (`ai.providers`, `ai.fallback_strategy`, `ai.critic.provider`,
  `ai.critic.api_key_env`, `ai.critic.base_url`) validates — the keys are ignored (System.Text.Json
  default for unmapped members) AND detected via `ConfigLoader.DetectDeprecatedKeys(rawJson)`, which
  returns the present dead keys for a non-blocking notice (FR-006).
- `ai.critic` with only `model` (or absent) validates; `IsValid()` no longer requires `provider`.
- No `response_format` key exists or is honored anywhere (FR-007) — asserted absent.

## State transitions

None — config is static, loaded once per command. No lifecycle.
