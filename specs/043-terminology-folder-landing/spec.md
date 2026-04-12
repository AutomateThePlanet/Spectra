# Feature Specification: Terminology, Folder Rename & Landing Page

**Feature Branch**: `043-terminology-folder-landing`  
**Created**: 2026-04-12  
**Status**: Draft  
**Input**: Rename default output folder from `tests/` to `test-cases/`, standardize "test case" terminology across all docs/SKILLs/agents, rewrite landing page with value proposition, and replace the cli-vs-chat-generation.md analysis with a concise 150-word page.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Default Output Folder Rename (Priority: P1)

A developer initializes a new SPECTRA project. The generated folder structure uses `test-cases/` as the default output directory for generated test case files, clearly distinguishing SPECTRA's markdown test case documents from automated test code that typically lives in `tests/`.

**Why this priority**: The folder rename is a breaking change that touches code, configuration, test fixtures, and demo repos. It must land first so all subsequent documentation and terminology changes reference the correct folder name. With zero external users, the cost of this breaking change is near zero now but would require migration logic after public launch.

**Independent Test**: Can be fully tested by running `spectra init` in a clean directory and verifying the created folder structure uses `test-cases/`.

**Acceptance Scenarios**:

1. **Given** a clean directory with no SPECTRA configuration, **When** a user runs `spectra init`, **Then** the system creates a `test-cases/` directory (not `tests/`).
2. **Given** a project initialized with the new defaults, **When** a user runs `spectra ai generate --suite checkout`, **Then** generated test case files are written to `test-cases/checkout/`.
3. **Given** the embedded configuration template, **When** the default `tests.dir` value is inspected, **Then** it reads `"test-cases/"`.
4. **Given** a user has an existing project with a custom `tests.dir` value in `spectra.config.json`, **When** they upgrade SPECTRA, **Then** their custom path is respected (no forced migration).

---

### User Story 2 - Consistent "Test Case" Terminology (Priority: P2)

A new user reads SPECTRA documentation, SKILLs, and agent descriptions. Every reference to SPECTRA's markdown output consistently uses "test case" (or "test cases"), clearly distinguishing it from "automated test" (code), "test data" (Testimize values), and "test run" (MCP execution session).

**Why this priority**: Terminology confusion compounds as documentation grows. Standardizing now while the docs corpus is manageable prevents a much larger cleanup later. This change is purely textual with no code impact, making it safe to do after Phase 1.

**Independent Test**: Can be tested by searching all documentation, SKILL descriptions, and agent descriptions for instances where "test" alone (without "case") refers to SPECTRA's markdown output.

**Acceptance Scenarios**:

1. **Given** the project tagline in the landing page, README, and PROJECT-KNOWLEDGE.md, **When** inspected, **Then** it reads "AI-native test case generation and execution framework".
2. **Given** all SKILL description fields, **When** they reference SPECTRA's markdown output, **Then** they use "test cases" not "tests".
3. **Given** both agent description fields, **When** they reference SPECTRA's markdown output, **Then** they use "test cases".
4. **Given** the testimize-integration.md documentation, **When** it discusses both SPECTRA output and Testimize values, **Then** it clearly distinguishes "test cases" (SPECTRA) from "test data" (Testimize).
5. **Given** configuration keys (`tests.dir`, `test_id_pattern`) and CLI commands (`spectra ai generate`, `spectra validate`), **When** inspected, **Then** they remain unchanged (no config/CLI renaming).
6. **Given** compound terms like "test run", "test suite", "test ID", and "test format", **When** encountered, **Then** they are left as-is (these are distinct concepts, not references to SPECTRA output).

---

### User Story 3 - Landing Page Value Proposition (Priority: P3)

A potential user visits the SPECTRA landing page. Instead of a feature list, they see a clear value proposition: what problem SPECTRA solves, how it compares to manual test case management, and how to get started in three commands. The page positions Copilot Chat as the recommended interface.

**Why this priority**: The landing page is the first impression for new users. It needs to communicate value clearly. However, it has no code impact and can be done after the folder rename and terminology changes are complete.

**Independent Test**: Can be tested by rendering the landing page and verifying it contains a comparison table, consistent "test case" terminology, and the Chat-first workflow example.

**Acceptance Scenarios**:

1. **Given** the landing page content, **When** rendered, **Then** it includes a comparison table contrasting manual test case management with SPECTRA.
2. **Given** the landing page content, **When** inspected for terminology, **Then** it uses "test case" consistently (not bare "test" when referring to SPECTRA output).
3. **Given** the landing page content, **When** a user looks for how to start, **Then** it shows the Chat-first workflow ("just say it in Copilot Chat") alongside the CLI quick-install.

---

### User Story 4 - Simplified CLI-vs-Chat Page (Priority: P3)

A user navigating SPECTRA documentation finds the "How Test Case Generation Works" page. It concisely explains that Chat and CLI use the same pipeline, positions Chat as the recommended interface, and explains why the CLI pipeline architecture matters for critic independence. The page replaces the previous 4000-word analysis that incorrectly framed CLI and Chat as competing approaches.

**Why this priority**: Same priority as the landing page since it is a content-only change with no code impact. The existing page creates confusion by framing Chat as hypothetical and inferior when it is actually the primary interface calling the CLI through SKILLs.

**Independent Test**: Can be tested by verifying the page is under 200 words, positions Chat as primary, and contains no competitive framing between CLI and Chat.

**Acceptance Scenarios**:

1. **Given** the cli-vs-chat-generation.md file, **When** its word count is measured, **Then** it is under 200 words.
2. **Given** the page content, **When** it describes the two usage modes, **Then** Chat is positioned as "recommended" and CLI as "the engine" useful for CI/CD.
3. **Given** the page content, **When** inspected for framing, **Then** there is no competitive language comparing CLI vs Chat as alternatives with tradeoffs.

---

### Edge Cases

- What happens when a user has an existing project with `tests.dir` set to `"tests/"` in their config? Their custom value is respected; only the default changes.
- What happens when documentation references `tests/` as a folder path in examples? All documentation examples must be updated to show `test-cases/`.
- How does the terminology change interact with the `test-format.md` page? It already says "Test Case Format", so no change needed.
- What about pluralization? "Test case" (singular) and "test cases" (plural) are both correct depending on context.

## Requirements *(mandatory)*

### Functional Requirements

**Phase 1 - Folder Rename:**

- **FR-001**: The default value for the test output directory MUST be `"test-cases/"` in the embedded configuration template.
- **FR-002**: The `spectra init` command MUST create a `test-cases/` directory in the project root.
- **FR-003**: All hardcoded references to `"tests/"` as the default SPECTRA output directory in source code MUST be updated to `"test-cases/"`.
- **FR-004**: All test fixtures that use `"tests/"` as a configured test directory MUST be updated to `"test-cases/"`.
- **FR-005**: Both demo repositories (Spectra_Demo and Spectra_Demo_Desktop) MUST have their test case folders renamed from `tests/` to `test-cases/` and their `spectra.config.json` updated accordingly.
- **FR-006**: No migration logic MUST be implemented for existing users (zero external users assumption).

**Phase 2 - Terminology:**

- **FR-007**: The project tagline MUST read "AI-native test case generation and execution framework" in the landing page, README, and PROJECT-KNOWLEDGE.md.
- **FR-008**: All documentation files MUST use "test case" or "test cases" when referring to SPECTRA's markdown output documents.
- **FR-009**: All SKILL description fields MUST use "test cases" when referring to SPECTRA's markdown output.
- **FR-010**: Both agent description fields MUST use "test cases" when referring to SPECTRA's markdown output.
- **FR-011**: The testimize-integration.md documentation MUST clearly distinguish between "test cases" (SPECTRA output) and "test data" (Testimize-generated values).
- **FR-012**: Configuration keys (`tests.dir`, `test_id_pattern`) and CLI command names MUST NOT be renamed.
- **FR-013**: Compound terms ("test run", "test suite", "test ID") MUST NOT be changed.

**Phase 3 - Landing Page:**

- **FR-014**: The landing page MUST include a comparison table showing manual test case management versus SPECTRA.
- **FR-015**: The landing page MUST show the Chat-first workflow with a Copilot Chat example.
- **FR-016**: The landing page MUST include a quick-install section with three commands.

**Phase 4 - CLI-vs-Chat Page:**

- **FR-017**: The cli-vs-chat-generation.md page MUST be replaced with a concise version under 200 words.
- **FR-018**: The replacement page MUST position Copilot Chat as the recommended interface and CLI as the engine.
- **FR-019**: The replacement page MUST NOT contain competitive framing between CLI and Chat.

## Assumptions

- **Zero external users**: Only the two demo repos (Spectra_Demo and Spectra_Demo_Desktop) need migration. No deprecation period or backward compatibility required.
- **Spec 040A lands first**: The configuration.md overhaul from Spec 040A is assumed to be complete before the Phase 2 terminology sweep begins, so the terminology changes apply to already-correct config documentation.
- **No config key renaming**: Changing config keys like `tests.dir` would be a separate, larger breaking change requiring migration logic. This spec explicitly excludes that.
- **Demo repo locations**: Both demo repos are accessible at paths known to the development team and can be updated directly.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `spectra init` creates a `test-cases/` directory in 100% of new project initializations.
- **SC-002**: All existing xUnit tests pass after the folder rename (zero regressions).
- **SC-003**: Zero instances of bare "test" or "tests" referring to SPECTRA markdown output remain in documentation, SKILL descriptions, or agent descriptions (verified by text search).
- **SC-004**: The landing page contains a comparison table, Chat-first workflow example, and quick-install section.
- **SC-005**: The cli-vs-chat-generation.md replacement is under 200 words with no competitive framing.
- **SC-006**: Both demo repos use `test-cases/` as their output directory.
