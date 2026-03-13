# Tasks: AI Test Generation CLI

**Input**: Design documents from `/specs/001-ai-test-generation-cli/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are included per Development Workflow Compliance (Core 80%+, CLI 60%+).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **Spectra.Core**: `src/Spectra.Core/` - Shared library (models, parsing, validation)
- **Spectra.CLI**: `src/Spectra.CLI/` - CLI application
- **Tests**: `tests/Spectra.Core.Tests/`, `tests/Spectra.CLI.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and .NET solution structure

- [ ] T001 Create .NET solution file at `Spectra.sln`
- [ ] T002 [P] Create Spectra.Core class library project at `src/Spectra.Core/Spectra.Core.csproj`
- [ ] T003 [P] Create Spectra.CLI console project at `src/Spectra.CLI/Spectra.CLI.csproj`
- [ ] T004 [P] Create Spectra.Core.Tests xUnit project at `tests/Spectra.Core.Tests/Spectra.Core.Tests.csproj`
- [ ] T005 [P] Create Spectra.CLI.Tests xUnit project at `tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj`
- [ ] T006 Add NuGet dependencies: System.CommandLine, Markdig, YamlDotNet, Microsoft.Extensions.Logging
- [ ] T007 [P] Create TestFixtures folder with sample docs at `tests/TestFixtures/docs/`
- [ ] T008 [P] Create TestFixtures folder with sample tests at `tests/TestFixtures/tests/`
- [ ] T009 Configure .editorconfig and Directory.Build.props for nullable reference types and C# 12

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, parsing, and result types that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Core Models

- [ ] T010 [P] Create Priority enum in `src/Spectra.Core/Models/Priority.cs`
- [ ] T011 [P] Create ParseError record in `src/Spectra.Core/Models/ParseError.cs`
- [ ] T012 [P] Create ParseResult<T> types in `src/Spectra.Core/Models/ParseResult.cs`
- [ ] T013 [P] Create ValidationError and ValidationWarning records in `src/Spectra.Core/Models/ValidationResult.cs`
- [ ] T014 [P] Create ValidationResult record in `src/Spectra.Core/Models/ValidationResult.cs`
- [ ] T015 Create TestCaseFrontmatter class (YamlDotNet DTO) in `src/Spectra.Core/Models/TestCaseFrontmatter.cs`
- [ ] T016 Create TestCase class in `src/Spectra.Core/Models/TestCase.cs`
- [ ] T017 [P] Create TestIndexEntry record in `src/Spectra.Core/Models/TestIndexEntry.cs`
- [ ] T018 [P] Create MetadataIndex record in `src/Spectra.Core/Models/MetadataIndex.cs`
- [ ] T019 [P] Create TestSuite class in `src/Spectra.Core/Models/TestSuite.cs`
- [ ] T020 [P] Create DocumentEntry record in `src/Spectra.Core/Models/DocumentEntry.cs`
- [ ] T021 [P] Create DocumentMap record in `src/Spectra.Core/Models/DocumentMap.cs`

### Configuration Models

- [ ] T022 [P] Create SourceConfig class in `src/Spectra.Core/Models/Config/SourceConfig.cs`
- [ ] T023 [P] Create TestsConfig class in `src/Spectra.Core/Models/Config/TestsConfig.cs`
- [ ] T024 [P] Create ProviderConfig class in `src/Spectra.Core/Models/Config/ProviderConfig.cs`
- [ ] T025 [P] Create AiConfig class in `src/Spectra.Core/Models/Config/AiConfig.cs`
- [ ] T026 [P] Create GenerationConfig class in `src/Spectra.Core/Models/Config/GenerationConfig.cs`
- [ ] T027 [P] Create ValidationConfig class in `src/Spectra.Core/Models/Config/ValidationConfig.cs`
- [ ] T028 [P] Create SuiteConfig class in `src/Spectra.Core/Models/Config/SuiteConfig.cs`
- [ ] T029 [P] Create GitConfig class in `src/Spectra.Core/Models/Config/GitConfig.cs`
- [ ] T030 Create SpectraConfig root class in `src/Spectra.Core/Models/Config/SpectraConfig.cs`

### Core Parsing

- [ ] T031 Implement MarkdownFrontmatterParser (extract YAML, return ParseResult) in `src/Spectra.Core/Parsing/MarkdownFrontmatterParser.cs`
- [ ] T032 Implement TestCaseParser (parse full test file to TestCase) in `src/Spectra.Core/Parsing/TestCaseParser.cs`
- [ ] T033 Implement DocumentMapExtractor (extract headings, preview) in `src/Spectra.Core/Parsing/DocumentMapExtractor.cs`
- [ ] T034 Write unit tests for MarkdownFrontmatterParser in `tests/Spectra.Core.Tests/Parsing/MarkdownFrontmatterParserTests.cs`
- [ ] T035 Write unit tests for TestCaseParser in `tests/Spectra.Core.Tests/Parsing/TestCaseParserTests.cs`

### Configuration Loading

- [ ] T036 Implement ConfigLoader (load spectra.config.json) in `src/Spectra.Core/Config/ConfigLoader.cs`
- [ ] T037 Write unit tests for ConfigLoader in `tests/Spectra.Core.Tests/Config/ConfigLoaderTests.cs`

### CLI Infrastructure

- [ ] T038 Create GlobalOptions class (verbosity, dry-run, no-review) in `src/Spectra.CLI/Options/GlobalOptions.cs`
- [ ] T039 Create ExitCodes class (0 success, 1 error, 130 cancelled) in `src/Spectra.CLI/Infrastructure/ExitCodes.cs`
- [ ] T040 Create VerbosityLevel enum in `src/Spectra.CLI/Infrastructure/VerbosityLevel.cs`
- [ ] T041 Create RootCommand with global options in `src/Spectra.CLI/Program.cs`
- [ ] T042 Setup Microsoft.Extensions.Logging with verbosity support in `src/Spectra.CLI/Infrastructure/LoggingSetup.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Initialize Repository (Priority: P1) 🎯 MVP

**Goal**: Users can run `spectra init` to set up SPECTRA in any repository

**Independent Test**: Run `spectra init` in empty repo, verify config and folders created

### Tests for User Story 1

- [ ] T043 [P] [US1] Write integration test for init command in `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs`

### Implementation for User Story 1

- [ ] T044 [US1] Create default spectra.config.json template in `src/Spectra.CLI/Templates/spectra.config.json`
- [ ] T045 [US1] Create default SKILL.md template in `src/Spectra.CLI/Templates/test-generation-skill.md`
- [ ] T046 [US1] Implement InitHandler (create folders, config, skill) in `src/Spectra.CLI/Commands/Init/InitHandler.cs`
- [ ] T047 [US1] Implement InitCommand with --force option in `src/Spectra.CLI/Commands/Init/InitCommand.cs`
- [ ] T048 [US1] Add init command to Program.cs root command registration

**Checkpoint**: User Story 1 complete - `spectra init` works independently

---

## Phase 4: User Story 3 - Validate Test Files (Priority: P1) 🎯 MVP

**Goal**: Users can run `spectra validate` to check all tests are valid (CI-compatible)

**Independent Test**: Run `spectra validate` with valid/invalid test files, verify exit codes

### Tests for User Story 3

- [ ] T049 [P] [US3] Write unit tests for TestValidator in `tests/Spectra.Core.Tests/Validation/TestValidatorTests.cs`
- [ ] T050 [P] [US3] Write integration test for validate command in `tests/Spectra.CLI.Tests/Commands/ValidateCommandTests.cs`

### Implementation for User Story 3

- [ ] T051 [US3] Implement TestValidator (schema, ID uniqueness, priority enum) in `src/Spectra.Core/Validation/TestValidator.cs`
- [ ] T052 [US3] Implement IdUniquenessValidator in `src/Spectra.Core/Validation/IdUniquenessValidator.cs`
- [ ] T053 [US3] Implement DependsOnValidator in `src/Spectra.Core/Validation/DependsOnValidator.cs`
- [ ] T054 [US3] Implement IndexFreshnessValidator in `src/Spectra.Core/Validation/IndexFreshnessValidator.cs`
- [ ] T055 [US3] Implement ValidationOrchestrator (combines all validators) in `src/Spectra.Core/Validation/ValidationOrchestrator.cs`
- [ ] T056 [US3] Implement ValidateHandler in `src/Spectra.CLI/Commands/Validate/ValidateHandler.cs`
- [ ] T057 [US3] Implement ValidateCommand with --suite option in `src/Spectra.CLI/Commands/Validate/ValidateCommand.cs`
- [ ] T058 [US3] Add validate command to Program.cs

**Checkpoint**: User Story 3 complete - `spectra validate` works with exit code 0/1

---

## Phase 5: User Story 4 - Build Metadata Indexes (Priority: P2)

**Goal**: Users can run `spectra index` to rebuild `_index.json` for all suites

**Independent Test**: Run `spectra index` with test files, verify _index.json created correctly

### Tests for User Story 4

- [ ] T059 [P] [US4] Write unit tests for IndexBuilder in `tests/Spectra.Core.Tests/Index/IndexBuilderTests.cs`
- [ ] T060 [P] [US4] Write integration test for index command in `tests/Spectra.CLI.Tests/Commands/IndexCommandTests.cs`

### Implementation for User Story 4

- [ ] T061 [P] [US4] Implement IndexReader (load _index.json) in `src/Spectra.Core/Index/IndexReader.cs`
- [ ] T062 [P] [US4] Implement IndexWriter (write _index.json) in `src/Spectra.Core/Index/IndexWriter.cs`
- [ ] T063 [US4] Implement IndexBuilder (parse tests, build index) in `src/Spectra.Core/Index/IndexBuilder.cs`
- [ ] T064 [US4] Implement SuiteDiscovery (find all suite folders) in `src/Spectra.Core/Index/SuiteDiscovery.cs`
- [ ] T065 [US4] Implement IndexHandler in `src/Spectra.CLI/Commands/Index/IndexHandler.cs`
- [ ] T066 [US4] Implement IndexCommand with --suite option in `src/Spectra.CLI/Commands/Index/IndexCommand.cs`
- [ ] T067 [US4] Add index command to Program.cs

**Checkpoint**: User Story 4 complete - `spectra index` rebuilds all indexes

---

## Phase 6: User Story 2 - Generate Tests from Documentation (Priority: P1) 🎯 Core Value

**Goal**: Users can run `spectra ai generate --suite <name>` to generate tests from docs

**Independent Test**: Run `spectra ai generate --suite checkout` with sample docs, verify test files created

**Dependencies**: Requires US1 (init) and US4 (index) to be useful, but can be tested independently

### Tests for User Story 2

- [ ] T068 [P] [US2] Write unit tests for DocumentMapBuilder in `tests/Spectra.Core.Tests/Source/DocumentMapBuilderTests.cs`
- [ ] T069 [P] [US2] Write unit tests for DuplicateDetector in `tests/Spectra.Core.Tests/Validation/DuplicateDetectorTests.cs`
- [ ] T070 [P] [US2] Write integration test for generate command in `tests/Spectra.CLI.Tests/Commands/GenerateCommandTests.cs`

### Source Document Handling

- [ ] T071 [P] [US2] Implement DocumentMapBuilder in `src/Spectra.CLI/Source/DocumentMapBuilder.cs`
- [ ] T072 [P] [US2] Implement SourceDocumentReader (load doc content with truncation) in `src/Spectra.CLI/Source/SourceDocumentReader.cs`
- [ ] T073 [US2] Implement DocumentSearcher (keyword search across docs) in `src/Spectra.CLI/Source/DocumentSearcher.cs`

### AI Agent Tools

- [ ] T074 Add GitHub.Copilot.SDK and Microsoft.Extensions.AI NuGet packages
- [ ] T075 [P] [US2] Implement get_document_map tool in `src/Spectra.CLI/Agent/Tools/GetDocumentMapTool.cs`
- [ ] T076 [P] [US2] Implement load_source_document tool in `src/Spectra.CLI/Agent/Tools/LoadSourceDocumentTool.cs`
- [ ] T077 [P] [US2] Implement search_source_docs tool in `src/Spectra.CLI/Agent/Tools/SearchSourceDocsTool.cs`
- [ ] T078 [P] [US2] Implement read_test_index tool in `src/Spectra.CLI/Agent/Tools/ReadTestIndexTool.cs`
- [ ] T079 [P] [US2] Implement get_next_test_ids tool in `src/Spectra.CLI/Agent/Tools/GetNextTestIdsTool.cs`
- [ ] T080 [P] [US2] Implement check_duplicates_batch tool in `src/Spectra.CLI/Agent/Tools/CheckDuplicatesBatchTool.cs`
- [ ] T081 [P] [US2] Implement batch_write_tests tool in `src/Spectra.CLI/Agent/Tools/BatchWriteTestsTool.cs`
- [ ] T082 [US2] Create ToolRegistry (register all tools) in `src/Spectra.CLI/Agent/Tools/ToolRegistry.cs`

### Test Generation Core

- [ ] T083 [US2] Implement DuplicateDetector (title/step similarity) in `src/Spectra.Core/Validation/DuplicateDetector.cs`
- [ ] T084 [US2] Implement TestIdAllocator (sequential IDs) in `src/Spectra.Core/Index/TestIdAllocator.cs`
- [ ] T085 [US2] Implement TestCaseWriter (write test Markdown files) in `src/Spectra.CLI/IO/TestCaseWriter.cs`
- [ ] T086 [US2] Implement PendingTestQueue (hold tests for review) in `src/Spectra.CLI/Review/PendingTestQueue.cs`

### AI Session Management

- [ ] T087 [US2] Implement CopilotSessionFactory in `src/Spectra.CLI/Agent/CopilotSessionFactory.cs`
- [ ] T088 [US2] Implement SkillLoader (load SKILL.md) in `src/Spectra.CLI/Agent/Skills/SkillLoader.cs`

### Lock File Support

- [ ] T089 [US2] Implement LockManager (acquire, release, expire) in `src/Spectra.CLI/IO/LockManager.cs`

### Interactive Review UI

- [ ] T090 Add Spectre.Console NuGet package
- [ ] T091 [US2] Implement ReviewPresenter (show summary, options) in `src/Spectra.CLI/Review/ReviewPresenter.cs`
- [ ] T092 [US2] Implement TestReviewer (one-by-one review flow) in `src/Spectra.CLI/Review/TestReviewer.cs`
- [ ] T093 [US2] Implement StreamingRenderer (display AI streaming output) in `src/Spectra.CLI/Review/StreamingRenderer.cs`

### Generate Command

- [ ] T094 [US2] Implement GenerateHandler (orchestrate full flow) in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [ ] T095 [US2] Implement GenerateCommand with all options in `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs`
- [ ] T096 [US2] Create AiCommand parent (groups ai subcommands) in `src/Spectra.CLI/Commands/Ai/AiCommand.cs`
- [ ] T097 [US2] Add ai generate command to Program.cs

**Checkpoint**: User Story 2 complete - core value proposition works

---

## Phase 7: User Story 5 - Update Tests from Changed Documentation (Priority: P2)

**Goal**: Users can run `spectra ai update --suite <name>` to sync tests with docs

**Independent Test**: Modify docs, run update command, verify tests classified correctly

### Tests for User Story 5

- [ ] T098 [P] [US5] Write unit tests for TestClassifier in `tests/Spectra.Core.Tests/Update/TestClassifierTests.cs`
- [ ] T099 [P] [US5] Write integration test for update command in `tests/Spectra.CLI.Tests/Commands/UpdateCommandTests.cs`

### Implementation for User Story 5

- [ ] T100 [P] [US5] Create UpdateClassification enum (UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT) in `src/Spectra.Core/Models/UpdateClassification.cs`
- [ ] T101 [P] [US5] Create UpdateProposal record in `src/Spectra.Core/Models/UpdateProposal.cs`
- [ ] T102 [US5] Implement batch_read_tests tool in `src/Spectra.CLI/Agent/Tools/BatchReadTestsTool.cs`
- [ ] T103 [US5] Implement batch_propose_updates tool in `src/Spectra.CLI/Agent/Tools/BatchProposeUpdatesTool.cs`
- [ ] T104 [US5] Implement TestClassifier (compare tests vs docs) in `src/Spectra.Core/Update/TestClassifier.cs`
- [ ] T105 [US5] Implement DiffPresenter (show test changes) in `src/Spectra.CLI/Review/DiffPresenter.cs`
- [ ] T106 [US5] Implement UpdateReviewer (batch update review flow) in `src/Spectra.CLI/Review/UpdateReviewer.cs`
- [ ] T107 [US5] Implement UpdateHandler in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`
- [ ] T108 [US5] Implement UpdateCommand with --all, --diff options in `src/Spectra.CLI/Commands/Update/UpdateCommand.cs`
- [ ] T109 [US5] Add ai update command to Program.cs

**Checkpoint**: User Story 5 complete - tests can be synced with documentation

---

## Phase 8: User Story 7 - Use Multiple AI Providers (Priority: P3)

**Goal**: Users can configure provider chain with auto-fallback

**Independent Test**: Configure multiple providers, simulate failure, verify fallback

### Tests for User Story 7

- [ ] T110 [P] [US7] Write unit tests for ProviderChain in `tests/Spectra.CLI.Tests/Provider/ProviderChainTests.cs`

### Implementation for User Story 7

- [ ] T111 [P] [US7] Implement ProviderResolver (select provider from config) in `src/Spectra.CLI/Provider/ProviderResolver.cs`
- [ ] T112 [US7] Implement ProviderChain (fallback logic) in `src/Spectra.CLI/Provider/ProviderChain.cs`
- [ ] T113 [US7] Implement RecoverableErrorDetector (rate limit, quota, auth) in `src/Spectra.CLI/Provider/RecoverableErrorDetector.cs`
- [ ] T114 [US7] Update CopilotSessionFactory to use ProviderChain
- [ ] T115 [US7] Add --provider flag to all AI commands

**Checkpoint**: User Story 7 complete - multi-provider fallback works

---

## Phase 9: User Story 6 - Analyze Test Coverage (Priority: P3)

**Goal**: Users can run `spectra ai analyze` to get coverage reports

**Independent Test**: Run analyze command, verify report generated

### Tests for User Story 6

- [ ] T116 [P] [US6] Write integration test for analyze command in `tests/Spectra.CLI.Tests/Commands/AnalyzeCommandTests.cs`

### Implementation for User Story 6

- [ ] T117 [P] [US6] Create CoverageReport record in `src/Spectra.Core/Models/CoverageReport.cs`
- [ ] T118 [US6] Implement AnalyzeHandler in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`
- [ ] T119 [US6] Implement AnalyzeCommand with --format, --output options in `src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs`
- [ ] T120 [US6] Implement ReportWriter (markdown, JSON formats) in `src/Spectra.CLI/IO/ReportWriter.cs`
- [ ] T121 [US6] Add ai analyze command to Program.cs

**Checkpoint**: User Story 6 complete - coverage analysis available

---

## Phase 10: Utility Commands

**Purpose**: List, show, config commands for discoverability

### Implementation

- [ ] T122 [P] Implement ListHandler in `src/Spectra.CLI/Commands/List/ListHandler.cs`
- [ ] T123 [P] Implement ListCommand in `src/Spectra.CLI/Commands/List/ListCommand.cs`
- [ ] T124 [P] Implement ShowHandler in `src/Spectra.CLI/Commands/Show/ShowHandler.cs`
- [ ] T125 [P] Implement ShowCommand with <test-id> argument in `src/Spectra.CLI/Commands/Show/ShowCommand.cs`
- [ ] T126 [P] Implement ConfigHandler in `src/Spectra.CLI/Commands/Config/ConfigHandler.cs`
- [ ] T127 [P] Implement ConfigCommand in `src/Spectra.CLI/Commands/Config/ConfigCommand.cs`
- [ ] T128 Add list, show, config commands to Program.cs

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements across all user stories

- [ ] T129 [P] Add XML documentation to all public APIs
- [ ] T130 [P] Create sample documentation in `tests/TestFixtures/docs/features/`
- [ ] T131 Code review: ensure all commands follow exit code conventions (0/1)
- [ ] T132 Code review: ensure all file operations handle path sanitization (FR-031)
- [ ] T133 Performance: optimize validation for 500+ test files (SC-004)
- [ ] T134 Performance: optimize index rebuild for 500+ test files (SC-005)
- [ ] T135 Run full test suite and ensure 80%+ coverage on Core, 60%+ on CLI
- [ ] T136 Validate quickstart.md workflow works end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Stories (Phase 3-9)**: All depend on Foundational completion
- **Utility Commands (Phase 10)**: Depends on Foundational
- **Polish (Phase 11)**: Depends on all user stories complete

### User Story Dependencies

| Story | Priority | Dependencies | Can Parallelize With |
|-------|----------|--------------|---------------------|
| US1 (Init) | P1 | Foundation only | US3, US4 |
| US3 (Validate) | P1 | Foundation only | US1, US4 |
| US4 (Index) | P2 | Foundation only | US1, US3 |
| US2 (Generate) | P1 | Foundation + Index (T061-T064) | US5 (after index) |
| US5 (Update) | P2 | Generate tools (T075-T082) | US6, US7 |
| US7 (Providers) | P3 | Generate (CopilotSessionFactory) | US6 |
| US6 (Analyze) | P3 | Generate tools | US7 |

### Parallel Opportunities

```text
After Setup:
  ├── T010-T030: All model classes [P]
  └── T038-T042: CLI infrastructure [P]

After Foundation:
  ├── US1 (T043-T048)
  ├── US3 (T049-T058)
  └── US4 (T059-T067)

After US4 complete:
  └── US2 (T068-T097) - core value, do this next

After US2 tools (T075-T082):
  ├── US5 (T098-T109)
  ├── US6 (T116-T121)
  └── US7 (T110-T115)
```

---

## Parallel Example: Foundational Models

```bash
# Launch all model tasks in parallel (different files):
Task: "Create Priority enum in src/Spectra.Core/Models/Priority.cs"
Task: "Create ParseError record in src/Spectra.Core/Models/ParseError.cs"
Task: "Create ValidationError record in src/Spectra.Core/Models/ValidationResult.cs"
Task: "Create DocumentEntry record in src/Spectra.Core/Models/DocumentEntry.cs"
Task: "Create SourceConfig class in src/Spectra.Core/Models/Config/SourceConfig.cs"
# ... all T010-T029 can run simultaneously
```

---

## Implementation Strategy

### MVP First (P1 Stories Only)

1. Complete Phase 1: Setup (~9 tasks)
2. Complete Phase 2: Foundational (~34 tasks)
3. Complete Phase 3: US1 Init (~6 tasks)
4. Complete Phase 4: US3 Validate (~10 tasks)
5. Complete Phase 5: US4 Index (~9 tasks)
6. Complete Phase 6: US2 Generate (~30 tasks)
7. **STOP and VALIDATE**: Full MVP ready

### Incremental Delivery

1. Setup + Foundation → Ready for user stories
2. US1 (Init) → Users can initialize
3. US3 (Validate) → CI integration possible
4. US4 (Index) → Index management works
5. US2 (Generate) → **Core value delivered!**
6. US5 (Update) → Test maintenance
7. US7 (Providers) → Enterprise flexibility
8. US6 (Analyze) → Coverage insights

---

## Notes

- Total tasks: 136
- [P] tasks can run in parallel when marked
- Each user story is independently testable
- Commit after each task or logical group
- Exit codes: 0 success, 1 error, 130 cancelled (per Constitution)
- All file operations must sanitize paths (FR-031)
