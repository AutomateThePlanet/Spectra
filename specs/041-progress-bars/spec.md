# Feature Specification: Generation & Verification Progress Bars

**Feature Branch**: `041-progress-bars`
**Created**: 2026-04-11
**Status**: Draft
**Input**: User description: "Spec 042: Generation & Verification Progress Bar — Add real-time progress bars to terminal (Spectre.Console) and progress page showing per-batch generation and per-test critic verification progress."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Terminal progress visibility during long generation runs (Priority: P1)

A test author runs `spectra ai generate <suite> --count 80` and watches the terminal so they know the run is making forward progress and roughly how much is left, instead of staring at a blank screen for 10+ minutes between the analysis summary and the final report.

**Why this priority**: This is the core pain point — long-running generations currently appear hung. Without this, users abort runs, re-run unnecessarily, or lose trust in the tool. Solving terminal feedback alone delivers most of the value.

**Independent Test**: Run `spectra ai generate <suite> --count 40` in a normal terminal and confirm a generation progress bar appears immediately, advances after each batch completes, transitions to a verification progress bar after generation finishes, and clears cleanly when the run completes.

**Acceptance Scenarios**:

1. **Given** a generation run for 40 tests with batch size 10, **When** the user invokes `spectra ai generate` at default verbosity, **Then** a "Generating tests" progress bar appears showing `0/40` and advances to `10/40`, `20/40`, `30/40`, `40/40` as each batch completes, with a "batch N/M" detail.
2. **Given** generation has completed for 40 tests, **When** the critic phase begins, **Then** a second "Verifying tests" progress bar appears and increments by 1 after each individual test verification, showing the most recently verified test ID and verdict.
3. **Given** a run finishes successfully, **When** the final result is rendered, **Then** the progress bars no longer occupy the screen and the standard run summary panel is shown.

---

### User Story 2 - Browser progress page reflects per-test progress (Priority: P2)

A user who launched a generation run from the SPECTRA SKILL (or any background invocation) opens `.spectra-progress.html` in a browser to watch progress remotely, and sees a live-updating progress bar with current/target counts in addition to the existing phase stepper.

**Why this priority**: The progress page is the secondary surface used by SKILL/CI/remote workflows where the terminal bar is suppressed. It reuses data already being written to `.spectra-result.json`, so the cost is small but the value for headless usage is high.

**Independent Test**: Trigger a generation run with `--output-format json` (which suppresses terminal progress) and open `.spectra-progress.html`. Confirm a generation progress bar advances with each refresh cycle, transitions to a verification bar, and finally disappears when the run completes.

**Acceptance Scenarios**:

1. **Given** a run is mid-generation, **When** the progress page auto-refreshes, **Then** it shows a generation progress section with a filled bar matching `tests_generated / tests_target` and a "Batch N of M" detail line.
2. **Given** generation has completed and verification is in progress, **When** the page refreshes, **Then** the generation bar shows 100% and a second verification bar advances independently, with the dimmed/active styling switched accordingly.
3. **Given** the run is complete, **When** the final page renders, **Then** the in-flight progress section is replaced by the existing run summary, with no leftover progress bar artifacts.

---

### User Story 3 - Update command shows chunk progress (Priority: P3)

A user running `spectra ai update <suite>` on a suite with many existing tests sees a progress bar advancing through update chunks, so they know how far the classification/update pass has progressed.

**Why this priority**: `update` runs are typically shorter than `generate --count 80+`, so the pain is smaller, but the same UX pattern naturally extends here. Lowest priority because it can ship after generate works.

**Independent Test**: Run `spectra ai update <suite>` against a suite with multiple update chunks and confirm an "Updating tests" progress bar appears, advances per chunk, and clears on completion.

**Acceptance Scenarios**:

1. **Given** an update run with 30 tests across multiple chunks, **When** processing begins, **Then** an "Updating tests" progress bar appears and advances by chunk size as each chunk finishes.

---

### Edge Cases

- **Output mode is JSON or quiet**: Progress bars must be fully suppressed so structured stdout / SKILL/CI parsers see no ANSI escape sequences interleaved with the JSON result.
- **Verbosity is minimal**: Progress bars are shown but per-test detail strings (test ID, verdict) are omitted, leaving only counts and percentages.
- **Run fails mid-batch**: Progress bar must stop cleanly at its current value without leaving orphaned cursor state, and the in-flight `progress` object in `.spectra-result.json` must be replaced (not appended to) when the failure result is written.
- **User cancels with Ctrl+C**: Cursor is restored, partial progress bars do not corrupt the next prompt. (Formal cancel-with-cleanup is out of scope; we only need clean teardown.)
- **Total count is 1 or very small**: Bar still appears and reaches 100% in a single increment without flickering.
- **Critic phase is skipped (`--skip-critic`)**: Only the generation bar appears; no empty verification bar is shown.
- **Result file is read mid-write by the progress page**: The page must not crash or display half-parsed values; partial reads either succeed with the previous snapshot or are ignored until the next refresh.
- **Non-interactive terminal (redirected stdout / no TTY)**: Progress bars must not be drawn even at default verbosity, since they would corrupt redirected output.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a terminal progress bar during the generation phase of `spectra ai generate` runs at default verbosity, advancing after each batch completes and showing current count, target count, and current batch index.
- **FR-002**: System MUST display a second terminal progress bar during the critic verification phase, advancing once per verified test and showing the most recently processed test identifier and its verdict.
- **FR-003**: System MUST suppress all terminal progress bars when output mode is JSON, when verbosity is `quiet`, or when stdout is not attached to an interactive terminal, so that machine-readable output and CI logs are not corrupted by ANSI escape sequences.
- **FR-004**: System MUST display terminal progress bars without per-test detail strings (test ID, verdict) when verbosity is `minimal`, leaving only counts and percentages.
- **FR-005**: System MUST display a terminal progress bar during `spectra ai update` runs at default verbosity, advancing per processed chunk, subject to the same suppression rules as generation.
- **FR-006**: System MUST write a `progress` object into `.spectra-result.json` after each generation batch completes, containing at minimum the current phase, target test count, generated count, verified count, current batch index, total batch count, and most recent test identifier.
- **FR-007**: System MUST update the `progress` object in `.spectra-result.json` after each individual critic verification completes, including the most recent test identifier and verdict.
- **FR-008**: System MUST remove the `progress` object from `.spectra-result.json` when the run completes (success or failure), so the final result file contains the run summary instead of in-flight progress data.
- **FR-009**: System MUST render a generation progress section in `.spectra-progress.html` showing a filled bar proportional to `tests_generated / tests_target` and a "Batch N of M" detail, derived from the `progress` object on each auto-refresh cycle.
- **FR-010**: System MUST render a verification progress section in `.spectra-progress.html` shown in a dimmed/inactive state until generation completes, then activated and advanced per verified test.
- **FR-011**: System MUST style the new progress sections consistently with the existing progress page theme (navy/teal palette established in earlier work).
- **FR-012**: System MUST determine the target test count for progress bars from either the user-supplied `--count` value or the analysis-recommended count that the user has approved, and MUST compute total batches as the ceiling of target count divided by the configured batch size.
- **FR-013**: System MUST NOT introduce any additional disk writes for the progress object beyond the existing per-batch result file writes during generation; verification updates MAY introduce per-test writes since critic calls are inherently slow.
- **FR-014**: System MUST restore the terminal cursor and clear any in-flight progress bars cleanly when a run is interrupted, fails, or completes, so subsequent shell prompts are not corrupted.

### Key Entities *(include if feature involves data)*

- **Progress Snapshot**: Represents the current in-flight state of a generation or update run. Attributes: phase (generating | verifying | updating), target count, generated count, verified count, current batch index, total batch count, most recent test identifier, most recent verdict. Lives inside `.spectra-result.json` only while the run is active and is removed on completion.
- **Run Target**: The agreed-upon total number of tests the run intends to produce, sourced from either the explicit `--count` flag or the analysis recommendation. Drives the maximum value of both progress bars.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: During a generation run of 40 or more tests, users see visible progress feedback within 2 seconds of the generation phase starting, and observe at least one progress advancement no later than the completion of the first batch.
- **SC-002**: 100% of generation runs at default verbosity in an interactive terminal display both generation and verification progress bars when both phases run; runs with `--skip-critic` display only the generation bar.
- **SC-003**: 0% of runs invoked with `--output-format json` or `--verbosity quiet` emit any progress bar characters or ANSI escape sequences on stdout (verified by parsing stdout as strict JSON or comparing to a plain-text baseline).
- **SC-004**: The browser progress page reflects new per-test progress within one auto-refresh cycle (≤ 2 seconds plus one critic call latency) of the underlying state changing.
- **SC-005**: After a run completes (success or failure), the final `.spectra-result.json` contains a run summary and contains no `progress` object, in 100% of completed runs.
- **SC-006**: After a generation run completes or is interrupted, subsequent terminal prompts render normally with no leftover progress bar artifacts or cursor corruption.
- **SC-007**: User-reported "is it stuck?" support questions about long-running generation are eliminated for runs that show the progress bar (qualitative outcome verifiable via support feedback after release).

## Assumptions

- The terminal UX library already in use (Spectre.Console) is the chosen rendering surface for terminal progress bars; the spec does not constrain which library is used, only the observable behavior.
- The existing 2-second auto-refresh interval of `.spectra-progress.html` is acceptable for progress feedback granularity; no new push channel is required.
- The generation pipeline already knows the total target test count before generation starts (from `--count` or approved analysis recommendation). Runs where the count is not knowable in advance are out of scope.
- The existing per-batch result file write path is reliable enough for progress data (it is, per the prior NTFS-flush work).
- Per-test result file writes during the verification phase are acceptable cost given critic calls take 4–6 seconds each, making the write overhead negligible.

## Out of Scope

- Estimated time remaining / ETA display.
- Per-test progress within a single generation batch (a batch is atomic from SPECTRA's perspective).
- Formal cancel-with-cleanup on Ctrl+C (only clean teardown of progress bar rendering is required here).
- Progress bars for commands other than `spectra ai generate` and `spectra ai update`.
- Push-based (websocket / SSE) updates to the progress page; polling is sufficient.
