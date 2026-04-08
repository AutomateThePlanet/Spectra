# Tasks: Docs Index Progress, SKILL Integration & Coverage Dashboard Fix

**Input**: Design documents from `/specs/024-docs-skill-coverage-fix/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

**Tests**: Included per the spec's test plan.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Foundational — Terminology Rename (US3)

**Goal**: Complete the "requirements" → "acceptance criteria" rename in all user-facing strings and add the `_requirements.yaml` auto-rename logic.

**Independent Test**: Run CLI commands and verify all output says "acceptance criteria". Run with old `_requirements.yaml` and verify it gets renamed to `.bak`.

- [ ] T001 [P] [US3] Fix user-facing strings in `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` — change "Extracting requirements from documentation" → "Extracting acceptance criteria from documentation", "Extracted {n} requirement(s)" → "Extracted {n} acceptance criteria", and prompt text "Extract all testable behavioral requirements" → "Extract all testable acceptance criteria"
- [ ] T002 [P] [US3] Fix help text in `dashboard-site/scripts/app.js` ~line 1386 — change "requirements field" → "criteria field"
- [ ] T003 [US3] Add `_requirements.yaml` → `.bak` auto-rename in the criteria reader. In `src/Spectra.Core/Parsing/AcceptanceCriteriaParser.cs` (or `CriteriaIndexReader`): when `_criteria_index.yaml` does not exist but `_requirements.yaml` does, rename to `_requirements.yaml.bak` and log message. When both exist, prefer `_criteria_index.yaml`.
- [ ] T004 [P] [US3] Grep codebase for remaining user-facing "requirement" strings (excluding backward-compat aliases, class names, comments) and fix any found. Check `src/Spectra.CLI/` and `dashboard-site/` directories.

### Tests for Phase 1

- [ ] T005 [US3] Add test `UserFacingStrings_DoNotContainRequirement` — scan string literals in `Spectra.CLI` output paths for "requirement" (exclude known backward-compat strings)
- [ ] T006 [US3] Add test for `_requirements.yaml` auto-rename: given only `_requirements.yaml` exists → renamed to `.bak`, criteria reader returns empty
- [ ] T007 [US3] Add test: given both `_requirements.yaml` and `_criteria_index.yaml` exist → `_criteria_index.yaml` is used, `_requirements.yaml` untouched

**Checkpoint**: All user-facing text uses "acceptance criteria". Old format file is auto-renamed.

---

## Phase 2: Dashboard Coverage Fix (US2)

**Goal**: Fix the dashboard Coverage tab to handle missing/null criteria data gracefully and show correct labels.

**Independent Test**: Generate dashboard with and without criteria data; verify Coverage tab renders without JS errors.

- [ ] T008 [P] [US2] Fix `src/Spectra.CLI/Dashboard/DataCollector.cs` — ensure `AcceptanceCriteriaSectionData` is never null in the coverage output. When criteria data is missing, provide zero-state default: `{ Covered = 0, Total = 0, Percentage = 0, HasCriteriaFile = false, Details = [] }`
- [ ] T009 [P] [US2] Fix `dashboard-site/scripts/app.js` — add null guard for `summary.acceptance_criteria` before accessing `.covered`, `.total`, `.percentage`. Show empty-state guidance div when `has_criteria_file` is false or section is missing.

### Tests for Phase 2

- [ ] T010 [US2] Add test `Dashboard_CoverageSection_HandlesNullCriteria` — DataCollector with no criteria file produces valid non-null coverage JSON
- [ ] T011 [US2] Add test `Dashboard_CoverageLabels_UseAcceptanceCriteria` — verify generated HTML contains "Acceptance Criteria Coverage" not "Requirement Coverage"

**Checkpoint**: Dashboard Coverage tab renders correctly in all scenarios (full data, partial, no data).

---

## Phase 3: DocsIndexHandler Result & Progress (US1)

**Goal**: Add `.spectra-result.json` and `.spectra-progress.html` writing to `DocsIndexHandler`, add `--skip-criteria` flag, pass `--no-interaction` to criteria extraction.

**Independent Test**: Run `spectra docs index --no-interaction --output-format json` and verify `.spectra-result.json` and `.spectra-progress.html` are written correctly.

- [ ] T012 [US1] Extend `DocsIndexResult` model in `src/Spectra.CLI/Results/DocsIndexResult.cs` — add `DocumentsSkipped`, `DocumentsNew`, `DocumentsTotal`, `CriteriaExtracted` (int?), `CriteriaFile` (string?) with JSON property names
- [ ] T013 [US1] Add `--skip-criteria` option to `src/Spectra.CLI/Commands/Docs/DocsIndexCommand.cs` and thread through to handler constructor
- [ ] T014 [US1] Update `DocsIndexHandler` constructor to accept `bool noInteraction` and `bool skipCriteria` parameters
- [ ] T015 [US1] Add result/progress file writing to `DocsIndexHandler.ExecuteAsync()`:
  - Delete stale `.spectra-result.json` and `.spectra-progress.html` at start
  - Write in-progress result at each phase transition (scanning → indexing → extracting-criteria)
  - Call `ProgressPageWriter.WriteProgressPage()` at each phase with `isTerminal: false`
  - Write final result with `isTerminal: true` on completion or failure
  - Use `FlushWriteFile` pattern from GenerateHandler for NTFS reliability
- [ ] T016 [US1] Extend `ProgressPageWriter.WriteProgressPage()` in `src/Spectra.CLI/Progress/ProgressPageWriter.cs` to handle docs-index statuses (`scanning`, `indexing`, `extracting-criteria`). Render phase stepper: Scanning → Indexing → Extracting Criteria → Completed. Show summary cards for document counts.
- [ ] T017 [US1] Pass `noInteraction` flag through to `TryExtractRequirementsAsync()` — when set, criteria extraction runs with defaults (no prompts). When `skipCriteria` is true, skip `TryExtractRequirementsAsync()` entirely.
- [ ] T018 [US1] Update existing tests in `DocsIndexHandlerTests` that may assert old message strings

### Tests for Phase 3

- [ ] T019 [US1] Add test `DocsIndex_WritesResultJson_OnSuccess` — verify `.spectra-result.json` written with correct schema
- [ ] T020 [US1] Add test `DocsIndex_DeletesOldResultFile_AtStart` — verify stale result file removed
- [ ] T021 [US1] Add test `DocsIndex_WritesProgressHtml_DuringExecution` — verify `.spectra-progress.html` created
- [ ] T022 [US1] Add test `DocsIndex_SkipCriteria_DoesNotExtract` — verify `--skip-criteria` skips extraction
- [ ] T023 [US1] Add test `DocsIndex_NoInteraction_RunsCriteriaWithDefaults` — verify no prompts with `--no-interaction`

**Checkpoint**: `spectra docs index` writes result/progress files, supports `--skip-criteria`, runs non-interactively.

---

## Phase 4: New SKILL — `spectra-docs` (US4)

**Goal**: Create the 9th bundled SKILL for docs indexing with progress page integration.

**Independent Test**: Run `spectra init` and verify `.github/skills/spectra-docs/SKILL.md` is created. Verify SKILL content includes progress page and result file steps.

- [ ] T024 [US4] Create embedded SKILL file `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md` with structured tool-call-sequence (show preview, runInTerminal with `--no-interaction --output-format json --verbosity quiet`, awaitTerminal, readFile result). Set build action to `EmbeddedResource` in `.csproj`.
- [ ] T025 [US4] Add `DocsIndex` static property to `src/Spectra.CLI/Skills/SkillContent.cs` referencing the new embedded resource
- [ ] T026 [P] [US4] Update `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md` — update docs index section to reference progress page, `--no-interaction`, and `.spectra-result.json` flow

### Tests for Phase 4

- [ ] T027 [US4] Add test `SkillContent_DocsIndexSkill_NotEmpty` — verify embedded content loads
- [ ] T028 [US4] Add test `Init_CreatesDocsIndexSkill` — verify `spectra init` creates `.github/skills/spectra-docs/SKILL.md`

**Checkpoint**: Docs indexing is fully SKILL-integrated with live progress.

---

## Phase 5: Polish & Validation

**Purpose**: Cross-cutting verification and cleanup.

- [ ] T029 Run full test suite (`dotnet test`) and fix any broken tests from terminology changes
- [ ] T030 Build project (`dotnet build`) and verify no warnings related to changes
- [ ] T031 Manual verification: run `spectra docs index --no-interaction --output-format json` in demo project, verify `.spectra-result.json` and `.spectra-progress.html` work end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Rename)**: No dependencies — start immediately. Unblocks Phase 2 and 3.
- **Phase 2 (Dashboard)**: Depends on Phase 1 (terminology must be consistent before fixing dashboard)
- **Phase 3 (Result/Progress)**: Depends on Phase 1 (strings fixed). Independent of Phase 2.
- **Phase 4 (SKILL)**: Depends on Phase 3 (SKILL references result/progress files that must exist)
- **Phase 5 (Polish)**: Depends on all phases complete

### Parallel Opportunities

- **Within Phase 1**: T001, T002, T004 can run in parallel (different files)
- **Within Phase 2**: T008, T009 can run in parallel (C# vs JS files)
- **Phase 2 and Phase 3**: Can run in parallel after Phase 1 completes
- **Within Phase 4**: T026 can run in parallel with T024/T025

### Execution Order (sequential path)

```
T001,T002,T004 (parallel) → T003 → T005,T006,T007 (tests)
  → T008,T009 (parallel) → T010,T011 (tests)
  → T012 → T013,T014 → T015,T016 → T017 → T018 → T019-T023 (tests)
  → T024 → T025 → T026 → T027,T028 (tests)
  → T029,T030,T031 (polish)
```

---

## Notes

- [P] tasks = different files, no dependencies
- Tests are included per the spec's test plan
- Phase 1 (rename) is done first because inconsistent terminology causes confusion in all subsequent phases
- The `ProgressPageWriter` extension (T016) is the most complex task — it requires understanding the existing HTML rendering logic
- The `_requirements.yaml` auto-rename (T003) should be a small, focused change in the criteria reader
