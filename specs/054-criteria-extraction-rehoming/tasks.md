---
description: "Task list for 054-criteria-extraction-rehoming"
---

# Tasks: Criteria Extraction Re-homing + Extractor Unification

**Input**: Design documents from `/specs/054-criteria-extraction-rehoming/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the spec explicitly enumerates Rewrite + Net-new test tasks.

**Scope**: *Additive surface (match 053)* — add the model-free compile/ingest surface and unify
`RequirementsExtractor`'s failure semantics; **keep** the existing in-process model calls so
`ai analyze --extract-criteria` and `docs index` stay green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 from spec.md

## Path Conventions
Single project: `src/Spectra.CLI/`, `tests/Spectra.CLI.Tests/` at repo root.

---

## Phase 1: Setup

- [X] T001 Create `src/Spectra.CLI/Extraction/` folder (mirrors `src/Spectra.CLI/Generation/`) and `tests/Spectra.CLI.Tests/Extraction/` folder; confirm `dotnet build` is green before changes.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Shared typed-contract values that the stories below depend on. No behavior yet.

- [X] T002 [P] Create `ExtractionPromptCompileResult` record in `src/Spectra.CLI/Extraction/ExtractionPromptCompileResult.cs` — `IsSuccess`/`Prompt`/`MissingInput`/`Message` with `Success(prompt)` and `MissingRequired(input, message)` factories (mirror `Generation/PromptCompileResult.cs`).
- [X] T003 [P] Create `RequirementsExtractionResult` record in `src/Spectra.CLI/Agent/Copilot/RequirementsExtractionResult.cs` — `(ExtractionOutcome Outcome, IReadOnlyList<RequirementDefinition> Requirements)` with `IsCacheable => Outcome == ExtractionOutcome.Extracted`. **Reuse** the existing `ExtractionOutcome` enum (do not redefine it).

**Checkpoint**: Types compile; nothing wired yet.

---

## Phase 3: User Story 1 — Extract criteria without a model call inside the CLI (Priority: P1) 🎯 MVP

**Goal**: Deterministic, model-free compile of an extraction prompt + a model-free ingest boundary
that persists genuine `Extracted` results through the existing writer/index path.

**Independent Test**: `compile-extraction-prompt` emits a byte-identical prompt for identical input
with nothing written to disk; feeding a well-formed agent response to `ingest-criteria` classifies
`Extracted` and writes the `.criteria.yaml` + index, with no model/provider configured.

### Implementation

- [X] T004 [US1] Add `ExtractionPromptCompiler` static class in `src/Spectra.CLI/Extraction/ExtractionPromptCompiler.cs`: relocate the body of `CriteriaExtractor.BuildExtractionPrompt` into a lenient `Assemble(docPath, content, component, templateLoader?)` and a validated `Compile(docPath, content, component, templateLoader?) → ExtractionPromptCompileResult` that refuses (`MissingRequired`) when `docPath` or `content` is null/whitespace. Output must stay byte-identical to today's prompt (FR-002).
- [X] T005 [US1] Modify `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs`: make `BuildExtractionPrompt` a thin delegate to `ExtractionPromptCompiler.Assemble` (single source of prompt truth). Keep `ExtractFromDocumentAsync` and its in-process model call unchanged (additive scope).
- [X] T006 [US1] Create `CriteriaIngestor` + `CriteriaIngestResult` in `src/Spectra.CLI/Extraction/CriteriaIngestor.cs`: pure `Classify(content, source, component)` delegating to the reused-verbatim `CriteriaExtractor.ClassifyResponse`, and `IngestAsync(content, currentDir, docPath, component, dryRun, ct)` that on `Extracted` assigns/reuses IDs and persists via `CriteriaFileWriter` + criteria-index upsert (the exact path in `AnalyzeHandler.cs:588–668`). Persist **iff** `IsCacheable` (FR-006).
- [X] T007 [US1] Add `CompileExtractionPromptCommand` (`spectra ai compile-extraction-prompt`) in `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs` per `contracts/compile-extraction-prompt.md`: `--doc`/`--component`, emit prompt to stdout on success, exit `4` refuse, exit `1` env error (mirror `CompilePromptCommand`).
- [X] T008 [US1] Add `IngestCriteriaCommand` (`spectra ai ingest-criteria`) in `src/Spectra.CLI/Commands/Generate/IngestCriteriaCommand.cs` per `contracts/ingest-criteria.md`: `--doc`/`--component`/`--from`/`--dry-run`, read stdin or `--from`, call `CriteriaIngestor.IngestAsync`, exit `0` on `Extracted` (happy path here; failure exits wired in US3).
- [X] T009 [US1] Register both subcommands on the `ai` command group in `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` (beside `CompilePromptCommand`/`IngestTestsCommand`).

### Tests

- [X] T010 [P] [US1] `tests/Spectra.CLI.Tests/Extraction/ExtractionPromptCompilerTests.cs`: identical inputs → byte-identical `Assemble` output; `Compile` returns `Success` with a prompt containing the document content/component; no model/provider used.
- [X] T011 [P] [US1] `tests/Spectra.CLI.Tests/Extraction/CriteriaIngestorTests.cs` (happy path): a well-formed JSON response classifies `Extracted` and `IngestAsync` (dry-run=false against a temp dir) writes the `.criteria.yaml` + index entry; dry-run writes nothing.

**Checkpoint**: US1 is a working, model-free compile→ingest MVP. Existing commands untouched.

---

## Phase 4: User Story 2 — Empty/whitespace source short-circuits with no model turn (Priority: P1)

**Goal**: Empty/whitespace source returns `Extracted, []` with no prompt compilation and no handoff.

**Independent Test**: Empty + whitespace-only content → `Extracted` with zero criteria, no prompt
compiled, no handoff requested.

### Implementation

- [X] T012 [US2] In `CompileExtractionPromptCommand` (`CompileExtractionPromptCommand.cs`), short-circuit before compiling: when document content is empty/whitespace, print the empty-source notice (`{"short_circuit": true, "outcome": "Extracted"}` in json mode), emit **no** prompt, exit `0` (FR-003). Confirm `CriteriaExtractor.cs:64` short-circuit on the in-process path remains intact (do not modify it).

### Tests

- [X] T013 [P] [US2] `tests/Spectra.CLI.Tests/Extraction/ExtractionPromptCompilerTests.cs` (add cases): empty and whitespace-only content → the command/compiler signals short-circuit and emits no prompt; assert `Compile` is not invoked to produce a prompt for empty content.

**Checkpoint**: Empty-source path proven to incur no model turn.

---

## Phase 5: User Story 3 — Malformed agent responses become typed failures, never exceptions (Priority: P1)

**Goal**: `ingest-criteria` classifies bad content into `EmptyResponse`/`ParseFailure`, never throws,
persists nothing, returns typed exit codes; cache never poisoned.

**Independent Test**: Malformed → `ParseFailure` (exit 6); empty → `EmptyResponse` (exit 5); neither
writes anything to the index.

### Implementation

- [X] T014 [US3] In `IngestCriteriaCommand.cs`, wire the failure branches: `EmptyResponse` → fail-loud exit `5`, `ParseFailure` → fail-loud exit `6`, emit machine-readable payload (`success:false, outcome, errors`); persist nothing on either (FR-003, FR-006). Mirror `IngestTestsCommand`'s 5/6 split.
- [X] T015 [US3] In `CriteriaIngestor.IngestAsync` (`CriteriaIngestor.cs`), assert the cache gate: early-return without any write when `!result.IsCacheable`, leaving `_criteria_index.yaml`/`DocHash` byte-for-byte unchanged.

### Tests

- [X] T016 [P] [US3] `tests/Spectra.CLI.Tests/Extraction/CriteriaIngestorTests.cs` (add cases): malformed content → `ParseFailure`, never throws, nothing persisted; empty/whitespace content → `EmptyResponse`, nothing persisted; index unchanged after both (no cache poisoning).
- [X] T017 [P] [US3] `tests/Spectra.CLI.Tests/Commands/IngestCriteriaCommandTests.cs`: exit-code contract — `Extracted`→0, `EmptyResponse`→5, `ParseFailure`→6, env error→1.

**Checkpoint**: Fail-loud boundary + cache-poisoning guard proven, token-free.

---

## Phase 6: User Story 4 — `docs index` on the unified typed contract (Priority: P2)

**Goal**: `RequirementsExtractor` returns a typed outcome and no longer throws on empty/timeout;
`docs index` consumes it; corpus run never aborts on one bad doc.

**Independent Test**: Empty input and simulated timeout → typed outcome (no exception); a mixed
corpus completes, good docs cached, bad docs in `failed_documents`.

### Implementation

- [X] T018 [US4] Modify `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs`: change `ExtractFromDocumentAsync` to return `RequirementsExtractionResult`; empty source → `(Extracted, [])`; empty response → `(EmptyResponse, [])` (was `throw InvalidOperationException`); remove the internal 2-min `Task.WhenAny` **timeout throw** (loop deadline owns timeout); parse→`(Extracted, requirements)` / unparseable→`(ParseFailure, [])`. Remove `#pragma warning disable CS0618`. Update `ExtractAsync` aggregation accordingly.
- [X] T019 [US4] Modify `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs`: change `ExtractCriteriaLoopAsync.extractPerDoc` to return `RequirementsExtractionResult`; aggregate only `IsCacheable` results' `Requirements`; route `EmptyResponse`/`ParseFailure` into the `failed` count (existing `onDocFailure`/`failed_documents` channel). Preserve the per-doc `Task.Delay` deadline + `onSlowDoc`, `ComputeCriteriaWarning` (Spec 048), and all `documents_failed`/`failed_documents` reporting.

### Tests

- [X] T020 [P] [US4] **Rewrite** the legacy throw tests for `RequirementsExtractor` (search `tests/Spectra.CLI.Tests` for the throw-on-empty / throw-on-timeout assertions) into typed-outcome assertions: empty input → `EmptyResponse` (no throw); timeout no longer throws from the extractor.
- [X] T021 [P] [US4] `tests/Spectra.CLI.Tests/Agent/RequirementsExtractorUnificationTests.cs`: `RequirementsExtractionResult.IsCacheable` matches `CriteriaExtractionResult` semantics; `ExtractCriteriaLoopAsync` over a mixed set aggregates only `Extracted` and counts non-cacheable as failed; corpus does not abort.

**Checkpoint**: One failure-semantics contract; `docs index` resilient; no throwing path remains.

---

## Phase 7: Polish & Cross-Cutting

- [X] T022 [P] Fix factually-wrong docs: criteria-extraction + `docs index` pages that describe in-CLI provider/model behavior or the throwing legacy path; update the criteria workflow page to the compile→handoff→classify→retry choreography (per spec Documentation Impact). Search `docs/` for the affected pages.
- [X] T023 Run `dotnet build` then `dotnet test tests/Spectra.CLI.Tests` and `dotnet test tests/Spectra.Core.Tests` — confirm the protected regression net (`Spectra.Core` parsing/requirements + `CriteriaExtractionResult`/`ClassifyResponse` tests) is **unchanged and green**. Any break there is a regression to investigate, not a test to edit.
- [X] T024 Manual smoke per `quickstart.md`: `compile-extraction-prompt` determinism (`diff` two runs), refuse-to-emit exit 4, `ingest-criteria` exits 0/5/6, `docs index` over a mixed corpus completes cleanly.

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002, T003)** → stories.
- **US1 (T004–T011)**: MVP. T004→T005; T006 needs T002-adjacent types but is independent of T004; T007 needs T004; T008 needs T006; T009 needs T007+T008. Tests T010/T011 after their targets.
- **US2 (T012–T013)**: depends on US1 (T007 command exists).
- **US3 (T014–T017)**: depends on US1 (T006 ingestor + T008 command exist).
- **US4 (T018–T021)**: independent of US1–US3 (requirements path); needs Foundational T003.
- **Polish (T022–T024)**: after all stories.

**Story independence**: US4 can be built in parallel with US1–US3 (different files:
`RequirementsExtractor.cs`/`DocsIndexHandler.cs` vs the `Extraction/` surface).

## Parallel Execution Examples

- Foundational: T002 ‖ T003 (different files).
- After US1 implementation lands: T010 ‖ T011 (different test files).
- US4 alongside US1–US3: T018→T019 while the `Extraction/` surface is built.
- Polish: T022 ‖ (T023 sequential gate) — keep T023 last as the green-net gate.

## Implementation Strategy

- **MVP = US1** (compile + happy-path ingest): a working model-free extraction surface.
- **+US2/US3** harden the boundary (short-circuit, fail-loud, cache gate) — all P1.
- **+US4** unifies the legacy `docs index` path (P2).
- Keep every existing command green at each checkpoint (additive scope); T023 is the final gate.
