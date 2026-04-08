# Feature Specification: Docs Index Progress, SKILL Integration & Coverage Dashboard Fix

**Feature Branch**: `024-docs-skill-coverage-fix`  
**Created**: 2026-04-08  
**Status**: Draft  
**Input**: User description: "Docs Index Progress, SKILL Integration & Coverage Dashboard Fix"

## Clarifications

### Session 2026-04-08

- Q: When `--no-interaction` is set, should criteria extraction run with defaults or be skipped entirely? → A: Run with defaults (best-effort, no prompts).
- Q: Which commands trigger auto-migration from `_requirements.yaml` to `_criteria_index.yaml`? → A: Any command that reads criteria (centralized in the criteria reader).
- Q: How should malformed `_requirements.yaml` be handled during migration? → A: No format migration needed. Rename old file to `.bak` and let the system create a fresh `_criteria_index.yaml` via normal extraction. The old format is not used anywhere beyond the demo project.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SKILL-Driven Docs Indexing Without Blocking (Priority: P1)

A user working in VS Code Copilot Chat tells the agent "index my docs." The agent invokes the `spectra docs index` command via the SKILL. The command runs non-interactively to completion without blocking on prompts. The agent reads the structured result file and presents a summary to the user.

**Why this priority**: This is the core blocker. Without non-interactive execution and result reporting, the docs index command is unusable from SKILL/agent workflows, which is the primary integration path.

**Independent Test**: Can be tested by running `spectra docs index --no-interaction --output-format json` and verifying no terminal prompts appear, `.spectra-result.json` is written, and `.spectra-progress.html` shows live status.

**Acceptance Scenarios**:

1. **Given** a project with documentation files, **When** the agent runs `spectra docs index --no-interaction --output-format json`, **Then** the command completes without interactive prompts and writes `.spectra-result.json` with document counts and status.
2. **Given** the docs index command is running, **When** the user opens `.spectra-progress.html`, **Then** they see a live progress page with phase steps (Scanning, Indexing, Extracting Criteria, Completed) that auto-refreshes every 2 seconds.
3. **Given** a previous `.spectra-result.json` exists from an old run, **When** a new docs index run starts, **Then** the old result file is deleted before the new run begins.
4. **Given** the command runs with `--no-interaction`, **When** the auto-triggered criteria extraction runs, **Then** it executes with defaults (no prompts, best-effort) and completes without exit code 3 failure.

---

### User Story 2 - Dashboard Coverage Page Displays Correctly (Priority: P1)

A user generates a dashboard and opens the Coverage tab. All three coverage sections (Documentation, Acceptance Criteria, Automation) display correctly with accurate data, proper labels, and graceful handling of missing data.

**Why this priority**: A broken dashboard undermines user trust and makes coverage analysis unusable. This is a regression fix, not new functionality.

**Independent Test**: Can be tested by generating a dashboard with `spectra dashboard --output ./site` and verifying the Coverage tab loads without errors, shows correct section labels, and handles missing criteria data gracefully.

**Acceptance Scenarios**:

1. **Given** a project with all three coverage data sources, **When** the user opens the dashboard Coverage tab, **Then** all three sections (Documentation Coverage, Acceptance Criteria Coverage, Automation Coverage) render with correct percentages and details.
2. **Given** a project with no `_criteria_index.yaml` file, **When** the user opens the Coverage tab, **Then** the Acceptance Criteria section shows an empty state with setup instructions instead of crashing.
3. **Given** a project using the old `_requirements.yaml` format, **When** the user opens the Coverage tab, **Then** the dashboard still renders (showing migration guidance or zero-state) without JavaScript errors.

---

### User Story 3 - Consistent "Acceptance Criteria" Terminology (Priority: P2)

A user interacts with SPECTRA through CLI, dashboard, or SKILLs. All user-facing text consistently refers to "acceptance criteria" instead of the legacy "requirements" terminology. The old `_requirements.yaml` file is auto-migrated to the new format.

**Why this priority**: Inconsistent terminology confuses users and makes the product feel unfinished. However, it does not block core workflows.

**Independent Test**: Can be tested by running common CLI commands and verifying all output messages use "acceptance criteria" terminology, and by checking dashboard labels.

**Acceptance Scenarios**:

1. **Given** the user runs `spectra docs index`, **When** criteria extraction begins, **Then** the progress message reads "Extracting acceptance criteria from documentation" (not "requirements").
2. **Given** a project with only `_requirements.yaml` and no `_criteria_index.yaml`, **When** any command that reads criteria runs, **Then** the old file is renamed to `_requirements.yaml.bak` with a log message, and a fresh `_criteria_index.yaml` is created via normal extraction.
3. **Given** the dashboard is generated, **When** the user views the Coverage tab labels, **Then** all references say "Acceptance Criteria Coverage" (not "Requirement Coverage").

---

### User Story 4 - Dedicated Docs SKILL for Copilot Chat (Priority: P2)

A user in VS Code Copilot Chat asks to "index my docs" or "rebuild the documentation index." The agent has a dedicated `spectra-docs` SKILL that provides the structured tool-call-sequence format with progress page, `--no-interaction`, and `--output-format json` flags.

**Why this priority**: Improves discoverability and consistency of the docs index workflow in Copilot Chat, but the core functionality (Part A) works even without a dedicated SKILL.

**Independent Test**: Can be tested by running `spectra init` and verifying `.github/skills/spectra-docs/SKILL.md` is created with correct content, and by verifying the SKILL content is embedded in the CLI binary.

**Acceptance Scenarios**:

1. **Given** a new project, **When** the user runs `spectra init`, **Then** a `spectra-docs` SKILL file is created at `.github/skills/spectra-docs/SKILL.md` with the structured tool-call-sequence format.
2. **Given** the `spectra-docs` SKILL exists, **When** the agent reads it, **Then** it contains instructions to open `.spectra-progress.html`, run the command with `--no-interaction --output-format json`, and read `.spectra-result.json`.
3. **Given** the user runs `spectra update-skills`, **When** the docs SKILL was not previously present, **Then** it is added alongside existing SKILLs.

---

### User Story 5 - Skip Criteria Extraction During Indexing (Priority: P3)

A user wants to quickly update only the documentation index without the AI-powered criteria extraction step (which is slower). They pass `--skip-criteria` to skip extraction entirely.

**Why this priority**: Useful optimization for large projects, but auto-trigger with `--no-interaction` already provides a workable default.

**Independent Test**: Can be tested by running `spectra docs index --skip-criteria` and verifying no criteria extraction occurs.

**Acceptance Scenarios**:

1. **Given** a project with documentation, **When** the user runs `spectra docs index --skip-criteria`, **Then** only the document index is updated and no criteria extraction occurs.
2. **Given** the `--skip-criteria` flag is used, **When** the result file is written, **Then** the `criteriaExtracted` field is 0 or absent and `criteriaFile` is absent.

---

### Edge Cases

- What happens when `.spectra-result.json` is locked by another process when the command tries to write it?
- How does the progress page behave if the browser has it open and the command fails mid-execution?
- What if the project has both `_requirements.yaml` and `_criteria_index.yaml`? (Answer: prefer `_criteria_index.yaml`, ignore the old file.)
- What happens if the user runs two `spectra docs index` commands concurrently?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `DocsIndexHandler` MUST write `.spectra-result.json` upon completion (success or failure) containing command name, status, document counts, and optional criteria extraction counts.
- **FR-002**: The `DocsIndexHandler` MUST write `.spectra-progress.html` with live auto-refreshing progress during execution, showing phases: Scanning, Indexing, Extracting Criteria, Completed/Failed.
- **FR-003**: The `DocsIndexHandler` MUST delete any existing `.spectra-result.json` and `.spectra-progress.html` at the start of each run.
- **FR-004**: When `--no-interaction` is set, the `DocsIndexHandler` MUST pass that flag through to auto-triggered criteria extraction, which MUST run with defaults (best-effort, no prompts) rather than being skipped.
- **FR-005**: The `DocsIndexHandler` MUST accept a `--skip-criteria` flag that suppresses the automatic criteria extraction after indexing.
- **FR-006**: All user-facing CLI messages MUST use "acceptance criteria" instead of "requirements" (excluding intentional backward-compatibility aliases).
- **FR-007**: When `_requirements.yaml` exists and `_criteria_index.yaml` does not, the system MUST rename `_requirements.yaml` to `_requirements.yaml.bak` and let normal criteria extraction create a fresh `_criteria_index.yaml`. No format conversion is needed. This is centralized in the criteria reader so any command that reads criteria triggers it automatically.
- **FR-008**: The original `_requirements.yaml` MUST be preserved as `_requirements.yaml.bak` (not deleted).
- **FR-009**: When both `_requirements.yaml` and `_criteria_index.yaml` exist, the system MUST prefer the new `_criteria_index.yaml`.
- **FR-010**: The dashboard Coverage tab MUST render without errors when criteria data is missing, showing an empty state with setup instructions.
- **FR-011**: The dashboard MUST use "Acceptance Criteria Coverage" labels instead of "Requirement Coverage" in all sections.
- **FR-012**: The `DataCollector` MUST never produce null coverage sections in the JSON — always provide zero-state defaults.
- **FR-013**: A new `spectra-docs` SKILL MUST be bundled as the 9th SKILL, created during `spectra init`, and tracked in the `SkillsManifest`.
- **FR-014**: The `spectra-docs` SKILL MUST include the structured tool-call-sequence format with progress page, `--no-interaction`, and `--output-format json` flags.
- **FR-015**: The generation agent prompt MUST be updated to reference the new docs index progress-aware flow.
- **FR-016**: The `.spectra-progress.html` auto-refresh meta tag MUST be removed upon command completion (same pattern as generate command).
- **FR-017**: If `spectra.config.json` contains `requirements_file` but not `criteria_file`, the system MUST treat `requirements_file` as a deprecated alias and read from it as a fallback.

### Key Entities

- **DocsIndexResult**: Result model containing command status, document counts (indexed, skipped, total, new, changed, unchanged), criteria extraction counts, and file paths.
- **DocsIndexProgressPhase**: Enum representing the four progress phases (Scanning, Indexing, ExtractingCriteria, Completed/Failed).
- **CoverageSummaryData**: Dashboard data model with three non-null coverage sections (documentation, acceptance criteria, automation).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The `spectra docs index --no-interaction --output-format json` command completes without any interactive prompts in 100% of SKILL-driven invocations.
- **SC-002**: `.spectra-result.json` is written for every docs index run (both success and failure cases).
- **SC-003**: The dashboard Coverage tab renders without JavaScript errors for all three data scenarios: full data, partial data (missing criteria), and no data.
- **SC-004**: Zero user-facing strings in the CLI contain the word "requirement" (excluding documented backward-compatibility aliases).
- **SC-005**: The `spectra-docs` SKILL is created during `spectra init` and contains the complete structured workflow.
- **SC-006**: When `_requirements.yaml` exists without `_criteria_index.yaml`, it is renamed to `.bak` and the system continues normally.
- **SC-007**: All existing tests continue to pass after the rename and dashboard fixes, with updated assertions where needed.

## Assumptions

- The existing `ProgressHtmlWriter` is generic enough to support a new phase enum without major refactoring. If it is tightly coupled to the generate command's phases, a minor abstraction may be needed.
- The `DocsIndexResult` model from spec 020 already exists and can be extended with the new fields (`DocumentsNew`, `DocumentsChanged`, `DocumentsUnchanged`, `CriteriaExtracted`, `CriteriaFile`).
- The dashboard JavaScript uses a single `{{DASHBOARD_DATA}}` placeholder that receives JSON — field name changes in the C# model propagate to the JS automatically via serialization.
- The `--skip-criteria` flag is a new addition; no existing backward-compatibility concerns.
- No format conversion is needed for migration — the old `_requirements.yaml` is simply renamed to `.bak` and a fresh `_criteria_index.yaml` is created via normal extraction.
