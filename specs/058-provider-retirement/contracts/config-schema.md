# Contract: Cleaned `spectra.config.json` schema (v2)

**Feature**: `058-provider-retirement`

The cleaned `ai` block. Sections outside `ai` are unchanged and omitted here.

## Cleaned shape (what `spectra init` now emits)

```jsonc
{
  "source": { /* unchanged */ },
  "tests":  { /* unchanged */ },
  "ai": {
    // NO "providers" — the Claude Code session selects the generator model
    // NO "fallback_strategy"
    "critic": {
      "enabled": true,
      "model": "claude-sonnet-4-6"   // the ONLY surviving model selector
      // NO "provider", "api_key_env", "base_url"
      // optional: "timeout_seconds", "max_concurrent"
    },
    "generation_timeout_minutes": 5,
    "analysis_timeout_minutes": 2,
    "generation_batch_size": 30
  },
  "analysis": { "max_prompt_tokens": 96000 },   // unchanged — cost lever
  "debug":    { "enabled": true }               // unchanged — telemetry
}
```

## Acceptance rules

| # | Given | Then |
|---|---|---|
| C-1 | config omits `ai.providers` | validates (no `MISSING_PROVIDERS`); generate flow proceeds |
| C-2 | config omits `ai.fallback_strategy` and critic `provider`/`api_key_env`/`base_url` | validates; `ai.critic.model` honored as sole selector |
| C-3 | config sets only `ai.critic.model` (or omits `ai.critic`) | validates; `IsValid()` does not require `provider`; unset model → `claude-sonnet-4-6` default |
| C-4 | config still carries any dead key | validates; the run proceeds; a **non-blocking** notice names the present dead keys (not a silent drop, exit code unchanged) |
| C-5 | config sets `analysis.max_prompt_tokens` / `debug.enabled` | bind and behave exactly as before |
| C-6 | any config | no `response_format` key is read or honored anywhere |

## Dead-key detection contract

`ConfigLoader.DetectDeprecatedKeys(string rawJson) → IReadOnlyList<string>` (pure):
- Inspects raw JSON (via `JsonDocument`/`JsonNode`) for: `ai.providers`, `ai.fallback_strategy`,
  `ai.critic.provider`, `ai.critic.api_key_env`, `ai.critic.base_url`.
- Returns the dotted names of those **present**, in declaration order; empty list if none.
- Does not mutate the file. The caller surfaces the list as a non-blocking note naming the keys and
  the recommended action ("remove these deprecated keys").
