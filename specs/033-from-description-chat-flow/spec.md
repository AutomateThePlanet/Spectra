# Feature Specification: From-Description Chat Flow & Doc-Aware Manual Tests

**Feature Branch**: `033-from-description-chat-flow`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: Add dedicated SKILL flow and agent intent routing for `spectra ai generate --from-description`, plus enhance `UserDescribedGenerator` to load doc/criteria context as best-effort formatting reference.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Specific test creation from chat (Priority: P1)

A test author working in Copilot Chat says "Add a test for double-click submit creating duplicate orders." The chat assistant immediately routes this through a short, single-test flow — no analysis question, no count question — and produces exactly one test case in the right suite, populated with steps and expected results that match the described behavior.

**Why this priority**: This is the most common friction users hit today. The current chat flow forces every "create a test for X" request through the full analyze → approve → generate path, adding two extra round-trips and a count prompt for what should be a single-test creation.

**Independent Test**: Open Copilot Chat in a project, type "Add a test for [specific behavior]", and verify (1) the agent does NOT run analysis, (2) the agent does NOT ask how many tests to generate, and (3) exactly one test file is created with the described behavior captured as title/steps/expected.

**Acceptance Scenarios**:

1. **Given** a user types a request shaped like a test title ("Add a test that verifies X"), **When** the chat agent processes it, **Then** the agent uses the from-description flow and creates exactly one test without asking about count or running analysis.
2. **Given** a user types a topic-shaped request ("Generate tests for the payments module"), **When** the chat agent processes it, **Then** the agent uses the main generation flow with `--focus` and runs analysis first.
3. **Given** a user request is ambiguous, **When** the agent must choose a flow, **Then** it picks from-description for behavior descriptions and focus for topic words, without asking clarifying questions about count or scope.

---

### User Story 2 - Doc-aware manual test creation (Priority: P1)

When a user creates a manual test from a description, the system uses any available documentation and acceptance criteria as formatting context — so the resulting test aligns with real product terminology, navigation paths, and known criteria — without turning the user's description into a verification target.

**Why this priority**: Manual tests created today are isolated from the rest of the project. They never link to docs (`source_refs` empty) or criteria (`criteria` empty), which means coverage reports under-count their value and tests drift from product language.

**Independent Test**: In a project with `docs/_index.md` and per-document `.criteria.yaml` files, run `spectra ai generate --suite checkout --from-description "expired session redirects to login"` and verify the produced test (1) has steps using the same terminology as the docs, (2) has `source_refs` populated with the docs the AI used, (3) has `criteria` populated with any matching criteria IDs, and (4) still has `grounding.verdict: manual`.

**Acceptance Scenarios**:

1. **Given** a project with `docs/_index.md` containing docs that match the suite or component, **When** the user runs from-description, **Then** the handler loads matching doc content as context and the resulting test's `source_refs` field lists those docs.
2. **Given** a project with per-document `.criteria.yaml` files matching the suite, **When** the user runs from-description, **Then** the handler loads matching criteria as context and the resulting test's `criteria` field lists IDs the AI matched to the description.
3. **Given** a project with NO `docs/_index.md` and NO criteria files, **When** the user runs from-description, **Then** the flow completes successfully with `source_refs` and `criteria` empty — no errors, no warnings, identical to the current behavior.
4. **Given** doc context is loaded, **When** the test is written, **Then** `grounding.verdict` is still `manual` — doc context is for formatting, not for verification.

---

### User Story 3 - Discoverable from-description in SKILL (Priority: P2)

A test author scanning the `spectra-generate` SKILL file can find a clearly labelled "create a specific test case" section with its own step sequence, distinct from the main generation flow.

**Why this priority**: This is the documentation-discoverability slice. Without it, even a correctly-implemented agent prompt can be undermined when a user reads the SKILL directly and sees only the analyze→generate flow.

**Independent Test**: Open `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`, search for "specific test case" — the section exists with its own numbered step sequence and references `--from-description`.

**Acceptance Scenarios**:

1. **Given** the spectra-generate SKILL file, **When** a user reads it, **Then** they find a section titled "When the user wants to create a specific test case" with its own step list.
2. **Given** the spectra-generate SKILL file, **When** a user reads it, **Then** they find a routing table that maps user intent signals (topic vs scenario) to the correct flow (focus vs from-description vs from-suggestions).
3. **Given** the from-description step in the SKILL, **When** the documented command is examined, **Then** it includes `--no-interaction --output-format json --verbosity quiet` like all other SKILL-wrapped commands.

### Edge Cases

- A user description contradicts loaded docs → user description is the source of truth; the test reflects the description, not the docs.
- No matching docs/criteria exist → flow proceeds exactly as before; `source_refs` and `criteria` remain empty.
- Multiple matching docs exist → handler caps doc context at 3 docs and 8000 chars per doc (same limits as the critic).
- Hybrid request like "Add some negative tests for checkout" → topic-shaped + plural; routes to main generation flow with `--focus "negative"`.
- Question-shaped request ("Can you verify X works?") → treated as a scenario description; routes to from-description.
- Doc/criteria load fails (file not found, parse error) → silently fall back to no-context behavior; do not fail the command.

## Requirements *(mandatory)*

### Functional Requirements

#### SKILL & Agent Content (visible to chat)

- **FR-001**: The `spectra-generate` SKILL file MUST contain a dedicated "When the user wants to create a specific test case" section with a numbered step sequence (open progress page → run command → await → read result → present).
- **FR-002**: The `spectra-generate` SKILL file MUST contain an intent-to-flow routing table mapping at least three intents (explore feature area → focus, create specific test → from-description, generate from suggestions → from-suggestions).
- **FR-003**: The from-description command documented in the SKILL MUST include the standard SKILL-wrapped flags `--no-interaction --output-format json --verbosity quiet`.
- **FR-004**: The from-description SKILL section MUST explicitly state the flow produces exactly 1 test, runs no analysis, and asks no count question.
- **FR-005**: The `spectra-generation` agent prompt MUST contain a "Test Creation Intent Routing" section that classifies user requests into at least three intents (explore area, create specific test, create from suggestions) with example phrasings.
- **FR-006**: The agent prompt routing MUST explicitly delegate "create a specific test" intents to the from-description flow and "explore feature area" intents to the focus flow.
- **FR-007**: The agent prompt routing rules MUST instruct the agent NOT to ask clarifying questions about count or scope when routing — the topic-vs-scenario distinction is the only signal needed.

#### Doc-aware `--from-description` (CLI behavior)

- **FR-008**: When `--from-description` is invoked and `docs/_index.md` (or source documentation) exists, the handler MUST attempt to load documents whose suite or component matches the target suite, capped at 3 documents and 8000 characters per document.
- **FR-009**: When `--from-description` is invoked and per-document `.criteria.yaml` files exist matching the suite, the handler MUST load matching criteria as context using the same matching logic as the main generation flow.
- **FR-010**: The handler MUST pass loaded doc context and criteria context to `UserDescribedGenerator` via optional parameters; existing callers without context MUST continue to work unchanged.
- **FR-011**: When doc context is loaded, the AI prompt used by `UserDescribedGenerator` MUST include a "Reference Documentation (for formatting context only)" section instructing the AI to use docs for terminology and navigation but NOT to verify the user's description against them.
- **FR-012**: When criteria context is loaded, the AI prompt MUST include a "Related Acceptance Criteria" section instructing the AI to populate the test's `criteria` field with any matching criteria IDs.
- **FR-013**: When doc context is used, the resulting test's `source_refs` field MUST contain the doc paths that were loaded as context.
- **FR-014**: When criteria context is used, the resulting test's `criteria` field MUST contain the criteria IDs the AI identified as matching the description.
- **FR-015**: The resulting test's `grounding.verdict` MUST always be `manual` for the from-description flow, regardless of whether doc/criteria context was loaded.
- **FR-016**: When no source documentation exists and no `.criteria.yaml` files exist, the from-description flow MUST behave exactly as it does today — no errors, no warnings, no behavioral change.
- **FR-017**: Doc and criteria loading MUST be best-effort and non-blocking — failures (file not found, parse error, AI returns no matches) MUST NOT cause the from-description command to fail.

### Key Entities

- **Doc context**: A formatted string of relevant documentation content passed to the AI as a reference. Bounded by document count (3) and per-doc character cap (8000). Sourced from files matched by suite/component to the target suite.
- **Criteria context**: A formatted string of acceptance criteria entries (`ID [RFC2119] text`) sourced from per-document `.criteria.yaml` files matching the target suite. Same matching logic as the main generation flow.
- **Manual test grounding metadata**: Always `verdict: manual`, `score: 1.0`, `critic: user-described`. Independent of whether doc/criteria context was loaded.
- **Intent classification**: Topic-shaped phrases ("error handling", "payment module") route to `--focus`. Scenario-shaped phrases ("X creates duplicate Y", "expired session redirects to login") route to `--from-description`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user creating a single test from chat ("Add a test for X") completes the request in one round-trip with no analysis step and no count question — measurable as: zero behavior-analyzer invocations on the from-description path.
- **SC-002**: When a project has documentation and criteria, from-description tests written for matching suites have non-empty `source_refs` and may have non-empty `criteria` (criteria population depends on AI match).
- **SC-003**: When a project has no documentation, from-description tests still complete successfully (zero new failures vs. baseline).
- **SC-004**: Every from-description test, regardless of doc context, has `grounding.verdict: manual`.
- **SC-005**: A reader of the `spectra-generate` SKILL or `spectra-generation` agent prompt can identify the correct flow for a given user request in under 30 seconds without consulting other documentation.
- **SC-006**: All existing `spectra ai generate` flows (main generation, `--from-suggestions`, `--analyze-only`) remain unchanged in behavior — measurable as: zero regressions in the existing test suite.

## Assumptions

- The existing `LoadCriteriaContextAsync` helper in `GenerateHandler` is the right primitive for criteria-loading; it already handles "match by component, source doc, file name" rules used by the main generation flow.
- The existing `SourceDocumentLoader` is the right primitive for loading documents; the from-description path will use the same matching strategy as the main generation path (filter by file name / suite name match).
- The 3-doc / 8000-char cap matches the critic budget and is a reasonable starting point.
- Empty `criteria` field on output is acceptable when no criteria match — the AI is responsible for emitting only IDs it can justify.
- The `grounding.verdict: manual` contract is permanent. Doc context never upgrades a manual test to grounded — that would require running the critic, which from-description explicitly skips.
- Existing `UserDescribedGenerator` callers (none outside `GenerateHandler` today) will not break because new context parameters are optional with `null` defaults.
