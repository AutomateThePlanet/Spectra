---
description: "Task list for Spec 053 — Prompt-compiler + generation handoff inversion"
---

# Tasks: Prompt-compiler + generation handoff inversion

**Input**: Design documents from `/specs/053-prompt-compiler/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — the spec explicitly requests token-free unit tests for the compiler, fail-loud boundary, and choreography contract.

**Hard regression rule**: Do NOT modify any `Spectra.Core` test or `TestPersistenceService` test. If one breaks, STOP and investigate a regression.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Create `src/Spectra.CLI/Generation/` namespace folder and `tests/Spectra.CLI.Tests/Generation/` test folder.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Result types every command/story depends on.

- [x] T002 [P] Add `PromptCompileResult` record in `src/Spectra.CLI/Generation/PromptCompileResult.cs` (`Success(prompt)` / `MissingRequired(input, message)`), per data-model.md.
- [x] T003 [P] Add `IngestResult` record + `error_code` constants in `src/Spectra.CLI/Generation/IngestResult.cs` (`EMPTY_CONTENT`/`MALFORMED_JSON`/`TRUNCATED`/`NO_TESTS`/`SCHEMA_INVALID`), per data-model.md.

**Checkpoint**: result types compile; user stories can begin.

---

## Phase 3: User Story 1 + 2 — Deterministic prompt-compiler & refuse-to-emit (Priority: P1) 🎯 MVP

**Goal**: A standalone, model-free `PromptCompiler` that emits a grounded prompt and refuses to emit on missing required input. (US1 + US2 share one class.)

**Independent Test**: `dotnet test` the compiler tests — determinism + refuse-to-emit, no model/network.

### Tests for US1/US2 ⚠️ (write first)

- [x] T004 [P] [US1] `tests/Spectra.CLI.Tests/Generation/PromptCompilerTests.cs`: identical inputs ⇒ byte-identical prompt; prompt contains criteria + count + behaviors + Testimize block; no I/O.
- [x] T005 [P] [US2] In the same file: null/whitespace criteria ⇒ `MissingRequired("criteria_context", …)`; count ≤ 0 ⇒ `MissingRequired("count", …)`; whitespace user prompt ⇒ `MissingRequired("user_prompt", …)`; failure carries the input name and emits no prompt.

### Implementation for US1/US2

- [x] T006 [US1] Create `src/Spectra.CLI/Generation/PromptCompiler.cs`: move the body of `GenerationAgent.BuildFullPrompt` here as `Compile(...)` returning `PromptCompileResult`; preserve template + inline-fallback behavior verbatim; add the FR-004 required-input guards (D-3) BEFORE assembling the prompt.
- [x] T007 [US1] Edit `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs`: `BuildFullPrompt` delegates to `PromptCompiler.Compile(...)` and returns `result.Prompt` (single source of truth). Keep the exact existing signature so current callers/tests are unchanged.

**Checkpoint**: compiler unit-tested, existing GenerationAgent tests still green.

---

## Phase 4: User Story 3 — Fail-loud validation boundary (Priority: P1)

**Goal**: `GeneratedTestIngestor` parses agent content, validates fail-loud (no salvage), and persists valid batches via `TestPersistenceService`.

**Independent Test**: `dotnet test` the ingestor tests — malformed/truncated/schema-invalid ⇒ specific error + zero persistence; valid ⇒ persisted + index regenerated.

### Tests for US3 ⚠️ (write first)

- [x] T008 [P] [US3] `tests/Spectra.CLI.Tests/Generation/GeneratedTestIngestorTests.cs`: empty ⇒ `EMPTY_CONTENT`; non-JSON ⇒ `MALFORMED_JSON`; `[`-opened-never-closed ⇒ `TRUNCATED` (asserts NO salvage); array of invalid tests ⇒ `SCHEMA_INVALID` with echoed `ValidationError` codes; each failure persists nothing (assert target dir + `_index.json` unchanged via temp dir).
- [x] T009 [P] [US3] In the same file: a valid JSON array ⇒ `IsSuccess`, files written, `_index.json` regenerated through `TestPersistenceService`; batch atomicity — one invalid test in an otherwise-valid array ⇒ whole batch fails, nothing written.

### Implementation for US3

- [x] T010 [US3] Create `src/Spectra.CLI/Generation/GeneratedTestIngestor.cs`: relocate `ExtractJson`/`TryParseJsonArray`/`ParseTestCase` logic; **omit** `TryRepairTruncatedArray` (D-4); distinguish `TRUNCATED` (opened `[`, depth never returns to close) from `MALFORMED_JSON`; run `TestValidator.ValidateAll`; on full success call `TestPersistenceService.PersistAsync`. Inject the persistence service + a function to load existing tests for the suite index.

**Checkpoint**: boundary unit-tested; zero-persistence invariant proven.

---

## Phase 5: CLI surface (exposes US1–US3 to the skill)

- [x] T011 [P] Create `src/Spectra.CLI/Commands/Generate/CompilePromptCommand.cs` per `contracts/compile-prompt.md`: resolve criteria/profile/testimize like the handler, call `PromptCompiler`, print to stdout on success (exit 0), report missing input to stderr/`missing_input` (exit 4).
- [x] T012 [P] Create `src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs` per `contracts/ingest-tests.md`: read `--from`/stdin, call `GeneratedTestIngestor`, persist on success (exit 0), emit `error_code` + messages on failure (exit 5/6).
- [x] T013 Edit `src/Spectra.CLI/Commands/Ai/AiCommand.cs`: register `CompilePromptCommand` and `IngestTestsCommand`.

---

## Phase 6: User Story 4 — Bounded choreography retry contract (Priority: P2)

**Goal**: Prove the C# side of the retry contract — the ingestor returns an error specific enough for a skill to re-prompt against, and the loop/limit is the skill's job (not C#).

- [x] T014 [P] [US4] `tests/Spectra.CLI.Tests/Generation/ChoreographyContractTests.cs`: assert the `IngestResult.Failure` for each `error_code` exposes a non-empty, specific `errors[]` referencing the offending test/field — i.e. the exact payload a skill feeds back to the agent. (No C# retry loop exists by design; this test pins the contract Spec 055's skill depends on.)

---

## Phase 7: FR-001 completion (call-site removal) — FLAGGED, do last & carefully

> See plan.md "Scoping decision". This task removes the literal model call; it touches the protected handler paths, so it is isolated, last, and gated on the full corpus staying green.

- [ ] T015 [US?] Remove `session.SendAndWaitAsync` (`GenerationAgent.cs:239`) and the generation session/provider construction from the generation flow; rewire `GenerateHandler` entry paths to the compile→ingest seam. **Rewrite** (do not silently fix) the CLI tests that exercised the old `GenerateTestsAsync` model call. STOP if any `Spectra.Core`/persistence test breaks. *(May be deferred to the Spec 055 skill-wiring increment — record the decision in the PR.)*

---

## Phase 8: Polish & Docs

- [x] T016 [P] Update the generation workflow doc + `ARCHITECTURE-v2` handoff references (Documentation Impact in spec).
- [x] T017 Run `quickstart.md` validation: `dotnet test tests/Spectra.CLI.Tests` + `dotnet test tests/Spectra.Core.Tests` (regression net green & unchanged).

---

## Dependencies & Execution Order

- T001 → T002/T003 (Foundational) → stories.
- US1/US2 (T004–T007): compiler. US3 (T008–T010): ingestor. Independent of each other; both depend only on Phase 2.
- CLI (T011–T013) depends on T006 (compiler) + T010 (ingestor).
- US4 (T014) depends on T010 (ingestor error contract).
- T015 (FR-001 deletion) depends on T011–T013 being in place (the replacement path must exist first). Last, flagged.
- Polish (T016–T017) last.

### Parallel opportunities

- T002 ∥ T003 (different files).
- T004/T005 ∥ T008/T009 (compiler tests vs ingestor tests, different files).
- T011 ∥ T012 (different command files).

## Implementation Strategy

**MVP = Phases 1–4** (compiler + ingestor, fully tested) + Phase 5 (CLI surface). That delivers the model-free compile→generate→ingest seam the skill (Spec 055) will call. Phase 7 (literal `:239` deletion + handler rewire) is the flagged remainder — deliver the green increment first, then complete or hand to 055.
