# Data Model: Unified Critic Provider List

**Feature**: 039-unify-critic-providers
**Date**: 2026-04-11

## Overview

No new entities. Two existing files change semantically.

## Entities

### CriticConfig (modified — docstring + switch defaults only)

**File**: `src/Spectra.Core/Models/Config/CriticConfig.cs`

The on-disk JSON schema is unchanged. What changes:

| Field | Change |
|-------|--------|
| `Provider` | Docstring updated from `"google", "openai", "anthropic", "github"` to `"github-models", "azure-openai", "azure-anthropic", "openai", "anthropic"` (with note that legacy `github` and `google` are still recognized). |
| `GetEffectiveModel()` | Switch statement gains explicit cases for `github-models`, `azure-openai`, `azure-anthropic` mapping to the same defaults as the generator. Legacy cases (`google`, `github`) retained for read-side safety. |
| `GetDefaultApiKeyEnv()` | Same shape — gains canonical cases, legacy cases retained. |

### CriticFactory (modified)

**File**: `src/Spectra.CLI/Agent/Critic/CriticFactory.cs`

| Member | Change |
|--------|--------|
| `SupportedProviders` | Reduced from 7 entries to the canonical 5: `github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`. (Removes `azure-deepseek`, `google`.) |
| `LegacyAliases` (NEW, private static) | Map: `"github" → "github-models"` (soft, with deprecation warning). |
| `HardErrorProviders` (NEW, private static) | Set: `{ "google" }`. |
| `TryCreate(CriticConfig)` | Now validates `config.Provider` against the canonical set + legacy aliases. Returns `Failed(...)` for unknown or hard-error providers. Emits a one-line stderr deprecation warning when an alias is used. Uses the resolved/normalized provider name when constructing `CopilotCritic`. |
| `TryCreateAsync(CriticConfig, CancellationToken)` | Same validation as `TryCreate`, applied before the Copilot availability check. |
| `IsSupported(string)` | Updated to also accept legacy aliases (returns true for `github`). Returns false for `google` and unknown values. |

## State Transitions

None.

## Validation Rules

| Rule | Source | Enforcement |
|------|--------|-------------|
| Provider name in canonical 5 (or alias) | spec FR-001, FR-002 | Hard: `CriticFactory.TryCreate` returns `Failed` otherwise. |
| Case-insensitive | spec FR-002 | Hard: lowercase before lookup. |
| `google` → hard error | spec FR-006 | Hard: `Failed` with explicit error message listing supported providers. |
| `github` → soft alias to `github-models` | spec FR-005 | Soft: rewrite + stderr warning. |
| Empty/null provider when enabled | spec FR-004 | Soft: fall back to default `github-models`. |

## Migration

No on-disk migration. Config files are unchanged. Users with `provider: "github"` see a deprecation warning on next run; users with `provider: "google"` see a hard error directing them to update.
