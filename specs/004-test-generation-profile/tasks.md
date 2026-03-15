# Tasks: Test Generation Profile

**Input**: Design documents from `/specs/004-test-generation-profile/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/profile-format.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **Core models**: `src/Spectra.Core/Models/Profile/`
- **Core services**: `src/Spectra.Core/Profile/`
- **CLI commands**: `src/Spectra.CLI/Commands/`
- **CLI profile**: `src/Spectra.CLI/Profile/`
- **Core tests**: `tests/Spectra.Core.Tests/Profile/`
- **CLI tests**: `tests/Spectra.CLI.Tests/Profile/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and folder structure

- [ ] T001 Create Profile folder structure in src/Spectra.Core/Models/Profile/
- [ ] T002 [P] Create Profile folder structure in src/Spectra.Core/Profile/
- [ ] T003 [P] Create Profile folder structure in src/Spectra.CLI/Profile/
- [ ] T004 [P] Create Profile test folder in tests/Spectra.Core.Tests/Profile/
- [ ] T005 [P] Create Profile test folder in tests/Spectra.CLI.Tests/Profile/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and enums that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T006 [P] Create DetailLevel enum in src/Spectra.Core/Models/Profile/DetailLevel.cs
- [ ] T007 [P] Create Priority enum in src/Spectra.Core/Models/Profile/Priority.cs
- [ ] T008 [P] Create StepFormat enum in src/Spectra.Core/Models/Profile/StepFormat.cs
- [ ] T009 [P] Create DomainType enum in src/Spectra.Core/Models/Profile/DomainType.cs
- [ ] T010 [P] Create PiiSensitivity enum in src/Spectra.Core/Models/Profile/PiiSensitivity.cs
- [ ] T011 [P] Create SourceType enum in src/Spectra.Core/Models/Profile/SourceType.cs
- [ ] T012 [P] Create QuestionType enum in src/Spectra.Core/Models/Profile/QuestionType.cs
- [ ] T013 Create FormattingOptions class in src/Spectra.Core/Models/Profile/FormattingOptions.cs
- [ ] T014 Create DomainOptions class in src/Spectra.Core/Models/Profile/DomainOptions.cs
- [ ] T015 Create ProfileOptions class in src/Spectra.Core/Models/Profile/ProfileOptions.cs (depends on T013, T014)
- [ ] T016 Create GenerationProfile class in src/Spectra.Core/Models/Profile/GenerationProfile.cs (depends on T015)
- [ ] T017 [P] Create ProfileSource class in src/Spectra.Core/Models/Profile/ProfileSource.cs
- [ ] T018 Create EffectiveProfile class in src/Spectra.Core/Models/Profile/EffectiveProfile.cs (depends on T016, T017)
- [ ] T019 [P] Create ValidationError class in src/Spectra.Core/Models/Profile/ValidationError.cs
- [ ] T020 [P] Create ValidationWarning class in src/Spectra.Core/Models/Profile/ValidationWarning.cs
- [ ] T021 Create ProfileValidationResult class in src/Spectra.Core/Models/Profile/ProfileValidationResult.cs (depends on T016, T019, T020)
- [ ] T022 [P] Create ProfileDefaults static class in src/Spectra.Core/Models/Profile/ProfileDefaults.cs

**Checkpoint**: Foundation ready - all core models and enums defined

---

## Phase 3: User Story 1 - Create Repository Profile (Priority: P1) 🎯 MVP

**Goal**: QA lead creates team-wide test generation standards via interactive questionnaire

**Independent Test**: Run `spectra init-profile`, answer all questions, verify spectra.profile.md is created with correct content

### Core Services for US1

- [ ] T023 [P] [US1] Create Question class in src/Spectra.Core/Models/Profile/Question.cs
- [ ] T024 [P] [US1] Create QuestionnaireState class in src/Spectra.Core/Models/Profile/QuestionnaireState.cs
- [ ] T025 [US1] Implement ProfileParser.cs in src/Spectra.Core/Profile/ProfileParser.cs - parse YAML frontmatter from Markdown
- [ ] T026 [US1] Implement ProfileValidator.cs in src/Spectra.Core/Profile/ProfileValidator.cs - validate profile against schema
- [ ] T027 [US1] Implement ProfileWriter.cs in src/Spectra.Core/Profile/ProfileWriter.cs - write profile to Markdown file
- [ ] T028 [US1] Add ProfileParser unit tests in tests/Spectra.Core.Tests/Profile/ProfileParserTests.cs
- [ ] T029 [US1] Add ProfileValidator unit tests in tests/Spectra.Core.Tests/Profile/ProfileValidatorTests.cs

### CLI Components for US1

- [ ] T030 [US1] Implement ProfileQuestionnaire.cs in src/Spectra.CLI/Profile/ProfileQuestionnaire.cs - interactive Q&A flow
- [ ] T031 [US1] Implement ProfileRenderer.cs in src/Spectra.CLI/Profile/ProfileRenderer.cs - format profile for display
- [ ] T032 [US1] Implement InitProfileCommand.cs in src/Spectra.CLI/Commands/InitProfileCommand.cs - `spectra init-profile` command
- [ ] T033 [US1] Add non-interactive mode flags to InitProfileCommand (--non-interactive, --detail-level, etc.)
- [ ] T034 [US1] Add existing profile detection and overwrite confirmation to InitProfileCommand
- [ ] T035 [US1] Register InitProfileCommand in CLI command tree in src/Spectra.CLI/Program.cs
- [ ] T036 [US1] Add InitProfileCommand integration tests in tests/Spectra.CLI.Tests/Profile/InitProfileCommandTests.cs

**Checkpoint**: User Story 1 complete - profiles can be created interactively or via CLI flags

---

## Phase 4: User Story 2 - Generate Tests with Profile (Priority: P1)

**Goal**: Generated test cases automatically follow the team's established profile preferences

**Independent Test**: Create profile with specific settings, run `spectra ai generate`, verify output matches profile

### Core Services for US2

- [ ] T037 [US2] Implement ProfileLoader.cs in src/Spectra.Core/Profile/ProfileLoader.cs - load profile from file path
- [ ] T038 [US2] Add repository root profile discovery to ProfileLoader (find spectra.profile.md)
- [ ] T039 [US2] Add ProfileLoader unit tests in tests/Spectra.Core.Tests/Profile/ProfileLoaderTests.cs

### Profile-to-Context Conversion for US2

- [ ] T040 [US2] Create ProfileContextBuilder.cs in src/Spectra.Core/Profile/ProfileContextBuilder.cs - convert profile to AI prompt section
- [ ] T041 [US2] Implement detail level prompt generation (high-level vs detailed vs very-detailed)
- [ ] T042 [US2] Implement formatting preferences prompt generation (step format, action verbs)
- [ ] T043 [US2] Implement domain considerations prompt generation (payments, PII, etc.)
- [ ] T044 [US2] Implement exclusions prompt generation (what NOT to generate)
- [ ] T045 [US2] Add ProfileContextBuilder unit tests in tests/Spectra.Core.Tests/Profile/ProfileContextBuilderTests.cs

### Agent Runtime Integration for US2

- [ ] T046 [US2] Modify AgentRuntime.cs to load profile at generation command start in src/Spectra.CLI/Agent/AgentRuntime.cs
- [ ] T047 [US2] Add profile context injection to AI prompt construction
- [ ] T048 [US2] Add handling for missing profile (use defaults, log info message)
- [ ] T049 [US2] Add handling for invalid profile (log warning, use defaults)
- [ ] T050 [US2] Ensure profile only applies to generate commands, not update commands

**Checkpoint**: User Story 2 complete - profiles are automatically loaded and applied during generation

---

## Phase 5: User Story 3 - Override Profile for Specific Suite (Priority: P2)

**Goal**: Suite-level profiles override repository profile for specific test suites

**Independent Test**: Create repo profile + suite _profile.md with different settings, generate tests in suite, verify suite settings used

### Suite Profile Support for US3

- [ ] T051 [US3] Add suite profile path resolution to ProfileLoader (_profile.md in suite directory)
- [ ] T052 [US3] Implement profile inheritance/merge logic in ProfileLoader (shallow merge at category level)
- [ ] T053 [US3] Create ProfileMerger.cs in src/Spectra.Core/Profile/ProfileMerger.cs - merge suite profile over repo profile
- [ ] T054 [US3] Add inheritance chain tracking to EffectiveProfile (which profiles were applied)
- [ ] T055 [US3] Add ProfileMerger unit tests in tests/Spectra.Core.Tests/Profile/ProfileMergerTests.cs
- [ ] T056 [US3] Add suite override integration tests in tests/Spectra.Core.Tests/Profile/ProfileLoaderTests.cs

### Suite Profile Creation for US3

- [ ] T057 [US3] Add --suite flag to InitProfileCommand for creating suite-level profiles
- [ ] T058 [US3] Update InitProfileCommand to write to _profile.md when --suite is used
- [ ] T059 [US3] Add suite profile creation tests in tests/Spectra.CLI.Tests/Profile/InitProfileCommandTests.cs

**Checkpoint**: User Story 3 complete - suite profiles override repository profile correctly

---

## Phase 6: User Story 4 - View Current Profile (Priority: P2)

**Goal**: Testers can view effective profile settings before generating tests

**Independent Test**: Create profiles at different levels, run `spectra profile show` from various directories, verify correct profile displayed

### Profile Display for US4

- [ ] T060 [US4] Implement ProfileCommand.cs in src/Spectra.CLI/Commands/ProfileCommand.cs - `spectra profile show` command
- [ ] T061 [US4] Add profile source indicator to display (repository, suite, or default)
- [ ] T062 [US4] Add formatted output showing all profile settings
- [ ] T063 [US4] Add override indicators when suite profile overrides repo settings
- [ ] T064 [US4] Handle no profile case (display "no profile configured" message)
- [ ] T065 [US4] Register ProfileCommand in CLI command tree in src/Spectra.CLI/Program.cs
- [ ] T066 [US4] Add ProfileCommand tests in tests/Spectra.CLI.Tests/Profile/ProfileShowTests.cs

**Checkpoint**: User Story 4 complete - users can view effective profile with source information

---

## Phase 7: User Story 5 - Edit Existing Profile (Priority: P3)

**Goal**: QA lead updates specific preferences without recreating entire profile

**Independent Test**: Create profile, run edit command for one setting, verify only that setting changes

### Profile Editing for US5

- [ ] T067 [US5] Add --edit flag to InitProfileCommand for modifying existing profile
- [ ] T068 [US5] Implement single-setting update mode (e.g., --detail-level detailed)
- [ ] T069 [US5] Load existing profile, apply changes, preserve other settings
- [ ] T070 [US5] Update profile timestamps on edit (updated_at)
- [ ] T071 [US5] Add edit mode tests in tests/Spectra.CLI.Tests/Profile/InitProfileCommandTests.cs

**Checkpoint**: User Story 5 complete - profiles can be edited without full recreation

---

## Phase 8: Edge Cases & Validation

**Purpose**: Handle edge cases identified in spec.md

- [ ] T072 [P] Handle malformed profile YAML - report specific parse errors with line numbers
- [ ] T073 [P] Handle invalid enum values - warn and use default for that option
- [ ] T074 [P] Handle profile deleted mid-generation - use cached profile from session start
- [ ] T075 [P] Handle profile version mismatch - detect outdated format, prompt for upgrade
- [ ] T076 Add edge case tests in tests/Spectra.Core.Tests/Profile/ProfileValidatorTests.cs
- [ ] T077 Add edge case tests in tests/Spectra.Core.Tests/Profile/ProfileLoaderTests.cs

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T078 [P] Add profile-related configuration options to spectra.config.json (profile_file_name, profile_path)
- [ ] T079 [P] Add profile validation to `spectra validate` command
- [ ] T080 Ensure all commands return deterministic exit codes (0=success, 1=error)
- [ ] T081 [P] Add performance logging for profile loading (<1s goal)
- [ ] T082 Run quickstart.md scenarios to validate end-to-end workflow
- [ ] T083 Update CLI help text with profile command documentation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational - MVP, must complete first
- **US2 (Phase 4)**: Depends on Foundational + US1 (needs profile parsing)
- **US3 (Phase 5)**: Depends on US2 (extends ProfileLoader)
- **US4 (Phase 6)**: Depends on US2 (needs ProfileLoader for display)
- **US5 (Phase 7)**: Depends on US1 (extends InitProfileCommand)
- **Edge Cases (Phase 8)**: Can start after US2, parallel with US3-US5
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### User Story Dependencies

```
Foundational (T006-T022)
         │
         ▼
   ┌─────┴─────┐
   │           │
   ▼           │
 US1 (T023-T036) ──────────────────┐
   │                               │
   ▼                               │
 US2 (T037-T050)                   │
   │                               │
   ├───────────┬───────────┐       │
   ▼           ▼           ▼       ▼
 US3        US4          US5
(T051-T059) (T060-T066) (T067-T071)
```

### Parallel Opportunities

**Within Phase 2 (Foundational)**:
- T006-T012: All enums can run in parallel
- T013, T014: FormattingOptions and DomainOptions can run in parallel
- T017, T019, T020, T022: Independent classes can run in parallel

**Within US1**:
- T023, T024: Question and QuestionnaireState can run in parallel
- T028, T029: Tests can run in parallel after implementations

**Within US2**:
- T041-T044: All prompt generation tasks can run in parallel

**Across User Stories**:
- US3, US4, US5 can proceed in parallel after US2 completes
- Edge Cases (Phase 8) can run parallel with US3-US5

---

## Parallel Example: Foundational Phase

```bash
# Launch all enum tasks together (T006-T012):
Task: "Create DetailLevel enum in src/Spectra.Core/Models/Profile/DetailLevel.cs"
Task: "Create Priority enum in src/Spectra.Core/Models/Profile/Priority.cs"
Task: "Create StepFormat enum in src/Spectra.Core/Models/Profile/StepFormat.cs"
Task: "Create DomainType enum in src/Spectra.Core/Models/Profile/DomainType.cs"
Task: "Create PiiSensitivity enum in src/Spectra.Core/Models/Profile/PiiSensitivity.cs"
Task: "Create SourceType enum in src/Spectra.Core/Models/Profile/SourceType.cs"
Task: "Create QuestionType enum in src/Spectra.Core/Models/Profile/QuestionType.cs"

# Then launch independent classes (T013, T014, T17, T19, T20, T22):
Task: "Create FormattingOptions class..."
Task: "Create DomainOptions class..."
Task: "Create ProfileSource class..."
Task: "Create ValidationError class..."
Task: "Create ValidationWarning class..."
Task: "Create ProfileDefaults static class..."
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Create Profile)
4. Complete Phase 4: User Story 2 (Generate with Profile)
5. **STOP and VALIDATE**: Test profile creation and generation integration
6. Deploy/demo if ready - users can now customize test generation

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test profile creation → MVP Part 1
3. Add User Story 2 → Test generation with profile → **Full MVP!**
4. Add User Story 3 → Test suite overrides → Enhanced flexibility
5. Add User Story 4 → Test profile viewing → Visibility improvement
6. Add User Story 5 → Test profile editing → Convenience feature
7. Edge Cases + Polish → Production hardening

### Task Count Summary

| Phase | Tasks | Parallelizable |
|-------|-------|----------------|
| Setup | 5 | 4 |
| Foundational | 17 | 11 |
| US1 (P1) | 14 | 2 |
| US2 (P1) | 14 | 4 |
| US3 (P2) | 9 | 0 |
| US4 (P2) | 7 | 0 |
| US5 (P3) | 5 | 0 |
| Edge Cases | 6 | 4 |
| Polish | 6 | 3 |
| **Total** | **83** | **28** |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
