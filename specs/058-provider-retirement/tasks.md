---
description: "Task list for 058-provider-retirement (NARROW scope — see spec.md Scope-revised note)"
---

# Tasks: Critic-provider retirement + config cleanup + demo repos (058, narrow)

**Input**: `specs/058-provider-retirement/` (spec.md re-scoped 2026-06-05).
**Scope**: Retire the in-process **critic** chain + dead provider-config; KEEP the in-process
generator (`ai.providers`, `AgentFactory`, Copilot generation session, GitHub.Copilot.SDK). Full
generation inversion + SDK removal = **Spec 059**.
**Tests**: INCLUDED.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup
- [ ] T001 Confirm green baseline (`dotnet build`/`dotnet test`); `git status src/Spectra.MCP` empty (FR-008).

## Phase 2: Foundational — dead-key notice channel
- [ ] T002 Add pure `ConfigLoader.DetectDeprecatedKeys(json) → IReadOnlyList<string>` detecting the FOUR retired keys (`ai.fallback_strategy`, `ai.critic.provider`, `ai.critic.api_key_env`, `ai.critic.base_url` — NOT `ai.providers`); wire its result onto the parse/command result as a non-blocking note (Spec 048 `Notes` channel) without changing validation outcome. `src/Spectra.Core/Config/ConfigLoader.cs`.

## Phase 3: US1 — retire the in-process critic-provider chain (P1) 🎯
**Goal**: critic of record is the subagent; no in-process critic or critic-provider config remains. Generator untouched.
- [ ] T003 [US1] Config models: `AiConfig` remove `FallbackStrategy` (keep `Providers`); `CriticConfig` remove `Provider`/`ApiKeyEnv`/`BaseUrl` + `GetEffectiveModel`/`GetDefaultApiKeyEnv`, `IsValid()`→`true` (done). `SpectraConfig.Default` drop `Critic.Provider` (keep providers + `Critic.Model`). `src/Spectra.Core/Models/Config/`.
- [ ] T004 [US1] Retire `CopilotCritic` (`Agent/Copilot/GroundingAgent.cs`) and the `CriticFactory` SDK construction path; remove `CopilotService.CreateCriticSessionAsync` + `GetCriticModel`. Delete `ICriticRuntime.cs` if `CopilotCritic` was its only implementor. Keep `CriticModelResolver` (single `ai.critic.model` source).
- [ ] T005 [US1] `GenerateHandler`: the in-process verification branch must no longer construct a critic — generation produces tests; verification is the subagent. Keep `--skip-critic` accepted (now the effective behavior). Keep the generator path (`AgentFactory.CreateAgentAsync`, `BehaviorAnalyzer`) intact.
- [ ] T006 [P] [US1] `ConfigHandler`: stop printing `fallback_strategy` + critic `provider`; keep the providers list + critic `model`.
- [ ] T007 [P] [US1] Tests: rewrite `tests/Spectra.Core.Tests/Models/Config/CriticConfigTests.cs` (drop `provider`/`GetEffectiveModel` asserts; `IsValid` no longer needs provider) and the critic-provider factory tests (`Agent/Critic/CriticFactory*Tests`, `CopilotCriticDefaultModelTests` as needed). Keep `CriticModelResolverTests`, providers/generator tests, MCP, surviving-config (do NOT touch Core/persistence net).

**Checkpoint**: build green; in-process critic gone; generator + `ai generate --count`/`--analyze-only` still work.

## Phase 4: US2 — config cleanup + ignore-with-notice (P2)
- [ ] T008 [US2] Net-new cleaned-critic-schema test: a config with `ai.critic.model` only (no provider/key/url) + no `fallback_strategy` validates; `ai.critic.model` honored (FR-003/SC-003).
- [ ] T009 [US2] Net-new ignore-with-notice test: a config carrying all four retired keys validates, exit unchanged, note names each key (FR-006); `ai.providers` is NOT flagged.
- [ ] T010 [P] [US2] Net-new `response_format`-absence guard (FR-007).
- [ ] T011 [P] [US2] Template seed `src/Spectra.CLI/Templates/spectra.config.json`: drop `fallback_strategy` + critic `provider`/`api_key_env`/`base_url`; keep `providers` + `critic.model` (v2 value).

## Phase 5: US3 — demo migration + docs (P3)
- [ ] T012 [P] [US3] Migrate `C:/SourceCode/Spectra_Demo/test_app_documentation/spectra.config.json`: drop the four retired keys; keep `ai.providers`; set `ai.critic.model`. Verify `spectra validate`.
- [ ] T013 [P] [US3] Migrate `C:/SourceCode/AutomateThePlanet_SystemTests/spectra.config.json` the same way; verify.
- [ ] T014 [US3] Net-new demo-config smoke test (FR-009): both migrated configs validate with no retired keys (committed fixtures).
- [ ] T015 [P] [US3] Docs: fix `configuration`/`cli-reference`/`copilot-*` (critic provider/fallback sections); note generator provider config + full retirement pending Spec 059. Update `overview`/getting-started snippets.
- [ ] T016 [P] [US3] `CLAUDE.md`: note the critic runs as the subagent and critic-provider config is retired; keep the SDK-as-generator-runtime line accurate (full retirement → 059). Keep < 40K.

## Phase 6: Verify
- [ ] T017 Full `dotnet build` + `dotnet test`; MCP + surviving-config corpora green; `git status src/Spectra.MCP` empty.
- [ ] T018 Quickstart-style checks; `CHANGELOG.md` entry (critic-provider retirement + config cleanup; full provider retirement deferred to 059).

## Dependencies
Phase 1→2→3→4→5→6. US2 cleaned-schema depends on US1 (CriticConfig fields gone). US3 depends on US2 (template/demo shape).
