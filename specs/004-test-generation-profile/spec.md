# Feature Specification: Test Generation Profile

**Feature Branch**: `004-test-generation-profile`
**Created**: 2026-03-15
**Status**: Draft
**Input**: Test Generation Profile system - customizable preferences for AI-generated test cases

## Clarifications

### Session 2026-03-15

- Q: What file format should the profile use? → A: Markdown (spectra.profile.md) - consistent with test case format

## Overview

The Test Generation Profile system allows teams to customize how the AI generates test cases. Rather than accepting generic defaults, teams can define their preferences for detail level, formatting, domain-specific needs, and exclusions. Profiles ensure consistent test case quality aligned with team standards.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create Repository Profile (Priority: P1)

A QA lead wants to establish team-wide standards for how test cases are generated. They run an interactive questionnaire that guides them through all profile options and generates a profile file that applies to all test generation in the repository.

**Why this priority**: This is the core value proposition - enabling customization of AI-generated tests. Without profile creation, the feature has no utility.

**Independent Test**: Can be fully tested by running the profile creation command, answering all questions, and verifying a valid profile file is generated with the specified preferences.

**Acceptance Scenarios**:

1. **Given** no profile exists, **When** I run the profile initialization command, **Then** I am presented with an interactive questionnaire about my preferences
2. **Given** I am completing the questionnaire, **When** I answer each question, **Then** I see clear options and can select my preference
3. **Given** I have completed all questions, **When** the command finishes, **Then** a profile file is created at the repository root with my selections
4. **Given** a profile already exists, **When** I run the initialization command, **Then** I am asked whether to overwrite or edit the existing profile

---

### User Story 2 - Generate Tests with Profile (Priority: P1)

A tester wants their generated test cases to follow the team's established preferences. When they run test generation, the system automatically loads and applies the profile, resulting in tests that match the team's standards.

**Why this priority**: Profile loading during generation is essential - creating a profile is meaningless if it isn't applied.

**Independent Test**: Can be tested by creating a profile with specific preferences (e.g., detailed steps, bullet format), generating tests, and verifying the output matches the profile settings.

**Acceptance Scenarios**:

1. **Given** a repository profile exists, **When** I run test generation, **Then** the profile is automatically loaded and applied
2. **Given** a profile specifies "detailed" step level, **When** tests are generated, **Then** each test step includes detailed instructions
3. **Given** a profile specifies minimum 3 negative scenarios, **When** tests are generated for a feature, **Then** at least 3 negative test cases are included
4. **Given** a profile specifies exclusions, **When** tests are generated, **Then** the excluded categories are not present in the output

---

### User Story 3 - Override Profile for Specific Suite (Priority: P2)

A test architect needs different generation preferences for a specific suite (e.g., payment tests need more detailed steps than UI tests). They create a suite-level profile that overrides the repository profile for that suite only.

**Why this priority**: Suite-level overrides provide flexibility but are an enhancement over the core profile functionality.

**Independent Test**: Can be tested by creating a repository profile and a suite-specific profile with different settings, generating tests for that suite, and verifying the suite profile takes precedence.

**Acceptance Scenarios**:

1. **Given** a repository profile exists, **When** I create a suite-level profile in a test suite folder, **Then** that profile takes precedence for tests generated in that suite
2. **Given** a suite has its own profile, **When** I generate tests for that suite, **Then** the suite profile settings are used instead of the repository profile
3. **Given** a suite has no profile, **When** I generate tests for that suite, **Then** the repository profile is used as fallback

---

### User Story 4 - View Current Profile (Priority: P2)

A tester wants to understand what profile settings are currently active before generating tests. They can view the effective profile that will be applied, showing either the repository profile or any suite override.

**Why this priority**: Visibility into active settings prevents confusion but is not required for core functionality.

**Independent Test**: Can be tested by creating profiles at different levels, viewing the effective profile from various directories, and verifying the correct profile is shown.

**Acceptance Scenarios**:

1. **Given** a repository profile exists, **When** I view the current profile from the repository root, **Then** I see the repository profile contents
2. **Given** a suite override exists, **When** I view the current profile from that suite directory, **Then** I see the suite profile contents
3. **Given** no profile exists, **When** I view the current profile, **Then** I see a message indicating no profile is configured

---

### User Story 5 - Edit Existing Profile (Priority: P3)

A QA lead wants to update specific preferences without recreating the entire profile. They can modify individual settings while preserving other preferences.

**Why this priority**: Editing is convenient but users can delete and recreate the profile as an alternative.

**Independent Test**: Can be tested by creating a profile, modifying one setting, and verifying only that setting changes while others remain.

**Acceptance Scenarios**:

1. **Given** a profile exists, **When** I run the edit command with a specific setting, **Then** only that setting is updated
2. **Given** I am editing a profile, **When** I change the detail level, **Then** the new detail level is saved and other settings are preserved

---

### Edge Cases

- What happens when the profile file is malformed? The system reports a validation error with specific issues and continues with defaults.
- What happens when a suite profile references an invalid setting? The system warns about the invalid setting and uses the default for that option.
- What happens during generation if the profile file is deleted mid-session? The system uses the profile loaded at session start; file changes don't affect in-progress generation.
- What happens when running generation without any profile? The system uses built-in defaults and informs the user that no profile was found.
- What happens when profile format version changes? The system detects outdated formats and prompts the user to update via the init command.

## Requirements *(mandatory)*

### Functional Requirements

**Profile Creation**

- **FR-001**: System MUST provide an interactive questionnaire for creating a profile
- **FR-002**: System MUST generate a profile file in Markdown format (spectra.profile.md)
- **FR-003**: System MUST support the following profile options:
  - Detail level for test steps (high-level, detailed, very detailed)
  - Minimum negative scenarios per feature
  - Domain-specific needs (payments, authentication, PII/GDPR)
  - Default priority for generated tests
  - Formatting preferences (bullets vs paragraphs, action verbs)
  - Exclusions (categories of tests NOT to generate)
- **FR-004**: System MUST validate profile content before saving
- **FR-005**: System MUST warn before overwriting an existing profile

**Profile Loading**

- **FR-006**: System MUST automatically load the repository profile during test generation
- **FR-007**: System MUST check for suite-level profile and use it if present, falling back to repository profile
- **FR-008**: System MUST include profile content in AI context during generation
- **FR-009**: System MUST only apply profile to generation commands, not update commands

**Profile Structure**

- **FR-010**: System MUST store the repository profile at a configurable location (default: repository root)
- **FR-011**: System MUST support suite-level overrides via a profile file in the suite directory
- **FR-012**: System MUST support configuration of profile file names via the main configuration file

**Profile Viewing**

- **FR-013**: System MUST allow users to view the currently effective profile
- **FR-014**: System MUST indicate which profile is active (repository or suite-level)

### Key Entities

- **Profile**: A collection of test generation preferences stored as a file
- **Repository Profile**: The default profile at repository root, applies to all suites unless overridden
- **Suite Profile**: An optional override profile in a specific suite folder
- **Profile Option**: A single configurable preference (e.g., detail level, exclusions)
- **Effective Profile**: The profile that will actually be applied, considering inheritance

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create a complete profile through the questionnaire in under 5 minutes
- **SC-002**: 100% of generated tests conform to the active profile settings
- **SC-003**: Profile loading adds less than 1 second to test generation startup time
- **SC-004**: 90% of users report that generated tests better match their team's standards after profile adoption
- **SC-005**: Suite-level overrides correctly supersede repository profile in 100% of cases
- **SC-006**: Profile validation catches 100% of malformed or invalid profile files

## Assumptions

- Users understand their team's test case preferences (detail level, formatting, etc.)
- The profile file format is simple enough to be edited manually if needed
- Teams typically have consistent preferences across most suites, with occasional exceptions
- Profile options will evolve over time as more AI capabilities are added
- The questionnaire can be completed non-interactively via command-line flags for CI/automation

## Dependencies

- Spectra.CLI for command integration
- Existing AI agent runtime for context loading
- Spectra.Core for configuration management

## Out of Scope

- Automatic profile recommendations based on repository analysis
- Profile sharing across repositories
- Version control integration for profile changes
- Profile migration tools between major versions
- GUI for profile editing
