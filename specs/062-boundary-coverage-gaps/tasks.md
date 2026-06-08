---
description: "Task list for Boundary-coverage gap detection (analysis phase)"
---

# Tasks: Boundary-coverage gap detection (analysis phase)

**Input**: Design documents from `/specs/062-boundary-coverage-gaps/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the spec's "Tests" section explicitly enumerates net-new tests (boundary-gap detection, fail-loud, critic-unchanged guard, advisory). They are authored per user story.

**Organization**: Tasks grouped by the three user stories (US1 P1, US2 P2, US3 P3) from spec.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3
- All paths are relative to repo root `C:/SourceCode/Spectra/`

## Path Conventions

Single-solution .NET CLI: production in `src/Spectra.CLI/`, tests in `tests/Spectra.CLI.Tests/`. `src/Spectra.Core/` and the critic trio are **off-limits** (regression net).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the baseline builds and the regression net is green before touching the seam.

- [x] T001 Build the solution and run the existing analysis + critic test suites to capture a green baseline: `dotnet build` then `dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Generation|FullyQualifiedName~Verification"`. Record pass counts so SC-005 (critic/Core unchanged) can be confirmed at the end.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared typed model that all three stories reference. MUST complete before any story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 Create the `BoundaryGap` agent-output record in `src/Spectra.CLI/Agent/Analysis/BoundaryGap.cs` with `[JsonPropertyName]`-bound `Field` (`field`), `Kind` (`kind`), `Description` (`description`), and `Source` (`source`, defaulted to `""`). Mirror the style of `IdentifiedBehavior.cs` (sealed record, XML docs noting `kind` is free-form with expected vocabulary min-max/off-by-one/empty-null/overflow/timeout/max-length). `Field`/`Kind`/`Description` are `required`-shaped for validity but bind tolerantly so the builder (not deserialization) produces the fail-loud message.
- [x] T003 Add the additive `BoundaryGaps` field to `src/Spectra.CLI/Generation/AnalysisRecommendation.cs`: `public IReadOnlyList<BoundaryGap> BoundaryGaps { get; private init; } = [];`. Extend the `Recommendation(...)` factory with a `boundaryGaps` parameter (default `null` → `[]`), wired only on the success path; `Empty(...)`/`ParseFail(...)` leave it `[]`. Do not change any existing field, `RecommendedCount`, or breakdown semantics.

**Checkpoint**: Typed surface exists; stories can build on it.

---

## Phase 3: User Story 1 - Surface missing boundary cases before they are generated (Priority: P1) 🎯 MVP

**Goal**: Analysis output identifies boundary-coverage gaps implied by docs/criteria and not covered by planned/existing tests; emits nothing when no boundary is implied.

**Independent Test**: Feed `ingest-analysis` an agent JSON whose `boundary_gaps` lists an uncovered boundary → it surfaces in the recommendation/output; feed one with no `boundary_gaps` → none reported.

### Tests for User Story 1

- [x] T004 [P] [US1] Create `tests/Spectra.CLI.Tests/Generation/BoundaryGapParsingTests.cs`: assert `AnalysisRecommendationBuilder.Build` with a well-formed top-level `boundary_gaps` array carries each gap into `AnalysisRecommendation.BoundaryGaps` (field/kind/description/source preserved); with no `boundary_gaps` key → `BoundaryGaps` empty and `Outcome == Recommendation`; with `boundary_gaps: []` → empty. (FR-002, FR-004 no-condition case.)
- [x] T005 [P] [US1] Add an ingest-level test in `tests/Spectra.CLI.Tests/Generation/IngestAnalysisBoundaryGapTests.cs` (new) asserting the JSON success output includes a `boundary_gaps` array with the parsed gaps, and that it is `[]` (present) when none were emitted. Use the existing ingest-command test harness pattern (temp workspace + `spectra.config.json`).

### Implementation for User Story 1

- [x] T006 [US1] In `src/Spectra.CLI/Generation/AnalysisRecommendationBuilder.cs`, add a `ParseBoundaryGaps(string agentContent)` helper that locates the top-level JSON object, reads the optional `boundary_gaps` property, and deserializes the **happy path** (absent → empty list; well-formed array of objects → `List<BoundaryGap>`). Reuse `ExtractJson`. Attach the result via the extended `Recommendation(...)` factory in `Build`. Boundary gaps MUST NOT affect `TotalBehaviors`, `AlreadyCovered`, `Breakdown`, or `TechniqueBreakdown` (FR-005).
- [x] T007 [US1] Extend `behavior-analysis.md` (`src/Spectra.CLI/Prompts/Content/behavior-analysis.md`): under/after Technique 2 (BVA) and in OUTPUT INSTRUCTIONS, instruct the analyst to emit a top-level `boundary_gaps` array listing boundary conditions (min/max, off-by-one, empty/null, overflow, timeout) **implied by the docs/criteria but not covered** by existing tests (`coverage_context`) or the emitted behaviors — and to return `"boundary_gaps": []` when none are implied (conservative, FR-004). Update the example JSON to show the `boundary_gaps` shape `{field, kind, description, source}`. Frame as extending BVA, not a 7th technique (no new `technique` code).
- [x] T008 [US1] In `src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs`, add `boundary_gaps` to the JSON success object (array of `{field, kind, description, source}`, always present, possibly `[]`) and a human-output `Boundary gaps:` section printed only when non-empty (`{kind} · {field} — {description}  [{source}]`). No exit-code change.
- [x] T009 [US1] Update `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`: add a sentence to the ISTQB/recommendation paragraph telling the agent to surface `boundary_gaps` alongside the category and technique breakdowns when presenting the analyze recommendation, framed as advisory (FR-007).

**Checkpoint**: US1 fully functional — gaps are detected, carried, emitted, and presented; no-condition case is clean.

---

## Phase 4: User Story 2 - Boundary gaps are carried through the typed result, fail-loud (Priority: P2)

**Goal**: A malformed `boundary_gaps` payload fails ingest loud with a specific, attributable error (exit 6); a missing section never fails; well-formed survives end-to-end.

**Independent Test**: Ingest a payload with `boundary_gaps` as a non-array, and one with an element missing `kind` → both fail exit 6 with a `boundary_gaps...` error; legacy payload (no key) → success.

### Tests for User Story 2

- [x] T010 [P] [US2] Extend `tests/Spectra.CLI.Tests/Generation/BoundaryGapParsingTests.cs` (or add cases) asserting: `boundary_gaps` present but a JSON object/scalar (not array) → `Outcome == ParseFailure` with error containing `"boundary_gaps must be a JSON array"`; an element with blank/missing `field`/`kind`/`description` → `ParseFailure` naming the index and missing field; behaviors still parse fine. (FR-003.)
- [x] T011 [P] [US2] Add to `tests/Spectra.CLI.Tests/Generation/IngestAnalysisBoundaryGapTests.cs`: malformed boundary-gap payload → exit code 6 and stderr carries the boundary-gap-specific error (JSON `errors[]` and human form); legacy payload (no `boundary_gaps`) → exit 0 with `boundary_gaps: []`. (FR-003 fail-loud + backward-compatible.)

### Implementation for User Story 2

- [x] T012 [US2] In `AnalysisRecommendationBuilder.ParseBoundaryGaps` (from T006), add **strict** validation: if `boundary_gaps` exists and is not a JSON array → return a signal that `Build` converts to `AnalysisRecommendation.ParseFail("boundary_gaps must be a JSON array.")`; if any element is not an object or has blank `field`/`kind`/`description` → `ParseFail($"boundary_gaps[{i}] is missing required field '{name}'.")`. Wire `Build` so a boundary-gap parse failure short-circuits to the `ParseFailure` outcome (carrying the specific message) **before** returning a success recommendation. Behaviors parse stays tolerant; only boundary gaps are strict.
- [x] T013 [US2] Confirm `IngestAnalysisCommand` already routes `ParseFailure` → exit 6 and prints `Errors` (it does, lines ~157-173); add no new exit code. If needed, ensure the boundary-gap error text flows through unchanged (no truncation).

**Checkpoint**: US1 + US2 both work; malformed input is loud, legacy input is silent-safe.

---

## Phase 5: User Story 3 - Advisory only; the grounding critic stays untouched (Priority: P3)

**Goal**: Boundary gaps never mutate counts or block generation; the critic remains grounding-only.

**Independent Test**: Same behaviors with vs. without `boundary_gaps` → identical `recommended`/`breakdown`/`technique_breakdown` and identical exit; critic verdict vocabulary unchanged.

### Tests for User Story 3

- [x] T014 [P] [US3] Add an advisory-invariance test (in `BoundaryGapParsingTests.cs`): build two recommendations from identical `behaviors`, one with a populated `boundary_gaps` and one without — assert `RecommendedCount`, `Breakdown`, `TechniqueBreakdown`, `AlreadyCovered`, `DocumentsAnalyzed`, and `Outcome` are equal across both (FR-005 non-mutating). Exit/blocking is covered by the ingest success path (exit 0 regardless of gap count).
- [x] T015 [P] [US3] Add a critic-unchanged guard test in `tests/Spectra.CLI.Tests/Verification/CriticUnchangedGuardTests.cs` (new): assert the `VerificationVerdict` enum values are exactly `{Grounded, Partial, Hallucinated, Manual}` and that `VerdictIngestor` classifies a `"boundary"`/`"completeness"` verdict string as a fail-loud/invalid outcome (not a new accepted verdict). This fails iff seam (a) was wrongly touched. (FR-001, SC-005.)

### Implementation for User Story 3

- [x] T016 [US3] No production change expected (advisory non-mutation is structural from T006/T012). If T014 reveals any count drift, fix it in `AnalysisRecommendationBuilder` so boundary-gap parsing is side-effect-free on the accounting. Verify `spectra-critic.agent.md`, `CriticPromptBuilder.cs`, `VerdictIngestor.cs`, and all of `src/Spectra.Core/` have **zero** diffs (`git diff --stat` over those paths must be empty).

**Checkpoint**: All three stories independently functional; separation of grounding vs. completeness proven by green critic tests.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T017 [P] Update the grounding-verification / generation-analysis workflow docs to clarify the split: grounding (critic — "does each claim trace to docs") vs. boundary/edge completeness (analysis phase — "are the edges that should be tested covered"). Locate via `Grep "grounding"` under `docs/` and `src/Spectra.CLI/Skills/Content/`; edit the doc(s) that describe the verification/generation workflow.
- [x] T018 [P] Add a CHANGELOG entry and bump notes for spec 062 (boundary-coverage gap detection) consistent with the repo's changelog style.
- [x] T019 Run `dotnet build` then the full `dotnet test` suite; confirm all new tests pass and the critic + `Spectra.Core` corpora are unchanged and green (SC-005). Capture counts against the T001 baseline.
- [x] T020 Run the `quickstart.md` scenario table manually (or via the ingest tests) to confirm each row's expected behavior; fix any mismatch.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — start immediately.
- **Foundational (Phase 2)**: after Setup. BLOCKS all stories (T002, T003 are the shared types).
- **US1 (Phase 3)**: after Foundational. MVP.
- **US2 (Phase 4)**: after Foundational; builds on US1's `ParseBoundaryGaps` (T006) — T012 extends it. Sequential after US1 (same file).
- **US3 (Phase 5)**: after Foundational; T014 depends on T006, T015 is independent (critic guard, [P]).
- **Polish (Phase 6)**: after all stories.

### Within/Across Stories

- T002 → T003 (record before field).
- T006 (US1 happy-path parse) → T012 (US2 strict validation) — **same file**, so not parallel; US1 before US2.
- T015 (critic guard) is fully independent and may run any time after Setup.
- Tests T004/T005 (US1), T010/T011 (US2), T014/T015 (US3) are written alongside their story.

### Parallel Opportunities

- T004 ∥ T005 (different test files) within US1.
- T007 ∥ T009 (prompt file vs. agent-doc file) — different files, both after T002/T003; can run alongside T006/T008 if staffed.
- T010 ∥ T011 within US2.
- T014 ∥ T015 within US3 (different files).
- T017 ∥ T018 in Polish.

---

## Parallel Example: User Story 1

```bash
# Tests (different files):
Task: "BoundaryGapParsingTests.cs — detection + no-condition"
Task: "IngestAnalysisBoundaryGapTests.cs — ingest emits boundary_gaps"

# Implementation that doesn't collide:
Task: "behavior-analysis.md prompt section (T007)"
Task: "spectra-generation.agent.md presentation (T009)"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → green baseline.
2. Phase 2 Foundational (T002, T003).
3. Phase 3 US1 — detection, parse, emit, present.
4. **STOP & VALIDATE**: gaps surface for an implied boundary; none for no boundary.

### Incremental Delivery

1. Foundation ready.
2. US1 → MVP (boundary gaps visible).
3. US2 → fail-loud hardening (malformed loud, legacy safe).
4. US3 → advisory + critic-unchanged guards.

---

## Notes

- The behaviors parse stays **tolerant** (truncation recovery); boundary-gap parse is **strict** (fail-loud) — this asymmetry is intentional (research.md Decision 2).
- `kind` is a free-form string (like `technique`), not an enum — avoids rejecting valid-but-novel kinds (research.md Decision 3).
- Zero changes to `Spectra.Core` and the critic trio is a hard requirement; T016 verifies it with `git diff --stat`.
- Commit after each task or logical group.
