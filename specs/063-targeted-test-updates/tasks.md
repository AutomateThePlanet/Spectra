# Tasks: Targeted test updates (inverted update seam)

**Input**: Design documents from `/specs/063-targeted-test-updates/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the spec's "Tests" section explicitly requests net-new compiler/ingestor/boundary tests and a rewrite of stale assertions.

**Organization**: Tasks grouped by user story (US1–US4) for independent implementation and testing. The four seams already in the repo (generation 053, extraction 054, critic 055, analysis 059) are the templates being mirrored.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 maps to spec user stories

## Path Conventions

Single multi-project repo. CLI source under `src/Spectra.CLI/`, core under `src/Spectra.Core/`, tests under `tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green baseline and confirm the exact reuse anchors before mirroring.

- [X] T001 Confirm baseline: `dotnet build` green (1 warning, 0 errors). Tests baseline deferred to T026.
- [X] T002 [P] Template contract confirmed: templates resolve by id via `BuiltInTemplates.GetTemplate` (embedded `Prompts/Content/{id}.md`) with user override at `.spectra/prompts/{id}.md`. `test-update` is already registered in `AllTemplateIds` and a `test-update.md` exists but holds the OLD classify-and-propose design — must be rewritten to the inverted edit shape.
- [X] T003 [P] `TestCase` is an immutable class (`init` setters; no `with`) — copy-construct to force invariants. Field classification finalized: editable = Title/Steps/ExpectedResult/TestData/ScenarioFromDoc/Criteria/SourceRefs (round-tripped by the generation parse); protected (drift-guarded) = Priority/Component/Tags; preserved-from-original = Id/FilePath/Grounding (incl. Manual) + all non-round-tripped fields (DependsOn/Requirements/Environment/Custom/etc.).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Stand up the seam scaffolding all stories build on — template, command shells, registration. No story behavior yet.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Add the `test-update` prompt template (mirror `test-generation`) in the template engine's builtin location identified in T002, with placeholders for the original test, the changed source/criteria block, suite name, profile format, and explicit "edit, don't regenerate; preserve id/structure/manual fields" directives.
- [X] T005 [P] Create `CompileUpdatePromptCommand` shell in `src/Spectra.CLI/Commands/Generate/CompileUpdatePromptCommand.cs` — options `--suite`/`-s`, `--test-id`, `--output-format`; exit-code constants `0/1/4`; handler stub (mirror `CompilePromptCommand`).
- [X] T006 [P] Create `IngestUpdateCommand` shell in `src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs` — positional `suite`, options `--test-id`, `--from`, `--output-format`; exit-code constants `0/1/5/6`; handler stub (mirror `IngestTestsCommand`).
- [X] T007 Register both new commands in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` (add a "Spec 063: inverted update surface" block with `AddCommand(new CompileUpdatePromptCommand())` and `AddCommand(new IngestUpdateCommand())`).

**Checkpoint**: `spectra ai compile-update-prompt --help` and `spectra ai ingest-update --help` resolve; solution builds.

---

## Phase 3: User Story 1 - Doc-aware targeted edit of an outdated test (Priority: P1) 🎯 MVP

**Goal**: An OUTDATED test is edited in-session and persisted with its original id — compile → edit → ingest, end to end.

**Independent Test**: Take a classifier-OUTDATED test + changed source doc, run compile-update-prompt → edit → ingest-update; confirm the persisted test reflects the change, keeps its id, and was edited (not recreated).

### Tests for User Story 1 ⚠️ (write first, ensure they fail)

- [X] T008 [P] [US1] `UpdatePromptCompilerTests` in `tests/Spectra.CLI.Tests/Generation/UpdatePromptCompilerTests.cs` — asserts the compiled prompt contains the original test, the changed source/criteria, and edit-don't-regenerate directives; refuses (MissingRequired) when original test or changed context is absent.
- [X] T009 [P] [US1] `UpdatedTestIngestor` happy-path tests in `tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs` — `ParseAndValidate` reuses the generation pipeline; on a valid edited candidate the persisted test's id equals the **original** id even when the model JSON carries a different id; no new id allocated.
- [X] T010 [P] [US1] `IngestUpdateBoundaryTests` in `tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateBoundaryTests.cs` — end-to-end: valid edit → exit 0, file persisted, `_index.json` regenerated, id unchanged; UP_TO_DATE tests not passed to the seam are untouched on disk.

### Implementation for User Story 1

- [X] T011 [US1] Implement `UpdatePromptCompiler` in `src/Spectra.CLI/Generation/UpdatePromptCompiler.cs` (mirror `PromptCompiler`): pure assemble of original-test + changed-source/criteria + directives via the `test-update` template; `Compile`/`MissingRequired` validation for absent inputs.
- [X] T012 [US1] Implement `CompileUpdatePromptCommand.RunAsync` in `src/Spectra.CLI/Commands/Generate/CompileUpdatePromptCommand.cs`: validate suite+test-id (refuse exit 4), load config (exit 1), load original test by id (refuse exit 4 if absent), resolve changed source/criteria via the same loaders `CompilePromptCommand` uses, assemble via `UpdatePromptCompiler`, print to stdout (exit 0).
- [X] T013 [US1] Implement `UpdatedTestIngestor` core in `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`: reuse the generation parse/validate (`ExtractJson`/`TryParseArray`/`ParseTestCase`/`TestValidator.ValidateAll`) via a shared/duplicated pipeline; force `candidate.Id`/`FilePath` ← original; persist through `TestPersistenceService.PersistAsync` with `allForIndex` = existing suite set with the original replaced by the candidate (incoming-wins-by-id). Expose `ParseAndValidate` as a pure static for token-free tests.
- [X] T014 [US1] Implement `IngestUpdateCommand.RunAsync` in `src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs`: load config (exit 1), read content (`--from`/stdin), load original test by id (exit 1 if absent), call `UpdatedTestIngestor.IngestAsync`, emit JSON/human success (persisted id), map fail-loud codes to exit 5/6.

**Checkpoint**: US1 fully functional — an OUTDATED test round-trips through the seam with its id preserved. MVP deliverable.

---

## Phase 4: User Story 2 - Manual content is never lost to an update (Priority: P1)

**Goal**: A pre-existing `Manual` verdict and manual note(s) survive an update regardless of model output.

**Independent Test**: Update a test whose grounding verdict is `manual` (with a note); have the edit JSON omit/alter those fields; confirm the persisted test still carries the original manual verdict + note.

### Tests for User Story 2 ⚠️

- [X] T015 [P] [US2] Add manual-preservation tests to `tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs` — original `Grounding.Verdict == Manual` (+ note) is re-asserted onto the candidate even when the model output drops or changes it; a non-manual original is unaffected.

### Implementation for User Story 2

- [X] T016 [US2] Add manual-field re-assertion to `UpdatedTestIngestor` (`src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`): after parse/validate and id-forcing, if the original test's `Grounding.Verdict == Manual` (and/or human note present) copy that grounding/note from the original onto the candidate before the drift guard and persist. Use the field list confirmed in T003.

**Checkpoint**: US1 + US2 — edits preserve id AND human-authored content.

---

## Phase 5: User Story 3 - Out-of-scope drift is surfaced, not silently accepted (Priority: P2)

**Goal**: Any change to a protected/out-of-scope field fails loud (`DRIFT_DETECTED`); in-scope edits pass.

**Independent Test**: Put an out-of-scope change (e.g. flip `priority`) into the edited JSON → ingest exits 5 `DRIFT_DETECTED`, nothing persisted; an edit confined to editable fields persists normally.

### Tests for User Story 3 ⚠️

- [X] T017 [P] [US3] Add drift-guard tests to `tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs` — a change to a protected field (priority/component/tags/depends_on) yields `IngestResult.Failure(DRIFT_DETECTED)` with a `DriftEntry` naming the field + before/after, and persists nothing; a change confined to editable fields (title/steps/expected/test_data/criteria/source_refs) passes.
- [X] T018 [P] [US3] Add `DRIFT_DETECTED` exit-mapping test to `tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateBoundaryTests.cs` — drift → exit 5, JSON error payload carries `error_code: "DRIFT_DETECTED"` and the drift detail.

### Implementation for User Story 3

- [X] T019 [US3] Implement `DriftReport`/`DriftEntry` and `CompareForDrift(original, candidate)` (deterministic field-by-field, editable vs protected partition from T003; err toward protected for ambiguous fields) — co-located with `UpdatedTestIngestor` in `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`.
- [X] T020 [US3] Wire the drift guard into `UpdatedTestIngestor.IngestAsync`: after manual re-assertion, run `CompareForDrift`; non-empty → `IngestResult.Failure(DRIFT_DETECTED, entries…)` before persist. Add the `DRIFT_DETECTED` code to the update ingestor's error set and map it to exit 5 in `IngestUpdateCommand`.

**Checkpoint**: US1–US3 — edits preserve id + manual fields and refuse silent collateral drift.

---

## Phase 6: User Story 4 - Invalid edits fail loudly and retry (Priority: P2)

**Goal**: Invalid edits are rejected with a specific error, fed back into a bounded retry, and never overwrite the original; the skill drives the loop.

**Independent Test**: Feed malformed JSON → exit 5/6 with a specific error, original untouched; the skill retries ≤2× then stops.

### Tests for User Story 4 ⚠️

- [X] T021 [P] [US4] Add bounded-error tests to `tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateBoundaryTests.cs` — malformed/empty/truncated/schema-invalid edits each produce the specific error code + exit (5/6) and persist nothing (original byte-for-byte unchanged).
- [X] T022 [US4] Rewrite stale assertions in `tests/Spectra.CLI.Tests/Commands/UpdateCommandTests.cs` that pin the "no AI / heuristic rewrite" framing, so targeted rewrite is documented as routing through the seam (the `ai update` classifier command itself stays unchanged — only stale "rewrites" assertions are corrected). DO NOT touch `tests/Spectra.Core.Tests/Update/TestClassifierTests.cs` or `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs`.

### Implementation for User Story 4

- [X] T023 [US4] Rewrite `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` to drive the loop: obtain OUTDATED set via the classifier (`ai update --diff`), per test `compile-update-prompt` → edit in-session → write candidate JSON → `ingest-update`, with bounded fail-loud retry (max 2 attempts) feeding the specific error/`DRIFT_DETECTED` back. Remove the false "rewrites affected test cases" sentences at lines 9-12 and the "rewritten" framing at lines 31, 64 (FR-006/FR-009).

**Checkpoint**: Full seam closed end-to-end and driven by the skill.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T024 [P] Update user-facing docs: the update workflow page and getting-started update snippets to describe the inverted edit-in-session flow (remove any heuristic-only/"rewrites" framing); add a command reference for `compile-update-prompt` / `ingest-update`.
- [X] T025 [P] Update `CLAUDE.md` Recent Changes + `CHANGELOG.md` with the 063 entry; bump version per project convention if releasing.
- [X] T026 Run `dotnet test` full suite; confirm net-new tests green and the regression net (TestClassifier/TestPersistenceService tests) is unchanged and green vs. the T001 baseline.
- [X] T027 Run `spectra validate` and the quickstart.md walkthrough end-to-end on a sample suite (id preserved, manual preserved, drift refused, index parity).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; BLOCKS all user stories.
- **US1 (Phase 3)**: depends on Foundational. The MVP.
- **US2 (Phase 4)**: depends on US1 (extends `UpdatedTestIngestor`).
- **US3 (Phase 5)**: depends on US1 (extends `UpdatedTestIngestor` + `IngestUpdateCommand`); independent of US2.
- **US4 (Phase 6)**: depends on US1 (skill drives the seam; error surfacing exists from US1/US3).
- **Polish (Phase 7)**: depends on all desired stories.

### Story independence note

US1 is the standalone MVP. US2 and US3 each add one guarantee on top of US1 and are independently testable. Because US2/US3/US4 all extend the **same** ingestor/command files authored in US1, they are sequenced by priority (not parallel across stories) to avoid same-file conflicts — this matches the "within each story" ordering rule.

### Within a story

Tests (marked [P], different files) before implementation; compiler/ingestor before the command that calls them.

### Parallel Opportunities

- Setup: T002, T003 in parallel.
- Foundational: T005, T006 in parallel (different command files) before T007 registers both.
- US1 tests T008/T009/T010 in parallel (different test files) before T011–T014.
- Each story's [P] test tasks run in parallel; implementation within a story is sequential on the shared files.

---

## Parallel Example: User Story 1

```bash
# Tests first (different files):
Task: "UpdatePromptCompilerTests in tests/Spectra.CLI.Tests/Generation/UpdatePromptCompilerTests.cs"
Task: "UpdatedTestIngestor happy-path tests in tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs"
Task: "IngestUpdateBoundaryTests in tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateBoundaryTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE** (OUTDATED test round-trips with id preserved) → deliver MVP.

### Incremental Delivery

US1 (edit + id) → US2 (manual preserved) → US3 (drift guard) → US4 (bounded retry + skill) → Polish. Each adds a guarantee without breaking the prior.

---

## Notes

- Regression net — **DO NOT TOUCH**: `tests/Spectra.Core.Tests/Update/TestClassifierTests.cs`, `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs`, and `Spectra.Core` / `TestClassifier` / `TestPersistenceService` sources. Breakage there is a regression in a reused component — investigate, don't edit.
- Persistence flows ONLY through `TestPersistenceService` (FR-002/FR-008); the ingestor never writes files directly.
- Id is taken from the original test — no `PersistentTestIdAllocator` call on the update path.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
