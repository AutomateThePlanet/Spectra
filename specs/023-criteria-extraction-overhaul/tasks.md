# Tasks: Acceptance Criteria Import & Extraction Overhaul

**Input**: Design documents from `/specs/023-criteria-extraction-overhaul/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included per constitution requirement (test-required discipline).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup

**Purpose**: Add new dependency and verify build

- [x] T001 Add CsvHelper NuGet package to `src/Spectra.Core/Spectra.Core.csproj`
- [x] T002 Verify solution builds with `dotnet build` and all 1241+ existing tests pass

---

## Phase 2: Foundational — Terminology Rename (US3, Priority: P1)

**Purpose**: Rename "requirements" to "acceptance criteria" across the entire codebase. This MUST complete before any user story work begins, as all new code depends on the renamed types.

**Goal**: All user-facing references use "acceptance criteria" terminology. Old `--extract-requirements` flag works as hidden alias.

**Independent Test**: Run `spectra ai analyze --extract-requirements` and verify it works. Check CLI output, dashboard labels, and reports for consistent terminology.

### Core Models & Parsers (rename + extend)

- [x] T003 [P] [US3] Rename `RequirementDefinition` to `AcceptanceCriterion` in `src/Spectra.Core/Models/Coverage/RequirementDefinition.cs` → `AcceptanceCriterion.cs`. Add new fields: `Rfc2119`, `SourceType`, `SourceDoc`, `SourceSection`, `Component`, `Tags`, `LinkedTestIds`. Keep `Priority` (already exists). Remove `Title` field, replace with `Text`.
- [x] T004 [P] [US3] Rename `RequirementsCoverage` to `AcceptanceCriteriaCoverage` in `src/Spectra.Core/Models/Coverage/RequirementsCoverage.cs` → `AcceptanceCriteriaCoverage.cs`. Rename JSON properties: `total_requirements` → `total_criteria`, `covered_requirements` → `covered_criteria`, `has_requirements_file` → `has_criteria_file`. Add `SourceBreakdown` dictionary field.
- [x] T005 [P] [US3] Update `UnifiedCoverageReport` in `src/Spectra.Core/Models/Coverage/UnifiedCoverageReport.cs`: rename `RequirementsCoverage` property to `AcceptanceCriteriaCoverage`, update JSON property name from `requirements_coverage` to `acceptance_criteria_coverage`.
- [x] T006 [P] [US3] Rename `RequirementsParser` to `AcceptanceCriteriaParser` in `src/Spectra.Core/Parsing/RequirementsParser.cs` → `AcceptanceCriteriaParser.cs`. Update to deserialize `AcceptanceCriterion` model. Keep backward-compatible reading of old `_requirements.yaml` format.
- [x] T007 [P] [US3] Rename `RequirementsCoverageAnalyzer` to `AcceptanceCriteriaCoverageAnalyzer` in `src/Spectra.Core/Coverage/RequirementsCoverageAnalyzer.cs` → `AcceptanceCriteriaCoverageAnalyzer.cs`. Update return type to `AcceptanceCriteriaCoverage`.
- [x] T008 [P] [US3] Update `UnifiedCoverageBuilder` in `src/Spectra.Core/Coverage/UnifiedCoverageBuilder.cs` to use renamed types.
- [x] T009 [P] [US3] Update `CoverageConfig` in `src/Spectra.Core/Models/Config/CoverageConfig.cs`: add `criteria_file`, `criteria_dir`, `criteria_import` properties alongside (not replacing) `requirements_file`.
- [x] T010 [P] [US3] Add `Criteria` field (`List<string>`) to `TestCaseFrontmatter` in `src/Spectra.Core/Models/TestCaseFrontmatter.cs`, `TestCase` in `src/Spectra.Core/Models/TestCase.cs`, and `TestIndexEntry` in `src/Spectra.Core/Models/TestIndexEntry.cs`. Keep existing `Requirements` field for backward compatibility.

### Dashboard Models & Data

- [x] T011 [P] [US3] Rename `RequirementsSectionData` to `AcceptanceCriteriaSectionData` in `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`. Update `CoverageSummaryData.Requirements` property to `AcceptanceCriteria`. Update JSON property names.

### CLI Components

- [x] T012 [P] [US3] Update `CoverageReportWriter` in `src/Spectra.CLI/Coverage/CoverageReportWriter.cs`: rename all "Requirements Coverage" strings to "Acceptance Criteria Coverage" in markdown, text, and JSON output sections.
- [x] T013 [P] [US3] Update `AnalysisPresenter` in `src/Spectra.CLI/Output/AnalysisPresenter.cs`: rename terminology in human-readable output.
- [x] T014 [P] [US3] Update `AnalyzeCommand` in `src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs`: add `--extract-criteria` option, keep `--extract-requirements` as hidden alias (set `IsHidden = true`).
- [x] T015 [US3] Update `AnalyzeHandler` in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`: update method names and references from requirements to criteria terminology. Wire hidden alias to same handler.
- [x] T016 [P] [US3] Rename `ExtractRequirementsResult` to `ExtractCriteriaResult` in `src/Spectra.CLI/Results/ExtractRequirementsResult.cs` → `ExtractCriteriaResult.cs`. Update property names per contracts/cli-commands.md.
- [x] T017 [P] [US3] Update `InitHandler` in `src/Spectra.CLI/Commands/Init/InitHandler.cs`: change template file creation from `_requirements.yaml` to `_criteria_index.yaml` and `sample.criteria.yaml` with criteria format.
- [x] T018 [P] [US3] Update `NextStepHints` in `src/Spectra.CLI/Output/NextStepHints.cs`: change "extract-requirements" references to "extract-criteria".

### Dashboard Frontend

- [x] T019 [P] [US3] Update `dashboard-site/scripts/app.js`: rename "Requirements Coverage" to "Acceptance Criteria Coverage" (lines ~927, 954, 1381-1387, 1444-1458). Rename `renderReqDetails` function.

### SKILL & Agent Content

- [x] T020 [P] [US3] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-coverage.md`: rename "requirements" to "acceptance criteria".
- [x] T021 [P] [US3] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-help.md`: rename all "requirements" references.
- [x] T022 [P] [US3] Update `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`: rename "requirements" to "acceptance criteria", update CLI commands.
- [x] T023 [P] [US3] Update `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`: rename "requirements" to "acceptance criteria", update CLI commands.

### Delete Old Files

- [x] T024 [US3] Delete old `RequirementsWriter` at `src/Spectra.Core/Parsing/RequirementsWriter.cs` (functionality replaced by new CriteriaFileWriter in Phase 3). Delete old `RequirementsExtractor` at `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` (replaced by CriteriaExtractor in Phase 3). Delete `ExtractionResult.cs` and `DuplicateMatch.cs` from `src/Spectra.Core/Models/Coverage/` if no longer needed.

### Tests for Rename

- [x] T025 [P] [US3] Rename `RequirementsParserTests` to `AcceptanceCriteriaParserTests` in `tests/Spectra.Core.Tests/Parsing/`. Update all assertions to use new model field names.
- [x] T026 [P] [US3] Rename `RequirementsCoverageAnalyzerTests` to `AcceptanceCriteriaCoverageAnalyzerTests` in `tests/Spectra.Core.Tests/Coverage/`. Update assertions for renamed properties.
- [x] T027 [P] [US3] Update all test files in `tests/Spectra.CLI.Tests/` referencing old class names, JSON property names, and "Requirements Coverage" strings.
- [x] T028 [P] [US3] Update dashboard tests in `tests/Spectra.CLI.Tests/Dashboard/` referencing old terminology.
- [x] T029 [US3] Run full test suite (`dotnet test`) and fix any remaining compilation errors or failing assertions from the rename.

**Checkpoint**: All 1241+ tests pass with new terminology. `--extract-requirements` hidden alias compiles. No "Requirement" references remain in user-facing output (excluding backward-compat `requirements` frontmatter field).

---

## Phase 3: User Story 1 — Extract Acceptance Criteria (Priority: P1) MVP

**Goal**: Per-document iterative extraction with SHA-256 incremental tracking, producing `.criteria.yaml` files and master index.

**Independent Test**: Run `spectra ai analyze --extract-criteria` against a docs folder, verify per-doc criteria files + index. Change one doc, re-run, verify only changed doc reprocessed.

### New Core Infrastructure

- [x] T030 [P] [US1] Create `CriteriaIndex` and `CriteriaSource` models in `src/Spectra.Core/Models/Coverage/CriteriaIndex.cs` and `CriteriaSource.cs` per data-model.md.
- [x] T031 [P] [US1] Create `CriteriaIndexReader` in `src/Spectra.Core/Parsing/CriteriaIndexReader.cs`: read `_criteria_index.yaml` via YamlDotNet, return `CriteriaIndex`. Handle missing file (return empty index).
- [x] T032 [P] [US1] Create `CriteriaIndexWriter` in `src/Spectra.Core/Parsing/CriteriaIndexWriter.cs`: write `CriteriaIndex` to `_criteria_index.yaml` with atomic temp-file+rename. Include auto-generated header comment.
- [x] T033 [P] [US1] Create `CriteriaFileReader` in `src/Spectra.Core/Parsing/CriteriaFileReader.cs`: read per-document `.criteria.yaml` files, return `List<AcceptanceCriterion>`.
- [x] T034 [P] [US1] Create `CriteriaFileWriter` in `src/Spectra.Core/Parsing/CriteriaFileWriter.cs`: write `List<AcceptanceCriterion>` to a `.criteria.yaml` file with header comments (source doc, hash, timestamp). Atomic write.

### AI Extraction (rewrite)

- [x] T035 [US1] Create `CriteriaExtractor` in `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs`: per-document AI extraction. Accept a single document's text + path. Build prompt instructing AI to extract criteria with RFC 2119 normalization, source section, component, priority. Return `List<AcceptanceCriterion>`. Use standard cancellation token (no `Task.WhenAny` hack). Parse clean JSON response (no truncation recovery needed).

### CLI Handler (extraction flow rewrite)

- [x] T036 [US1] Rewrite extraction flow in `AnalyzeHandler` at `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`: iterate documents from document map, compute SHA-256 per doc via `FileHasher`, compare against `CriteriaIndex` hashes, skip unchanged docs, call `CriteriaExtractor` per changed doc, write per-doc `.criteria.yaml` via `CriteriaFileWriter`, update `CriteriaIndex` via `CriteriaIndexWriter`. Handle per-document failures (continue processing, collect errors, exit code 2 for partial). Support `--force` (ignore hashes) and `--dry-run` (no writes). Generate criterion IDs as `AC-{COMPONENT}-{NNN}`.
- [x] T037 [US1] Create `ExtractCriteriaResult` model in `src/Spectra.CLI/Results/ExtractCriteriaResult.cs` with fields per contracts/cli-commands.md: `documentsProcessed`, `documentsSkipped`, `documentsFailed`, `failedDocuments`, `criteriaExtracted`, `criteriaNew`, `criteriaUpdated`, `criteriaUnchanged`, `orphanedCriteria`, `totalCriteria`, `indexFile`.
- [x] T038 [US1] Update `AnalysisPresenter` in `src/Spectra.CLI/Output/AnalysisPresenter.cs` to display extraction results with per-document progress (processing/skipped/failed counts).
- [x] T039 [US1] Detect orphaned criteria: when a source doc in the index no longer exists on disk, mark its `CriteriaSource` as orphaned in output and warn the user. Do not delete the criteria file.
- [x] T040 [US1] Update `AcceptanceCriteriaCoverageAnalyzer` in `src/Spectra.Core/Coverage/AcceptanceCriteriaCoverageAnalyzer.cs` to read criteria from the master index (iterate all criteria files) instead of a single flat YAML file.

### Tests for US1

- [x] T041 [P] [US1] Create `CriteriaIndexReaderWriterTests` in `tests/Spectra.Core.Tests/Parsing/CriteriaIndexReaderWriterTests.cs`: test read valid index, write and round-trip, empty index, mixed source types.
- [x] T042 [P] [US1] Create `CriteriaFileReaderWriterTests` in `tests/Spectra.Core.Tests/Parsing/CriteriaFileReaderWriterTests.cs`: test read/write per-doc criteria, missing optional fields, malformed YAML.
- [x] T043 [P] [US1] Create incremental extraction tests in `tests/Spectra.CLI.Tests/Commands/ExtractCriteriaTests.cs`: test unchanged hash → skip, changed hash → re-extract, new doc → create, deleted doc → orphan warning, `--force` ignores hashes, `--dry-run` no writes, `--output-format json` correct result, partial failure continues.
- [x] T044 [US1] Run full test suite and verify all tests pass including new extraction tests.

**Checkpoint**: Per-document extraction works end-to-end. Incremental extraction via hashing works. Orphan detection works. No truncation possible. All tests pass.

---

## Phase 4: User Story 2 — Import Criteria (Priority: P1)

**Goal**: Import acceptance criteria from YAML/CSV/JSON files with AI splitting and RFC 2119 normalization.

**Independent Test**: Create a CSV with Jira columns, run `spectra ai analyze --import-criteria ./jira-export.csv`, verify auto-mapped fields, split entries, and normalization.

### Core Import Infrastructure

- [x] T045 [P] [US2] Create `CsvCriteriaImporter` in `src/Spectra.Core/Parsing/CsvCriteriaImporter.cs`: use CsvHelper to parse CSV with priority-ordered column auto-detection per contracts/cli-commands.md. Return `List<AcceptanceCriterion>` with mapped fields. Fail with descriptive error if no `text`-equivalent column found.
- [x] T046 [P] [US2] Create `JsonCriteriaImporter` in `src/Spectra.Core/Parsing/JsonCriteriaImporter.cs`: deserialize JSON file with `criteria` array into `List<AcceptanceCriterion>` using System.Text.Json.
- [x] T047 [P] [US2] Create `CriteriaMerger` in `src/Spectra.Core/Parsing/CriteriaMerger.cs`: merge logic — match by ID first, then by source. Update matched, append new. Replace mode: clear target file, insert new. Return merge stats (merged count, new count).

### AI Splitting

- [x] T048 [US2] Add splitting prompt to `CriteriaExtractor` in `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs`: new method `SplitAndNormalizeAsync(rawText, sourceKey, ct)` that takes a single raw text field (e.g., Jira acceptance criteria bullet list) and returns split, RFC 2119-normalized `List<AcceptanceCriterion>`. Generate IDs as `AC-{SOURCE_KEY}-{N}`.

### CLI Handler (import flow)

- [x] T049 [US2] Add import flow to `AnalyzeHandler` in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`: detect file format by extension (.yaml/.yml → YAML, .csv → CSV, .json → JSON). Parse via appropriate importer. If `--skip-splitting` is false and text fields contain bullet lists, call AI splitting. Write result to `docs/requirements/imported/{filename}.criteria.yaml`. Update master index. Support `--merge` (default) and `--replace` (target file only). Support `--dry-run` and `--output-format json`.
- [x] T050 [P] [US2] Add `--import-criteria`, `--merge`, `--replace`, `--skip-splitting` options to `AnalyzeCommand` in `src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs`.
- [x] T051 [P] [US2] Create `ImportCriteriaResult` model in `src/Spectra.CLI/Results/ImportCriteriaResult.cs` with fields per contracts/cli-commands.md.
- [x] T052 [US2] Update `AnalysisPresenter` to display import results (imported count, split count, merge results).

### Tests for US2

- [x] T053 [P] [US2] Create `CsvCriteriaImporterTests` in `tests/Spectra.Core.Tests/Parsing/CsvCriteriaImporterTests.cs`: standard columns, Jira column names, ADO column names, missing text column → error, empty rows skipped, quoted fields with commas.
- [x] T054 [P] [US2] Create `JsonCriteriaImporterTests` in `tests/Spectra.Core.Tests/Parsing/JsonCriteriaImporterTests.cs`: valid JSON, minimal fields, invalid schema → error, empty array.
- [x] T055 [P] [US2] Create `CriteriaMergerTests` in `tests/Spectra.Core.Tests/Parsing/CriteriaMergerTests.cs`: merge by ID match → update, merge by source match → update, no match → append, replace mode → clear+insert, duplicate detection across files.
- [x] T056 [P] [US2] Create `ImportCriteriaTests` in `tests/Spectra.CLI.Tests/Commands/ImportCriteriaTests.cs`: YAML import, CSV import with Jira columns, JSON import, `--merge` preserves existing, `--replace` clears target only, `--skip-splitting` no AI calls, `--dry-run` no writes, `--output-format json`, missing file → exit 1, `--no-interaction`.
- [x] T057 [US2] Run full test suite and verify all tests pass.

**Checkpoint**: Import from YAML/CSV/JSON works. AI splitting and normalization work. Merge/replace modes work. All tests pass.

---

## Phase 5: User Story 4 — List & Filter Criteria (Priority: P2)

**Goal**: Browse and filter criteria by source type, component, and priority with coverage status.

**Independent Test**: After extracting and importing criteria, run `spectra ai analyze --list-criteria --component checkout` and verify filtered output with coverage status.

### Implementation

- [x] T058 [P] [US4] Add `--list-criteria`, `--source-type`, `--component`, `--priority` options to `AnalyzeCommand` in `src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs`.
- [x] T059 [US4] Add list flow to `AnalyzeHandler` in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`: read all criteria from index, apply filters (source type, component, priority), compute coverage status per criterion (check `criteria`/`requirements` fields in test frontmatter), return filtered list with coverage info.
- [x] T060 [P] [US4] Create `ListCriteriaResult` model in `src/Spectra.CLI/Results/ListCriteriaResult.cs` with fields per contracts/cli-commands.md.
- [x] T061 [US4] Update `AnalysisPresenter` to display criteria list in human-readable table format with coverage status indicators. Show empty-state guidance when no criteria exist.

### Tests for US4

- [x] T062 [P] [US4] Create `ListCriteriaTests` in `tests/Spectra.CLI.Tests/Commands/ListCriteriaTests.cs`: all criteria listed, `--source-type jira` filter, `--component checkout` filter, `--priority high` filter, combined filters, no criteria → guidance message, `--output-format json`.
- [x] T063 [US4] Run full test suite and verify all tests pass.

**Checkpoint**: List and filter criteria works with coverage status. All tests pass.

---

## Phase 6: User Story 5 — Generation & Update Integration (Priority: P2)

**Goal**: Auto-load criteria as generation context, include `criteria` field in generated test frontmatter, detect criteria changes in update flow.

**Independent Test**: Create criteria for "checkout", run `spectra ai generate checkout`, verify frontmatter includes `criteria` references. Modify criterion text, run `spectra ai update checkout`, verify OUTDATED classification.

### Generation Integration

- [x] T064 [US5] Update `GroundedPromptBuilder` in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`: add method to load related criteria (by component match and source doc mapping) from criteria index/files. Append criteria context to the generation prompt with format: `AC-ID: criterion text`.
- [x] T065 [US5] Update generation prompt template in `GroundedPromptBuilder` to instruct AI to include `criteria` field in generated test JSON output, listing relevant criterion IDs.
- [x] T066 [US5] Update test case writing flow to include `criteria` field in YAML frontmatter when writing generated tests.

### Update Integration

- [x] T067 [US5] Update `TestClassifier` in `src/Spectra.Core/Update/TestClassifier.cs`: when classifying tests, check if any linked criteria (from test's `criteria` field) have changed text since last extraction. If changed → classify as OUTDATED. If criteria ID no longer exists → classify as ORPHANED.
- [x] T068 [US5] Update update flow in CLI to detect new unlinked criteria: after classification, check if criteria exist for the suite's component that have no linked tests. If found, suggest "N new acceptance criteria found for {suite} — consider generating additional tests".

### Tests for US5

- [x] T069 [P] [US5] Create generation integration tests in `tests/Spectra.CLI.Tests/Commands/GenerateCriteriaIntegrationTests.cs`: generate with criteria present → criteria in prompt, generate without criteria → no regression, generated frontmatter includes `criteria` field.
- [x] T070 [P] [US5] Create update integration tests: criteria text changed → OUTDATED, new criteria added → suggestion, criteria removed → ORPHANED.
- [x] T071 [US5] Run full test suite and verify all tests pass.

**Checkpoint**: Generation auto-loads criteria and produces `criteria` frontmatter. Update flow detects criteria changes. All tests pass.

---

## Phase 7: User Story 6 — Coverage Dashboard (Priority: P3)

**Goal**: "Acceptance Criteria Coverage" section in dashboard with per-source-type breakdown.

**Independent Test**: Generate dashboard after extracting and importing criteria. Verify per-source-type breakdown with percentages and drill-down.

### Implementation

- [x] T072 [P] [US6] Add `SourceCoverageStats` model in `src/Spectra.Core/Models/Coverage/SourceCoverageStats.cs` per data-model.md.
- [x] T073 [US6] Update `DataCollector` in `src/Spectra.CLI/Dashboard/DataCollector.cs`: populate `AcceptanceCriteriaSectionData` with per-source-type breakdown from `AcceptanceCriteriaCoverage.SourceBreakdown`.
- [x] T074 [US6] Update `dashboard-site/scripts/app.js`: render per-source-type breakdown (document, jira, manual, etc.) under "Acceptance Criteria Coverage" section with progress bars per source type. Update drill-down to show per-criterion coverage status. Update empty state to reference `--extract-criteria` and `--import-criteria`.

### Tests for US6

- [x] T075 [P] [US6] Create dashboard criteria coverage tests in `tests/Spectra.CLI.Tests/Dashboard/`: verify "Acceptance Criteria Coverage" label, source type breakdown, empty state guidance.
- [x] T076 [US6] Run full test suite and verify all tests pass.

**Checkpoint**: Dashboard shows per-source-type criteria coverage. All tests pass.

---

## Phase 8: User Story 7 — SKILL & Agent Updates (Priority: P3)

**Goal**: New `spectra-criteria` bundled SKILL. Agent prompts include criteria awareness.

**Independent Test**: Run `spectra init`, verify `spectra-criteria/SKILL.md` is created. Verify agent prompts reference criteria.

### Implementation

- [x] T077 [P] [US7] Create `spectra-criteria` SKILL content at `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md` per spec: extract, import, list commands with `--output-format json --verbosity quiet`, JSON parsing instructions, next-step suggestions.
- [x] T078 [P] [US7] Update `SkillContent.cs` in `src/Spectra.CLI/Skills/SkillContent.cs`: add criteria SKILL as 7th entry, embed content.
- [x] T079 [P] [US7] Update `SkillsManifest.cs` in `src/Spectra.CLI/Skills/SkillsManifest.cs`: add SHA-256 hash for criteria SKILL file.
- [x] T080 [US7] Update agent prompts (already partially done in T022/T023): add criteria awareness section to generation agent — check for criteria before generating, inform user of criteria count. Add criteria reference section to execution agent — show linked criteria during test execution.
- [x] T081 [US7] Update `InitHandler` in `src/Spectra.CLI/Commands/Init/InitHandler.cs`: include `spectra-criteria/SKILL.md` in SKILL file creation during `spectra init`.

### Tests for US7

- [x] T082 [P] [US7] Create SKILL tests: verify `spectra-criteria/SKILL.md` present after init, `spectra update-skills` updates criteria SKILL, hash tracked in manifest.
- [x] T083 [US7] Run full test suite and verify all tests pass.

**Checkpoint**: New SKILL is bundled and deployed via init/update-skills. Agent prompts are criteria-aware. All tests pass.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, cleanup, and documentation

- [x] T084 [P] Update `NextStepHints` in `src/Spectra.CLI/Output/NextStepHints.cs`: after init → suggest `--extract-criteria`, after extract → suggest coverage or generate, after import → suggest list or coverage.
- [x] T085 [P] Update `CLAUDE.md` at project root with new CLI commands (`--extract-criteria`, `--import-criteria`, `--list-criteria`), updated project structure (new files), and spec 023 in recent changes.
- [x] T086 Run `spectra validate` on a sample project to verify no schema regressions.
- [x] T087 Run full test suite (`dotnet test`) — final verification all tests pass.
- [x] T088 Run quickstart.md validation: execute the key workflows end-to-end on a sample project.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational/Rename (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1 Extract (Phase 3)**: Depends on Phase 2 completion
- **US2 Import (Phase 4)**: Depends on Phase 2 completion. Can run in parallel with US1.
- **US4 List (Phase 5)**: Depends on Phase 3 (needs criteria index infrastructure)
- **US5 Generation/Update (Phase 6)**: Depends on Phase 3 (needs criteria loading)
- **US6 Dashboard (Phase 7)**: Depends on Phase 3 (needs coverage data)
- **US7 SKILL/Agents (Phase 8)**: Depends on Phase 2 (needs renamed terminology). Can run in parallel with US1/US2.
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US3 Rename (P1)**: Foundational — BLOCKS all others
- **US1 Extract (P1)**: Can start after US3 — no dependency on US2
- **US2 Import (P1)**: Can start after US3 — no dependency on US1 (but shares CriteriaFileWriter from US1, so start US1 first or extract shared code in Phase 2)
- **US4 List (P2)**: Depends on US1 (needs index reader infrastructure)
- **US5 Generation/Update (P2)**: Depends on US1 (needs criteria loading), independent of US2/US4
- **US6 Dashboard (P3)**: Depends on US1 (needs coverage analyzer updates), independent of US2/US4/US5
- **US7 SKILL/Agents (P3)**: Only depends on US3 rename, can run early

### Within Each User Story

- Models before services/parsers
- Parsers/readers before CLI handler integration
- CLI handler before presenter updates
- Core implementation before tests
- Tests validate the complete story

### Parallel Opportunities

- Phase 2: T003-T028 — most rename tasks are in different files and can run in parallel
- Phase 3: T030-T034 — all new reader/writer classes are independent
- Phase 4: T045-T047 — all importers are independent
- Phase 4 can start in parallel with Phase 3 after Phase 2 completes (but T049 needs CriteriaFileWriter from T034)
- Phase 7 and Phase 8 can run in parallel with Phases 3-6

---

## Parallel Example: Phase 2 Rename

```
# All model renames in parallel (different files):
T003: AcceptanceCriterion model
T004: AcceptanceCriteriaCoverage model
T005: UnifiedCoverageReport update
T006: AcceptanceCriteriaParser rename
T007: AcceptanceCriteriaCoverageAnalyzer rename
T008: UnifiedCoverageBuilder update
T009: CoverageConfig update
T010: TestCase frontmatter Criteria field
T011: Dashboard model rename
```

## Parallel Example: Phase 3 Infrastructure

```
# All new reader/writer classes in parallel (new files):
T030: CriteriaIndex + CriteriaSource models
T031: CriteriaIndexReader
T032: CriteriaIndexWriter
T033: CriteriaFileReader
T034: CriteriaFileWriter
```

## Parallel Example: Phase 4 Importers

```
# All importers in parallel (new files):
T045: CsvCriteriaImporter
T046: JsonCriteriaImporter
T047: CriteriaMerger
```

---

## Implementation Strategy

### MVP First (US3 + US1 Only)

1. Complete Phase 1: Setup (add CsvHelper)
2. Complete Phase 2: Terminology rename (US3)
3. Complete Phase 3: Per-document extraction (US1)
4. **STOP and VALIDATE**: Extract criteria from docs, verify per-doc files + index, test incremental
5. This alone solves the truncation bug and delivers the core value

### Incremental Delivery

1. Setup + Rename → Foundation ready
2. Add US1 Extract → MVP — truncation fixed, iterative extraction works
3. Add US2 Import → External sources supported
4. Add US4 List → Browse and filter criteria
5. Add US5 Generation → Criteria-aware test generation
6. Add US6 Dashboard → Visual coverage breakdown
7. Add US7 SKILL → Copilot Chat integration
8. Polish → Documentation, final validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The `requirements` field in test frontmatter is kept for backward compat — `criteria` is the new field
- CsvHelper is the only new NuGet dependency
