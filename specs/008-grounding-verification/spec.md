# Feature Specification: Grounding Verification Pipeline

**Feature Branch**: `008-grounding-verification`
**Created**: 2026-03-19
**Status**: Draft
**Input**: User description: "Add grounding verification pipeline to SPECTRA test generation that uses a second 'critic' model to verify generated tests against source documentation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automatic Grounding Verification (Priority: P1)

A QA lead generates test cases using `spectra ai generate` and wants confidence that the generated tests accurately reflect the source documentation without invented steps or expected results.

**Why this priority**: This is the core value proposition — preventing hallucinated test content from reaching the test suite. Without this, generated tests may contain fabricated details that waste QA time or create false confidence in test coverage.

**Independent Test**: Run `spectra ai generate --suite checkout`, observe verification progress, confirm grounded tests are written with metadata and hallucinated tests are rejected with clear explanations.

**Acceptance Scenarios**:

1. **Given** a user runs test generation with critic enabled, **When** the generator produces tests, **Then** each test is automatically verified against source documentation before being written
2. **Given** a test contains only claims traceable to documentation, **When** the critic evaluates it, **Then** the test receives a "grounded" verdict and is written with grounding metadata
3. **Given** a test contains invented behaviors not in documentation, **When** the critic evaluates it, **Then** the test receives a "hallucinated" verdict and is rejected (not written to disk)
4. **Given** a test has some verified claims and some unverified assumptions, **When** the critic evaluates it, **Then** the test receives a "partial" verdict and is written with a warning marker and list of unverified claims

---

### User Story 2 - Clear Verification Feedback (Priority: P1)

A test author wants to understand why tests were accepted, flagged, or rejected so they can improve documentation or refine test generation focus areas.

**Why this priority**: Without clear feedback, users cannot take corrective action. The value of verification is diminished if users don't understand the results.

**Independent Test**: Generate tests with mixed verdicts, confirm CLI output shows symbols (✓ ⚠ ✗), summary counts, and detailed explanations for partial/rejected tests.

**Acceptance Scenarios**:

1. **Given** verification completes, **When** the CLI displays results, **Then** grounded tests show ✓, partial tests show ⚠, and hallucinated tests show ✗
2. **Given** tests receive partial verdicts, **When** results are displayed, **Then** each partial test lists its specific unverified claims with context
3. **Given** tests are rejected as hallucinated, **When** results are displayed, **Then** the reason for rejection is clearly explained (e.g., "References 'fraud detection API' — not mentioned in documentation")

---

### User Story 3 - Configure Critic Model (Priority: P2)

An organization wants to use their preferred verification model (e.g., Gemini Flash for cost efficiency, GPT-4o-mini for speed) rather than a hardcoded default.

**Why this priority**: Different organizations have different API access, cost constraints, and model preferences. Configuration flexibility enables broader adoption.

**Independent Test**: Set critic provider in config file, run generation, confirm the configured model is used for verification (visible in grounding metadata).

**Acceptance Scenarios**:

1. **Given** a critic provider is configured in spectra.config.json, **When** verification runs, **Then** the configured model is used and recorded in test metadata
2. **Given** no critic is configured, **When** generation runs, **Then** tests are written without verification (backward-compatible behavior)
3. **Given** critic is configured but API authentication fails, **When** generation runs, **Then** a clear error message is shown and user can choose to proceed without verification

---

### User Story 4 - Skip Verification When Needed (Priority: P2)

A developer working with incomplete documentation wants to generate draft tests quickly without waiting for verification that would reject most tests.

**Why this priority**: Flexibility for rapid iteration. Users may intentionally work with sparse documentation during early feature development.

**Independent Test**: Run `spectra ai generate --skip-critic`, confirm tests are written immediately without verification step or grounding metadata.

**Acceptance Scenarios**:

1. **Given** the --skip-critic flag is provided, **When** generation completes, **Then** tests are written without verification and without grounding metadata
2. **Given** --skip-critic is used, **When** CLI output is shown, **Then** a notice indicates verification was skipped

---

### User Story 5 - Grounding Metadata in Test Files (Priority: P2)

A team lead reviewing test files wants to see verification results inline so they know which tests have been validated and which have unverified assumptions.

**Why this priority**: Persistent metadata enables downstream tooling, dashboards, and audit trails. It also helps during code review.

**Independent Test**: Generate verified tests, open the test files, confirm grounding metadata is present in YAML frontmatter with verdict, score, models used, and any unverified claims.

**Acceptance Scenarios**:

1. **Given** a grounded test is written, **When** the file is opened, **Then** frontmatter includes grounding.verdict="grounded", grounding.score, grounding.generator, grounding.critic, grounding.verified_at
2. **Given** a partial test is written, **When** the file is opened, **Then** frontmatter includes grounding.unverified_claims with specific descriptions
3. **Given** critic is configured but --skip-critic is used, **When** tests are written, **Then** no grounding metadata is added

---

### User Story 6 - Disable Critic Globally (Priority: P3)

An administrator wants to disable verification for all users in a repository (e.g., for a repository with no formal documentation yet).

**Why this priority**: Some repositories may not be ready for verification. Global disable prevents confusion and unnecessary error messages.

**Independent Test**: Set ai.critic.enabled=false in config, run generation, confirm no verification occurs and no errors are shown.

**Acceptance Scenarios**:

1. **Given** ai.critic.enabled is false, **When** generation runs, **Then** tests are written without verification (no spinner, no verdicts)
2. **Given** ai.critic is not present in config, **When** generation runs, **Then** tests are written without verification (backward compatible)

---

### Edge Cases

- What happens when critic model is unreachable mid-batch? System fails gracefully with option to write unverified tests
- What happens when source documentation is empty? All tests marked as unverified since nothing to ground against
- What happens when a test references multiple source documents? All referenced documents are provided to critic for verification
- How does system handle very large documentation files? Send relevant sections based on source_refs, not entire files
- What happens when critic returns malformed response? Log warning, treat test as unverified, allow user to proceed

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST verify each generated test against source documentation using a configured critic model before writing to disk
- **FR-002**: System MUST classify each test with one of three verdicts: grounded, partial, or hallucinated
- **FR-003**: System MUST write grounded tests to disk with grounding metadata in frontmatter
- **FR-004**: System MUST write partial tests to disk with grounding metadata including unverified_claims list
- **FR-005**: System MUST reject hallucinated tests and NOT write them to disk
- **FR-006**: System MUST display verification progress during generation (spinner with model name)
- **FR-007**: System MUST display summary counts of grounded, partial, and hallucinated tests after verification
- **FR-008**: System MUST display detailed reasons for partial verdicts (which claims were unverified)
- **FR-009**: System MUST display detailed reasons for hallucinated verdicts (which claims were fabricated)
- **FR-010**: System MUST support --skip-critic flag to bypass verification
- **FR-011**: System MUST read critic configuration from ai.critic section in spectra.config.json
- **FR-012**: System MUST support configuring critic provider, model, and API key environment variable
- **FR-013**: System MUST fall back to no-verification mode if critic is not configured or disabled
- **FR-014**: System MUST record grounding.generator with the model that created the test
- **FR-015**: System MUST record grounding.critic with the model that verified the test
- **FR-016**: System MUST record grounding.verified_at with timestamp of verification
- **FR-017**: System MUST record grounding.score as a confidence value between 0.0 and 1.0
- **FR-018**: System MUST provide clear error messages if critic API authentication fails
- **FR-019**: System MUST allow user to proceed without verification if critic model is unavailable
- **FR-020**: System MUST use visual symbols (✓ ⚠ ✗) for verdict display in CLI output

### Key Entities

- **Verification Verdict**: Classification result from critic (grounded/partial/hallucinated) with confidence score
- **Grounding Metadata**: Persistent record of verification result stored in test frontmatter (verdict, score, generator, critic, timestamp, unverified claims)
- **Critic Finding**: Individual assessment of a specific claim (step, expected result, precondition) including status, evidence, and reasoning
- **Critic Configuration**: Settings for the verification model including provider, model name, API key reference, and enabled flag

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Verification adds no more than 2 seconds per test on average (batch verification completes in under 30 seconds for 20 tests)
- **SC-002**: 95% of hallucinated test content is correctly identified and rejected (measured against human review baseline)
- **SC-003**: Users can understand why a test was flagged within 10 seconds of reading the verification output
- **SC-004**: Generated test suites contain less than 5% unverified claims after verification (compared to pre-verification baseline)
- **SC-005**: Zero breaking changes to existing workflows — generation without critic configured works exactly as before

## Assumptions

- Critic models (Gemini Flash, GPT-4o-mini) are capable of performing natural language inference tasks with sufficient accuracy
- Source documentation is available in Markdown format in the docs/ directory as per existing SPECTRA conventions
- The cost of critic API calls is acceptable (estimated <$0.01 per batch of 20 tests based on typical token usage)
- Test steps and expected results can be meaningfully compared to documentation text for grounding verification
- Users have API access to at least one verification-capable model provider

## Dependencies

- Existing AI provider infrastructure in Spectra.CLI (AgentRuntime, provider chain)
- Existing test file parser and frontmatter handling
- Existing spectra.config.json configuration system
- External API access to critic model providers (Google, OpenAI, etc.)
