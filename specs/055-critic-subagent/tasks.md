---
description: "Task list for 055-critic-subagent"
---

# Tasks: Critic as a `context: fork` Subagent (+ Gating Semantics)

**Input**: Design documents from `/specs/055-critic-subagent/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the spec explicitly enumerates Rewrite + Net-new test tasks.

**Scope**: *Additive surface (match 053/054)* — add the model-free critic compile/ingest surface,
the `context: fork` critic subagent skill, and collapse the critic-model selection + dead-code
cleanup; **keep** the in-process critic model call (`GroundingAgent.cs:124`) working so
`spectra ai generate`'s batch verification stays green. The literal removal + invocation wiring is
the subsequent spec.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 from spec.md

## Path Conventions
Single project: `src/Spectra.CLI/`, `tests/Spectra.CLI.Tests/` at repo root.

---

## Phase 1: Setup

- [X] T001 Create `src/Spectra.CLI/Verification/` folder (mirrors `src/Spectra.CLI/Generation/` and `src/Spectra.CLI/Extraction/`) and `tests/Spectra.CLI.Tests/Verification/` folder; confirm `dotnet build` is green before changes.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Shared typed-contract values the stories below depend on. No behavior yet.

- [X] T002 [P] Create `CriticPromptCompileResult` record in `src/Spectra.CLI/Verification/CriticPromptCompileResult.cs` — `IsSuccess`/`Prompt`/`MissingInput`/`Message` with `Success(prompt)` and `MissingRequired(input, message)` factories (mirror `Generation/PromptCompileResult.cs`).
- [X] T003 [P] Create `VerdictIngestOutcome` enum (`Verdict | EmptyResponse | ParseFailure`) and `VerdictIngestResult` record in `src/Spectra.CLI/Verification/VerdictIngestResult.cs` — `Outcome`, `IsSuccess => Outcome == Verdict`, `Result` (reused `VerificationResult?`), `Drops => Result?.Verdict == Hallucinated`, `Errors`, factories `FromVerdict(result)` / `Failure(outcome, errors)`. **Reuse** `VerificationResult`/`VerificationVerdict` from `Spectra.Core` (do not redefine).

**Checkpoint**: Types compile; nothing wired yet.

---

## Phase 3: User Story 1 — Critic runs as a fresh-context subagent, not an in-process model call (Priority: P1) 🎯 MVP

**Goal**: A deterministic, model-free critic-prompt compiler + a model-free verdict-ingest boundary
(happy path) + the `context: fork` critic subagent skill — the critic's model turn moves to a
subagent; the deterministic ingest stays in the CLI.

**Independent Test**: `compile-critic-prompt --test t.json` emits a byte-identical prompt for
identical input with nothing written, no provider configured; a well-formed verdict JSON fed to
`ingest-verdict` classifies `Verdict` (exit 0); the `spectra-critic` skill exists, parses, and is
`context: fork` + `disable-model-invocation: true`.

### Implementation

- [X] T004 [US1] Add `CriticPromptCompiler` static class in `src/Spectra.CLI/Verification/CriticPromptCompiler.cs`: `Assemble(test, docs, templateLoader?)` delegates to the **reused-verbatim** `CriticPromptBuilder` (`BuildSystemPrompt` + `BuildUserPrompt`, joined `{system}\n\n---\n\n{user}` exactly as `GroundingAgent.cs:77–79`); `Compile(test, docs, templateLoader?) → CriticPromptCompileResult` refuses (`MissingRequired("test_artifact", …)`) when the test is null or has no id/title. Empty doc set is **not** a refusal. Deterministic — no timestamps/GUIDs (FR-002).
- [X] T005 [US1] Add `VerdictIngestor` in `src/Spectra.CLI/Verification/VerdictIngestor.cs` with `Classify(string? json) → VerdictIngestResult` — **happy path here**: reuse the `CriticResponseParser` JSON-extraction *shape* (fence-strip + `{…}` slice) and, on a well-formed `{ verdict, score, … }`, return `FromVerdict(...)` carrying a reused `VerificationResult`. (Strict fail-loud branches land in US3.) Never throws.
- [X] T006 [US1] Add `CompileCriticPromptCommand` (`spectra ai compile-critic-prompt`) in `src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs` per `contracts/compile-critic-prompt.md`: `--test`/`--docs`, emit prompt to stdout (or `{ "prompt": ... }` json) on success, exit `4` refuse, exit `1` env error. `using Spectra.CLI.Infrastructure;` for `OutputFormat` (per 054 lesson).
- [X] T007 [US1] Add `IngestVerdictCommand` (`spectra ai ingest-verdict`) in `src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs` per `contracts/ingest-verdict.md`: `--from`/stdin, call `VerdictIngestor.Classify`, exit `0` on `Verdict` and print verdict/score/`drop` (happy path here; failure exits 5/6 wired in US3). `using Spectra.CLI.Infrastructure;`.
- [X] T008 [US1] Register both subcommands on the `ai` command group in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` (beside `CompileExtractionPromptCommand`/`IngestCriteriaCommand`).
- [X] T009 [US1] Add the net-new `context: fork` critic subagent skill `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md` per `contracts/critic-subagent-skill.md`: frontmatter `name: spectra-critic`, fork isolation, `disable-model-invocation: true`, model from `ai.critic.model`; procedure restricts input to the test artifact + selected source docs and emits `{ verdict, score, findings }`. No generator state.
- [X] T010 [US1] Register `spectra-critic` in `src/Spectra.CLI/Skills/SkillsManifest.cs` and surface it in `src/Spectra.CLI/Skills/AgentContent.cs` so `spectra update-skills` / `spectra init` deploy it (follow the existing `spectra-generation.agent.md` registration).

### Tests

- [X] T011 [P] [US1] `tests/Spectra.CLI.Tests/Verification/CriticPromptCompilerTests.cs`: identical `(test, docs)` → byte-identical `Assemble`; `Compile` returns `Success` with a prompt containing the test id/title and doc content; refuses with `MissingInput == "test_artifact"` when the test is null/has no id+title; runs with no provider (model-free).
- [X] T012 [P] [US1] `tests/Spectra.CLI.Tests/Skills/CriticSubagentSkillTests.cs`: the `spectra-critic.agent.md` resource exists, parses, declares fork isolation + `disable-model-invocation: true`, and its instruction restricts input to artifact + source documents (no generator-state references).

**Checkpoint**: US1 is a working, model-free compile→ingest MVP plus the subagent skill. Existing
generate verification untouched.

---

## Phase 4: User Story 2 — Verdict gating stays advisory and unchanged (Priority: P1)

**Goal**: The gate decision derived at the boundary matches today's behavior exactly: `hallucinated`
drops; `grounded`/`partial`/`unverified`/`manual` pass; `Manual` preserved and skipped.

**Independent Test**: `ingest-verdict` returns `drop=true` only for `hallucinated`; `grounded`/
`partial` return `drop=false`; the reused `Verdict != Hallucinated` filter and `Manual`-preservation
are unchanged.

### Implementation

- [X] T013 [US2] Confirm `VerdictIngestResult.Drops` is `Result?.Verdict == Hallucinated` (and false for every non-`Verdict` outcome) in `src/Spectra.CLI/Verification/VerdictIngestResult.cs`; have `IngestVerdictCommand` print the `drop`/`pass` gate decision from it. Do **not** modify the reused gating (`GenerateHandler.cs:847`) or `Manual`-preservation (`GenerateHandler.cs:2134`).

### Tests

- [X] T014 [P] [US2] `tests/Spectra.CLI.Tests/Verification/VerdictIngestorTests.cs` (gating cases): `hallucinated` → `Drops == true`; `grounded`/`partial` → `Drops == false`; an `unverified`-shaped (`Partial` + errors) result passes (not dropped).
- [X] T015 [P] [US2] Add a guard test asserting the reused verdict-gating contract is intact: a list of `(Test, VerificationResult)` filtered by `Verdict != Hallucinated` keeps grounded/partial/manual and drops only hallucinated (pin the drop-vs-pass set). **Do not** edit any existing `Spectra.Core` grounding-model or gating test — this is an additive guard.

**Checkpoint**: Gating proven unchanged; advisory verdict intact.

---

## Phase 5: User Story 3 — Damage fails loud; failure and parse-miss are distinct (Priority: P1)

**Goal**: `ingest-verdict` fails loud on a missing/unparseable `verdict` or `score` (specific error,
no `Partial`/`0.5`), `EmptyResponse` on empty input, never throws; the critic-call-failure path stays
non-blocking and distinct.

**Independent Test**: missing `verdict` or `score` → `ParseFailure` (exit 6, specific error, no soft
default); empty input → `EmptyResponse` (exit 5); `Classify` never throws.

### Implementation

- [X] T016 [US3] Harden `VerdictIngestor.Classify` in `src/Spectra.CLI/Verification/VerdictIngestor.cs`: empty/whitespace → `Failure(EmptyResponse, ["critic returned no content"])`; non-JSON / no `{…}` → `Failure(ParseFailure, …)`; **missing or unparseable `verdict`** → `Failure(ParseFailure, ["critic response missing required 'verdict' field"])`; **missing or unparseable `score`** → `Failure(ParseFailure, ["critic response missing required 'score' field"])`. No `Partial`/`0.5` default anywhere (FR-006). Wrap parsing so it never throws.
- [X] T017 [US3] In `IngestVerdictCommand.cs`, wire the failure branches: `EmptyResponse` → exit `5`, `ParseFailure` → exit `6`, emit machine-readable `{ outcome, errors }`; persist/print nothing as a verdict. Mirror `IngestCriteriaCommand`'s 5/6 split. Keep `Verdict` → exit `0`.
- [X] T018 [US3] Add a short XML-doc note in `VerdictIngestor.cs` recording the FR-007 distinction: a critic *call failure* (exception/timeout) is the runtime's `Unverified`-style result (retained in-process path) and is **not** routed through `Classify`; only a *returned* response is classified, so damage (`ParseFailure`) and failure are never conflated.

### Tests

- [X] T019 [P] [US3] `tests/Spectra.CLI.Tests/Verification/VerdictIngestorTests.cs` (fail-loud cases): missing `verdict` → `ParseFailure` with specific error and **no** `Partial`/`0.5`; missing `score` → `ParseFailure`; garbage/non-JSON → `ParseFailure`; empty/whitespace → `EmptyResponse`; every case `IsSuccess == false`, `Drops == false`, and `Classify` never throws.
- [X] T020 [P] [US3] `tests/Spectra.CLI.Tests/Commands/CriticVerificationCommandsTests.cs`: exit-code contract — `compile-critic-prompt` missing `--test` → `4`; `ingest-verdict` `Verdict`→0, `EmptyResponse`→5, `ParseFailure`→6, env error→1. (`[Collection("WorkingDirectory")]`, config via `JsonSerializer.Serialize(SpectraConfig.Default)` per 054 lesson.)

**Checkpoint**: Fail-loud boundary proven; damage vs. failure distinct; token-free.

---

## Phase 6: User Story 4 — Single model selector; dead/duplicated code removed (Priority: P2)

**Goal**: `ai.critic.model` is the single source of truth; the duplicated provider→default switch is
collapsed to one resolver; the dead second factory is deleted; stale comments corrected.

**Independent Test**: `CriticModelResolver.Resolve(config with Model="X")` → `"X"`; with no Model,
every provider → the single same-family default (no provider branch); `CopilotCriticFactory` and the
duplicated switch no longer exist.

### Implementation

- [X] T021 [US4] Create `CriticModelResolver` in `src/Spectra.CLI/Agent/Critic/CriticModelResolver.cs` per `contracts/critic-model-selector.md`: `const DefaultCriticModel` (single same-family default, §32 target Sonnet 4.6) and `Resolve(CriticConfig?) => config.Model when set, else DefaultCriticModel` — no provider-keyed branch.
- [X] T022 [US4] Modify `src/Spectra.CLI/Agent/Copilot/GroundingAgent.cs`: replace `GetEffectiveModel`'s provider switch body with `=> CriticModelResolver.Resolve(config)`; **delete** the dead `CopilotCriticFactory` class (`GroundingAgent.cs:226`, investigation F-1); update the stale cross-architecture comment (`:197`) to the §32 same-family direction. Keep the in-process model call (`:124`) intact.
- [X] T023 [US4] Modify `src/Spectra.CLI/Agent/Copilot/CopilotService.cs`: replace `GetCriticModel`'s provider switch body with `=> CriticModelResolver.Resolve(criticConfig)`; update the stale cross-architecture comment (`:324`) to the §32 same-family direction.

### Tests

- [X] T024 [P] [US4] `tests/Spectra.CLI.Tests/Agent/CriticModelResolverTests.cs`: `Resolve(Model="X")` → `"X"`; `Resolve(no Model)` → `DefaultCriticModel` for providers `anthropic`/`openai`/`azure-openai`/`azure-anthropic`/`github-models` (single default, no provider branch reachable).
- [X] T025 [P] [US4] **Rewrite** the dead/duplicated-code tests: remove any `CopilotCriticFactory` tests (cover deleted code) and any provider→default-model fallback tests, collapsing the latter into the `CriticModelResolver` single-selector assertions above. Confirm by repo search that zero provider→default switches and no `CopilotCriticFactory` remain.

**Checkpoint**: One model selector; dead factory gone; comments correct.

---

## Phase 7: Polish & Cross-Cutting

- [X] T026 [P] Fix factually-wrong docs (per spec Documentation Impact): cross-architecture critic guidance; `ai.critic` config docs describing the provider/fallback model selection; the grounding-verification page where it states failure/parse-miss behavior. Update the grounding-verification workflow narrative to the `context: fork` subagent + fail-loud-damage + distinct-failure story. Search `docs/` for the affected pages.
- [X] T027 Run `dotnet build` then `dotnet test tests/Spectra.CLI.Tests` and `dotnet test tests/Spectra.Core.Tests` — confirm the protected regression net (`Spectra.Core` grounding-model tests + verdict-gating drop-vs-pass tests) is **unchanged and green**. Any break there is a regression to investigate, not a test to edit.
- [X] T028 Manual smoke per `quickstart.md`: `compile-critic-prompt` determinism (`diff` two runs) + refuse exit 4; `ingest-verdict` exits 0/5/6 with the right `drop` decision; `spectra-critic.agent.md` deploys via `spectra update-skills`.

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002, T003)** → stories.
- **US1 (T004–T012)**: MVP. T004 needs T002; T005 needs T003; T006 needs T004; T007 needs T005; T008 needs T006+T007; T009→T010 (skill then register). Tests T011/T012 after their targets.
- **US2 (T013–T015)**: depends on US1 (T003 result type + T007 command exist).
- **US3 (T016–T020)**: depends on US1 (T005 ingestor + T007 command exist).
- **US4 (T021–T025)**: independent of US1–US3 (config/runtime files); needs only Setup. Can run in parallel with the `Verification/` surface.
- **Polish (T026–T028)**: after all stories; T027 is the final green-net gate.

**Story independence**: US4 touches `Agent/Critic/` + `Agent/Copilot/` (resolver, factory, comments)
while US1–US3 build the `Verification/` surface + skill — different files, parallelizable.

## Parallel Execution Examples

- Foundational: T002 ‖ T003 (different files).
- After US1 implementation lands: T011 ‖ T012 (different test files).
- US4 alongside US1–US3: T021→T022→T023 while the `Verification/` surface is built.
- Polish: T026 ‖ (T027 sequential gate) — keep T027 last.

## Implementation Strategy

- **MVP = US1** (model-free compile + happy-path ingest + subagent skill): the critic's model turn
  moves to a `context: fork` subagent; deterministic ingest stays in the CLI.
- **+US2/US3** lock the gating semantics (advisory verdict unchanged; damage fails loud; failure
  distinct) — all P1.
- **+US4** collapses the model selector and removes dead/duplicated code (P2).
- Keep `spectra ai generate` verification green at each checkpoint (additive scope); T027 is the
  final gate.
