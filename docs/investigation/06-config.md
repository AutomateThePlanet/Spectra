# 06 — Config & provider chain (Area F)

> **Purpose.** Map `spectra.config.json` as it exists today, trace the provider chain, and
> separate what dies when generation moves in-session and the critic becomes a subagent from
> what new config the v2 setup needs — grounding ARCHITECTURE-v2 §27–29, §63–76, §97.
>
> Investigation only. Confirmed claims cite `file:line`; new config is **scoped, not designed**.

---

## 1. Config schema today (model classes + template)

Root: `SpectraConfig` (`src/Spectra.Core/Models/Config/SpectraConfig.cs`); template seed
`src/Spectra.CLI/Templates/spectra.config.json`. Sections relevant to the seam:

### `ai` — `AiConfig` (`src/Spectra.Core/Models/Config/AiConfig.cs`)
| Key | Field:line | Default |
|---|---|---|
| `ai.providers[]` | `:10–11` (`required`) | — |
| `ai.fallback_strategy` | `:13–14` | `"auto"` |
| `ai.critic` | `:20–21` | null |
| `ai.generation_timeout_minutes` | `:29–30` | 5 |
| `ai.analysis_timeout_minutes` | `:39–40` | 2 |
| `ai.generation_batch_size` | `:47–48` | 30 |

`ai.providers[]` elements are `ProviderConfig` (`name`, `model`, `enabled`, `priority`,
`api_key_env`, `base_url`).

### `ai.critic` — `CriticConfig` (`src/Spectra.Core/Models/Config/CriticConfig.cs`)
| Key | Field:line | Default |
|---|---|---|
| `enabled` | `:14–15` | false |
| `provider` | `:24–25` | null (canonical set per Spec 039) |
| `model` | `:30–31` | null (→ `GetEffectiveModel`) |
| `api_key_env` | `:36–37` | null |
| `base_url` | `:42–43` | null |
| `timeout_seconds` | `:53–54` | 120 |
| `max_concurrent` | `:63–64` (clamp [1,20] via `GetEffectiveMaxConcurrent` `:71`) | 1 |

### `analysis` — `AnalysisConfig` (`src/Spectra.Core/Models/Config/AnalysisConfig.cs`)
- `analysis.categories` (`:10–11`).
- `analysis.max_prompt_tokens` (`:21–22`, default **96,000**; `0` disables). This is the
  Spec 040/045 pre-flight budget; exceeding it fails fast with exit code 4 (ARCHITECTURE-v2
  §45 framing). Survives v2 — it bounds the doc context the prompt-compiler feeds, which v2
  §73 makes the primary cost lever.

### `debug` — `DebugConfig` (`src/Spectra.Core/Models/Config/DebugConfig.cs`)
- `debug.enabled` telemetry (`.spectra-debug.log`), `log_file`, `mode`, `error_log_file`.
  The token telemetry here (`debug.enabled`) is what ARCHITECTURE-v2 §109 wants to use to
  measure the real "cases per session" curve — survives and gains importance.

### `execution` — `ExecutionConfig` (`src/Spectra.Core/Models/Config/ExecutionConfig.cs`)
- `execution.copilot_space` (`:10–11`), `execution.copilot_space_owner` (`:13–14`).
  Copilot-only — dead post-migration (see §3).

(Other sections — `tests`, `source`, `generation`, `update`, `suites`, `validation`,
`coverage`, `dashboard`, `bug_tracking`, `git`, `testimize` — exist on `SpectraConfig` and are
out of scope for the seam; none drives a model call.)

---

## 2. The provider chain

- **Selection:** `AgentFactory.CreateAgentAsync`
  (`src/Spectra.CLI/Agent/AgentFactory.cs:62`) picks the first **enabled** provider, else
  defaults to a hardcoded `github-models` / `gpt-4o` (`AgentFactory.cs:72–73`). It then runs
  auth/validation and constructs `CopilotGenerationAgent` (`AgentFactory.cs:115`).
- **SDK mapping:** `ProviderMapping.MapProvider`
  (`src/Spectra.CLI/Agent/Copilot/ProviderMapping.cs:16`) — `github-models`/`copilot` →
  `null` (SDK default); `azure-*` → `CreateAzureProvider`; `anthropic` →
  `CreateAnthropicProvider`; `openai` → `CreateOpenAiProvider`; unknown → `null`
  (`:23–40`). `GetModelName` (`ProviderMapping.cs:57`) returns `config.Model` or a
  provider-default (`:63–70`).
- **Critic chain (parallel):** `CriticFactory.ResolveProvider`
  (`src/Spectra.CLI/Agent/Critic/CriticFactory.cs:180`) →
  `CopilotService.CreateCriticSessionAsync` (`CopilotService.cs:85`) with model from
  `CopilotCritic.GetEffectiveModel` (`GroundingAgent.cs:192`). See `02-critic.md`.

---

## 3. What becomes DEAD once generation is in-session + critic is a subagent

| Surface | Reference | Fate |
|---|---|---|
| `CopilotService` (session factory) | `CopilotService.cs:61, 85` | **Dead** — no CLI model call |
| `CopilotGenerationAgent` | `GenerationAgent.cs:78, 239` | **Dead** — generation moves to the interactive agent |
| `ProviderMapping` (config→SDK) | `ProviderMapping.cs:16, 57` | **Dead for generation/critic**; only alive if any non-generation model call remains |
| `AgentFactory.CreateAgentAsync` | `AgentFactory.cs:62` | **Dead for generation** |
| `ai.providers[]` generator model/provider | `AiConfig.cs:10` | **Dead for generation** — the interactive session selects the model. The array may linger as inert config |
| `ai.fallback_strategy` | `AiConfig.cs:13` | **Dead** — no programmatic provider fallback when a human drives |
| `execution.copilot_space` / `_owner` | `ExecutionConfig.cs:10–14` | **Dead** — Copilot Spaces has no Claude Code equivalent (§122 gap) |
| critic-model default switches | `GroundingAgent.cs:192`, `CopilotService.cs:319` | **Dead as fallback guessers**; `ai.critic.model` becomes the explicit selector |

**No `response_format` config exists** anywhere (confirmed in `01` §1.2 / Findings F-1) — so
the "retire response_format" item from the migration prompt is a no-op: there is nothing to
remove. The generation retry is already orchestration-side (absent in CLI), per `01` §1.4.

---

## 4. What new config is NEEDED (scoped, not designed)

- **Critic model as explicit config** — already exists as `ai.critic.model`
  (`CriticConfig.cs:30`). For v2 (target Sonnet 4.6) this key **suffices**; no new field is
  required, only a default/value change and retirement of the `GetEffectiveModel` fallback as
  the source of truth.
- **`mcp__spectra__*` allowlist** — belongs in `.claude/settings.json`
  (`permissions.allow`), **not** in `spectra.config.json`. Absent today (`05` §3, F-4).
  Client-side; the server enforces nothing (`04` §5).
- **Critic `context: fork` isolation** — not a config value but a skill-frontmatter rule
  (where the critic skill lives), per ARCHITECTURE-v2 §84. No `spectra.config.json` change.
- **Doc-suite / token-budget discipline** — already covered by `analysis.max_prompt_tokens`
  (`AnalysisConfig.cs:21`) plus the `--doc-suite` flag; v2 leans on it harder (§73) but adds
  no new key.

Net new config is **minimal**: essentially one `.claude/settings.json` allowlist entry and a
value change to `ai.critic.model`. Most of v2's config story is **deletion**, not addition.

---

## 5. Findings (recorded, not fixed)

- **F-1 — Hardcoded default provider in code, not config.** `AgentFactory.cs:72–73` bakes in
  `github-models`/`gpt-4o` as the fallback when no provider is enabled. Post-migration this
  default is meaningless (the human's session picks the model) but it is a code constant, not
  a config default — recorded so the spec retires it deliberately.
- **F-2 — `ai.providers` is `required`** (`AiConfig.cs:11`). If generation no longer reads it,
  a `required` array that nothing consumes is a schema smell; deserialization still demands
  it. Recorded for the spec to decide: keep inert, relax to optional, or remove.
- **F-3 — Three critic-model defaults, one config key.** `CriticConfig.model` is the config;
  but two code switches (`GroundingAgent.cs:192`, `CopilotService.cs:319`) supply defaults.
  v2 should collapse to the single config key. (Also `02` F-2.)

---

## 6. Conclusion

The config surface that the seam touches is small and almost entirely **subtractive** under
v2: the provider chain (`AgentFactory` → `ProviderMapping` → `CopilotService`) and its config
(`ai.providers` model selection, `ai.fallback_strategy`, `execution.copilot_space*`) go dead;
`response_format` never existed. What survives and matters more: `analysis.max_prompt_tokens`
(the cost lever, `AnalysisConfig.cs:21`) and `debug.enabled` telemetry (the measurement path,
ARCHITECTURE-v2 §109). What is genuinely new is one client-side `mcp__spectra__*` allowlist
entry and a value change to `ai.critic.model` — not a config redesign.
