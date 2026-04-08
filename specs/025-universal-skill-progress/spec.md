# Feature Specification: Universal Progress/Result for SKILL-Wrapped Commands

**Feature Branch**: `025-universal-skill-progress`  
**Created**: 2026-04-08  
**Status**: Draft  
**Input**: User description: "Universal Progress/Result for SKILL-Wrapped Commands"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SKILL Reads Structured Results After Command Completion (Priority: P1)

A developer invokes a SPECTRA command through a VS Code Copilot Chat SKILL (e.g., "run coverage analysis"). Today, most commands produce only terminal text output, so the SKILL cannot reliably parse results. With this feature, every SKILL-wrapped command writes a `.spectra-result.json` file containing structured results that the SKILL can read and present in chat.

**Why this priority**: Without structured results, SKILLs are essentially blind — they can run commands but cannot report back meaningfully. This is the core value of the feature.

**Independent Test**: Run any SKILL-wrapped command (`spectra ai analyze --coverage`, `spectra dashboard`, `spectra docs index`, `spectra validate`) and verify `.spectra-result.json` exists with correct typed data after completion.

**Acceptance Scenarios**:

1. **Given** a SKILL invokes `spectra ai analyze --coverage --no-interaction --output-format json`, **When** the command completes successfully, **Then** `.spectra-result.json` contains an `AnalyzeCoverageResult` with coverage percentages, counts, and `"status": "completed"`.
2. **Given** a SKILL invokes `spectra dashboard --output ./site --no-interaction --output-format json`, **When** the command completes, **Then** `.spectra-result.json` contains a `DashboardResult` with output paths and suite/test counts.
3. **Given** a SKILL invokes `spectra validate --no-interaction --output-format json`, **When** validation finishes, **Then** `.spectra-result.json` contains a `ValidateResult` with valid count, error count, and error details.
4. **Given** a command fails mid-execution, **When** the SKILL reads `.spectra-result.json`, **Then** the file contains `"success": false` with an error message and any partial results.
5. **Given** two commands run back-to-back, **When** the second command starts, **Then** the stale `.spectra-result.json` from the first command is deleted before the second command writes its own result, preventing the SKILL from reading outdated data.

---

### User Story 2 - Live Progress Page for Long-Running Commands (Priority: P1)

A developer runs a long-running command via a SKILL (e.g., "index the docs" or "run coverage analysis"). Today, only `spectra ai generate` produces a live progress page. With this feature, all long-running SKILL-wrapped commands write a `.spectra-progress.html` file with auto-refreshing status, phase stepper, and summary cards. The SKILL opens this page in VS Code preview so the user can watch progress in real time.

**Why this priority**: Long-running commands (docs indexing with AI extraction, coverage analysis across many files, test updates with critic verification) leave the user staring at a blank chat for 30+ seconds. A live progress page transforms a "did it hang?" experience into an informative one.

**Independent Test**: Run `spectra docs index --no-interaction --output-format json` and verify `.spectra-progress.html` updates through phases (Scanning, Indexing, Extracting Criteria, Completed) with summary data at each phase.

**Acceptance Scenarios**:

1. **Given** a SKILL starts `spectra docs index`, **When** the command begins, **Then** `.spectra-progress.html` is created with a phase stepper showing "Scanning" as active and an auto-refresh meta tag.
2. **Given** the docs index command is in the "Indexing" phase, **When** the user views the progress page, **Then** they see "Indexing checkout.md (3/15)" with summary cards showing document counts.
3. **Given** any long-running command completes, **When** the progress page is viewed, **Then** the auto-refresh meta tag is removed, the final phase is marked complete, and a summary is shown.
4. **Given** a command fails during execution, **When** the progress page is viewed, **Then** the current phase is marked as failed with an error message, and the auto-refresh tag is removed.
5. **Given** `spectra ai analyze --extract-criteria` is running, **When** the user views progress, **Then** they see per-document extraction progress (e.g., "Extracting criteria from payments.md (2/4)") and running totals.

---

### User Story 3 - Shared Progress Infrastructure via ProgressManager (Priority: P1)

A developer maintaining the SPECTRA codebase needs to add progress/result support to a new command. Today, the progress and result file logic is embedded inline in `GenerateHandler`, making it impossible to reuse. With this feature, a shared `ProgressManager` service encapsulates all progress/result file operations, so adding progress support to any handler requires just a few lines.

**Why this priority**: This is the foundational infrastructure that enables Stories 1 and 2. Without it, each command handler would duplicate progress file logic, leading to inconsistencies and maintenance burden.

**Independent Test**: Refactor `GenerateHandler` to use `ProgressManager` and verify all existing generate progress/result tests still pass unchanged.

**Acceptance Scenarios**:

1. **Given** a `ProgressManager` is created for a command with defined phases, **When** `Reset()` is called, **Then** any existing `.spectra-result.json` and `.spectra-progress.html` files are deleted.
2. **Given** a `ProgressManager` has started, **When** `UpdateAsync(phase, message)` is called, **Then** both the progress HTML and result JSON are updated atomically with the current phase and message.
3. **Given** a `ProgressManager` is in use, **When** `CompleteAsync(result)` is called, **Then** the final typed result is written to `.spectra-result.json` and the progress HTML removes the auto-refresh tag and marks completion.
4. **Given** an error occurs during command execution, **When** `FailAsync(error)` is called, **Then** `.spectra-result.json` contains `"success": false` with the error, and the progress HTML shows the failure state.
5. **Given** `GenerateHandler` is refactored to use `ProgressManager`, **When** all existing generate tests are run, **Then** they pass without modification (pure internal refactor, no behavior change).

---

### User Story 4 - Dashboard Coverage Tab Fix (Priority: P2)

A user opens the SPECTRA dashboard and navigates to the Coverage tab. Today, the tab crashes or shows blank data because the C# model field names were renamed from "requirements" to "acceptance criteria" (spec 023), but the dashboard JavaScript still references the old field names. With this feature, the dashboard correctly renders all three coverage sections with the new terminology.

**Why this priority**: The dashboard is a shipped, user-facing feature that is currently broken. Fixing it is a regression fix with direct user impact, but it does not block SKILL integration (the primary goal).

**Independent Test**: Generate a dashboard with coverage data and verify the Coverage tab renders all three sections (Documentation, Acceptance Criteria, Automation) without errors.

**Acceptance Scenarios**:

1. **Given** coverage data with all three sections populated, **When** the dashboard is loaded, **Then** the Coverage tab shows "Documentation Coverage", "Acceptance Criteria Coverage", and "Automation Coverage" with correct percentages.
2. **Given** no `_criteria_index.yaml` exists, **When** the dashboard is loaded, **Then** the Acceptance Criteria section shows an empty state with guidance ("Run: spectra ai analyze --extract-criteria") instead of crashing.
3. **Given** criteria index exists but has zero entries, **When** the dashboard is loaded, **Then** the section shows "0%" with a message encouraging criteria extraction.
4. **Given** `DataCollector` encounters null coverage data for any section, **When** it produces the dashboard data, **Then** zero-state defaults (0%, 0 covered, 0 total) are used instead of null.

---

### User Story 5 - Terminology Rename Completion (Priority: P2)

A developer reviews CLI output and dashboard labels after upgrading to the latest version. Today, some strings still say "requirements" where they should say "acceptance criteria" (a rename started in spec 023 but not fully completed). With this feature, all user-facing strings consistently use "acceptance criteria" terminology.

**Why this priority**: Inconsistent terminology confuses users and undermines trust in the product. This also unblocks the dashboard fix (Story 4) and ensures progress messages (Story 2) use correct language from the start.

**Independent Test**: Run a comprehensive string search across all `.cs` files and dashboard assets for legacy "requirement" terminology in user-facing contexts and confirm zero matches.

**Acceptance Scenarios**:

1. **Given** the user runs `spectra docs index`, **When** criteria extraction starts, **Then** the progress message reads "Extracting acceptance criteria from documentation" (not "Extracting requirements").
2. **Given** the user opens the dashboard, **When** viewing coverage labels, **Then** all labels read "Acceptance Criteria Coverage" (not "Requirement Coverage" or "Requirements Coverage").
3. **Given** a user has a legacy `_requirements.yaml` file but no `_criteria_index.yaml`, **When** any CLI command runs, **Then** the content is auto-migrated and the old file is renamed to `.bak`.

---

### User Story 6 - New spectra-docs SKILL (Priority: P2)

A developer using VS Code Copilot Chat says "index the docs" or "rebuild the documentation index". Today, there is no SKILL for docs indexing, so the agent either fails or tries to construct the command from scratch (unreliably). With this feature, a dedicated `spectra-docs` SKILL provides the correct command sequence including progress page, result reading, and next-step suggestions.

**Why this priority**: Docs indexing is a frequently used command and the missing SKILL was the original problem that motivated this spec. However, it depends on the progress infrastructure (Story 3) to be fully effective.

**Independent Test**: Trigger the `spectra-docs` SKILL via Copilot Chat and verify it opens the progress page, runs the correct command, reads the result file, and presents a readable summary.

**Acceptance Scenarios**:

1. **Given** a user says "index the docs" in Copilot Chat, **When** the SKILL activates, **Then** it opens `.spectra-progress.html` in preview, runs `spectra docs index --no-interaction --output-format json --verbosity quiet`, waits for completion, reads `.spectra-result.json`, and presents a summary.
2. **Given** the user says "reindex all documentation", **When** the SKILL activates, **Then** it runs with the `--force` flag for a full rebuild.
3. **Given** the docs index completes, **When** the SKILL presents results, **Then** it suggests relevant next steps: generate tests, extract criteria, or run coverage analysis.
4. **Given** `spectra init` is run, **When** SKILL files are created, **Then** `spectra-docs/SKILL.md` is included alongside existing SKILLs.

---

### User Story 7 - Updated SKILLs Use Progress Flow (Priority: P3)

All existing SKILLs (coverage, criteria, dashboard, validate) are updated to follow the universal progress/result pattern: open progress page, run command with standard flags, wait, read result, present. This ensures a consistent user experience across all SKILL-driven workflows.

**Why this priority**: Consistency matters but is lower priority than getting the infrastructure working (P1) and fixing regressions (P2). Each SKILL update is a small change once the infrastructure exists.

**Independent Test**: For each updated SKILL, trigger it via Copilot Chat and verify it follows the 5-step flow (open preview, run command, wait, read result, present).

**Acceptance Scenarios**:

1. **Given** a user triggers the `spectra-coverage` SKILL, **When** the SKILL activates, **Then** it opens `.spectra-progress.html` before running the command and reads `.spectra-result.json` after completion.
2. **Given** a user triggers the `spectra-criteria` SKILL with "extract criteria", **When** the SKILL activates, **Then** it follows the progress page flow for the long-running `--extract-criteria` sub-command.
3. **Given** a user triggers the `spectra-validate` SKILL, **When** the SKILL activates, **Then** it runs without a progress page (fast command) but still reads `.spectra-result.json` for structured results.
4. **Given** a user triggers the `spectra-dashboard` SKILL, **When** the SKILL activates, **Then** it follows the progress page flow and additionally opens the generated dashboard HTML after completion.
5. **Given** all SKILL files are inspected, **When** checking command strings, **Then** every command includes `--no-interaction --output-format json --verbosity quiet` flags.

---

### Edge Cases

- What happens when a command is interrupted mid-execution (e.g., user kills the process)? The `.spectra-progress.html` will remain in its last-updated state with auto-refresh still active. On the next command run, `ProgressManager.Reset()` cleans up stale files.
- What happens when two SPECTRA commands run concurrently (e.g., from two terminal tabs)? Both write to the same `.spectra-result.json` and `.spectra-progress.html`, causing corruption. This is acceptable — concurrent CLI runs are not a supported scenario. The last writer wins.
- What happens when the file system is read-only or the write fails? `ProgressManager` catches I/O exceptions and logs a warning but does not fail the command itself — progress files are informational, not critical.
- What happens when a SKILL reads `.spectra-result.json` before the command finishes writing it? The atomic write pattern (write with `FileStream.Flush(true)`) ensures the file is never partially written.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a shared `ProgressManager` service that handles creation, updating, and finalization of `.spectra-result.json` and `.spectra-progress.html` files.
- **FR-002**: System MUST delete stale `.spectra-result.json` and `.spectra-progress.html` files at the start of every command that uses `ProgressManager`.
- **FR-003**: System MUST write `.spectra-result.json` with typed, command-specific result data for all SKILL-wrapped commands (generate, update, docs index, coverage, extract-criteria, import-criteria, list-criteria, dashboard, validate).
- **FR-004**: System MUST write `.spectra-progress.html` with auto-refreshing phase stepper and summary cards for all long-running commands (generate, update, docs index, coverage, extract-criteria, dashboard).
- **FR-005**: System MUST remove the auto-refresh meta tag from `.spectra-progress.html` when the command completes or fails.
- **FR-006**: System MUST use atomic file writes (flush to disk) for `.spectra-result.json` to prevent partial reads.
- **FR-007**: System MUST refactor `GenerateHandler` to use the shared `ProgressManager` without changing any user-visible behavior.
- **FR-008**: System MUST replace all user-facing occurrences of "requirements" with "acceptance criteria" in CLI output, progress messages, and dashboard labels.
- **FR-009**: System MUST fix dashboard JavaScript to reference the renamed coverage field names (`acceptanceCriteriaCoverage` instead of `requirementsCoverage`).
- **FR-010**: System MUST handle null or missing coverage data in the dashboard with zero-state defaults instead of crashing.
- **FR-011**: System MUST provide a `spectra-docs` SKILL file that wraps the `spectra docs index` command with the standard progress/result flow.
- **FR-012**: System MUST update all existing SKILL files to include the progress page flow (open preview, run command, wait, read result, present).
- **FR-013**: System MUST ensure every SKILL command string includes `--no-interaction --output-format json --verbosity quiet` flags.
- **FR-014**: System MUST display the command name in the progress page header (e.g., "SPECTRA — Documentation Index", "SPECTRA — Coverage Analysis").
- **FR-015**: System MUST not fail the main command if progress/result file I/O encounters errors — progress files are informational, not critical path.

### Key Entities

- **ProgressManager**: Shared service encapsulating progress/result file lifecycle (reset, start, update, complete, fail).
- **ProgressPhases**: Static definitions of phase sequences per command (e.g., Generate: Analyzing/Analyzed/Generating/Completed).
- **CommandResult**: Base result type with command name, status, success flag, timestamp, and optional error/data fields.
- **CoverageDefaults**: Zero-state coverage objects for dashboard null safety.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 9 SKILL-wrapped commands write `.spectra-result.json` with typed, parseable data on completion.
- **SC-002**: All 6 long-running commands write `.spectra-progress.html` with live-updating phase information during execution.
- **SC-003**: The dashboard Coverage tab renders correctly with all three coverage sections, including empty-state handling, with zero console errors.
- **SC-004**: Zero user-facing strings in the CLI or dashboard contain legacy "requirement(s)" terminology (excluding backward-compatible aliases and internal code comments).
- **SC-005**: The `GenerateHandler` refactor introduces no regressions — all existing generate-related tests pass without modification.
- **SC-006**: Each SKILL file includes `--no-interaction --output-format json --verbosity quiet` flags on every command invocation.
- **SC-007**: Progress page auto-refresh is active during execution and removed within 1 update cycle of completion or failure.

## Assumptions

- Concurrent CLI runs writing to the same progress/result files are not a supported scenario. Last writer wins.
- The existing `ProgressHtmlWriter` (from spec 023) is parameterizable enough for multiple commands, or can be generalized with minimal changes.
- SKILL files are static markdown — they do not need runtime parameterization beyond what's already supported.
- The `spectra docs index` command continues to auto-trigger criteria extraction by default; the `--skip-criteria` flag is opt-in.
- Fast commands (validate, list, import-criteria) do not need `.spectra-progress.html` since they complete in under a few seconds.
- All typed result models (e.g., `DashboardResult`, `ValidateResult`) already exist from spec 020/023 and only need the file-write integration.
