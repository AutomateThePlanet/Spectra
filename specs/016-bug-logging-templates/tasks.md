# Tasks: Bug Logging, Templates, and Execution Agent Integration

**Input**: Design documents from `/specs/016-bug-logging-templates/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new directories and namespace structure

- [x] T001 Create `src/Spectra.Core/BugReporting/` directory for new bug reporting services

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model changes that ALL user stories depend on. These modify shared entities and configuration.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 [P] Create `BugTrackingConfig` class with `provider`, `template`, `default_severity`, `auto_attach_screenshots`, `auto_prompt_on_failure` properties in `src/Spectra.Core/Models/Config/BugTrackingConfig.cs`
- [x] T003 Add `BugTracking` property of type `BugTrackingConfig` to `SpectraConfig` in `src/Spectra.Core/Models/Config/SpectraConfig.cs`
- [x] T004 [P] Add `Bugs` field (`List<string>`, YAML alias `bugs`) to `TestCaseFrontmatter` in `src/Spectra.Core/Models/TestCaseFrontmatter.cs`
- [x] T005 [P] Add `Bugs` property (`IReadOnlyList<string>`) to `TestCase` in `src/Spectra.Core/Models/TestCase.cs`
- [x] T006 [P] Add `Bugs` property (`IReadOnlyList<string>`, JSON key `bugs`, ignore when empty) to `TestIndexEntry` in `src/Spectra.Core/Models/TestIndexEntry.cs`
- [x] T007 Add `bug_tracking` section with defaults to config template in `src/Spectra.CLI/Templates/spectra.config.json`
- [x] T008 Update `DocumentIndexWriter` to include `bugs` field when building index entries in `src/Spectra.Core/Index/DocumentIndexWriter.cs`

**Checkpoint**: Foundation ready — all shared models and config updated. User story implementation can begin.

---

## Phase 3: User Story 1 — Log a Bug During Test Execution (Priority: P1) MVP

**Goal**: When a test is marked FAILED, the system can compose a bug report from test case data using a template, save it locally, and write the bug reference back to the test case frontmatter.

**Independent Test**: Fail a test, verify bug report is generated with correct data, verify `bugs` field is written to frontmatter, verify local file is saved to `reports/{run_id}/bugs/`.

### Implementation for User Story 1

- [x] T009 [P] [US1] Create `BugReportContext` data class with all template variable fields (TestId, TestTitle, SuiteName, Environment, Severity, RunId, FailedSteps, ExpectedResult, Attachments, SourceRefs, Requirements, Component, ExistingBugs) in `src/Spectra.Core/BugReporting/BugReportContext.cs`
- [x] T010 [P] [US1] Create default `bug-report.md` template file with all `{{variable}}` placeholders in `src/Spectra.CLI/Templates/bug-report.md`
- [x] T011 [US1] Create `BugReportTemplateEngine` with `PopulateTemplate(templateContent, context)` method that replaces `{{variable}}` placeholders using `BugReportContext`, and `ComposeReport(context)` method for template-less fallback in `src/Spectra.Core/BugReporting/BugReportTemplateEngine.cs`
- [x] T012 [US1] Create `BugReportWriter` with `WriteLocalBugReportAsync(reportsPath, runId, testId, content, attachmentPaths)` method that saves to `reports/{run_id}/bugs/BUG-{test_id}.md` and copies attachments to `reports/{run_id}/bugs/attachments/` in `src/Spectra.Core/BugReporting/BugReportWriter.cs`
- [x] T013 [US1] Add `UpdateBugs(fileContent, bugIds)` method and `UpdateBugsFileAsync(filePath, bugIds)` async wrapper to `FrontmatterUpdater` following the `UpdateAutomatedBy` pattern with regex-based YAML list insertion in `src/Spectra.Core/Parsing/FrontmatterUpdater.cs`
- [x] T014 [US1] Add severity mapping helper method `MapPriorityToSeverity(priority, defaultSeverity)` returning critical/major/minor based on high/medium/low input in `src/Spectra.Core/BugReporting/BugReportContext.cs`
- [x] T015 [US1] Update `AdvanceTestCaseTool` response to include `existing_bugs` list from test case frontmatter when status is FAILED, so the agent can check for duplicates in `src/Spectra.MCP/Tools/TestExecution/AdvanceTestCaseTool.cs`

### Tests for User Story 1

- [x] T016 [P] [US1] Add `BugReportTemplateEngineTests` — test variable substitution (all variables, partial variables, unknown variables left as-is, empty values, template-less composition) in `tests/Spectra.Core.Tests/BugReporting/BugReportTemplateEngineTests.cs`
- [x] T017 [P] [US1] Add `BugReportWriterTests` — test local file creation, attachment copying, directory creation, duplicate file naming in `tests/Spectra.Core.Tests/BugReporting/BugReportWriterTests.cs`
- [x] T018 [P] [US1] Add `FrontmatterUpdaterBugsTests` — test `UpdateBugs` with empty list, single bug, multiple bugs, existing bugs field replacement, no frontmatter in `tests/Spectra.Core.Tests/Parsing/FrontmatterUpdaterBugsTests.cs`
- [x] T019 [P] [US1] Add `BugReportContextTests` — test severity mapping (high→critical, medium→major, low→minor, null→default) in `tests/Spectra.Core.Tests/BugReporting/BugReportContextTests.cs`

**Checkpoint**: Core bug logging works end-to-end. Reports can be composed from templates or directly, saved locally, and linked back to test frontmatter.

---

## Phase 4: User Story 2 — Bug Report Template Initialization and Customization (Priority: P2)

**Goal**: `spectra init` creates the default bug report template and `bug_tracking` config section. Template can be customized or deleted without breaking anything.

**Independent Test**: Run `spectra init`, verify `templates/bug-report.md` exists with correct content and `spectra.config.json` has `bug_tracking` section with defaults.

### Implementation for User Story 2

- [x] T020 [US2] Update `InitHandler.CreateDirectoriesAsync()` to create `templates/` directory in `src/Spectra.CLI/Commands/Init/InitHandler.cs`
- [x] T021 [US2] Add `CreateBugReportTemplateAsync()` method to `InitHandler` that writes `templates/bug-report.md` from embedded resource (skip if file exists) in `src/Spectra.CLI/Commands/Init/InitHandler.cs`
- [x] T022 [US2] Call `CreateBugReportTemplateAsync()` from `InitHandler.HandleAsync()` after directory creation and add success hint output in `src/Spectra.CLI/Commands/Init/InitHandler.cs`

### Tests for User Story 2

- [x] T023 [P] [US2] Add init handler tests verifying template file creation, skip-if-exists behavior, and `bug_tracking` config section presence in `tests/Spectra.CLI.Tests/Commands/InitHandlerBugTrackingTests.cs`

**Checkpoint**: `spectra init` creates template and config. Users can customize or delete the template.

---

## Phase 5: User Story 3 — Bug Tracker Auto-Detection and Configuration (Priority: P2)

**Goal**: The agent detects available bug tracker MCPs and routes bug creation accordingly. Local fallback works when no tracker is connected.

**Independent Test**: Configure different `bug_tracking.provider` values; verify agent prompt instructions reference correct tracker routing and local fallback path.

### Implementation for User Story 3

- [x] T024 [US3] Add tracker detection priority documentation (Azure DevOps > Jira > GitHub) and provider-specific instructions (Work Item type Bug, Issue type Bug, Issue with bug label) to agent prompt in `.github/agents/spectra-execution.agent.md` (path is `test_app_documentation/.github/agents/spectra-execution.agent.md`)
- [x] T025 [US3] Add local fallback instructions to agent prompt — save to `reports/{run_id}/bugs/` when no tracker MCP is available or when provider is `"local"` in `.github/agents/spectra-execution.agent.md`

**Checkpoint**: Agent knows how to detect and route to trackers. Local fallback is documented.

---

## Phase 6: User Story 4 — Execution Agent Prompt Updates (Priority: P3)

**Goal**: The bundled execution agent prompt has a comprehensive Bug Logging section. Optional Copilot Chat skill created.

**Independent Test**: Review agent prompt for completeness — covers template loading, variable substitution, duplicate detection, tracker submission, local fallback, frontmatter writeback.

### Implementation for User Story 4

- [x] T026 [US4] Expand Bug Logging section in execution agent prompt with full workflow: failure detection → duplicate check → template loading → variable population → preview → confirmation → tracker submission → note recording → frontmatter writeback in `test_app_documentation/.github/agents/spectra-execution.agent.md`
- [x] T027 [US4] Add template variable reference table and template-less fallback instructions to agent prompt in `test_app_documentation/.github/agents/spectra-execution.agent.md`
- [x] T028 [US4] Add bulk failure bug logging instructions (consolidated selection prompt, one bug per selected failure) to agent prompt in `test_app_documentation/.github/agents/spectra-execution.agent.md`
- [x] T029 [P] [US4] Create Copilot Chat skill at `test_app_documentation/.github/skills/spectra-bug-logging/SKILL.md` with instructions for reading test details, populating template, creating issue, and adding note

**Checkpoint**: Agent prompt covers the complete bug logging workflow. Copilot skill available for teams using Agent Skills.

---

## Phase 7: User Story 5 — Suppress Bug Prompts (Priority: P3)

**Goal**: Testers can disable automatic bug prompts via config and log bugs on demand only.

**Independent Test**: Set `auto_prompt_on_failure` to `false`, fail a test, verify no bug prompt. Then explicitly request bug logging and verify it works.

### Implementation for User Story 5

- [x] T030 [US5] Add `auto_prompt_on_failure` handling to agent prompt — check config value before offering bug logging, include instructions for on-demand bug logging when prompts are suppressed in `test_app_documentation/.github/agents/spectra-execution.agent.md`

**Checkpoint**: Testers running large suites can suppress prompts. On-demand bug logging still works.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates, validation, and final cleanup

- [x] T031 [P] Verify all existing tests still pass by running `dotnet test` across all test projects
- [x] T032 [P] Validate `spectra.config.json` template is valid JSON and includes `bug_tracking` section with correct defaults in `src/Spectra.CLI/Templates/spectra.config.json`
- [x] T033 [P] Update `ConfigLoader` validation to handle `bug_tracking` section gracefully (no validation errors for missing optional section in existing configs) in `src/Spectra.Core/Config/ConfigLoader.cs`
- [x] T034 [P] Ensure `TestCase` builder/parser maps `Bugs` from frontmatter to model in any test case parsing code in `src/Spectra.Core/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational — MVP, complete first
- **US2 (Phase 4)**: Depends on Foundational + US1 (needs template engine)
- **US3 (Phase 5)**: Depends on Foundational — can run parallel with US1
- **US4 (Phase 6)**: Depends on US1 + US3 (needs complete workflow for prompt)
- **US5 (Phase 7)**: Depends on US4 (extends agent prompt)
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependencies on other stories. **This is the MVP.**
- **US2 (P2)**: Depends on US1 for template engine. Init creates the template that US1's engine consumes.
- **US3 (P2)**: Can start after Foundational — agent prompt work is independent of C# code.
- **US4 (P3)**: Depends on US1 + US3 — the full agent prompt needs complete workflow + tracker detection.
- **US5 (P3)**: Depends on US4 — adds config handling to the agent prompt from US4.

### Within Each User Story

- Models/data classes before services
- Services before tool updates
- Implementation before tests (tests validate implementation)
- All [P] tasks within a story can run in parallel

### Parallel Opportunities

- T002, T004, T005, T006 can all run in parallel (different files)
- T009, T010 can run in parallel (different files, no dependencies)
- T016, T017, T018, T019 can all run in parallel (different test files)
- US3 (Phase 5) can run in parallel with US1 (Phase 3) — agent prompt vs C# code

---

## Parallel Example: User Story 1

```bash
# Launch parallel model/template creation:
Task: "Create BugReportContext data class in src/Spectra.Core/BugReporting/BugReportContext.cs"
Task: "Create default bug-report.md template in src/Spectra.CLI/Templates/bug-report.md"

# After models complete, launch service tasks:
Task: "Create BugReportTemplateEngine in src/Spectra.Core/BugReporting/BugReportTemplateEngine.cs"

# Launch all tests in parallel:
Task: "BugReportTemplateEngineTests in tests/Spectra.Core.Tests/BugReporting/BugReportTemplateEngineTests.cs"
Task: "BugReportWriterTests in tests/Spectra.Core.Tests/BugReporting/BugReportWriterTests.cs"
Task: "FrontmatterUpdaterBugsTests in tests/Spectra.Core.Tests/Parsing/FrontmatterUpdaterBugsTests.cs"
Task: "BugReportContextTests in tests/Spectra.Core.Tests/BugReporting/BugReportContextTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test bug report generation, local saving, frontmatter writeback
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → MVP ready
3. Add User Story 2 → Init creates template → Enhanced setup
4. Add User Story 3 → Tracker routing documented → Multi-tracker support
5. Add User Story 4 → Full agent prompt → Complete workflow
6. Add User Story 5 → Suppress prompts → Power user support
7. Polish → All tests pass, docs updated

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (C# code)
   - Developer B: User Story 3 (agent prompt — no C# dependencies)
3. After US1 complete:
   - Developer A: User Story 2 (init handler)
   - Developer B: User Story 4 + 5 (agent prompt expansion)
4. Polish together

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The bulk of this feature is agent prompt authoring (US3, US4, US5) + C# services (US1, US2)
- No new NuGet dependencies required (Constitution Principle V)
