# Feature Specification: Automatic Requirements Extraction

**Feature Branch**: `015-auto-requirements-extraction`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Add automatic requirements extraction from documentation during test generation and as a standalone analysis command, so requirements coverage works out of the box without manual YAML authoring."

## Clarifications

### Session 2026-03-21

- Q: In interactive mode, should users review extracted requirements before saving, or should extraction happen silently? → A: Show extracted requirements for review in interactive mode; save only approved ones.
- Q: Should standalone extraction also scan existing tests and retroactively add requirement links? → A: No — extraction only builds the requirements file; linking happens naturally during future test generation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Standalone Requirements Extraction (Priority: P1)

A QA lead has a set of product documentation but no requirements file. They want to quickly build a requirements inventory from their existing docs without generating any tests. They run a single command that scans all documentation and produces a machine-readable requirements file listing every testable behavior found.

**Why this priority**: This is the foundational capability — extracting requirements from docs. Without this, no other story works. It also serves users who want requirements analysis independent of test generation.

**Independent Test**: Can be fully tested by running the extraction command against sample documentation and verifying the output file contains correctly structured requirements with IDs, titles, sources, and priorities.

**Acceptance Scenarios**:

1. **Given** a project with documentation files and no existing requirements file, **When** the user runs the extraction command, **Then** a requirements file is created at the configured path containing one requirement entry per testable behavior found in the docs.
2. **Given** a project with documentation files and an existing requirements file, **When** the user runs the extraction command, **Then** newly discovered requirements are added while existing requirements are preserved unchanged.
3. **Given** documentation that contains a behavior already captured in the existing requirements file, **When** extraction runs, **Then** the duplicate is detected by title similarity and not added again.
4. **Given** documentation across multiple files, **When** extraction runs, **Then** each requirement's `source` field references the document it was extracted from.

---

### User Story 2 - Integrated Extraction During Test Generation (Priority: P2)

A test author runs the test generation command against a document. As part of generation, the system automatically identifies requirements from the source document. In interactive mode, the user reviews the extracted requirements and can approve, edit, or reject each one before they are saved. Only approved requirements are written to the requirements file. The generated tests automatically reference the relevant requirement IDs in their frontmatter.

**Why this priority**: This is the primary workflow that makes requirements coverage "just work" — requirements grow organically as teams generate tests, eliminating the manual YAML authoring barrier.

**Independent Test**: Can be tested by running test generation against a sample document and verifying both: (a) requirements file is updated with extracted requirements, and (b) generated test cases contain `requirements` fields linking to the correct requirement IDs.

**Acceptance Scenarios**:

1. **Given** a document with testable behaviors and no existing requirements file, **When** the user runs test generation for that document, **Then** requirements are extracted and saved before tests are generated, and generated tests reference the extracted requirement IDs.
2. **Given** a document with testable behaviors and an existing requirements file, **When** the user runs test generation, **Then** new requirements are merged into the file (no overwrites) and generated tests link to both new and pre-existing matching requirement IDs.
3. **Given** test generation in dry-run mode, **When** the user runs the command, **Then** extracted requirements are shown in the preview but not written to disk.
4. **Given** test generation in non-interactive (CI) mode, **When** the command runs, **Then** requirements extraction happens automatically without prompts.
5. **Given** test generation in interactive mode, **When** requirements are extracted, **Then** the user is presented with the list of extracted requirements and can approve, edit, or reject each one before they are saved.

---

### User Story 3 - Requirement ID Allocation and Uniqueness (Priority: P2)

A team has been using Spectra across multiple documents over several weeks. Each extraction run has added requirements. Every requirement has a unique, sequentially allocated ID (REQ-001, REQ-002, ...) that remains stable across subsequent runs. No ID collisions occur regardless of how many times extraction is run or how many documents are processed.

**Why this priority**: Stable, unique IDs are essential for traceability — test cases reference requirement IDs, and coverage analysis depends on them. ID collisions or renumbering would break existing links.

**Independent Test**: Can be tested by running extraction multiple times against different documents and verifying that IDs are unique, sequential, and that existing IDs are never reassigned.

**Acceptance Scenarios**:

1. **Given** no existing requirements file, **When** extraction runs and finds 5 requirements, **Then** they are assigned IDs REQ-001 through REQ-005.
2. **Given** an existing requirements file with IDs up to REQ-012, **When** extraction finds 3 new requirements, **Then** they are assigned REQ-013, REQ-014, REQ-015.
3. **Given** an existing requirements file where some IDs have been manually deleted (e.g., REQ-005 removed), **When** extraction finds new requirements, **Then** new IDs continue from the highest existing ID (not reuse gaps).

---

### User Story 4 - Priority Assignment from Documentation Language (Priority: P3)

Requirements extracted from documentation are automatically assigned a priority (high, medium, or low) based on the language and emphasis used in the source document. Words like "must", "critical", "required", and "shall" indicate high priority. Words like "should" and "recommended" indicate medium. Words like "may", "optional", and "nice to have" indicate low.

**Why this priority**: Priority assignment adds value to coverage analysis by letting teams focus on high-priority uncovered requirements first, but a reasonable default (medium) works if this is deferred.

**Independent Test**: Can be tested by providing documents with known priority language and verifying the extracted requirements have correct priority values.

**Acceptance Scenarios**:

1. **Given** a document stating "The system MUST lock the account after 5 failed attempts", **When** extraction runs, **Then** the resulting requirement has priority "high".
2. **Given** a document stating "The system should display a warning message", **When** extraction runs, **Then** the resulting requirement has priority "medium".
3. **Given** a document stating "Users may optionally configure notification preferences", **When** extraction runs, **Then** the resulting requirement has priority "low".
4. **Given** a document with no clear priority language, **When** extraction runs, **Then** the resulting requirement defaults to priority "medium".

---

### Edge Cases

- What happens when a document contains no testable behaviors? The system produces no requirements for that document and reports this to the user.
- What happens when the requirements file is malformed or contains invalid YAML? The system reports a clear error and does not overwrite the file.
- What happens when two documents describe the same behavior with different wording? Title similarity matching detects the overlap and keeps the first occurrence, logging a note about the potential duplicate.
- What happens when the configured requirements file path does not exist (parent directory missing)? The system creates the necessary directories before writing the file.
- What happens when extraction runs concurrently (e.g., two CI jobs)? The system reads the latest file state before writing; last-writer-wins is acceptable for this scenario.
- What happens when a document is very large (hundreds of pages)? The system processes it within normal generation timeframes, extracting requirements in a single pass.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST extract testable behavioral requirements from documentation files during analysis.
- **FR-002**: System MUST write extracted requirements to the configured requirements file path (`coverage.requirements_file` setting, default: `docs/requirements/_requirements.yaml`).
- **FR-003**: System MUST merge new requirements into an existing file without overwriting or modifying existing entries.
- **FR-004**: System MUST detect duplicate requirements by comparing titles for similarity and skip adding them.
- **FR-005**: System MUST assign each new requirement a globally unique sequential ID (REQ-NNN format), continuing from the highest existing ID.
- **FR-006**: System MUST populate each requirement with: `id`, `title`, `source` (document path), and `priority` (high/medium/low).
- **FR-007**: System MUST assign priority based on requirement language keywords (must/shall/critical = high, should/recommended = medium, may/optional = low, default = medium).
- **FR-008**: System MUST extract requirements during the test generation workflow, after loading source documents and before generating test cases. In interactive mode, extracted requirements MUST be presented for user review (approve, edit, or reject) before saving. In non-interactive mode, all extracted requirements are saved automatically.
- **FR-009**: Generated test cases MUST include a `requirements` field in frontmatter referencing the IDs of requirements they cover.
- **FR-010**: System MUST provide a standalone command to extract requirements from all documentation without generating tests.
- **FR-011**: System MUST output requirements in YAML format compatible with the existing requirements parser.
- **FR-012**: System MUST create parent directories for the requirements file if they do not exist.
- **FR-013**: System MUST report extraction results to the user (count of new requirements found, count of duplicates skipped, total in file).
- **FR-014**: System MUST respect dry-run mode — showing extracted requirements without writing them to disk.

### Key Entities

- **Requirement**: A testable behavioral statement extracted from documentation, identified by a unique ID (REQ-NNN), with a title, source document reference, and priority level.
- **Requirements File**: A YAML document containing all known requirements, serving as the single source of truth for coverage analysis. Grows incrementally over time.
- **Source Document**: Any documentation file in the project that describes system behavior and can be scanned for requirements.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running the standalone extraction command against project documentation produces a valid requirements file with zero manual editing required.
- **SC-002**: After running test generation, 100% of generated test cases include `requirements` field references to extracted requirement IDs.
- **SC-003**: Coverage analysis reports accurate requirement coverage percentages immediately after test generation — no manual file creation step needed.
- **SC-004**: Duplicate detection prevents the same behavioral requirement from appearing more than once, maintaining less than 5% duplicate rate across repeated extraction runs.
- **SC-005**: Requirement IDs remain stable — re-running extraction against the same documents produces no ID changes to previously extracted requirements.
- **SC-006**: The extraction process adds less than 20% overhead to the total test generation time.
- **SC-007**: Requirements file is valid YAML that passes schema validation on every write.

## Assumptions

- Title similarity matching uses a straightforward comparison (e.g., normalized string similarity above a threshold). Exact algorithm is an implementation detail, but the threshold should be tunable if needed.
- The AI model used for extraction is the same generation model already configured in the project. No additional model configuration is required.
- Requirements extraction processes one document at a time during generation (the document being used for test generation) and all configured documents during standalone extraction.
- The existing requirement data model (`id`, `title`, `source`, `priority`) is sufficient — no schema changes are needed for the requirements file format.
- Non-interactive/CI mode performs extraction automatically without any user prompts.
- The `--extract-requirements` flag is additive to the existing `spectra ai analyze` command family.

## Scope Boundaries

### In Scope

- AI-powered extraction of testable requirements from documentation
- YAML file creation and incremental merge
- Duplicate detection by title similarity
- Sequential ID allocation
- Priority inference from document language
- Integration into test generation workflow
- Standalone extraction command
- Linking generated tests to extracted requirement IDs

### Out of Scope

- Manual editing UI for requirements (users edit YAML directly if needed)
- Requirements approval or review workflow
- Extraction from non-text sources (images, diagrams, videos)
- Retroactive linking of existing tests to newly extracted requirements (handled by future generation runs)
- Cross-project requirement deduplication
- Requirement versioning or change tracking
- Requirement hierarchy (parent/child relationships)
- Natural language translation of requirements
