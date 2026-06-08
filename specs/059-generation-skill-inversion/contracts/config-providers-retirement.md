# Contract: `ai.providers` retirement (config)

Retires the `ai.providers` config key once no generation code reads it (FR-005). Follows the Spec 058 ignore-with-notice pattern exactly.

## Schema change

| Item | Before (058) | After (059) |
|------|--------------|-------------|
| `AiConfig.Providers` | `required IReadOnlyList<ProviderConfig>` | optional `IReadOnlyList<ProviderConfig>` (default `[]`) |
| `ConfigLoader` `MISSING_PROVIDERS` validation | present (errors when absent) | **removed** |
| `ConfigLoader.DeprecatedKeyPaths` | 4 keys, NOT `ai.providers` | **adds** `ai.providers` (5 keys) |

## Behavior

| Config | Result |
|--------|--------|
| No `ai.providers` | Validates. `DetectDeprecatedKeys` returns no `ai.providers` entry. |
| Carries `ai.providers` | Validates (not rejected, not silently dropped). `DetectDeprecatedKeys` includes `ai.providers`; surfaced as a non-blocking note (e.g. in `spectra validate`). |

Surviving model/cost levers untouched: `ai.critic.model`, `analysis.max_prompt_tokens`, `debug.enabled`.

## Test impact

`tests/Spectra.Core.Tests/Config/ProviderRetirementTests.cs` asserted (058) that `ai.providers` was **RETAINED** and must NOT be flagged:

```csharp
// ai.providers is RETAINED (generator) — it must NOT be flagged as retired.
Assert.DoesNotContain("ai.providers", detected);
```

This assertion **inverts** in 059: `ai.providers` IS now flagged. Update the test (and any config-load test asserting providers are required) to expect: config-without-providers validates; config-with-providers validates **and** `DetectDeprecatedKeys` contains `ai.providers`.

## Acceptance mapping

- FR-005, US4 AS2 (no-providers config validates), US4 AS3 (legacy providers ignored-with-notice), SC-005.
