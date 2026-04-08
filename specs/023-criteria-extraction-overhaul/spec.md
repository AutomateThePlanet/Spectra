# Feature Specification: Acceptance Criteria Import & Extraction Overhaul

**Feature Branch**: `023-criteria-extraction-overhaul`  
**Created**: 2026-04-07  
**Status**: Draft  
**Input**: User description: "Overhaul acceptance criteria management: rename 'requirements' to 'acceptance criteria', switch from single-prompt extraction to per-document iterative extraction, and add import support for external sources (YAML/CSV/JSON)."  
**Depends on**: 015 (Requirements Extraction), 011 (Coverage Overhaul)  
**Affects**: `Spectra.CLI`, `Spectra.Core`, Dashboard, SKILLs, Agents, Docs

## Clarifications

### Session 2026-04-07

- Q: When AI extraction fails for one document during a multi-document batch, should the system stop or continue? → A: Continue processing remaining documents, write successful results, and report all failures at the end.
- Q: How should the flat `_requirements.yaml` be migrated to the new per-document structure? → A: No migration needed. The old `_requirements.yaml` file and `requirements_file` config key are not used beyond demo projects. Drop auto-migration; only keep `--extract-requirements` as a hidden CLI alias.
- Q: How should criterion IDs be generated for imports without an `id` column? → A: Derive from the source ticket key with a numeric suffix (e.g., `AC-PROJ-1234-1`, `AC-PROJ-1234-2`). When no source key exists either, fall back to configured prefix + sequential number.
- Q: What scope does `--replace` clear during import? → A: Only the specific target import file (e.g., re-importing `jira-sprint-42.csv` overwrites only `jira-sprint-42.criteria.yaml`). Other imported and extracted criteria are untouched.

## User Scenarios & Testing

### User Story 1 - Extract Acceptance Criteria from Documentation (Priority: P1)

A QA lead has 15 markdown documentation files in their `docs/` folder. They run a single CLI command to extract testable acceptance criteria from each document. The system processes documents one at a time, producing a separate criteria file per document and a master index. When they update a single doc and re-run the command, only that document is re-processed.

**Why this priority**: This is the core workflow that replaces the current broken single-prompt extraction. Without iterative extraction, large doc sets produce truncated results, making the entire feature unreliable.

**Independent Test**: Run `spectra ai analyze --extract-criteria` against a `docs/` folder with 3+ documents and verify per-document `.criteria.yaml` files are created alongside a `_criteria_index.yaml`. Change one document and re-run to confirm only that document is reprocessed.

**Acceptance Scenarios**:

1. **Given** a project with 5 markdown docs in `docs/`, **When** the user runs `spectra ai analyze --extract-criteria`, **Then** 5 per-document `.criteria.yaml` files are created in `docs/requirements/`, a `_criteria_index.yaml` master index is generated listing all 5 sources with SHA-256 hashes, and no truncation occurs regardless of document size.
2. **Given** a previous extraction has been completed, **When** the user modifies one document and re-runs `spectra ai analyze --extract-criteria`, **Then** only the changed document is re-extracted, unchanged documents are skipped, and the master index is updated.
3. **Given** a previous extraction has been completed, **When** a source document is deleted from `docs/`, **Then** the system warns that criteria from that document are orphaned but does not auto-delete the criteria file.
4. **Given** any project, **When** the user runs `spectra ai analyze --extract-criteria --force`, **Then** all documents are re-extracted regardless of hash status.
5. **Given** any project, **When** the user runs `spectra ai analyze --extract-criteria --dry-run`, **Then** no files are written and the output shows what would be created/updated.

---

### User Story 2 - Import Acceptance Criteria from External Sources (Priority: P1)

A test manager exports acceptance criteria from Jira as a CSV file. They import it into SPECTRA using a single CLI command. The system auto-detects column mappings (e.g., Jira's "Key" maps to "source", "Acceptance Criteria" maps to "text"), uses AI to split compound criteria into individual entries, normalizes language to RFC 2119 keywords, and stores the result in the `docs/requirements/imported/` directory.

**Why this priority**: Most teams maintain acceptance criteria in external tools (Jira, Azure DevOps). Without import, SPECTRA's coverage analysis is incomplete and teams must duplicate effort.

**Independent Test**: Create a CSV file with Jira-style columns, run `spectra ai analyze --import-criteria ./jira-export.csv`, and verify that a `.criteria.yaml` file is created in `imported/` with correctly mapped fields, split entries, and RFC 2119 normalization.

**Acceptance Scenarios**:

1. **Given** a CSV file with Jira column names (`Key`, `Summary`, `Acceptance Criteria`), **When** the user runs `spectra ai analyze --import-criteria ./jira-export.csv`, **Then** columns are auto-mapped to SPECTRA fields, compound acceptance criteria are split into individual entries by AI, each entry is normalized to RFC 2119 language, and the result is saved in `docs/requirements/imported/`.
2. **Given** a YAML file in SPECTRA's native criteria format, **When** the user runs `spectra ai analyze --import-criteria ./criteria.yaml`, **Then** criteria are imported directly without AI processing needed for splitting.
3. **Given** a JSON file with criteria, **When** the user runs `spectra ai analyze --import-criteria ./criteria.json`, **Then** criteria are imported and stored correctly.
4. **Given** previously imported criteria exist, **When** the user imports a new file with `--merge` (default), **Then** matching criteria (by ID or source) are updated and new criteria are appended without deleting existing ones.
5. **Given** previously imported criteria exist, **When** the user imports with `--replace`, **Then** all existing criteria in the target file are cleared and replaced with the new import.
6. **Given** any import, **When** the user specifies `--skip-splitting`, **Then** no AI processing occurs and criteria text is stored as-is without splitting or normalization.

---

### User Story 3 - Terminology Rename: Requirements to Acceptance Criteria (Priority: P1)

All user-facing references are renamed from "requirements" to "acceptance criteria" across the codebase. The old `--extract-requirements` CLI flag is kept as a hidden alias for discoverability. No auto-migration of `_requirements.yaml` files or `requirements_file` config keys is needed (only used in demo projects).

**Why this priority**: The rename is foundational - all other changes build on the new terminology. Consistent terminology reduces confusion across CLI, dashboard, reports, and agent prompts.

**Independent Test**: Run `spectra ai analyze --extract-requirements` and verify it works identically to `--extract-criteria`. Check that all CLI output, dashboard labels, and reports use "acceptance criteria" terminology.

**Acceptance Scenarios**:

1. **Given** the new CLI version, **When** the user runs `spectra ai analyze --extract-requirements`, **Then** the command works identically to `--extract-criteria` (hidden alias).
2. **Given** all user-facing surfaces (CLI output, dashboard, reports, SKILLs), **When** displayed, **Then** the term "Requirement Coverage" is replaced with "Acceptance Criteria Coverage" and all related terminology is updated consistently.

---

### User Story 4 - List and Filter Acceptance Criteria (Priority: P2)

A test engineer wants to review all acceptance criteria for the "checkout" component to plan their testing. They run a list command filtered by component and see criteria from both documentation extraction and Jira imports, along with coverage status for each.

**Why this priority**: Listing and filtering criteria enables users to understand their coverage landscape and plan test generation. It supports but is not required for core extraction/import workflows.

**Independent Test**: After extracting and importing criteria, run `spectra ai analyze --list-criteria --component checkout` and verify the output shows criteria from multiple sources with correct coverage status.

**Acceptance Scenarios**:

1. **Given** criteria exist from multiple sources (documents, Jira import, manual), **When** the user runs `spectra ai analyze --list-criteria`, **Then** all criteria are listed with their source type, component, priority, and coverage status.
2. **Given** criteria from multiple sources, **When** the user filters with `--source-type jira`, **Then** only criteria imported from Jira are shown.
3. **Given** criteria across components, **When** the user filters with `--component checkout`, **Then** only criteria associated with the checkout component are shown.
4. **Given** criteria with varying priorities, **When** the user filters with `--priority high`, **Then** only high-priority criteria are shown.
5. **Given** no criteria exist, **When** the user runs `--list-criteria`, **Then** an empty result with guidance on how to extract or import criteria is shown.

---

### User Story 5 - Generation & Update Integration with Criteria (Priority: P2)

A developer runs `spectra ai generate checkout` to generate tests. The system automatically loads acceptance criteria related to the checkout suite and includes them in the AI prompt as additional context, producing tests that reference specific criteria IDs in their frontmatter. When criteria later change, `spectra ai update checkout` flags affected tests as outdated.

**Why this priority**: Criteria-aware generation produces higher-quality, traceable tests. However, generation works without criteria (existing behavior), making this an enhancement rather than a prerequisite.

**Independent Test**: Create criteria for the "checkout" component, run `spectra ai generate checkout`, and verify generated test frontmatter includes `criteria` field references. Then modify a criterion's text, run `spectra ai update checkout`, and verify affected tests are classified as OUTDATED.

**Acceptance Scenarios**:

1. **Given** acceptance criteria exist for the "checkout" component, **When** the user runs `spectra ai generate checkout`, **Then** related criteria are automatically included in the generation prompt and generated tests include a `criteria` field in their YAML frontmatter linking to the relevant criterion IDs.
2. **Given** no criteria exist for a suite, **When** the user runs `spectra ai generate <suite>`, **Then** generation proceeds as before with no regression.
3. **Given** a criterion's text changes after re-extraction, **When** the user runs `spectra ai update checkout`, **Then** tests linked to that criterion are classified as OUTDATED.
4. **Given** new criteria are added (via extraction or import), **When** the user runs `spectra ai update checkout`, **Then** the system suggests generating additional tests for uncovered criteria.
5. **Given** a criterion is removed (source document deleted), **When** the user runs `spectra ai update checkout`, **Then** tests linked to that criterion are flagged as ORPHANED.

---

### User Story 6 - Coverage Dashboard with Criteria Breakdown (Priority: P3)

A project manager views the SPECTRA dashboard and sees "Acceptance Criteria Coverage" broken down by source type: extracted from docs (80%), imported from Jira (72%), and manual (60%). They drill down into each section to see individual criteria and their linked tests.

**Why this priority**: Dashboard visualization is valuable for stakeholder reporting but depends on all prior stories being complete. Coverage analysis already works; this adds visual polish and source-type drill-down.

**Independent Test**: Generate a dashboard after extracting and importing criteria. Verify the "Acceptance Criteria Coverage" section shows per-source-type breakdown with correct percentages and drill-down capability.

**Acceptance Scenarios**:

1. **Given** criteria from multiple source types, **When** the dashboard is generated, **Then** the "Acceptance Criteria Coverage" section displays overall coverage percentage and a per-source-type breakdown (e.g., "From documents: 80%, From Jira: 72%, Manual: 60%").
2. **Given** criteria with linked tests, **When** the user drills down in the dashboard, **Then** individual criteria are listed with their linked test IDs and coverage status.
3. **Given** no criteria exist, **When** the dashboard is generated, **Then** the criteria section shows an empty state with setup guidance.

---

### User Story 7 - New SKILL and Agent Updates (Priority: P3)

A developer uses VS Code Copilot Chat with the SPECTRA extension. They ask "extract acceptance criteria from my docs" and the Copilot agent translates this into the correct CLI command, runs it, and presents the results in a readable format. The generation agent also automatically checks for related criteria before generating tests.

**Why this priority**: SKILL/agent integration improves discoverability and developer experience but is not required for core functionality. All operations are accessible via CLI.

**Independent Test**: Run `spectra init` and verify the new `spectra-criteria/SKILL.md` is created. Verify the generation agent prompt references criteria awareness.

**Acceptance Scenarios**:

1. **Given** a new SPECTRA project, **When** the user runs `spectra init`, **Then** a `spectra-criteria/SKILL.md` is created in `.github/skills/` alongside existing SKILLs.
2. **Given** an existing project, **When** the user runs `spectra update-skills`, **Then** the new criteria SKILL is added and existing SKILLs are updated.
3. **Given** the generation agent prompt, **When** viewed, **Then** it includes instructions to check for related acceptance criteria before generating tests.

---

### Edge Cases

- What happens when a document contains zero extractable acceptance criteria? The per-document criteria file is created empty (with `criteria: []`) and the index entry shows `criteria_count: 0`.
- How does the system handle a CSV file with no recognizable column headers? The system fails with a clear error message listing the expected column names and auto-detection patterns.
- What happens when two imported files contain criteria with the same ID? In merge mode, the later import overwrites the earlier one for matching IDs. A warning is logged about the duplicate.
- How does the system handle a criteria file that references a non-existent source document? The criteria are preserved but flagged as orphaned in the master index.
- What happens when `--import-criteria` points to a non-existent file? The CLI exits with code 1 and a clear error message.
- How does the system handle concurrent runs of extraction? File-level locking on the master index prevents corruption. The second run waits or fails with a lock message.
- What happens when AI extraction fails for one document in a multi-document batch? The system continues processing remaining documents, writes successful results to their criteria files and updates the index, then reports all failed documents at the end with error details. The exit code reflects partial success (exit code 2) when some documents fail.

## Requirements

### Functional Requirements

- **FR-001**: System MUST extract acceptance criteria from documentation files one document at a time, producing a separate `.criteria.yaml` file per source document. If extraction fails for a document, the system MUST continue processing remaining documents, write successful results, and report all failures at the end.
- **FR-002**: System MUST maintain a master index (`_criteria_index.yaml`) that catalogs all criteria files, their source documents, SHA-256 hashes, and criteria counts.
- **FR-003**: System MUST support incremental extraction using SHA-256 document hashing, skipping unchanged documents and only re-extracting modified ones.
- **FR-004**: System MUST import acceptance criteria from YAML, CSV, and JSON file formats.
- **FR-005**: System MUST auto-detect common column names from Jira and Azure DevOps CSV exports and map them to SPECTRA fields.
- **FR-006**: System MUST use AI to split compound acceptance criteria (e.g., bullet lists in a single Jira field) into individual entries during import.
- **FR-007**: System MUST normalize acceptance criteria text to RFC 2119 keywords (MUST, SHOULD, MAY, etc.) during both extraction and import.
- **FR-008**: System MUST rename all user-facing references from "requirements" to "acceptance criteria" across CLI, dashboard, reports, SKILLs, and agent prompts.
- **FR-009**: System MUST keep `--extract-requirements` as a hidden CLI alias for `--extract-criteria`. No auto-migration of `_requirements.yaml` or `requirements_file` config is required.
- **FR-010**: System MUST support `--merge` (default) and `--replace` modes for imports. Merge matches by ID or source within the target file. Replace overwrites only the specific target import file; other imported and extracted criteria are unaffected.
- **FR-011**: System MUST list and filter criteria by source type, component, and priority via `--list-criteria` with optional filter flags.
- **FR-012**: System MUST automatically load related acceptance criteria as context when generating tests for a suite, matching by component name and source document.
- **FR-013**: System MUST include a `criteria` field in generated test YAML frontmatter linking tests to the acceptance criteria they verify.
- **FR-014**: System MUST detect criteria changes during `spectra ai update` and classify affected tests as OUTDATED (changed text), suggest new tests (new criteria), or flag as ORPHANED (removed criteria).
- **FR-015**: System MUST display acceptance criteria coverage in the dashboard with per-source-type breakdown (documents, Jira, manual, etc.).
- **FR-016**: System MUST provide a new bundled SKILL (`spectra-criteria`) for Copilot Chat integration covering extract, import, and list workflows.
- **FR-017**: System MUST support `--dry-run` for both extraction and import, showing what would be created/updated without writing files.
- **FR-018**: System MUST support `--skip-splitting` during import to bypass AI processing and store criteria text as-is.
- **FR-019**: System MUST produce structured JSON output for all criteria operations when `--output-format json` is specified.
- **FR-020**: System MUST create sample `_criteria_index.yaml` and `sample.criteria.yaml` files during `spectra init`.

### Key Entities

- **AcceptanceCriterion**: A single testable statement extracted or imported into SPECTRA. Key attributes: unique ID (e.g., `AC-CHECKOUT-001` for extracted, `AC-PROJ-1234-1` for imported with source key, `AC-001` fallback for imports without source key), text, RFC 2119 keyword, source reference, source type, component, priority, tags, linked test IDs.
- **CriteriaIndex**: The master index file tracking all criteria sources, their document hashes, criteria counts, and last extraction/import timestamps.
- **CriteriaFile**: A per-source YAML file containing a list of acceptance criteria extracted from a single document or imported from a single external file.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Extraction of acceptance criteria from a 20-document project completes without truncation or data loss, producing a complete criteria set regardless of total document volume.
- **SC-002**: Incremental extraction of a 20-document project where 1 document changed completes at least 5x faster than full extraction, by skipping unchanged documents.
- **SC-003**: Users can import acceptance criteria from a Jira CSV export in a single command without manual column mapping or file reformatting.
- **SC-004**: All user-facing surfaces (CLI, dashboard, reports, SKILLs, agent prompts) consistently use "acceptance criteria" terminology with zero references to the old "requirements" term.
- **SC-005**: The hidden `--extract-requirements` alias works identically to `--extract-criteria` for users who remember the old flag name.
- **SC-006**: AI splitting of compound criteria achieves at least 90% accuracy in separating distinct testable statements from multi-line Jira fields.
- **SC-007**: Coverage reports show per-source-type breakdown, enabling stakeholders to identify which external sources have the lowest test coverage.
- **SC-008**: Generated tests include traceable links to acceptance criteria, enabling end-to-end traceability from criterion to test to execution result.

## Assumptions

- Users export criteria from external tools (Jira, Azure DevOps, Confluence) manually; no direct API integration is provided.
- CSV files use standard comma delimiters and UTF-8 encoding. Other delimiters (semicolon, tab) are not supported in this version.
- RFC 2119 normalization is best-effort via AI and may require manual review for ambiguous statements.
- The `criteria` field in test frontmatter uses criterion IDs (not full text) to keep frontmatter concise.
- Orphaned criteria (from deleted source documents) are preserved and warned about, not auto-deleted, to prevent accidental data loss.
- The master index file is the single source of truth for criteria inventory; individual criteria files can be regenerated from source documents.
- Import file size is bounded by available memory; extremely large exports (100K+ rows) may need to be split manually.

## Out of Scope

- Direct API integration with Jira, Confluence, or Azure DevOps.
- Real-time synchronization with external tools.
- Criteria editing UI in the dashboard (read-only display only).
- MCP tools for criteria import/list (CLI-only; potential future spec).
- Bi-directional sync (SPECTRA writing back to Jira/ADO).
