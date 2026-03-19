# Tasks: Grounding Verification Pipeline

**Input**: Design documents from `/specs/008-grounding-verification/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Create directory structure and initial scaffolding

- [x] T001 Create `src/Spectra.Core/Models/Grounding/` directory
- [x] T002 [P] Create `src/Spectra.CLI/Agent/Critic/` directory

---

## Phase 2: Foundational (Core Models)

**Purpose**: Core data models that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 [P] Create `VerificationVerdict` enum in `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs`
- [x] T004 [P] Create `FindingStatus` enum in `src/Spectra.Core/Models/Grounding/FindingStatus.cs`
- [x] T005 [P] Create `CriticFinding` record in `src/Spectra.Core/Models/Grounding/CriticFinding.cs`
- [x] T006 Create `GroundingMetadata` record in `src/Spectra.Core/Models/Grounding/GroundingMetadata.cs` (depends on T003)
- [x] T007 Create `VerificationResult` class in `src/Spectra.Core/Models/Grounding/VerificationResult.cs` (depends on T003, T005)
- [x] T008 Create `CriticConfig` class in `src/Spectra.Core/Models/Config/CriticConfig.cs`
- [x] T009 Modify `AiConfig.cs` to add `Critic` property in `src/Spectra.Core/Models/Config/AiConfig.cs` (depends on T008)
- [x] T010 Modify `TestCase` model to add optional `Grounding` property in `src/Spectra.Core/Models/TestCase.cs` (depends on T006)
- [x] T011 [P] Add unit tests for grounding models in `tests/Spectra.Core.Tests/Models/Grounding/GroundingModelsTests.cs`
- [x] T012 [P] Add unit tests for CriticConfig in `tests/Spectra.Core.Tests/Models/Config/CriticConfigTests.cs`

**Checkpoint**: Core models complete - user story implementation can now begin

---

## Phase 3: User Story 1 - Automatic Grounding Verification (Priority: P1)

**Goal**: Verify each generated test against source documentation before writing to disk

**Independent Test**: Run `spectra ai generate --suite checkout`, observe verification progress, confirm grounded tests are written with metadata and hallucinated tests are rejected

### Critic Infrastructure

- [x] T013 Create `ICriticRuntime` interface in `src/Spectra.CLI/Agent/Critic/ICriticRuntime.cs`
- [x] T014 Create `CriticPromptBuilder` class in `src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs` (depends on T013)
- [x] T015 [P] [US1] Create `CriticResponseParser` class in `src/Spectra.CLI/Agent/Critic/CriticResponseParser.cs`
- [x] T016 Create `CriticFactory` class in `src/Spectra.CLI/Agent/Critic/CriticFactory.cs` (depends on T013, T008)

### Provider Implementations

- [x] T017 [P] [US1] Create `GoogleCritic` implementation in `src/Spectra.CLI/Agent/Critic/GoogleCritic.cs` (depends on T013, T014)
- [x] T018 [P] [US1] Create `OpenAiCritic` implementation in `src/Spectra.CLI/Agent/Critic/OpenAiCritic.cs` (depends on T013, T014)
- [x] T019 [P] [US1] Create `GitHubCritic` implementation in `src/Spectra.CLI/Agent/Critic/GitHubCritic.cs` (depends on T013, T014)
- [x] T020 [P] [US1] Create `AnthropicCritic` implementation in `src/Spectra.CLI/Agent/Critic/AnthropicCritic.cs` (depends on T013, T014)

### Handler Integration

- [x] T021 [US1] Modify `GenerateHandler` to call critic verification after test generation in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (depends on T016)
- [x] T022 [US1] Add verification logic to write grounded tests, reject hallucinated tests in `GenerateHandler.cs` (depends on T021)
- [x] T023 [US1] Add graceful fallback when critic unavailable per FR-019 in `GenerateHandler.cs` (depends on T022)

### Tests

- [x] T024 [P] [US1] Add unit tests for `CriticPromptBuilder` in `tests/Spectra.CLI.Tests/Agent/Critic/CriticPromptBuilderTests.cs`
- [x] T025 [P] [US1] Add unit tests for `CriticResponseParser` in `tests/Spectra.CLI.Tests/Agent/Critic/CriticResponseParserTests.cs`
- [x] T026 [P] [US1] Add unit tests for `CriticFactory` in `tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs`

**Checkpoint**: User Story 1 complete - tests can be verified against documentation automatically

---

## Phase 4: User Story 2 - Clear Verification Feedback (Priority: P1)

**Goal**: Display clear symbols (check, warning, X) and explanations so users understand verification results

**Independent Test**: Generate tests with mixed verdicts, confirm CLI output shows symbols, summary counts, and detailed explanations

### Presenter Implementation

- [x] T027 [US2] Create `VerificationPresenter` class in `src/Spectra.CLI/Output/VerificationPresenter.cs`
- [x] T028 [US2] Implement verdict symbol display (check grounded, warning partial, X hallucinated) in `VerificationPresenter.cs` (depends on T027)
- [x] T029 [US2] Implement summary counts display (N grounded, N partial, N hallucinated) in `VerificationPresenter.cs` (depends on T027)
- [x] T030 [US2] Implement detailed unverified claims display for partial verdicts per FR-008 in `VerificationPresenter.cs` (depends on T027)
- [x] T031 [US2] Implement detailed rejection reasons for hallucinated verdicts per FR-009 in `VerificationPresenter.cs` (depends on T027)

### Handler Integration

- [x] T032 [US2] Integrate `VerificationPresenter` into `GenerateHandler` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (depends on T027, T022)
- [x] T033 [US2] Add spinner with model name during verification per FR-006 in `GenerateHandler.cs` (depends on T032)

### Tests

- [x] T034 [P] [US2] Add unit tests for `VerificationPresenter` in `tests/Spectra.CLI.Tests/Output/VerificationPresenterTests.cs`

**Checkpoint**: User Story 2 complete - users see clear feedback with symbols and explanations

---

## Phase 5: User Story 5 - Grounding Metadata in Test Files (Priority: P2)

**Goal**: Persist verification results in test file YAML frontmatter for audit trail

**Independent Test**: Generate verified tests, open test files, confirm grounding metadata present in frontmatter

### File Writer Updates

- [x] T035 [US5] Modify `TestFileWriter` to serialize `GroundingMetadata` in frontmatter in `src/Spectra.CLI/IO/TestFileWriter.cs` (depends on T006)
- [x] T036 [US5] Add YAML serialization for nested grounding object per schema in `TestFileWriter.cs` (depends on T035)
- [x] T037 [US5] Ensure `unverified_claims` array serialized correctly for partial verdicts in `TestFileWriter.cs` (depends on T036)

### Parser Updates

- [x] T038 [US5] Modify test parser to read `GroundingMetadata` from frontmatter in `src/Spectra.Core/Parsing/TestCaseParser.cs` (depends on T006)

### Tests

- [x] T039 [P] [US5] Add serialization tests for grounding metadata in `tests/Spectra.CLI.Tests/IO/TestFileWriterTests.cs`
- [x] T040 [P] [US5] Add parsing tests for grounding metadata in `tests/Spectra.Core.Tests/Parsing/GroundingMetadataParsingTests.cs`

**Checkpoint**: User Story 5 complete - grounding metadata persisted in test files

---

## Phase 6: User Story 3 - Configure Critic Model (Priority: P2)

**Goal**: Allow users to configure their preferred verification model provider

**Independent Test**: Set critic provider in config file, run generation, confirm configured model is used and recorded in metadata

### Configuration Loading

- [x] T041 [US3] Add `critic` section parsing to config loader in `src/Spectra.CLI/Config/ConfigLoader.cs` (depends on T008)
- [x] T042 [US3] Validate critic provider is supported (google, openai, anthropic, github) in `ConfigLoader.cs` (depends on T041)
- [x] T043 [US3] Read API key from environment variable specified in config in `CriticFactory.cs` (depends on T016, T041)

### Error Handling

- [x] T044 [US3] Add clear error message for authentication failure per FR-018 in `CriticFactory.cs` (depends on T043)
- [x] T045 [US3] Allow user to proceed without verification if auth fails per FR-019 in `GenerateHandler.cs` (depends on T044)

### Tests

- [x] T046 [P] [US3] Add config loading tests for critic section in `tests/Spectra.CLI.Tests/Config/CriticConfigLoadingTests.cs`
- [x] T047 [P] [US3] Add authentication failure handling tests in `tests/Spectra.CLI.Tests/Agent/Critic/CriticAuthTests.cs`

**Checkpoint**: User Story 3 complete - users can configure their preferred critic model

---

## Phase 7: User Story 4 - Skip Verification When Needed (Priority: P2)

**Goal**: Allow developers to skip verification for rapid iteration

**Independent Test**: Run `spectra ai generate --skip-critic`, confirm tests written without verification step or grounding metadata

### CLI Flag

- [x] T048 [US4] Add `--skip-critic` option to `GenerateCommand` in `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs`
- [x] T049 [US4] Pass skip flag to `GenerateHandler` in `GenerateCommand.cs` (depends on T048)
- [x] T050 [US4] Skip verification step when flag is set in `GenerateHandler.cs` (depends on T049)
- [x] T051 [US4] Display notice when verification skipped per acceptance scenario in `VerificationPresenter.cs` (depends on T050)

### Tests

- [x] T052 [P] [US4] Add tests for `--skip-critic` flag behavior in `tests/Spectra.CLI.Tests/Commands/Generate/SkipCriticTests.cs`

**Checkpoint**: User Story 4 complete - users can skip verification with --skip-critic flag

---

## Phase 8: User Story 6 - Disable Critic Globally (Priority: P3)

**Goal**: Allow admins to disable verification repository-wide

**Independent Test**: Set `ai.critic.enabled=false` in config, run generation, confirm no verification occurs

### Configuration Handling

- [x] T053 [US6] Check `critic.enabled` flag before running verification in `GenerateHandler.cs` (depends on T041)
- [x] T054 [US6] Default to no verification when critic not configured per FR-013 in `GenerateHandler.cs` (depends on T053)
- [x] T055 [US6] Suppress verification progress spinner when disabled in `GenerateHandler.cs` (depends on T054)

### Tests

- [x] T056 [P] [US6] Add tests for `enabled=false` behavior in `tests/Spectra.CLI.Tests/Commands/Generate/CriticDisabledTests.cs`
- [x] T057 [P] [US6] Add tests for missing critic config (backward compatibility) in `tests/Spectra.CLI.Tests/Commands/Generate/CriticDisabledTests.cs`

**Checkpoint**: User Story 6 complete - critic can be disabled globally

---

## Phase 9: Polish & Integration

**Purpose**: Final integration, edge cases, and cross-cutting concerns

### Edge Case Handling

- [x] T058 Handle empty source documentation (all tests marked unverified) in `GenerateHandler.cs`
- [x] T059 Handle multiple source documents per test in `CriticPromptBuilder.cs`
- [x] T060 Handle large documentation files (send relevant sections only) in `CriticPromptBuilder.cs`
- [x] T061 Handle malformed critic response (log warning, treat as unverified) in `CriticResponseParser.cs`
- [x] T062 Handle critic timeout (retry once, then treat as unverified) in critic implementations

### Integration Tests

- [x] T063 [P] Add end-to-end verification integration test in `tests/Spectra.CLI.Tests/Commands/Generate/VerificationIntegrationTests.cs`
- [x] T064 [P] Add partial verdict integration test in `tests/Spectra.CLI.Tests/Commands/Generate/VerificationIntegrationTests.cs`
- [x] T065 [P] Add hallucinated rejection integration test in `tests/Spectra.CLI.Tests/Commands/Generate/VerificationIntegrationTests.cs`

### Quickstart Validation

- [x] T066 Validate quickstart.md scenarios work correctly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Stories (Phase 3-8)**: All depend on Foundational phase completion
  - US1 and US2 are both P1 - can run in parallel after foundational
  - US3, US4, US5 are P2 - can run in parallel after US1/US2 (or concurrently)
  - US6 is P3 - depends on US3 (config loading) being complete
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 - core verification flow
- **US2 (P1)**: Depends on US1 T022 (needs results to display)
- **US3 (P2)**: Can start after Phase 2 - independent config flow
- **US4 (P2)**: Depends on US1 T022 (needs verification to skip)
- **US5 (P2)**: Can start after Phase 2 - independent file writing
- **US6 (P3)**: Depends on US3 T041 (needs config loading)

### Parallel Opportunities

- T003, T004, T005, T008 can run in parallel (independent models)
- T011, T012 can run in parallel (independent test files)
- T017, T018, T019, T020 can run in parallel (independent provider implementations)
- T024, T025, T026 can run in parallel (independent test files)
- T039, T040 can run in parallel (independent test files)
- T046, T047 can run in parallel (independent test files)
- T063, T064, T065 can run in parallel (independent test scenarios)

---

## Summary

| Phase | Tasks | Purpose |
|-------|-------|---------|
| 1 - Setup | T001-T002 | Directory structure |
| 2 - Foundational | T003-T012 | Core models (blocks all stories) |
| 3 - US1 (P1) | T013-T026 | Automatic verification |
| 4 - US2 (P1) | T027-T034 | Clear feedback display |
| 5 - US5 (P2) | T035-T040 | Metadata in files |
| 6 - US3 (P2) | T041-T047 | Configure critic |
| 7 - US4 (P2) | T048-T052 | Skip verification |
| 8 - US6 (P3) | T053-T057 | Disable globally |
| 9 - Polish | T058-T066 | Edge cases & integration |

**Total**: 66 tasks
