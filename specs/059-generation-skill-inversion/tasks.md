---
description: "Task list for Spec 059 — Generation-skill inversion + completion"
---

# Tasks: Generation-skill inversion + completion

**Input**: Design documents from `/specs/059-generation-skill-inversion/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Requested by the spec (rewrite + net-new). Test tasks are included.

**Ordering (research D5)**: Phase A = seam coverage + skill rewrite (US1→US2→US3, additive, keeps the in-process command working as a safety net). Phase B = removal (US4, depends on US1–US3 being green). Remove call sites before their targets so each deletion compiles.

## Path Conventions

Single-project CLI: `src/Spectra.CLI/`, `src/Spectra.Core/`, tests in `tests/Spectra.CLI.Tests/` and `tests/Spectra.Core.Tests/`.

---

## Phase 1: Setup (Shared)

- [x] T001 Record green baseline: run `dotnet build` then `dotnet test` and note the pass counts for `Spectra.Core.Tests`, `Spectra.CLI.Tests`, `Spectra.MCP.Tests` (the regression net; any later red in Core/053/055 is a regression to investigate per FR-007).
- [x] T002 Confirm the existing seam test harness used by the 053/055 command tests (CLI invocation + stdout/exit capture) is reusable for the new seam tests; note its location for reuse in US1–US3 test tasks.

**Checkpoint**: Baseline known-green; harness located.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Verify the reused boundary the additive seam depends on, before wiring flows through it.

- [x] T003 Verify `ingest-tests` (`src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs`) accepts a single-element test array end-to-end (it must, for from-description reuse) — add a focused integration test in `tests/Spectra.CLI.Tests/Commands/IngestTestsCommandTests.cs` if not already covered. Do NOT modify `ingest-tests` behavior (FR-007).

**Checkpoint**: Boundary confirmed; user stories can proceed.

---

## Phase 3: User Story 1 - Bulk generation runs on the seam (Priority: P1) 🎯 MVP

**Goal**: Bulk generation runs entirely through `compile-prompt` → in-session generate → `ingest-tests` with the mandatory `spectra-critic` step and the 053 fail-loud retry; no `spectra ai generate --count`.

**Independent Test**: Run the rewritten skill's bulk path against a fixture suite; confirm it drives `compile-prompt`/`ingest-tests`, invokes the critic per test, and never calls `spectra ai generate --count`.

### Tests for User Story 1

- [x] T004 [P] [US1] Skill-on-seam assertion test: a test over `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` asserting the bulk flow text drives `compile-prompt`/`ingest-tests` and contains zero `spectra ai generate --count` invocations, plus a mandatory `spectra-critic` step. Add in `tests/Spectra.CLI.Tests/Skills/SpectraGenerateSkillSeamTests.cs`.
- [x] T005 [P] [US1] Bulk seam integration test: `compile-prompt --suite X --count N` emits a grounded prompt; piping a well-formed N-test array to `ingest-tests X` persists N and updates `_index.json`; a malformed/truncated array fails loud (exit 5/6) and persists nothing. Add in `tests/Spectra.CLI.Tests/Commands/BulkSeamFlowTests.cs`.

### Implementation for User Story 1

- [x] T006 [US1] Rewrite the **bulk** section of `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`: Step "generate" now runs `spectra ai compile-prompt --suite {suite} --count {count} [--focus]` → agent generates in-session → `spectra ai ingest-tests {suite}` (stdin), with the 053 fail-loud retry on ingest exit 5/6. Keep the existing mandatory `spectra-critic` subagent step (Step 8) verbatim. Remove the `spectra ai generate --count` call.
- [x] T007 [US1] Ensure the critic choreography in the skill is the single verification of record for the bulk flow and is reachable from the new ingest step (wire Step 7→8 to the ingested files). No code change expected beyond skill text.

**Checkpoint**: Bulk generation runs on the seam with mandatory critic; in-process command still present (safety net).

---

## Phase 4: User Story 2 - From-description generation runs on the seam (Priority: P2)

**Goal**: From-description routes through `compile-prompt --from-description` (count=1, criteria injected) → `ingest-tests` → mandatory critic; no in-process `--from-description`.

**Independent Test**: Run the skill's from-description path against a suite with matching criteria; confirm it routes through the seam, the test's `criteria` field is populated (Spec 050), and no in-process `--from-description` call occurs.

### Tests for User Story 2

- [x] T008 [P] [US2] Contract test for `compile-prompt --from-description`: with `--from-description "..." --context "..."` it emits a single-test (count=1) grounded prompt; with matching criteria present the prompt contains the Spec 050 mandatory-mapping block; determinism (identical inputs → identical output); refuse-to-emit exit 4 when `--suite` missing. Add in `tests/Spectra.CLI.Tests/Commands/CompilePromptFromDescriptionTests.cs`.
- [x] T009 [P] [US2] Skill from-description seam test: the skill's specific-test flow drives `compile-prompt --from-description`/`ingest-tests` and not `spectra ai generate --from-description`. Extend `tests/Spectra.CLI.Tests/Skills/SpectraGenerateSkillSeamTests.cs`.

### Implementation for User Story 2

- [x] T010 [US2] Extend `src/Spectra.CLI/Commands/Generate/CompilePromptCommand.cs`: add `--from-description` and `--context` options; when `--from-description` is set, force `count=1` and build the user-prompt from description+context (reuse/relocate `UserDescribedGenerator.BuildPrompt` shaping into a shared static helper). Criteria still resolved via `GenerateHandler.LoadCriteriaContextAsync` and injected by `PromptCompiler`. Absent `--from-description` → byte-identical to today.
- [x] T011 [US2] Relocate the description-prompt shaping helper so it survives `UserDescribedGenerator` removal (US4): extract the static prompt-building logic from `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs` into a standalone static helper (e.g. `Generation/DescriptionPromptBuilder.cs`) consumed by T010. Do not yet delete `UserDescribedGenerator` (US4).
- [x] T012 [US2] Rewrite the from-description section of `spectra-generate.md`: replace `spectra ai generate --from-description` with `compile-prompt --from-description …` → in-session generate → `ingest-tests`, then the mandatory `spectra-critic` step (the test now gets a real verdict, not `manual`). Preserve the surfaced result fields (id/title, suite, linked criteria, duplicate warnings, notes).

**Checkpoint**: Bulk + from-description both on the seam.

---

## Phase 5: User Story 3 - Behavior analysis runs on the seam (Priority: P2)

**Goal**: Analyze-first runs through new `compile-analysis-prompt` → in-session → `ingest-analysis` (deterministic recommendation); no in-process `--analyze-only` model call.

**Independent Test**: Run the skill's analyze step against a fixture suite; confirm the recommendation comes from the seam, the skill presents it and STOPs for approval, and no `spectra ai generate --analyze-only` model call occurs.

### Tests for User Story 3

- [x] T013 [P] [US3] Contract test for `compile-analysis-prompt`: emits the behavior-analysis prompt to stdout; refuse-to-emit exit 4 on missing `--suite`; deterministic; honors the doc-suite token-budget pre-flight (exit 4). Add in `tests/Spectra.CLI.Tests/Commands/CompileAnalysisPromptTests.cs`.
- [x] T014 [P] [US3] Contract test for `ingest-analysis`: well-formed behavior JSON → recommendation with `already_covered`, `recommended` (= max(0, total−covered)), `breakdown`, `technique_breakdown` (exit 0); empty → exit 5; missing/unparseable fields → exit 6. Add in `tests/Spectra.CLI.Tests/Commands/IngestAnalysisTests.cs`.
- [x] T015 [P] [US3] Unit test for the relocated deterministic accounting (`AnalysisRecommendationBuilder`): dedup via a `CoverageSnapshot`, breakdown/technique grouping, recommended-count math. Add in `tests/Spectra.CLI.Tests/Generation/AnalysisRecommendationBuilderTests.cs`.

### Implementation for User Story 3

- [x] T016 [P] [US3] Create `src/Spectra.CLI/Generation/AnalysisPromptCompiler.cs` (+ `AnalysisPromptCompileResult.cs`) by relocating the prompt-building half of `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs` (model-free; mirrors `PromptCompiler`/`ExtractionPromptCompiler`).
- [x] T017 [P] [US3] Create `src/Spectra.CLI/Generation/AnalysisRecommendation.cs` + `AnalysisRecommendationBuilder.cs` by relocating the deterministic accounting (`BehaviorAnalyzer` ~L158–172 + `BehaviorAnalysisResult` fields: total, already-covered via `CoverageSnapshot`/title-similarity, breakdown, technique-breakdown, recommended-count).
- [x] T018 [US3] Create `src/Spectra.CLI/Commands/Generate/CompileAnalysisPromptCommand.cs` (options `--suite/--doc-suite/--focus/--include-archived`; reuse the existing document-map + token-budget pre-flight; exit 0/4/1).
- [x] T019 [US3] Create `src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs` (`--suite`, `--from`/stdin; parse behaviors → `AnalysisRecommendationBuilder` → emit recommendation JSON; exit 0/5/6/1).
- [x] T020 [US3] Register both commands in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` next to `compile-prompt`/`ingest-tests`.
- [x] T021 [US3] Rewrite the analyze section of `spectra-generate.md`: Step "analyze" now runs `compile-analysis-prompt` → in-session behavior identification → `ingest-analysis`, presenting the same recommendation breakdown and STOPping for approval; preserve the analyze-fail guard (no auto-proceed). Remove `spectra ai generate --analyze-only`.

**Checkpoint**: All three flows on the seam (Phase A complete). The in-process generator is now unreferenced by the skill.

---

## Phase 6: User Story 4 - In-process generator, provider chain, and Copilot SDK removed (Priority: P1)

**Goal**: Delete the in-process generation path, provider chain, and SDK; retire `ai.providers`. **Depends on US1–US3 green.**

**Independent Test**: `dotnet build` clean with the symbols/package gone; a config without `ai.providers` validates; a legacy config with it validates with a notice.

### Tests for User Story 4

- [x] T022 [P] [US4] Update `tests/Spectra.Core.Tests/Config/ProviderRetirementTests.cs`: invert the `ai.providers` assertion — `DetectDeprecatedKeys` now CONTAINS `ai.providers`; config without providers validates; config with providers validates + flagged. (Per `contracts/config-providers-retirement.md`.)
- [x] T023 [P] [US4] Remove/rewrite tests of deleted surfaces: `AgentFactory` provider-selection, `CopilotService`, `ProviderMapping`, `ProviderChain`, `BehaviorAnalyzer` model-call, `UserDescribedGenerator` model-call, and `spectra ai generate --count/--from-description/--analyze-only` command tests. Enumerate via grep; delete dead tests, rewrite any asserting removed behavior. Do NOT touch Core/053/055 regression tests.

### Implementation for User Story 4 (remove call sites → then targets)

- [x] T024 [US4] Remove the model-calling execution methods from `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (batch/interactive generate loop, from-description model call, analyze model path) while PRESERVING the static deterministic helpers the seam uses (`LoadCriteriaContextAsync`, profile/template resolution). Remove its references to `AgentFactory`/`BehaviorAnalyzer`/agent types.
- [x] T025 [US4] Remove the `spectra ai generate` model flows: unregister `GenerateCommand` model paths from `AiCommand`/gut `GenerateCommand.cs` to the deterministic-only surface (or remove if nothing model-free remains). Confirm `compile-prompt` still resolves `GenerateHandler.LoadCriteriaContextAsync`.
- [x] T026 [US4] Delete the model-call half of `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs` and `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs` (their deterministic logic already relocated in T011/T016/T017); delete the files if fully unreferenced.
- [x] T027 [US4] Delete `AgentFactory.CreateAgentAsync` + hardcoded `github-models`/`gpt-4o` fallback in `src/Spectra.CLI/Agent/AgentFactory.cs` (and `AgentFactory` itself if now empty).
- [x] T028 [US4] Delete `src/Spectra.CLI/Agent/ProviderChain.cs`, `src/Spectra.CLI/Agent/Copilot/CopilotService.cs`, `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs` (CopilotGenerationAgent), and `src/Spectra.CLI/Agent/Copilot/ProviderMapping.cs`.
- [x] T029 [US4] Remove the `GitHub.Copilot.SDK` `PackageReference` from `src/Spectra.CLI/Spectra.CLI.csproj`; resolve any remaining `using` of the SDK.
- [x] T030 [US4] Retire `ai.providers` config: make `AiConfig.Providers` optional (default `[]`) in `src/Spectra.Core/Models/Config/AiConfig.cs`; remove the `MISSING_PROVIDERS` validation and add `ai.providers` to `DeprecatedKeyPaths` in `src/Spectra.Core/Config/ConfigLoader.cs`. Surface the notice in `spectra validate` (reuse the Spec 058 notes path).
- [x] T031 [US4] `dotnet build` clean; then `grep -r "CreateAgentAsync\|CopilotService\|ProviderMapping\|ProviderChain\|GitHub.Copilot.SDK" src/` returns nothing. Fix any stragglers.

**Checkpoint**: In-process generator/provider chain/SDK gone; `ai.providers` retired; build clean.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T032 [P] Docs (factually-wrong): update generation workflow / `spectra ai generate` reference and any `ai.providers` provider-config docs in `docs/configuration.md`, `docs/cli-reference.md`, `docs/getting-started.md` to the seam model.
- [x] T033 [P] Update the `CLAUDE.md` runtime line (currently "generator + ai.providers + SDK retained transitionally for Spec 059") to reflect full retirement; keep CLAUDE.md compact (<40K).
- [x] T034 [P] Add the Spec 059 entry to `CHANGELOG.md` under the Unreleased v2 section (generation now on the subscription seam; provider chain + SDK + `ai.providers` removed).
- [x] T035 Full sweep: `dotnet test` across all projects; confirm `Spectra.Core` + 053/055 corpora unchanged-green; run `specs/059-generation-skill-inversion/quickstart.md` validation steps.

---

## Dependencies & Execution Order

- **Setup (P1) → Foundational (P2)**: baseline + boundary check first.
- **US1 (P3 phase)**: bulk seam already exists; MVP — the headline cutover.
- **US2 (P4)**: extends `compile-prompt`; independent of US3.
- **US3 (P5)**: new command pair; independent of US2.
- **US4 (P6)**: **depends on US1+US2+US3** (skill fully on the seam) — only then are the removal targets unreferenced.
- **Polish (P7)**: after US4.

### Within-story rules

- Tests before/with implementation; verify red→green.
- US4: remove call sites (T024–T025) before deleting targets (T026–T029) so each delete compiles.
- `[P]` = different files, no ordering dependency.

## Parallel Opportunities

- T004/T005 (US1 tests) in parallel.
- T008/T009 (US2 tests) in parallel; T013/T014/T015 (US3 tests) in parallel.
- T016/T017 (US3 relocations, different files) in parallel; then T018/T019 sequential (depend on them) then T020.
- T022/T023 (US4 test edits) in parallel.
- T032/T033/T034 (docs) in parallel.

## Implementation Strategy

1. **Phase A first (US1→US2→US3)** — additive; the in-process command remains a safety net so the build stays green throughout.
2. **Gate**: all three flows proven on the seam (T021 checkpoint) before any removal.
3. **Phase B (US4)** — remove call sites then targets, building after each cluster (T031).
4. **Polish** — docs + full sweep.
