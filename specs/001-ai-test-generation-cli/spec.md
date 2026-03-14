# Feature Specification: AI Test Generation CLI

**Feature Branch**: `001-ai-test-generation-cli`
**Created**: 2026-03-13
**Status**: Draft
**Input**: User description: "AI Test Generation CLI - Generate test cases from documentation using GitHub Copilot SDK"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Initialize Repository (Priority: P1)

A QA engineer wants to set up SPECTRA in their existing project repository. They run a single command to initialize the necessary folder structure, configuration file, and agent skills so they can start generating tests immediately.

**Why this priority**: Without initialization, no other CLI features can function. This is the entry point for all users.

**Independent Test**: Can be fully tested by running `spectra init` in an empty repository and verifying the created structure delivers a working configuration.

**Acceptance Scenarios**:

1. **Given** a repository without SPECTRA configuration, **When** the user runs `spectra init`, **Then** the system creates `spectra.config.json`, `docs/`, `tests/`, and `.github/skills/` folders with default content.
2. **Given** a repository that already has a `spectra.config.json`, **When** the user runs `spectra init`, **Then** the system warns about existing configuration and asks whether to overwrite or merge.
3. **Given** a repository with existing `docs/` folder, **When** the user runs `spectra init`, **Then** the system preserves existing documentation and only creates missing structure.

---

### User Story 2 - Generate Tests from Documentation (Priority: P1)

A QA engineer has product documentation in their `docs/` folder. They want to generate a comprehensive set of manual test cases for a specific feature area (suite) by pointing the CLI at their documentation and letting the AI agent create test cases automatically.

**Why this priority**: This is the core value proposition of SPECTRA. Without test generation, the tool provides no differentiated value.

**Independent Test**: Can be fully tested by running `spectra ai generate --suite checkout` against a docs folder and verifying test files are created with valid format.

**Acceptance Scenarios**:

1. **Given** a configured repository with documentation in `docs/features/checkout/`, **When** the user runs `spectra ai generate --suite checkout`, **Then** the system generates test case files in `tests/checkout/` with valid YAML frontmatter.
2. **Given** an active AI session, **When** the agent generates tests, **Then** each test case covers exactly one scenario and includes explicit test data.
3. **Given** generated tests, **When** the user reviews them, **Then** they can accept all valid tests, review one-by-one, or view potential duplicates.
4. **Given** a `--dry-run` flag, **When** the user runs generation, **Then** the system validates and displays results without writing files.
5. **Given** a `--no-review` flag (CI mode), **When** the user runs generation, **Then** the system writes all valid tests without interactive prompts.

---

### User Story 3 - Validate Test Files (Priority: P1)

A QA engineer wants to ensure all test files in the repository are valid before committing changes. They run a validation command that checks schema compliance, ID uniqueness, and index freshness.

**Why this priority**: Validation is essential for CI/CD pipelines and maintaining test quality. It's foundational for the entire test management workflow.

**Independent Test**: Can be fully tested by running `spectra validate` against test files and verifying exit codes and error messages.

**Acceptance Scenarios**:

1. **Given** valid test files in `tests/`, **When** the user runs `spectra validate`, **Then** the system exits with code 0 and reports success.
2. **Given** a test file with invalid YAML frontmatter, **When** the user runs `spectra validate`, **Then** the system exits with code 1 and reports the specific validation error.
3. **Given** duplicate test IDs across suites, **When** the user runs `spectra validate`, **Then** the system reports which IDs are duplicated and in which files.
4. **Given** an outdated `_index.json`, **When** the user runs `spectra validate`, **Then** the system reports the index needs rebuilding.

---

### User Story 4 - Build Metadata Indexes (Priority: P2)

A QA engineer has manually edited test files or added new ones. They need to rebuild the metadata indexes (`_index.json`) for each suite so that the system can efficiently query and filter tests.

**Why this priority**: Indexes are required for efficient test selection and filtering, but tests can technically exist without them initially.

**Independent Test**: Can be fully tested by running `spectra index` and verifying `_index.json` files are created/updated in each suite folder.

**Acceptance Scenarios**:

1. **Given** test files in `tests/checkout/`, **When** the user runs `spectra index`, **Then** the system creates or updates `tests/checkout/_index.json` with metadata for all tests.
2. **Given** multiple suites, **When** the user runs `spectra index`, **Then** all suite indexes are rebuilt.
3. **Given** a deleted test file, **When** the user runs `spectra index`, **Then** the index no longer contains the deleted test's metadata.

---

### User Story 5 - Update Tests from Changed Documentation (Priority: P2)

Documentation has changed since tests were generated. The QA engineer wants to identify outdated tests, orphaned tests (feature removed), and redundant tests, then batch-update them to match current documentation.

**Why this priority**: Keeping tests in sync with documentation is critical for long-term maintenance, but requires generation to exist first.

**Independent Test**: Can be fully tested by modifying documentation, running `spectra ai update --suite checkout`, and verifying proposed changes.

**Acceptance Scenarios**:

1. **Given** tests generated from documentation that has since changed, **When** the user runs `spectra ai update --suite checkout`, **Then** the system identifies tests as UP_TO_DATE, OUTDATED, ORPHANED, or REDUNDANT.
2. **Given** proposed updates, **When** the user reviews changes, **Then** they see a diff view of what will change in each test.
3. **Given** accepted updates, **When** the system applies changes, **Then** it updates test files and rebuilds the index.
4. **Given** a `--dry-run` flag, **When** the user runs update, **Then** the system shows proposed changes without applying them.

---

### User Story 6 - Analyze Test Coverage (Priority: P3)

A QA lead wants to understand test coverage gaps, redundant tests, and priority distribution across their test suites. They run an analysis command that produces a coverage report without modifying any files.

**Why this priority**: Analysis is valuable for planning but not required for basic test generation and maintenance.

**Independent Test**: Can be fully tested by running `spectra ai analyze` and verifying a coverage report is produced.

**Acceptance Scenarios**:

1. **Given** a suite with tests, **When** the user runs `spectra ai analyze --suite checkout`, **Then** the system produces a report showing coverage gaps, redundant tests, and priority distribution.
2. **Given** a `--format json` flag, **When** the user runs analysis, **Then** the output is machine-readable JSON.
3. **Given** a `--output report.md` flag, **When** the user runs analysis, **Then** the report is written to the specified file.

---

### User Story 7 - Use Multiple AI Providers (Priority: P3)

A team uses GitHub Copilot as their primary AI provider but wants fallback to Anthropic Claude when quota is exhausted. They configure a provider chain that automatically switches providers on failure.

**Why this priority**: Provider flexibility is important for enterprise adoption but the tool works with a single provider initially.

**Independent Test**: Can be fully tested by configuring multiple providers and verifying fallback behavior when primary provider fails.

**Acceptance Scenarios**:

1. **Given** a provider chain with Copilot (priority 1) and Anthropic (priority 2), **When** Copilot returns a quota error, **Then** the system automatically switches to Anthropic and logs the switch.
2. **Given** a `--provider anthropic` flag, **When** the user runs a command, **Then** the system uses only the specified provider regardless of chain configuration.
3. **Given** `fallback_strategy: manual`, **When** the primary provider fails, **Then** the system prompts the user before switching.

---

### Edge Cases

- What happens when the docs folder is empty or contains no Markdown files?
- How does the system handle documentation files larger than the configured maximum size?
- **Partial batch failure**: When the AI provider returns an error mid-generation, the system MUST save all valid tests generated before the failure, then prompt the user whether to retry the remaining generation or exit with the partial results.
- **Concurrent generation**: When multiple processes attempt to generate tests for the same suite, the system MUST acquire a suite-level lock file; if locked, fail immediately with a clear message; locks auto-expire after 10 minutes to prevent deadlock from crashed processes.
- What happens when test IDs in the index don't match the actual files?
- How does the system handle malformed YAML frontmatter that crashes the parser?
- What happens when the user cancels an interactive review session mid-way?

## Requirements *(mandatory)*

### Functional Requirements

#### Initialization & Configuration

- **FR-001**: System MUST create `spectra.config.json` with sensible defaults when `spectra init` is run
- **FR-002**: System MUST create `docs/`, `tests/`, and `.github/skills/` folder structure on initialization
- **FR-003**: System MUST preserve existing files and only create missing structure during initialization
- **FR-004**: System MUST support configuration via `spectra.config.json` at repository root

#### Test Generation

- **FR-005**: System MUST read documentation from the configured source folder (`docs/` by default)
- **FR-006**: System MUST build a lightweight document map (paths, titles, headings, preview) before AI processing
- **FR-007**: System MUST generate test cases as Markdown files with YAML frontmatter
- **FR-008**: System MUST ensure each generated test covers exactly one scenario
- **FR-009**: System MUST auto-populate `source_refs` in test frontmatter with the documentation files used
- **FR-010**: System MUST check for duplicate tests before writing (title and step similarity)
- **FR-011**: System MUST provide batch review UX showing summary, then options (accept all, review one-by-one, view duplicates)
- **FR-012**: System MUST support `--dry-run` flag for validation without file writes
- **FR-013**: System MUST support `--no-review` flag for CI automation without interactive prompts
- **FR-014**: System MUST allocate unique, sequential test IDs within a suite
- **FR-033**: System MUST preserve valid tests generated before a mid-batch AI provider failure and prompt the user to retry or exit with partial results

#### Validation & Indexing

- **FR-015**: System MUST validate YAML frontmatter schema for all test files
- **FR-016**: System MUST ensure all test IDs are unique across the entire repository
- **FR-017**: System MUST verify all `depends_on` references point to existing test IDs
- **FR-018**: System MUST verify all `priority` values are in allowed enum (high, medium, low)
- **FR-019**: System MUST return exit code 0 for valid state, exit code 1 for errors (CI-compatible)
- **FR-020**: System MUST build and maintain `_index.json` metadata files per suite

#### Test Updates

- **FR-021**: System MUST compare existing tests against current documentation to detect drift
- **FR-022**: System MUST classify tests as UP_TO_DATE, OUTDATED, ORPHANED, or REDUNDANT
- **FR-023**: System MUST provide diff view for proposed test updates
- **FR-024**: System MUST support batch update operations with review workflow

#### Provider Chain

- **FR-025**: System MUST support multiple AI providers in priority order
- **FR-026**: System MUST automatically fallback to next provider on failure (rate limit, quota, auth error)
- **FR-027**: System MUST support BYOK (Bring Your Own Key) for OpenAI, Azure AI, and Anthropic
- **FR-028**: System MUST support `--provider` flag to override provider chain for single command
- **FR-029**: System MUST log provider switches when automatic fallback occurs

#### Safety & Constraints

- **FR-030**: AI agent MUST NOT write files directly; all output MUST go through validated tool handlers
- **FR-031**: System MUST sanitize all file paths (reject `..`, `/`, `\`, null bytes)
- **FR-032**: System MUST cap individual documentation files at configurable maximum size

#### Observability

- **FR-034**: System MUST emit structured logs (JSON-capable) for all operations including provider switches, validation results, and file writes
- **FR-035**: System MUST support verbosity flags (`-v` for info, `-vv` for debug) to control log detail level
- **FR-036**: System MUST output errors and warnings to stderr, normal output to stdout

#### Concurrency

- **FR-037**: System MUST acquire a suite-level lock file before write operations (generate, update, index)
- **FR-038**: System MUST fail immediately with a clear error message if the suite lock is held by another process
- **FR-039**: System MUST auto-expire lock files after 10 minutes to prevent deadlock from crashed processes

### Key Entities

- **Test Case**: A manual test stored as a Markdown file with YAML frontmatter containing id, priority, tags, component, steps, and expected results
- **Test Suite**: A collection of tests in a folder (e.g., `tests/checkout/`) with a shared `_index.json`
- **Metadata Index**: The `_index.json` file containing lightweight metadata for all tests in a suite, enabling efficient filtering without parsing all Markdown
- **Document Map**: A lightweight structure listing all documentation files with titles, headings, and previews for AI context selection
- **Provider Chain**: An ordered list of AI providers with fallback rules

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can generate a complete test suite (15+ tests) for a feature in under 5 minutes from documentation
- **SC-002**: Generated tests pass validation without manual correction 90% of the time
- **SC-003**: Duplicate detection catches 80% of semantically similar tests before writing
- **SC-004**: Validation command completes within 2 seconds for repositories with up to 500 test files
- **SC-005**: Index rebuild completes within 5 seconds for repositories with up to 500 test files
- **SC-006**: Users can review and accept/reject a batch of 20 tests in under 2 minutes
- **SC-007**: Provider fallback activates automatically within 3 seconds of primary provider failure
- **SC-008**: 95% of users can successfully initialize and generate their first test suite without documentation (intuitive CLI)
- **SC-009**: CI pipelines using `spectra validate` receive deterministic pass/fail within 10 seconds

## Assumptions

- Teams have existing product documentation in Markdown format (or will create it)
- Users have access to at least one AI provider (GitHub Copilot subscription or BYOK API key)
- Repository uses Git for version control
- Test cases follow the SPECTRA Markdown format with YAML frontmatter
- Documentation structure follows reasonable conventions (headings, sections) even if not enforced

## Clarifications

### Session 2026-03-13

- Q: What happens when the AI provider returns an error mid-generation (partial batch)? → A: Keep valid tests, prompt user to continue or exit with partial results
- Q: What logging/observability approach should the CLI use? → A: Structured logging with configurable verbosity (-v/-vv flags), JSON-capable output
- Q: How should the system handle concurrent generation attempts for the same suite? → A: Lock file with timeout - acquire suite-level lock, fail fast if locked, auto-expire after 10 min
