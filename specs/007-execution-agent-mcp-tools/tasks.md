# Tasks: Bundled Execution Agent & MCP Data Tools

**Input**: Design documents from `/specs/007-execution-agent-mcp-tools/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, embedded resources, and directory structure

- [x] T001 Create agent prompt embedded resource file at src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md
- [x] T002 [P] Create skill embedded resource file at src/Spectra.CLI/Agent/Resources/SKILL.md (identical content to T001)
- [x] T003 [P] Update src/Spectra.CLI/Spectra.CLI.csproj to include embedded resources
- [x] T004 [P] Create src/Spectra.MCP/Tools/Data/ directory for new MCP data tools

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and utilities that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T005 Create ValidationErrorModel record in src/Spectra.Core/Models/Validation/ValidationErrorModel.cs per data-model.md
  - Note: Extended existing ValidationError in ValidationResult.cs with LineNumber and FieldName fields
- [x] T006 [P] Create ValidationWarningModel record in src/Spectra.Core/Models/Validation/ValidationWarningModel.cs per data-model.md
  - Note: Using existing ValidationWarning in ValidationResult.cs
- [x] T007 [P] Create ValidationResultModel record in src/Spectra.Core/Models/Validation/ValidationResultModel.cs per data-model.md
  - Note: Extended existing ValidationResult with TotalFiles and ValidFiles fields
- [x] T008 [P] Create IndexRebuildResult record in src/Spectra.Core/Models/Index/IndexRebuildResult.cs per data-model.md
- [x] T009 [P] Create CoverageGap record in src/Spectra.Core/Models/Coverage/CoverageGap.cs per data-model.md
  - Note: Created DocCoverageGap in AnalyzeCoverageGapsTool.cs to avoid conflict with existing CoverageGap
- [x] T010 [P] Create GapSeverity enum in src/Spectra.Core/Models/Coverage/GapSeverity.cs per data-model.md
  - Note: Created DocGapSeverity in AnalyzeCoverageGapsTool.cs to avoid conflict with existing GapSeverity
- [x] T011 [P] Create CoverageAnalysisResult record in src/Spectra.Core/Models/Coverage/CoverageAnalysisResult.cs per data-model.md
  - Note: Result returned directly from AnalyzeCoverageGapsTool
- [x] T012 Create AgentResourceLoader utility in src/Spectra.CLI/Agent/AgentResourceLoader.cs for reading embedded resources

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Run Tests with AI Assistant (Priority: P1)

**Goal**: Provide bundled execution agent prompt that works with Copilot Chat, Copilot CLI, and Claude

**Independent Test**: Run `spectra init`, open Copilot Chat, invoke `@spectra-execution`, verify agent responds with suite selection

### Implementation for User Story 1

- [x] T013 [US1] Write full agent prompt content per contracts/agent-prompt.md in src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md
- [x] T014 [US1] Add YAML frontmatter with name and description to agent prompt file
- [x] T015 [US1] Include workflow section (list suites, start run, present test, collect result, advance, finalize)
- [x] T016 [US1] Include presentation rules (one test at a time, numbered steps, progress after result)
- [x] T017 [US1] Include natural language result mapping table (pass/fail/blocked/skip variations)
- [x] T018 [US1] Include bug logging section with Azure DevOps MCP integration
- [x] T019 [US1] Copy final prompt to src/Spectra.CLI/Agent/Resources/SKILL.md (must be identical)
- [x] T020 [US1] Add version comment to both files (<!-- SPECTRA Execution Agent v1.0.0 -->)

**Checkpoint**: Agent prompt files complete and ready for installation via init command

---

## Phase 4: User Story 2 - Validate Test Files (Priority: P1)

**Goal**: MCP tool that validates test files against SPECTRA schema with structured error output

**Independent Test**: Create test file with missing ID, call `validate_tests`, verify error code MISSING_ID returned

### Implementation for User Story 2

- [x] T021 [P] [US2] Create ValidateTestsTool class in src/Spectra.MCP/Tools/Data/ValidateTestsTool.cs implementing IMcpTool
- [x] T022 [US2] Implement Description property returning "Validate test files against SPECTRA schema"
- [x] T023 [US2] Implement ParameterSchema with optional suite parameter per contracts/mcp-tools.md
- [x] T024 [US2] Implement ExecuteAsync: parse suite parameter, scan test files
- [x] T025 [US2] Integrate with existing TestCaseParser for file parsing
- [x] T026 [US2] Integrate with existing TestValidator for validation rules
- [x] T027 [US2] Build ValidationResultModel with errors and warnings arrays per data-model.md
- [x] T028 [US2] Return McpToolResponse<ValidationResultModel> with proper error codes per contracts/mcp-tools.md
- [x] T029 [US2] Handle SUITE_NOT_FOUND, TESTS_DIR_NOT_FOUND error cases
- [x] T030 [US2] Register validate_tests tool in src/Spectra.MCP/Program.cs

**Checkpoint**: validate_tests MCP tool complete and testable independently

---

## Phase 5: User Story 3 - Rebuild Index Files (Priority: P2)

**Goal**: MCP tool that regenerates _index.json files from test files on disk

**Independent Test**: Add test file manually, call `rebuild_indexes`, verify new entry in _index.json

### Implementation for User Story 3

- [x] T031 [P] [US3] Create RebuildIndexesTool class in src/Spectra.MCP/Tools/Data/RebuildIndexesTool.cs implementing IMcpTool
- [x] T032 [US3] Implement Description property returning "Rebuild _index.json files from test files"
- [x] T033 [US3] Implement ParameterSchema with optional suite parameter per contracts/mcp-tools.md
- [x] T034 [US3] Implement ExecuteAsync: enumerate suites, scan test files per suite
- [x] T035 [US3] Integrate with existing IndexGenerator for index generation
- [x] T036 [US3] Integrate with existing IndexWriter for atomic index writes
- [x] T037 [US3] Track files_added and files_removed counts by comparing with previous index
- [x] T038 [US3] Build IndexRebuildResult with suites_processed, tests_indexed, index_paths per data-model.md
- [x] T039 [US3] Return McpToolResponse<IndexRebuildResult> per contracts/mcp-tools.md
- [x] T040 [US3] Handle TESTS_DIR_NOT_FOUND, SUITE_NOT_FOUND, INDEX_WRITE_ERROR error cases
- [x] T041 [US3] Register rebuild_indexes tool in src/Spectra.MCP/Program.cs

**Checkpoint**: rebuild_indexes MCP tool complete and testable independently

---

## Phase 6: User Story 4 - Analyze Coverage Gaps (Priority: P2)

**Goal**: MCP tool that compares docs folder against test source_refs to identify uncovered areas

**Independent Test**: Create doc file with no test coverage, call `analyze_coverage_gaps`, verify doc appears in gaps list

### Implementation for User Story 4

- [x] T042 [P] [US4] Create AnalyzeCoverageGapsTool class in src/Spectra.MCP/Tools/Data/AnalyzeCoverageGapsTool.cs implementing IMcpTool
- [x] T043 [US4] Implement Description property returning "Analyze documentation coverage gaps"
- [x] T044 [US4] Implement ParameterSchema with optional suite and docs_path parameters per contracts/mcp-tools.md
- [x] T045 [US4] Implement ExecuteAsync: enumerate docs using recursive glob docs/**/*.md
- [x] T046 [US4] Collect all source_refs from test files in target suite(s)
- [x] T047 [US4] Normalize paths (relative, forward slashes) for comparison
- [x] T048 [US4] Calculate coverage gaps as set difference: docs - source_refs
- [x] T049 [US4] Calculate severity per doc (High: >10KB or >5 headings, Medium: >5KB or >2 headings, Low: default)
- [x] T050 [US4] Extract document title from first H1 or use filename as fallback
- [x] T051 [US4] Build CoverageAnalysisResult with docs_scanned, docs_covered, coverage_percent, gaps per data-model.md
- [x] T052 [US4] Return McpToolResponse<CoverageAnalysisResult> per contracts/mcp-tools.md
- [x] T053 [US4] Handle DOCS_DIR_NOT_FOUND, TESTS_DIR_NOT_FOUND, SUITE_NOT_FOUND error cases
- [x] T054 [US4] Register analyze_coverage_gaps tool in src/Spectra.MCP/Program.cs

**Checkpoint**: analyze_coverage_gaps MCP tool complete and testable independently

---

## Phase 7: User Story 5 - Initialize Repository with Agent Files (Priority: P2)

**Goal**: Update spectra init command to install agent prompt files

**Independent Test**: Run `spectra init` in fresh repo, verify .github/agents/ and .github/skills/ files created

### Implementation for User Story 5

- [x] T055 [US5] Add InstallAgentFilesAsync method to src/Spectra.CLI/Commands/Init/InitHandler.cs
- [x] T056 [US5] Use AgentResourceLoader (T012) to read embedded agent prompt content
- [x] T057 [US5] Create .github/agents/ directory if not exists
- [x] T058 [US5] Create .github/skills/spectra-execution/ directory if not exists
- [x] T059 [US5] Write spectra-execution.agent.md to .github/agents/ (skip if exists and not --force)
- [x] T060 [US5] Write SKILL.md to .github/skills/spectra-execution/ (skip if exists and not --force)
- [x] T061 [US5] Log "Agent files installed" or "Agent files exist, skipping (use --force to overwrite)"
- [x] T062 [US5] Call InstallAgentFilesAsync from existing ExecuteAsync in InitHandler
- [x] T063 [US5] Verify --force flag handling respects existing ForceOption behavior

**Checkpoint**: spectra init installs agent files correctly with --force handling

---

## Phase 8: User Story 6 - Log Bugs from Failed Tests (Priority: P3)

**Goal**: Agent prompt includes bug logging integration with Azure DevOps MCP

**Independent Test**: Agent prompt includes bug logging section per contracts/agent-prompt.md (no code changes needed - prompt content only)

### Implementation for User Story 6

- [x] T064 [US6] Verify bug logging section in agent prompt includes title format "[SPECTRA] {test_title} - {failure_summary}"
- [x] T065 [US6] Verify bug logging section includes priority mapping (high→P1, medium→P2, low→P3)
- [x] T066 [US6] Verify fallback behavior documented (copyable bug details when no MCP connected)

**Checkpoint**: Bug logging integration documented in agent prompt (relies on external Azure DevOps MCP)

---

## Phase 9: Documentation

**Goal**: Provide usage guides for each orchestrator

### Implementation

- [x] T067 [P] Create docs/execution-agent/copilot-chat.md with VS Code setup and @agent invocation
- [x] T068 [P] Create docs/execution-agent/copilot-cli.md with CLI installation and skill discovery
- [x] T069 [P] Create docs/execution-agent/claude.md with MCP configuration and project instructions
- [x] T070 [P] Create docs/execution-agent/generic-mcp.md with MCP protocol overview and tool reference

**Checkpoint**: All documentation complete for orchestrator onboarding

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T071 Add unit tests for ValidateTestsTool in tests/Spectra.MCP.Tests/Tools/Data/ValidateTestsToolTests.cs
- [x] T072 [P] Add unit tests for RebuildIndexesTool in tests/Spectra.MCP.Tests/Tools/Data/RebuildIndexesToolTests.cs
- [x] T073 [P] Add unit tests for AnalyzeCoverageGapsTool in tests/Spectra.MCP.Tests/Tools/Data/AnalyzeCoverageGapsToolTests.cs
- [x] T074 [P] Add unit tests for InitHandler agent file installation in tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs
  - Note: Added 5 new tests to existing InitCommandTests.cs
- [ ] T075 Run quickstart.md validation scenarios end-to-end
- [ ] T076 Verify all MCP tools complete in <5s for 500 test file repository

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup (T001-T004) completion - BLOCKS all user stories
- **User Stories (Phase 3-8)**: All depend on Foundational phase completion
  - US1 (Agent Prompt): Can proceed independently
  - US2 (Validate): Can proceed independently
  - US3 (Rebuild): Can proceed independently
  - US4 (Coverage): Can proceed independently
  - US5 (Init): Depends on US1 (needs agent files to install)
  - US6 (Bug Logging): Depends on US1 (content in agent prompt)
- **Documentation (Phase 9)**: Can proceed after US1 (references agent prompt)
- **Polish (Phase 10)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies on other stories
- **User Story 2 (P1)**: No dependencies on other stories
- **User Story 3 (P2)**: No dependencies on other stories
- **User Story 4 (P2)**: No dependencies on other stories
- **User Story 5 (P2)**: Depends on US1 (agent files must exist to install)
- **User Story 6 (P3)**: Depends on US1 (content is in agent prompt)

### Within Each User Story

- Models before tools
- Tools before registration
- Registration before testing
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003, T004)
- All Foundational model tasks marked [P] can run in parallel (T006-T011)
- US2/US3/US4 tool creation tasks can run in parallel (different tools)
- All Documentation tasks marked [P] can run in parallel (T067-T070)
- All Polish test tasks marked [P] can run in parallel (T071-T074)

---

## Parallel Example: Foundational Models

```bash
# Launch all model creation tasks together (after T005):
Task: "Create ValidationWarningModel in src/Spectra.Core/Models/Validation/ValidationWarningModel.cs"
Task: "Create ValidationResultModel in src/Spectra.Core/Models/Validation/ValidationResultModel.cs"
Task: "Create IndexRebuildResult in src/Spectra.Core/Models/Index/IndexRebuildResult.cs"
Task: "Create CoverageGap in src/Spectra.Core/Models/Coverage/CoverageGap.cs"
Task: "Create GapSeverity enum in src/Spectra.Core/Models/Coverage/GapSeverity.cs"
Task: "Create CoverageAnalysisResult in src/Spectra.Core/Models/Coverage/CoverageAnalysisResult.cs"
```

## Parallel Example: MCP Tool Creation

```bash
# Launch all three MCP tool creation tasks together (after Foundational phase):
Task: "Create ValidateTestsTool in src/Spectra.MCP/Tools/Data/ValidateTestsTool.cs"
Task: "Create RebuildIndexesTool in src/Spectra.MCP/Tools/Data/RebuildIndexesTool.cs"
Task: "Create AnalyzeCoverageGapsTool in src/Spectra.MCP/Tools/Data/AnalyzeCoverageGapsTool.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Agent Prompt)
4. Complete Phase 4: User Story 2 (Validate Tests)
5. **STOP and VALIDATE**: Test agent prompt invocation + validation tool
6. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Agent prompt ready → Can demo with Copilot
3. Add User Story 2 → Validation tool ready → Can validate test files
4. Add User Story 5 → Init installs agent → Full onboarding flow
5. Add User Story 3 → Index rebuild ready
6. Add User Story 4 → Coverage analysis ready
7. Add User Story 6 → Bug logging documented
8. Documentation + Polish → Release ready

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Agent Prompt) → User Story 5 (Init) → User Story 6 (Bug Logging)
   - Developer B: User Story 2 (Validate) → User Story 3 (Rebuild)
   - Developer C: User Story 4 (Coverage) → Documentation
3. All join for Polish phase tests

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Agent prompt files (US1) must be complete before Init (US5) can install them
- All MCP tools use existing McpToolResponse<T> pattern
- All MCP tools are deterministic (no AI dependency)
- Commit after each task or logical group
