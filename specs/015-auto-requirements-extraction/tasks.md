# Tasks: Automatic Requirements Extraction

**Input**: Design documents from `/specs/015-auto-requirements-extraction/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Foundational (Core Models & Writer)

**Purpose**: Create the data models and YAML write/merge infrastructure that all user stories depend on. Includes ID allocation logic (US3) and duplicate detection since these are intrinsic to the writer.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T001 [P] Create `ExtractionResult` record in `src/Spectra.Core/Models/Coverage/ExtractionResult.cs` with fields: `Extracted` (List\<RequirementDefinition\>), `Merged` (List\<RequirementDefinition\>), `Duplicates` (List\<DuplicateMatch\>), `SkippedCount` (int), `TotalInFile` (int), `SourceDocCount` (int). Immutable record type.

- [ ] T002 [P] Create `DuplicateMatch` record in `src/Spectra.Core/Models/Coverage/DuplicateMatch.cs` with fields: `NewTitle` (string), `ExistingId` (string), `ExistingTitle` (string), `Source` (string). Immutable record type.

- [ ] T003 Create `RequirementsWriter` in `src/Spectra.Core/Parsing/RequirementsWriter.cs`. Must implement: (a) `MergeAndWriteAsync(string filePath, IReadOnlyList<RequirementDefinition> newRequirements)` — reads existing file via `RequirementsParser.ParseAsync()`, detects duplicates by case-insensitive normalized title comparison (lowercase, trim, strip punctuation) plus substring matching with minimum 15-char threshold, allocates sequential IDs from max existing + 1 (REQ-NNN zero-padded to 3+ digits, never reuse gaps), appends new non-duplicate requirements, writes atomically (temp file then rename), creates parent directories if missing. (b) `AllocateIds(IReadOnlyList<RequirementDefinition> existing, IReadOnlyList<RequirementDefinition> newItems)` — assigns REQ-NNN IDs to new items. (c) `DetectDuplicates(IReadOnlyList<RequirementDefinition> existing, IReadOnlyList<RequirementDefinition> candidates)` — returns ExtractionResult with merged/duplicate lists. Uses YamlDotNet serializer with `UnderscoreNamingConvention` matching `RequirementsParser`. Returns `ExtractionResult`.

- [ ] T004 Create unit tests in `tests/Spectra.Core.Tests/Parsing/RequirementsWriterTests.cs`. Test cases: (a) writes new file when none exists with correct YAML format, (b) merges into existing file preserving all existing entries unchanged, (c) detects exact title duplicates (case-insensitive) and skips them, (d) detects substring duplicates above threshold and skips them, (e) allocates IDs sequentially from REQ-001 on empty file, (f) continues from highest existing ID (REQ-012 → REQ-013), (g) does not reuse gap IDs (deleted REQ-005 still allocates from max), (h) creates parent directories, (i) handles malformed YAML gracefully (error, no overwrite), (j) handles empty new requirements list (no-op), (k) atomic write — verifies temp-then-rename pattern.

**Checkpoint**: RequirementsWriter can merge, deduplicate, and write requirements YAML with correct IDs. US3 acceptance scenarios validated by T004.

---

## Phase 2: User Story 1 - Standalone Requirements Extraction (Priority: P1) 🎯 MVP

**Goal**: Users can run `spectra ai analyze --extract-requirements` to scan all documentation and produce a valid `_requirements.yaml` without generating tests.

**Independent Test**: Run the extraction command against sample docs and verify the output file has correct structure, IDs, sources, and priorities.

### Implementation for User Story 1

- [ ] T005 [US1] Create `RequirementsExtractor` in `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs`. Follows `CopilotGenerationAgent` pattern. Constructor takes `ProviderConfig`. Methods: (a) `ExtractAsync(IReadOnlyList<SourceDocument> documents, IReadOnlyList<RequirementDefinition> existingRequirements, CancellationToken ct)` — builds extraction prompt with: document content, existing requirement titles (to avoid duplicates at AI level), RFC 2119 priority rules (MUST/SHALL/critical = high, SHOULD/recommended = medium, MAY/optional = low, default = medium), output format instruction (JSON array of `{title, source, priority}`). Invokes Copilot SDK via `CopilotService`. Parses AI response into `List<RequirementDefinition>` (without IDs — IDs assigned by `RequirementsWriter`). Returns extracted list. (b) `CheckAvailabilityAsync()` — delegates to Copilot SDK availability check. Handles errors gracefully (returns empty list with error message, never throws).

- [ ] T006 [US1] Add `--extract-requirements` option to `AnalyzeCommand` in `src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs`. Register as `Option<bool>("--extract-requirements", "Extract requirements from documentation")`. Wire to handler.

- [ ] T007 [US1] Implement `RunExtractRequirementsAsync` method in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`. Flow: (a) load config, (b) ensure document index via `DocumentIndexService.EnsureIndexAsync()`, (c) load all source documents via `SourceDocumentLoader.LoadAllAsync()`, (d) load existing requirements via `RequirementsParser.ParseAsync(config.Coverage.RequirementsFile)`, (e) create agent via `AgentFactory.CreateAgentAsync()`, (f) instantiate `RequirementsExtractor` and call `ExtractAsync()`, (g) pass results through `RequirementsWriter.MergeAndWriteAsync()` (skip write if `--dry-run`), (h) display results using Spectre.Console: table of new requirements (ID, title, source, priority), summary line ("X new requirements extracted, Y duplicates skipped, Z total in file"). Return exit code 0 on success.

- [ ] T008 [US1] Add dry-run support to the extraction flow in `AnalyzeHandler.RunExtractRequirementsAsync`. When `--dry-run` is set: run extraction and dedup logic normally, display what would be written (table of new requirements with proposed IDs), display summary with "(dry run — no files written)" suffix, skip `RequirementsWriter` file write, return exit code 0.

- [ ] T009 [US1] Create integration tests in `tests/Spectra.CLI.Tests/Commands/AnalyzeExtractRequirementsTests.cs`. Test cases: (a) extracts requirements from sample docs and creates valid YAML file, (b) merges into existing requirements file without overwriting, (c) dry-run mode produces output but writes no file, (d) handles empty documentation (no docs found — reports and exits cleanly), (e) handles AI extraction returning empty results, (f) reports correct counts (new, duplicates, total).

**Checkpoint**: `spectra ai analyze --extract-requirements` produces a valid `_requirements.yaml` from project documentation. US1 is fully functional and independently testable. US4 (priority inference) is handled by the extraction prompt in T005.

---

## Phase 3: User Story 2 - Integrated Extraction During Test Generation (Priority: P2)

**Goal**: Requirements extraction runs automatically during `spectra ai generate`, with interactive review in interactive mode. Generated tests reference extracted requirement IDs.

**Independent Test**: Run test generation against a sample document and verify (a) requirements file is created/updated, (b) generated tests contain `requirements` field with correct IDs.

### Implementation for User Story 2

- [ ] T010 [US2] Create `RequirementsReviewer` in `src/Spectra.CLI/Review/RequirementsReviewer.cs`. Follows `TestReviewer` pattern using Spectre.Console. Methods: (a) `ReviewAsync(IReadOnlyList<RequirementDefinition> extractedRequirements, CancellationToken ct)` — displays requirements in a numbered table (title, source, priority), offers per-item actions (Accept / Reject / Edit title / Edit priority), offers bulk actions (Accept All / Reject All), returns `ReviewResult<RequirementDefinition>` with accepted list. (b) Uses `AnsiConsole.Prompt` for selections. Consistent UX with existing test review flow.

- [ ] T011 [US2] Modify `GenerateHandler` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` to insert extraction step. After document loading (~line 124) and before agent creation (~line 193), add: (a) load existing requirements via `RequirementsParser.ParseAsync(config.Coverage.RequirementsFile)`, (b) create `RequirementsExtractor` and call `ExtractAsync()` with loaded source documents (single document in direct mode, selected doc in interactive mode), (c) merge via `RequirementsWriter.DetectDuplicates()`, (d) if interactive mode: call `RequirementsReviewer.ReviewAsync()` with merged requirements to let user approve/edit/reject, (e) if non-interactive mode: auto-accept all merged requirements, (f) write approved requirements via `RequirementsWriter.MergeAndWriteAsync()` (skip if `--dry-run`), (g) display extraction summary (new count, duplicates, total), (h) store full requirements list (existing + newly written) for passing to generation prompt.

- [ ] T012 [US2] Enhance the generation prompt in `GenerateHandler` (in `BuildPrompt()` or the agent prompt template). Add: (a) full requirements list as context block (formatted as ID: title pairs), (b) instruction: "For each generated test case, populate the `requirements` field in YAML frontmatter with the IDs of requirements the test validates. A test may cover multiple requirements. Every generated test MUST reference at least one requirement if requirements are available.", (c) example showing `requirements: [REQ-001, REQ-003]` in frontmatter.

- [ ] T013 [US2] Create integration tests in `tests/Spectra.CLI.Tests/Commands/GenerateHandlerRequirementsTests.cs`. Test cases: (a) generation creates requirements file and generated tests reference requirement IDs, (b) generation merges into existing requirements file, (c) dry-run shows extracted requirements without writing, (d) non-interactive mode auto-accepts all extracted requirements, (e) generated test frontmatter contains valid `requirements` field entries matching extracted requirement IDs.

**Checkpoint**: `spectra ai generate` automatically extracts requirements before generating tests. Interactive mode shows review UI. Generated tests include `requirements` field references. US2 is fully functional.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases, error handling, and validation across all stories

- [ ] T014 [P] Add edge case handling to `RequirementsWriter`: (a) when requirements file exists but is malformed YAML — log error, do not overwrite, return error result, (b) when document has no extractable requirements — return ExtractionResult with empty Merged list and log info message, (c) when all extracted requirements are duplicates — write nothing, report "0 new, X duplicates skipped".

- [ ] T015 [P] Add edge case handling to `RequirementsExtractor`: (a) when AI returns malformed JSON — log warning, return empty list, (b) when AI returns requirements without titles — filter them out with warning, (c) when source document is very large — process in single pass (no chunking needed, Copilot SDK handles context limits).

- [ ] T016 Validate quickstart.md scenarios end-to-end: run `spectra ai analyze --extract-requirements` and `spectra ai analyze --extract-requirements --dry-run` against test fixtures, verify output matches documented examples.

- [ ] T017 Update CLAUDE.md Recent Changes section with 015-auto-requirements-extraction summary: AI-powered extraction of testable requirements from documentation, `--extract-requirements` flag on analyze command, integrated extraction during `spectra ai generate`, interactive review, YAML merge with dedup and sequential ID allocation, priority inference from RFC 2119 keywords.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — can start immediately
- **US1 (Phase 2)**: Depends on Phase 1 completion (needs RequirementsWriter + models)
- **US2 (Phase 3)**: Depends on Phase 2 completion (needs RequirementsExtractor from US1)
- **Polish (Phase 4)**: Depends on Phase 2 and Phase 3 completion

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 1. No dependencies on other stories. **This is the MVP.**
- **US2 (P2)**: Depends on US1 (reuses RequirementsExtractor). Can start once US1's T005 is complete.
- **US3 (P2)**: ID allocation is built into RequirementsWriter (Phase 1 T003). Validated by T004 tests. No separate implementation phase needed.
- **US4 (P3)**: Priority inference is built into the extraction prompt (Phase 2 T005). Validated by extraction tests. No separate implementation phase needed.

### Within Each Phase

- Models (T001, T002) before writer (T003)
- Writer (T003) before writer tests (T004)
- Extractor (T005) before handler integration (T006, T007)
- Handler integration before tests (T009)
- Reviewer (T010) before generate handler modification (T011)

### Parallel Opportunities

- T001 and T002 can run in parallel (different files, independent models)
- T014 and T015 can run in parallel (different files, independent edge cases)
- Within Phase 2: T006 (command registration) and T005 (extractor) can start in parallel
- US1 and US2 share the extractor (T005), so US2 implementation starts after T005 completes

---

## Parallel Example: Phase 1

```bash
# Launch model creation in parallel:
Task T001: "Create ExtractionResult in src/Spectra.Core/Models/Coverage/ExtractionResult.cs"
Task T002: "Create DuplicateMatch in src/Spectra.Core/Models/Coverage/DuplicateMatch.cs"

# Then sequential:
Task T003: "Create RequirementsWriter in src/Spectra.Core/Parsing/RequirementsWriter.cs"
Task T004: "Create RequirementsWriter tests in tests/Spectra.Core.Tests/Parsing/RequirementsWriterTests.cs"
```

## Parallel Example: Phase 2

```bash
# Launch in parallel (different files):
Task T005: "Create RequirementsExtractor in src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs"
Task T006: "Add --extract-requirements option to src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs"

# Then sequential:
Task T007: "Implement RunExtractRequirementsAsync in AnalyzeHandler"
Task T008: "Add dry-run support to extraction flow"
Task T009: "Create integration tests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational (models + writer)
2. Complete Phase 2: US1 (extractor + analyze command)
3. **STOP and VALIDATE**: Run `spectra ai analyze --extract-requirements` against real docs
4. Verify requirements file is valid YAML with correct IDs, sources, priorities
5. Run `spectra ai analyze --coverage` to confirm coverage analysis works

### Incremental Delivery

1. Phase 1 → Foundation ready (models + writer with full dedup/ID logic)
2. Phase 2 (US1) → Standalone extraction works → **MVP!**
3. Phase 3 (US2) → Generation integration + interactive review → Full feature
4. Phase 4 → Polish, edge cases, documentation → Production-ready

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- US3 (ID allocation) and US4 (priority) are built into the foundational writer and extraction prompt respectively — no separate implementation phases needed, validated by tests in their parent phases
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
