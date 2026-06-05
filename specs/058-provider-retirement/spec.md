# Feature Specification: Provider retirement + config cleanup + demo repos

**Feature Branch**: `058-provider-retirement`
**Created**: 2026-06-05
**Status**: Draft
**Conceptual**: Migration spec 6 of 6 (052–057). Convergence point — implemented LAST.
**Grounds**: `docs/investigation/06-config.md`, `03-deterministic-core.md`, `02-critic.md`
**Input**: User description: "Retire the now-dead provider chain and its config, collapse to a Claude-Code-only shape, and migrate both demo repos to the cleaned schema."

## Overview

> **Scope revised 2026-06-05 (post-investigation).** The shipped v2 generation skill
> (`spectra-generate.md`, Spec 056) still drives **bulk generation** through the in-process
> `spectra ai generate --count N` command — the `compile-prompt`/`ingest-tests` seam (Spec 053)
> exists as CLI commands but generation was never switched onto it (only the *critic* was re-homed to
> a subagent). Removing the in-process **generation** model call therefore requires inverting the
> generation skill itself onto the compile→generate→ingest seam — a substantial, user-facing rewrite
> that is **deferred to Spec 059**. This spec (058) is correspondingly **narrowed**: it retires the
> **critic** provider chain and the dead provider-config, keeps the in-process generator (and
> `ai.providers` + the GitHub Copilot SDK) working, and migrates the demo configs. "Full provider
> retirement" completes in 059.

The first five migration specs (052–056) shipped **additively** — each added the Claude Code surface
(skills, the prompt-compiler handoff, the critic subagent, the execution-agent port) while leaving the
legacy in-process model-call path in place behind it.

This spec collapses the **dead** remainder that is safe to remove without breaking the shipped
generation flow: it **retires the in-process critic-provider chain** (the critic of record is now the
`spectra-critic` subagent, Spec 055; the shipped skill always runs generation with `--skip-critic`),
**removes the dead provider-config** (`ai.fallback_strategy` and the critic
`provider`/`api_key_env`/`base_url` selectors — `ai.critic.model` becomes the single critic selector),
and **migrates both demo configs**. A config still carrying any retired key is accepted with a
non-blocking, key-naming notice (never a silent drop). The in-process **generator** path
(`ai.providers`, `AgentFactory`, the Copilot generation session) and the **GitHub Copilot SDK** are
intentionally **retained** — their removal, together with the generation-skill inversion, is Spec 059.

## Clarifications

### Session 2026-06-05

- Q: With the provider chain gone, what is the end-state of the in-process AI path in `spectra ai generate` / extraction / critic CLI commands (the current live callers)? → A: **Remove the in-process model call entirely.** The CLI AI subcommands stop invoking the model in-process; generation/extraction run via the Claude Code skills (Spec 055) plus the Spec 053 compiled-prompt handoff. The deterministic phases (analysis, prompt compile, persist, index) survive; `CopilotService`, `AgentFactory`, `CopilotGenerationAgent`, and `ProviderChain` are deleted.
- Q: The feature text names `Spectra_Demo` and `Spectra_Demo_Desktop`, but `Spectra_Demo_Desktop` does not exist on disk. Which two repos does FR-009 migrate? → A: The two configs that exist and carry the dead keys: `C:/SourceCode/Spectra_Demo/test_app_documentation/spectra.config.json` and `C:/SourceCode/AutomateThePlanet_SystemTests/spectra.config.json`.
- Q: (post-investigation) The shipped generation skill still uses in-process `ai generate --count` for bulk generation, so removing the generation provider chain requires inverting that skill onto the compile/ingest seam. Should 058 do the full inversion or narrow? → A: **Narrow 058 + defer the generation inversion (and full SDK removal) to Spec 059.** 058 retires only the **critic** provider chain + dead provider-config and migrates the demos; the in-process generator + `ai.providers` + the Copilot SDK stay.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The dead provider chain and in-process model path are gone (Priority: P1)

A maintainer builds the solution after the cut. The legacy in-process model invocation no longer
exists anywhere in the CLI: there is no `CopilotService` session factory, no `CopilotGenerationAgent`,
no `ProviderMapping`, no `AgentFactory.CreateAgentAsync`, and no hardcoded `github-models`/`gpt-4o`
fallback. The generation and extraction CLI flows keep their deterministic phases (analysis, prompt
compilation, persist, index) and hand off to Claude Code — they never call a model in-process. The
solution compiles with no references to any removed symbol.

**Why this priority**: This is the headline of the whole 6-spec migration — the moment Spectra stops
embedding a model runtime in the CLI. Nothing else in this spec is meaningful until the code path it
describes as "dead" is actually removed. It is independently demonstrable: build the solution and grep
the source tree.

**Independent Test**: Build `Spectra.CLI` and confirm zero references to `CopilotService`,
`CopilotGenerationAgent`, `ProviderMapping`, `AgentFactory.CreateAgentAsync`, and `ProviderChain`
remain; confirm `spectra ai generate` / extraction reach their handoff without an in-process model
call.

**Acceptance Scenarios**:

1. **Given** the cleaned codebase, **When** the solution is built, **Then** `CopilotService`,
   `CopilotGenerationAgent`, `ProviderMapping`, and `AgentFactory.CreateAgentAsync` (with its hardcoded
   `github-models`/`gpt-4o` fallback) no longer exist and nothing references them.
2. **Given** the generation flow, **When** `spectra ai generate` runs, **Then** it completes its
   deterministic phases and hands off to Claude Code (Spec 055 skill / Spec 053 compiled prompt)
   without constructing an in-process agent or calling a model.
3. **Given** the critic flow, **When** the model selector is resolved, **Then** it is resolved only by
   `ai.critic.model` (via the single resolver) — the per-provider default switches that lived in
   `CopilotService.GetCriticModel` / `CopilotCritic.GetEffectiveModel` are gone.

---

### User Story 2 - The config collapses to a Claude-Code-only shape (Priority: P2)

A maintainer opens a `spectra.config.json` that no longer carries dead provider keys. `ai.providers[]`
is no longer required; `ai.fallback_strategy` is gone; the critic block keeps only `ai.critic.model`
(plus the behavioral keys `enabled`, `timeout_seconds`, `max_concurrent`) — its `provider`,
`api_key_env`, and `base_url` selectors are gone. The surviving cost-and-measurement config —
`analysis.max_prompt_tokens` and `debug.enabled` telemetry — is untouched and keeps working.

**Why this priority**: The config is the user-facing contract. Once US1 makes the chain dead, the
config that fed it is inert and misleading; collapsing it is what makes the v2 shape real for anyone
who reads or writes the file. Depends on US1 (don't relax config the code still requires) but is
independently testable against the config model.

**Independent Test**: Load a `spectra.config.json` that omits `ai.providers`, `ai.fallback_strategy`,
and the dead critic selector fields; confirm it validates and that `analysis.max_prompt_tokens` and
`debug.enabled` still bind and behave.

**Acceptance Scenarios**:

1. **Given** a config without `ai.providers`, **When** it loads, **Then** it validates (the array is no
   longer required) and the generation flow proceeds (the interactive session selects the model).
2. **Given** a config without `ai.fallback_strategy` or the dead critic selector fields, **When** it
   loads, **Then** it validates and `ai.critic.model` is honored as the single selector.
3. **Given** a config that sets `analysis.max_prompt_tokens` and `debug.enabled`, **When** it loads,
   **Then** both bind to their fields and behave exactly as before (pre-flight token budget enforced;
   telemetry written).

---

### User Story 3 - Existing configs migrate cleanly; both demo repos run on the cleaned schema (Priority: P3)

A maintainer (and each demo repo) has an old `spectra.config.json` that still lists `ai.providers`,
`ai.fallback_strategy`, and dead critic selector fields. On the next run the dead keys do not silently
disappear and do not break the run: the run proceeds on the cleaned schema while the maintainer is told,
in a non-blocking message naming the keys, that those keys are now ignored. Both demo repos
(`Spectra_Demo/test_app_documentation` and `AutomateThePlanet_SystemTests`) are migrated to the cleaned
schema and verified to init/run.

**Why this priority**: Migration safety and the demo repos matter, but only after the code and schema
are settled (US1, US2). This is the "don't strand existing users" slice. Pre-external-users, this is
the clean breaking-change window — so the bar is "defined and non-silent," not "transparently
preserved."

**Independent Test**: Run with an old config carrying all dead keys; confirm the defined behavior fires
(non-silent, non-blocking, run still succeeds) and the dead keys have no effect. Init/run both demo
repos on their migrated configs.

**Acceptance Scenarios**:

1. **Given** an old config still carrying `ai.providers` / `ai.fallback_strategy` / dead critic
   selector fields, **When** it loads, **Then** the dead keys are ignored with a non-blocking message
   that names them (not a silent drop) and the run proceeds on the cleaned schema.
2. **Given** either demo repo, **When** it inits and runs, **Then** it runs on the cleaned
   `spectra.config.json` with no dead provider config and `ai.critic.model` set to the v2 value.

---

### Edge Cases

- **Config with neither `ai.providers` nor `ai.critic`**: validates; generation hands off to the
  interactive session (which selects the model); the critic falls back to the single
  `ai.critic.model` default when the block is absent.
- **Config that still sets `ai.critic.provider`/`base_url`/`api_key_env`**: those keys are inert —
  ignored with the same non-blocking notice; only `ai.critic.model` influences the critic.
- **An empty or `[]` `ai.providers`**: no longer an error (it was satisfying a `required` constraint
  that is being removed); ignored.
- **A surviving non-generation in-process model call, if any is discovered during implementation**:
  if removing the chain would orphan a real model call outside generation/extraction/critic, that is a
  finding to surface — not a silent breakage. The investigation found none (`06` §3), but the build is
  the arbiter.
- **`response_format`**: there is no `response_format` key anywhere (`06` §3) — its "retirement" is a
  verified no-op; the spec asserts its continued absence rather than removing anything.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The in-process **critic-provider chain** MUST be retired. The critic of record is the
  `spectra-critic` subagent (Spec 055); the shipped generation skill always runs with `--skip-critic`.
  The in-process critic (`CopilotCritic`/`GroundingAgent`), the `CriticFactory` Copilot-SDK
  construction path, and the `CopilotService` critic session/model helpers
  (`CreateCriticSessionAsync`, `GetCriticModel`) MUST be removed, and `GenerateHandler`'s in-process
  verification branch MUST no longer construct an in-process critic.
- **FR-002**: The in-process **generator** path MUST be left working: `ai.providers`, `AgentFactory`,
  the Copilot generation session, the behavior analyzer, the extractors, and the `GitHub.Copilot.SDK`
  package are **retained** for this spec. Their removal, with the generation-skill inversion onto the
  `compile-prompt`/`ingest-tests` seam, is **Spec 059**.
- **FR-003**: Dead provider-config MUST be removed: `ai.fallback_strategy` MUST be removed; the critic
  selector fields `provider`, `api_key_env`, and `base_url` MUST be removed. `ai.critic.model` MUST be
  the single surviving **critic** model selector. (`ai.providers` is **retained** — it still feeds the
  in-process generator.)
- **FR-004**: The critic-model logic MUST resolve solely from `ai.critic.model` via `CriticModelResolver`
  (Spec 055). The per-provider default switches (`CriticConfig.GetEffectiveModel`/`GetDefaultApiKeyEnv`,
  `CopilotService.GetCriticModel`, `CopilotCritic.GetEffectiveModel`) MUST be removed so no second
  source can drift, and `CriticConfig.IsValid()` MUST no longer require a `provider`.
- **FR-005**: Surviving config MUST be untouched in behavior: `analysis.max_prompt_tokens` (the cost
  lever / pre-flight token budget) and `debug.enabled` telemetry MUST remain and keep working exactly
  as before; the generator's `ai.providers` validation (`MISSING_PROVIDERS`) is unchanged.
- **FR-006**: A migration path MUST exist for existing `spectra.config.json` files: a config that
  omits the retired keys MUST validate, and a config that still carries them MUST exhibit a **defined,
  non-silent** behavior — the retired keys (`ai.fallback_strategy`, `ai.critic.provider`,
  `ai.critic.api_key_env`, `ai.critic.base_url`) are ignored with a non-blocking message that names
  them, and the run still proceeds. It MUST NOT be a silent drop and MUST NOT block the run.
- **FR-007**: The continued **absence** of any `response_format` config MUST be asserted (no-op
  retirement) — there is nothing to remove, and nothing may be added.
- **FR-008**: No server-side surface in `Spectra.MCP` may change; this spec touches only the CLI
  critic chain, its config, and demo configs. (The execution path was ported in Spec 056.)
- **FR-009**: Both demo repos MUST be migrated and verified to init/run:
  `C:/SourceCode/Spectra_Demo/test_app_documentation` and
  `C:/SourceCode/AutomateThePlanet_SystemTests`. Each migrated config MUST drop
  `ai.fallback_strategy` and the critic `provider`/`api_key_env`/`base_url` fields, and set
  `ai.critic.model` to the v2 value. (`ai.providers` is retained.)

### Key Entities *(config shape — not implementation)*

- **`ai` block (AiConfig)**: After the cut, holds `critic` plus the deterministic generation knobs
  (`generation_timeout_minutes`, `analysis_timeout_minutes`, `generation_batch_size`). Loses
  `providers` as a required member and loses `fallback_strategy`.
- **`ai.critic` block (CriticConfig)**: After the cut, holds `enabled`, `model` (the single selector),
  `timeout_seconds`, `max_concurrent`. Loses `provider`, `api_key_env`, `base_url`.
- **`analysis` block**: Unchanged. `max_prompt_tokens` remains the primary cost lever.
- **`debug` block**: Unchanged. `enabled` telemetry remains and gains importance as the measurement
  path.
- **`execution` block**: Already emptied of `copilot_space*` in Spec 056 — referenced here only to
  confirm the schema is not left half-cleaned.
- **Demo configs**: Two on-disk `spectra.config.json` files that must end on the cleaned schema.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After the change the in-process critic is gone — `CopilotCritic`/`GroundingAgent` is
  deleted, `CriticFactory` no longer constructs a critic or reads a provider, and
  `CopilotService.CreateCriticSessionAsync`/`GetCriticModel` are removed — and the solution builds
  clean. (The in-process **generator** path — `CopilotService` generation session, `AgentFactory`,
  `BehaviorAnalyzer`, the `GitHub.Copilot.SDK` package — is intentionally retained for Spec 059.)
- **SC-002**: `spectra ai generate` runs unchanged for generation; in-process verification no longer
  occurs (`ShouldVerify` is always false) — tests are written unverified and the `spectra-critic`
  subagent is the critic of record.
- **SC-003**: A `spectra.config.json` whose `ai.critic` has only `model` (no `provider`/`api_key_env`/
  `base_url`) and no `ai.fallback_strategy` validates successfully; one that still carries those keys
  validates and surfaces a non-blocking, key-naming notice via `spectra validate` (and
  `ConfigLoader.DetectDeprecatedKeys`).
- **SC-004**: `ai.critic.model` is the only value that changes the resolved critic model; with it unset
  the single documented default (`claude-sonnet-4-6`) applies, and `CriticConfig.IsValid()` no longer
  requires a provider.
- **SC-005**: `analysis.max_prompt_tokens` and `debug.enabled` retain their pre-change behavior, and
  `ai.providers` (generator) still validates (`MISSING_PROVIDERS` unchanged) — the surviving-config
  regression tests are unchanged and green.
- **SC-006**: Both demo repos validate/run on their migrated configs (the retired critic keys +
  `fallback_strategy` removed; `ai.providers` retained; `ai.critic.model` set).
- **SC-007**: The `Spectra.MCP` engine/tool test corpus and the `Spectra.Core` surviving-config test
  corpus are unchanged and green (no regression in surfaces that should not have changed).

## Assumptions

- **Additive precedent now converging.** Specs 052–056 shipped additively ("CLI surface"), each
  deferring the literal in-process model-call removal. This spec is the agreed convergence point where
  that removal lands once — confirmed by the Q1 clarification ("remove the in-process model call
  entirely"). Removing the chain therefore necessarily removes its live callers (FR-002), not just
  inert code.
- **Critic-model collapse is largely pre-done.** Spec 055 already introduced `CriticModelResolver` as
  the single source of truth (`ai.critic.model`), with the old switches delegating to it. FR-004 here
  finishes the collapse by deleting the delegating shims that die with `CopilotService` — it does not
  re-architect critic-model selection.
- **Old-config behavior = ignore-with-notice.** Per the Spec 056 back-compat precedent (unknown JSON
  keys are ignored, not rejected) and FR-006's "not a silent drop," the defined behavior for a config
  still carrying dead keys is: ignore them, emit a non-blocking message naming them, proceed. No
  rewrite-the-file migration is performed automatically; the demo configs are migrated by hand under
  FR-009.
- **Demo repos identity.** The two demo repos are `Spectra_Demo/test_app_documentation` and
  `AutomateThePlanet_SystemTests` (the feature text's `Spectra_Demo_Desktop` does not exist on disk);
  confirmed by the Q2 clarification and the saved demo-sync note.
- **`response_format` is a no-op.** No such key exists anywhere (`06` §3); FR-007 asserts its absence
  rather than removing anything.
- **No `Spectra.MCP` change.** The execution path is client-agnostic and was ported in Spec 056; this
  spec is CLI-and-config only.

## Dependencies

- **Specs 052, 053, 054, 055** — the replacement surfaces (in-session generation, the prompt-compiler
  handoff, the critic subagent, the skills) must exist before the in-process path they replace can be
  cut. This is the **last** spec; the chain can only be removed once nothing of value still calls it.
- **Spec 056** — independent, but its `execution.copilot_space*` removal must be landed before/with
  this spec's final config sweep to avoid a half-cleaned schema (already merged on `claude-code-v2`).

## Out of Scope

- Everything functional in 052–056 (this spec only removes the dead remainder they leave behind).
- The defect-injection bake-off (post-migration).
- Rewriting queued specs 048–051 (post-migration, new numbers).
- Any `Spectra.MCP` server-side change.
- An automatic in-place rewrite of arbitrary user config files (the defined behavior is
  ignore-with-notice; the two demo configs are migrated by hand).

## Documentation Impact

- **Factually wrong (must fix)**: `configuration` (provider / critic-selector / fallback sections);
  `cli-reference` (provider-related flags, if any); `copilot-cli` / `copilot-chat` pages (superseded).
- **Stale (update)**: `overview`; any getting-started config snippets still showing `ai.providers`,
  `ai.fallback_strategy`, or critic `provider`/`api_key_env`/`base_url`.
