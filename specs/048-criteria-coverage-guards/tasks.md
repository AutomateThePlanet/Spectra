---
description: "Task list for Spec 048 — Criteria Coverage Guards"
---

# Tasks: Criteria Coverage Guards

**Input**: Design documents from `/specs/048-criteria-coverage-guards/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Tests**: Tests are REQUESTED — spec.md §"Test Plan" enumerates 9 specific test cases. Generate them.
**Organization**: Tasks grouped by user story. US1 and US2 are P1; US3 is P2. All three are independent and can land in any order (no cross-story dependencies).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: `[US1]`, `[US2]`, or `[US3]` — maps to user stories in spec.md

## Path Conventions

Single-project layout. All paths are repo-relative from `C:\SourceCode\Spectra\`.

- Source: `src/Spectra.Core/...`, `src/Spectra.CLI/...`
- Tests: `tests/Spectra.Core.Tests/...`, `tests/Spectra.CLI.Tests/...`
- Bundled SKILLs: `src/Spectra.CLI/Skills/Content/Skills/` (NOT `.github/skills/` — that's the install target after `spectra update-skills`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Brownfield fix on a stable codebase. No new project, no new package, no toolchain change.

- [X] T001 [P] Ensure `tests/Spectra.Core.Tests/Models/Coverage/` directory exists for the new `CriteriaSourceTests.cs` (create with `New-Item -ItemType Directory -Force` if absent).
- [X] T002 [P] Confirm green baseline: `dotnet build` and `dotnet test` pass on the `048-criteria-coverage-guards` branch starting state (note: 047 is already merged into main, so this branch should be at the same baseline). Capture the pass-count for cross-reference at T020.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None strictly required. All three user stories are independent:
- US1 (docs-index warning) computes from in-run counts (`extracted.Count` + `documentMap.Documents.Count`) — no new types needed.
- US2 (generate note) introduces an internal `CriteriaContextResult` record private to `GenerateHandler` — folded into the US2 phase.
- US3 (persist outcome) introduces the `CriteriaSource.Outcome` field — folded into the US3 phase.

This phase intentionally has zero tasks. Documented for clarity so reviewers see we considered the question.

**Checkpoint**: None — proceed directly to user-story phases.

---

## Phase 3: User Story 1 — Docs-index zero-criteria warning (Priority: P1) 🎯 MVP

**Goal**: `DocsIndexHandler.TryExtractCriteriaAsync` emits a prominent non-blocking warning and surfaces a new optional `criteria_warning` JSON field on `DocsIndexResult` when the corpus produced zero criteria across at least one indexed document.

**Independent Test**: Drive `DocsIndexHandler.ExtractCriteriaLoopAsync` (existing internal seam from 047) with stub per-doc extractors. Verify (a) zero-corpus condition emits both the human warning and the JSON field, (b) any-criteria-present case emits neither, (c) `--skip-criteria` suppresses the warning regardless.

### Tests for User Story 1 (Test Plan rows 3, 4, 5 + edge case) ⚠️

> Write tests FIRST. They MUST FAIL before implementation tasks T006–T008.

- [X] T003 [P] [US1] Add `DocsIndex_ZeroCriteriaAcrossCorpus_WarnsNonBlocking` xUnit case in new file `tests/Spectra.CLI.Tests/Commands/DocsIndexZeroCriteriaTests.cs`. Stub the per-doc extractor delegate to return zero criteria for every doc; assert (i) `criteria_warning` is present on the resulting `DocsIndexResult` and contains `"spectra ai analyze --extract-criteria"`, (ii) the warning callback was invoked once, (iii) exit code is `Success`.
- [X] T004 [P] [US1] Add `DocsIndex_CriteriaFound_NoWarning` case in the same file. Stub at least one doc to return a `RequirementDefinition`; assert (i) `criteria_warning` is `null` (absent from serialized JSON), (ii) no warning callback was invoked.
- [X] T005 [P] [US1] Add `DocsIndex_RealEmptyDocs_NoFalseWarning` case in the same file. Stub mixed (some docs empty, some with criteria); assert `criteria_warning` is null because corpus total > 0. Also add `DocsIndex_SkipCriteria_SuppressesWarning` case verifying `criteria_warning` is null when `--skip-criteria` is passed (the handler should never enter the extraction branch).

### Implementation for User Story 1

- [X] T006 [US1] Modify `src/Spectra.CLI/Results/DocsIndexResult.cs`: add `[JsonPropertyName("criteria_warning")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CriteriaWarning { get; init; }`. Place adjacent to the existing `CriteriaExtracted` field (~`:53-55`). No change to existing fields.
- [X] T007 [US1] Modify `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs::TryExtractCriteriaAsync`. Change return type from `(int? criteriaCount, string? criteriaFile)` to `(int? criteriaCount, string? criteriaFile, string? criteriaWarning)` per `contracts/docs-index-handler.md`. After the per-doc loop returns (`loopResult.Aggregated` count is the corpus total), compute the warning string when `documentMap.Documents.Count > 0 && extracted.Count == 0`, route it through `_progress.Warning` for the human channel, and return it as the third tuple element. All other return points (provider-null, empty docs, dry-run, catch-all) return `null` for the third element.
- [X] T008 [US1] Update `DocsIndexHandler::ExecuteCoreAsync` at `~:199` to destructure the new third tuple element and assign it to `result.CriteriaWarning` (~`:216-234` site).
- [X] T009 [US1] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md` per `contracts/skill-rendering.md`: add a short "Criteria warning surfacing" section instructing the agent to render `criteria_warning` verbatim when present and to NOT treat it as a failure.
- [X] T010 [US1] Run `dotnet test --filter "FullyQualifiedName~DocsIndexZeroCriteria"` and confirm all four tests green (T003-T005).

**Checkpoint**: US1 acceptance scenarios from spec.md verifiable end-to-end via stubs. Quickstart §A/§B (manual sandbox) further validates against the real CLI binary.

---

## Phase 4: User Story 2 — Generate no-match note (Priority: P1)

**Goal**: Both `ExecuteDirectModeAsync` and `ExecuteFromDescriptionAsync` attach a non-blocking note to the result when zero acceptance criteria match the target suite. Note is present in JSON regardless of `--verbosity quiet`.

**Independent Test**: Drive direct mode and from-description with fixture criteria files where suite component doesn't match. Assert `notes` collection contains the expected message in the JSON result. Repeat with matching criteria and assert `notes` is null.

> Phase 4 has no dependency on Phase 3 — they touch different handlers, different result classes, different test files.

### Tests for User Story 2 (Test Plan rows 6, 7, 8, 9) ⚠️

> Write tests FIRST. They MUST FAIL before implementation tasks T013–T016.

- [X] T011 [P] [US2] Add `Generate_Batch_NoMatchedCriteria_AddsNote` and `Generate_WithMatchedCriteria_NoNote` xUnit cases in new file `tests/Spectra.CLI.Tests/Commands/GenerateNoCriteriaNoteTests.cs`. Tests drive `ExecuteDirectModeAsync` via the existing direct-mode test harness; use fixture `.criteria.yaml` files with non-matching/matching component fields. Assert `result.Notes` is a one-element list with the expected message (first case) and `null` (second case).
- [X] T012 [P] [US2] Add `Generate_FromDescription_NoMatchedCriteria_AddsNote` case in the same file. Drives `ExecuteFromDescriptionAsync` with the same non-matching fixture. Assert the JSON written via `JsonResultWriter` carries `notes` with the expected message.
- [X] T013 [P] [US2] Add `Generate_Note_PresentInJson_EvenWhenQuiet` case. Set `_verbosity = VerbosityLevel.Quiet`, run the no-match direct-mode case, assert `result.Notes` is the one-element list AND the serialized JSON contains the `notes` key. (Console output is not asserted — the test verifies the JSON-channel invariant only.)

### Implementation for User Story 2

- [X] T014 [US2] Modify `src/Spectra.CLI/Results/GenerateResult.cs`: add `[JsonPropertyName("notes")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<string>? Notes { get; init; }`. Place near the existing optional-list fields.
- [X] T015 [US2] Refactor `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs::LoadCriteriaContextAsync` per `contracts/generate-handler.md` Part 1. Change return type from `Task<string?>` to `Task<CriteriaContextResult>`. Add the `internal sealed record CriteriaContextResult(string? Context, int SuiteMatchedCount, int TotalCriteriaCount)` declaration in the same file (near the handler class, internal so test code can reach it if needed). Compute `SuiteMatchedCount` as the count after the component/source-doc filter (line ~`:2452-2462`) PLUS the file-name fallback (line ~`:2467-2482`) — but NOT the last-resort all-criteria fallback (line ~`:2485-2486`). `TotalCriteriaCount = allCriteria.Count` from line ~`:2448`.
- [X] T016 [US2] Update both call sites to use the new return shape:
  - `ExecuteDirectModeAsync` at line ~`:672`: split `var criteriaContext = await LoadCriteriaContextAsync(...);` into `var criteriaResult = await LoadCriteriaContextAsync(...); var criteriaContext = criteriaResult.Context;`. Carry `criteriaResult.SuiteMatchedCount` forward.
  - `ExecuteFromDescriptionAsync` at line ~`:1793`: same pattern.
- [X] T017 [US2] Add the note to the final `GenerateResult` built in `ExecuteDirectModeAsync` at line ~`:985-1013`. When `criteriaResult.SuiteMatchedCount == 0`, set `Notes = new[] { string.Format(NoCriteriaNoteFormat, suite) }`. Add the constant `private const string NoCriteriaNoteFormat = "No acceptance criteria matched suite '{0}'. Generated tests have no criteria linkage; acceptance-criteria coverage will not include them. Run 'spectra ai analyze --extract-criteria' if criteria are expected.";` near the top of the handler. Also emit `_progress.Info(...)` with the same message when `_verbosity >= VerbosityLevel.Normal` (suppressed under quiet).
- [X] T018 [US2] Apply the same note logic to `ExecuteFromDescriptionAsync`'s JSON result at line ~`:1852-1867`. (No human-facing echo here because from-description doesn't use `_progress` for completion summary; the JSON channel carries the signal.)
- [X] T019 [US2] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` per `contracts/skill-rendering.md`: add a short "Notes surfacing" section instructing the agent to render each entry in `notes` after the results summary and to NOT treat notes as failures.
- [X] T020 [US2] Run `dotnet test --filter "FullyQualifiedName~GenerateNoCriteriaNote"` and confirm all four tests green (T011-T013).

**Checkpoint**: US1 + US2 both independently functional. Quickstart §C/§D/§E further validates against the real CLI binary.

---

## Phase 5: User Story 3 — Persist extraction outcome per document (Priority: P2)

**Goal**: `CriteriaSource` gains an `Outcome` field defaulting to `"extracted"`. `AnalyzeHandler` sets it on every cacheable upsert. Legacy entries without the field deserialize as `"extracted"` via the property default.

**Independent Test**: Roundtrip a `CriteriaSource` through YamlDotNet and assert the field. Drive `AnalyzeHandler.RunExtractCriteriaAsync` with a stub extractor returning a genuine empty result and assert the persisted index entry carries `outcome: extracted`. Read a legacy YAML payload (no `outcome:` key) and assert the in-memory record has `Outcome == "extracted"`.

> Phase 5 has no dependency on Phase 3 or Phase 4 — different files. The implementation can land in parallel.

### Tests for User Story 3 (Test Plan rows 1, 2) ⚠️

> Write tests FIRST. They MUST FAIL before implementation tasks T023–T024.

- [X] T021 [P] [US3] Add `CriteriaSource_Roundtrip_PreservesOutcome` xUnit case in new file `tests/Spectra.Core.Tests/Models/Coverage/CriteriaSourceTests.cs`. Three sub-assertions: (a) `CriteriaSource { Outcome = "extracted" }` serializes to YAML containing `outcome: extracted`; (b) round-tripping that YAML through `Deserializer.Deserialize<CriteriaSource>(...)` produces `Outcome == "extracted"`; (c) deserializing a legacy YAML payload with NO `outcome:` key produces `Outcome == "extracted"` (the property default).
- [X] T022 [P] [US3] Add `Extract_RealEmpty_RecordsOutcomeExtracted` case in new file `tests/Spectra.CLI.Tests/Agent/Copilot/CriteriaExtractorOutcomeTests.cs`. Drive `AnalyzeHandler.RunExtractCriteriaAsync` with a stub extractor that returns `new CriteriaExtractionResult(ExtractionOutcome.Extracted, [])`. Assert the resulting `_criteria_index.yaml` upsert contains an entry with `criteria_count: 0` AND `outcome: extracted`. (Use the existing 047 stub seams.)

### Implementation for User Story 3

- [X] T023 [US3] Modify `src/Spectra.Core/Models/Coverage/CriteriaSource.cs`: add `[YamlMember(Alias = "outcome")] public string Outcome { get; set; } = "extracted";` per `contracts/criteria-source.md`. XML doc comment names valid values (`extracted`; reserved future `empty_response`, `parse_failure`).
- [X] T024 [US3] Modify `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` upsert sites:
  - Extract path: at ~`:639-645` (update existing source branch), set `existingSource.Outcome = "extracted";`.
  - Extract path: at ~`:646-657` (add new source branch), include `Outcome = "extracted"` in the object initializer.
  - Import path: at ~`:1143-1147` (add new source branch in `--import-criteria`), include `Outcome = "extracted"` in the object initializer (imports are by definition genuine extractions).
  - Use a single private const `OutcomeExtracted = "extracted"` to keep the string literal in one place.
- [X] T025 [US3] Run `dotnet test --filter "FullyQualifiedName~CriteriaSource|FullyQualifiedName~CriteriaExtractorOutcome"` and confirm both tests green (T021-T022).

**Checkpoint**: All three user stories independently functional. Quickstart §F (legacy index roundtrip) further validates.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates, full-suite regression, manual quickstart walkthrough.

- [X] T026 [P] Update `docs/coverage.md`: explain the `outcome` field on `CriteriaSource` (Spec 048 subsection following the existing "Resilience (Spec 047)" entry). Clarify that an entry with `criteria_count: 0` and `outcome: extracted` is affirmed-empty (not a failure).
- [X] T027 [P] Update `docs/usage.md`: add a troubleshooting bullet titled "Indexed but 0 criteria" pointing at `spectra ai analyze --extract-criteria` (mirrors the new warning text). Add a short note about the no-match note appearing on generation when the suite has zero matching criteria.
- [X] T028 [P] Update `docs/cli-reference.md`: under `spectra docs index`, add a one-paragraph note about the zero-criteria warning. Under `spectra ai generate`, add a one-paragraph note about the no-match `notes` field.
- [X] T029 [P] Update `PROJECT-KNOWLEDGE.md`: add a Spec 048 row to the criteria section recording the additive `outcome` field, the docs-index warning, and the generation no-match note. Keep tight per the CLAUDE.md compactness preference.
- [X] T030 Run full test suite (`dotnet test`): expect 547 Core + 351 MCP + 1011 CLI + new 048 tests (1 Core + ~8 CLI) = 1918+ total. All green.
- [ ] T031 _Deferred to user_: walk through `quickstart.md` end-to-end against a real CLI binary in a scratch sandbox. Validate all six scenarios pass. Report results in the merge PR.
- [X] T032 Bumped `Directory.Build.props` Version: 1.52.1 → 1.52.2 (patch — matches the 047 precedent of "follow-up fix on the criteria-extraction path"; new JSON fields are additive/optional so no minor bump warranted).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Empty by design.
- **US1 (Phase 3)**: Depends only on Setup. Independent of US2 and US3.
- **US2 (Phase 4)**: Depends only on Setup. Independent of US1 and US3.
- **US3 (Phase 5)**: Depends only on Setup. Independent of US1 and US2.
- **Polish (Phase 6)**: Depends on whichever user stories are being shipped (typically all three).

### Within Each User Story

- Tests MUST be written and FAIL before the corresponding implementation tasks.
- Within US1: T003–T005 (tests) before T006 (result class), T007 (handler), T008 (call-site), T009 (SKILL); T010 (run) last.
- Within US2: T011–T013 (tests) before T014 (result class), T015 (refactor), T016 (call sites), T017–T018 (notes), T019 (SKILL); T020 (run) last.
- Within US3: T021–T022 (tests) before T023 (model field), T024 (upsert sites); T025 (run) last.

### Parallel Opportunities

- **Phase 1**: T001 and T002 run in parallel.
- **All three user stories are fully parallelizable** — they touch disjoint files. With three developers:
  - Dev A: US1 (T003 → T005 in parallel → T006 → T007 → T008 → T009 → T010)
  - Dev B: US2 (T011 → T013 in parallel → T014 → T015 → T016 → T017–T018 → T019 → T020)
  - Dev C: US3 (T021 → T022 in parallel → T023 → T024 → T025)
- **Phase 6**: T026, T027, T028, T029 are different files → all parallel.

---

## Parallel Example: All three stories in parallel

```text
Dev A (US1, P1):
  T003, T004, T005 in parallel → T006 → T007 → T008 → T009 → T010

Dev B (US2, P1):
  T011, T012, T013 in parallel → T014 → T015 → T016 → T017, T018 (sequential within this file) → T019 → T020

Dev C (US3, P2):
  T021, T022 in parallel → T023 → T024 → T025

Then collaborative:
  T026, T027, T028, T029 in parallel → T030 → T031 → T032 (deferred to user)
```

---

## Implementation Strategy

### MVP — single P1 story shipped alone

Both P1 stories are independently shippable. The user-facing payoff is symmetric on the docs-index and the generate sides, so neither is more MVP than the other. If shipping incrementally:

1. Phase 1 → Phase 3 (US1) → Phase 6 (US1-relevant docs only) → ship as patch.
2. Phase 4 (US2) → Phase 6 (US2-relevant docs) → ship as patch.
3. Phase 5 (US3) → Phase 6 (US3-relevant docs) → ship as patch.

### Recommended — single release bundling all three

The three changes are small (≈9 tests, ≈4 source files modified, 2 SKILL files). Bundle them into a single release:

1. Phase 1.
2. Phase 3, Phase 4, Phase 5 in parallel (or sequentially if single-developer).
3. Phase 6 (docs + full regression + quickstart).
4. Ship as a single patch/minor release with all three user stories.

### Parallel Team Strategy

Three small stories, one developer per story, single review pass. Each story's tests-then-implementation cycle is independently completable in well under a day.

---

## Notes

- Per the `MEMORY.md` `feedback_no_coauthor` entry, do NOT add `Co-Authored-By` lines to commits.
- Per the `MEMORY.md` `feedback_version_sync` entry, T032 must happen before any NuGet pack.
- Per the `MEMORY.md` `feedback_sync_demo_skills` entry, after the release the demo project consumer needs `spectra update-skills` to pick up the modified SKILL files.
- All three new user-facing strings (the docs-index warning, the generate note, the future-reserved outcome values) follow the FR-014 rule: name the recovery command directly. Tests assert presence of `"spectra ai analyze --extract-criteria"` so accidental wording changes that drop the command name are caught.
- `[P]` tasks operate on different files and have no incomplete dependencies.
- Commit at each completed phase boundary (or finer if useful). Per the 047 precedent, a single feature commit at the end of all phases works too.
- Verify each test FAILS before its corresponding implementation task is started — that confirms the test actually exercises the new behaviour, not just compiles.
