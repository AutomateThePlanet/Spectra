---
description: "Task list for 060-coverage-doc-exclusions"
---

# Tasks: Coverage-Scoped Document Exclusions

**Input**: Design documents from `/specs/060-coverage-doc-exclusions/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the spec's Tests section and SC-006 explicitly require net-new tests
(denominator filtering, excluded-status reporting, no-config equivalence, three-concept disambiguation).

**Organization**: Tasks grouped by user story. Note: this feature's user stories are facets of one
deterministic code path verified by distinct tests, so the Foundational phase (config + matcher
relocation + model extensions) is shared by all stories and MUST complete first.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US4 maps to spec user stories

## Path Conventions

Single-repo multi-project: `src/Spectra.Core/`, `src/Spectra.CLI/`, `tests/Spectra.Core.Tests/`,
`tests/Spectra.CLI.Tests/`, `docs/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm baseline before changes

- [X] T001 Run `dotnet build` and `dotnet test --filter "FullyQualifiedName~Coverage"` to capture a green baseline before any change (records the pre-feature coverage behavior the no-config equivalence tests will protect).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Config key, matcher relocation, and model extensions that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Relocate `ExclusionPatternMatcher` from `src/Spectra.CLI/Source/ExclusionPatternMatcher.cs` to `src/Spectra.Core/Source/ExclusionPatternMatcher.cs`, changing only the namespace `Spectra.CLI.Source` → `Spectra.Core.Source` (behavior-preserving move, FR-004). Delete the original CLI file.
- [X] T003 Update the two existing consumers' `using` to the new namespace: `src/Spectra.CLI/Source/DocumentIndexService.cs` (line ~255 usage) and `src/Spectra.CLI/Index/LegacyIndexMigrator.cs` (line ~121 usage). Run `dotnet build` to confirm no other references broke.
- [X] T004 [P] Add `CoverageExcludePatterns` to `src/Spectra.Core/Models/Config/CoverageConfig.cs`: JSON key `coverage_exclude_patterns`, type `IReadOnlyList<string>`, default `[]` (empty — NO implicit patterns, FR-005/FR-007), with an XML-doc comment that distinguishes it from `AnalysisExcludePatterns` and `source.exclude_patterns`.
- [X] T005 [P] Extend `src/Spectra.Core/Models/Coverage/DocumentationCoverage.cs`: add `Excluded` (`excluded`, bool, default false) and `ExcludedByPattern` (`excluded_by_pattern`, string?, default null) to `DocumentCoverageDetail`; add `ExcludedDocs` (`excluded_docs`, int, default 0) to `DocumentationCoverage`. Apply `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` to all three new properties so unconfigured JSON is byte-for-byte unchanged (FR-005).

**Checkpoint**: Build green; matcher reachable from Core; config + models carry exclusion data.

---

## Phase 3: User Story 1 - Exclude non-testable docs from the coverage percentage (Priority: P1) 🎯 MVP

**Goal**: A configured coverage-exclude pattern drops matched docs from the documentation-coverage
denominator while they remain in `documentMap.Documents` for generation/analysis/indexing.

**Independent Test**: Configure `coverage_exclude_patterns` matching a subset of docs, run coverage,
assert the percentage is computed over the remaining docs and the excluded docs are still present in the
document map.

### Tests for User Story 1

- [X] T006 [P] [US1] In `tests/Spectra.Core.Tests/Coverage/DocumentationCoverageAnalyzerExclusionTests.cs`, add tests: (a) with a pattern matching a subset of docs, `TotalDocs` (denominator) equals total-minus-matched and `Percentage` is computed over the remainder; (b) excluded docs are NOT counted in `CoveredDocs` nor as uncovered. (FR-002, SC-001)
- [X] T007 [P] [US1] In the same test file, add a test asserting the analyzer does NOT mutate or remove entries from the input `DocumentMap.Documents` (excluded docs remain available for gen/analysis/indexing). (FR-002, SC-003)

### Implementation for User Story 1

- [X] T008 [US1] Modify `DocumentationCoverageAnalyzer.Analyze` in `src/Spectra.Core/Coverage/DocumentationCoverageAnalyzer.cs` to accept the coverage-exclude patterns (e.g. an added `IReadOnlyList<string> coverageExcludePatterns` parameter), build an `ExclusionPatternMatcher`, mark matched docs as `Excluded`/`ExcludedByPattern`, exclude them from `TotalDocs`/`CoveredDocs`/`Percentage`, set `ExcludedDocs`, and keep them in `Details`. Preserve existing behavior when the list is empty.
- [X] T009 [US1] Wire the unified path in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` (~line 207-208) to pass `config.Coverage.CoverageExcludePatterns` into `docAnalyzer.Analyze(...)`.
- [X] T010 [US1] Apply the same exclusion in the legacy path `RunLegacyCoverageAsync` in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` (~line 1533+): construct the matcher from `config.Coverage.CoverageExcludePatterns`, flag excluded docs, drop them from the `TotalDocuments` denominator and from `CoveredDocuments`/`UncoveredDocuments`, and skip gap generation for them. (research.md Decision 4)

**Checkpoint**: Coverage % reflects only non-excluded docs in BOTH paths; docs remain in the map.

---

## Phase 4: User Story 2 - Excluded docs are visibly reported, never silently dropped (Priority: P1)

**Goal**: Excluded docs appear in coverage output with a distinct "excluded" status (JSON + human),
auditable with the matched pattern.

**Independent Test**: With an exclusion configured, render the report and confirm each excluded doc
appears with a distinct "excluded" status (not omitted, not `Yes`/`No`).

### Tests for User Story 2

- [X] T011 [P] [US2] In `tests/Spectra.CLI.Tests/Coverage/CoverageReportWriterExclusionTests.cs`, assert the markdown report renders a distinct `Excluded` status in the Covered column for excluded docs and that the summary line includes the excluded count when `excluded_docs > 0`. (FR-003, SC-002)
- [X] T012 [P] [US2] In the same file, assert the compact/terminal renderer uses a distinct mark (e.g. `~`) for excluded docs, separate from covered `+` and uncovered `-`. (FR-003)

### Implementation for User Story 2

- [X] T013 [US2] Update `src/Spectra.CLI/Coverage/CoverageReportWriter.cs`: in the markdown table (~line 90-95) render `Excluded` as a third status when `detail.Excluded`, and add `(N excluded)` to the summary line (~line 73) when `doc.ExcludedDocs > 0`; in the compact renderer (~line 210-214) emit a distinct mark for excluded docs.
- [X] T014 [US2] Update the legacy report renderer `src/Spectra.CLI/IO/ReportWriter.cs` to surface `ExcludedDocuments` and avoid listing excluded docs under "Uncovered Documents". (data-model.md §4)

**Checkpoint**: Excluded docs are visibly distinct in every coverage output; nothing silently dropped.

---

## Phase 5: User Story 3 - No-config behavior is unchanged (Priority: P1)

**Goal**: With no patterns configured, coverage output is byte-for-byte identical to pre-feature.

**Independent Test**: With `coverage_exclude_patterns` absent/empty, run coverage and diff output
against the pre-feature baseline — identical.

### Tests for User Story 3

- [X] T015 [P] [US3] In `tests/Spectra.Core.Tests/Coverage/DocumentationCoverageAnalyzerExclusionTests.cs`, add a no-config equivalence test: with an empty pattern list, `TotalDocs`, `CoveredDocs`, `Percentage`, and `Details` equal the values produced by the parameterless/legacy call, and no detail has `Excluded == true`. (FR-005, SC-004)
- [X] T016 [P] [US3] In `tests/Spectra.Core.Tests/Coverage/`, add a serialization test asserting that with no exclusions the JSON for `DocumentationCoverage`/`DocumentCoverageDetail` contains none of the new keys (`excluded`, `excluded_by_pattern`, `excluded_docs`) — confirms `WhenWritingDefault` preserves byte equivalence. (FR-005)

### Implementation for User Story 3

- [X] T017 [US3] Verify/adjust the `Analyze` change from T008 so the empty-list path takes an early branch identical to current logic (no matcher construction side effects, no new fields emitted). If any existing no-exclusion `Spectra.Core/Coverage` test changed value, treat as a regression and fix the implementation — do NOT update the expected value (spec Tests guard).

**Checkpoint**: Unconfigured workspaces see zero output change.

---

## Phase 6: User Story 4 - The three exclusion concepts are non-confusable (Priority: P2)

**Goal**: Lock the independent semantics of the three exclusion mechanisms with a test and docs.

**Independent Test**: A doc matched only by `analysis_exclude_patterns` still counts in the coverage
denominator; once also added to `coverage_exclude_patterns` it is excluded.

### Tests for User Story 4

- [X] T018 [P] [US4] In `tests/Spectra.CLI.Tests/Coverage/` (or `Spectra.Core.Tests` if the analyzer-level assertion suffices), add a disambiguation test: a doc matched by `coverage.analysis_exclude_patterns` but NOT by `coverage_exclude_patterns` still counts in the coverage denominator; the same doc added to `coverage_exclude_patterns` becomes excluded. (FR-006, SC-005)

### Implementation for User Story 4

- [X] T019 [P] [US4] Update `docs/` coverage documentation to describe `coverage_exclude_patterns` and add the three-mechanism disambiguation table (source.exclude_patterns / coverage.analysis_exclude_patterns / coverage.coverage_exclude_patterns) from `contracts/config-schema.md`. (FR-008)
- [X] T020 [P] [US4] Update `docs/` configuration documentation with the new `coverage.coverage_exclude_patterns` key, its empty default, and a cross-reference to the disambiguation table. (FR-008)

**Checkpoint**: The three concepts are documented and test-locked as independent.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T021 Update `src/Spectra.CLI/Dashboard/DataCollector.cs` and `src/Spectra.CLI/Dashboard/SampleDataFactory.cs` only if they break against the extended `DocumentationCoverage` model; otherwise leave untouched (they consume existing fields). Confirm via `dotnet build`.
- [X] T022 Run full `dotnet test` and confirm all suites green, with the only changed coverage-test expectations being those whose denominator legitimately moved under a configured exclusion (SC-006).
- [X] T023 Execute `specs/060-coverage-doc-exclusions/quickstart.md` end-to-end against a scratch workspace (configure a pattern, run `spectra ai analyze --coverage`, verify denominator + excluded status + that excluded docs still generate/analyze).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — start immediately.
- **Foundational (Phase 2)**: depends on Setup. **BLOCKS all user stories.** Within it, T002→T003 are sequential (move then fix references); T004 and T005 are [P] (different files) and can run alongside T003.
- **User Stories (Phase 3–6)**: all depend on Foundational.
  - US1 (Phase 3) is the MVP and should land first — US2/US3/US4 verify facets of the same code US1 introduces.
  - US2 (Phase 4) depends on US1's analyzer/handler changes (it renders the data US1 produces).
  - US3 (Phase 5) depends on US1 (T008) — it asserts the empty-list branch of that change.
  - US4 (Phase 6) depends on US1 (denominator logic); its docs tasks (T019, T020) are independent and [P].
- **Polish (Phase 7)**: after all desired stories.

### Within Each User Story

- Tests (T006/T007, T011/T012, T015/T016, T018) are written first and fail before the matching implementation.
- Analyzer change (T008) precedes handler wiring (T009, T010).
- Report-model fields (Phase 2) precede report rendering (Phase 4).

### Parallel Opportunities

- T004 + T005 (Foundational, different files).
- All test-authoring tasks within a story marked [P] (T006/T007, T011/T012, T015/T016).
- Docs tasks T019 + T020 (different doc files).

---

## Parallel Example: Foundational

```bash
# After T002→T003 (matcher move + reference fix), run in parallel:
Task: "T004 Add CoverageExcludePatterns to CoverageConfig.cs"
Task: "T005 Extend DocumentationCoverage.cs with Excluded/ExcludedByPattern/ExcludedDocs"
```

## Parallel Example: User Story 1 tests

```bash
Task: "T006 Denominator-filtering analyzer tests"
Task: "T007 Map-immutability analyzer test"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL) → 3. Phase 3 US1 → **STOP & VALIDATE**: a
   configured pattern moves the denominator and excluded docs remain in the map. This is a shippable
   slice (the number is correct), though the "excluded" status rendering (US2) makes it auditable.

### Incremental Delivery

1. Foundational ready.
2. US1 → denominator filtering works (MVP).
3. US2 → excluded status visibly reported.
4. US3 → no-config equivalence locked.
5. US4 → three-concept disambiguation documented + test-locked.

---

## Notes

- [P] = different files, no dependencies.
- The DO-NOT-TOUCH regression net: existing `Spectra.Core/Coverage` tests stay unchanged EXCEPT those
  whose headline coverage number legitimately moves under a configured exclusion (T022). A no-exclusion
  test that breaks is a regression — fix the code, not the expectation (T017).
- Relocation (T002) is behavior-preserving — no logic edits to `ExclusionPatternMatcher`.
- Commit after each task or logical group.
