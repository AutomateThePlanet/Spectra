# Feature Specification: Test Lifecycle & Process Control

**Feature Branch**: `046-test-lifecycle-control`
**Created**: 2026-04-30
**Status**: Draft
**Input**: User description: "Spec 040 — Test Lifecycle & Process Control: safe test deletion, suite rename/delete, cancel running operation, and test ID collision fix"

## Overview

SPECTRA users currently have no first-class way to delete a test, rename or remove a suite, or stop a long-running AI operation that has gone off the rails. They also occasionally produce duplicate test IDs across suites — a silent correctness bug that breaks coverage analysis, dashboard test resolution, and execution-history lookups. This feature closes those four day-to-day gaps with a single coherent "lifecycle" surface that uses Git as the undo mechanism, prefers cooperative cancellation over force, and guarantees globally unique test IDs going forward.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prevent duplicate test IDs going forward (Priority: P1)

A QA engineer or test author runs `spectra ai generate` repeatedly across suites — sometimes concurrently, sometimes after editing test files by hand. Today, two suites can end up with the same `TC-NNN` ID, which silently corrupts coverage reports, dashboards, and execution history. They need the tool to guarantee that every newly generated test gets a globally unique ID, regardless of the state of the index files or whether two generation runs are happening at the same time. They also need a one-shot way to inspect the current ID landscape and to repair existing duplicates on demand.

**Why this priority**: This is a correctness bug that quietly invalidates downstream artifacts (coverage, dashboard, run history). Fixing it unblocks trustworthy reporting and is a prerequisite for every other lifecycle operation: deleting, renaming, or cancelling a generation run is unsafe while ID allocation is racy.

**Independent Test**: Run two generation operations in parallel against an empty repo, then run the diagnostic. Verify that no two generated tests share an ID, that the diagnostic reports zero duplicates, and that the next allocation continues monotonically from the highest ID ever issued — even if a test was deleted or its index entry was hand-removed.

**Acceptance Scenarios**:

1. **Given** two AI generation runs started within the same second on a fresh repo with `id_start = 100`, **When** both complete, **Then** every generated test has a unique ID and the IDs form two non-overlapping contiguous ranges starting at 100.
2. **Given** a repo with a test file on disk whose ID is missing from `_index.json`, **When** the next generation runs, **Then** the new IDs skip past the on-disk maximum (no collision with the orphaned file).
3. **Given** a test was previously deleted (file gone, index updated), **When** the next generation runs, **Then** the deleted ID is never reused — the next allocation continues from the historical high-water-mark.
4. **Given** a repo with two existing tests sharing the same ID (legacy data), **When** the user runs the diagnostic, **Then** the diagnostic reports the duplicate with both file paths, modification times, and a recommended next ID.
5. **Given** the same duplicate situation, **When** the user runs the diagnostic with a fix flag, **Then** the older file keeps its ID, the newer file is reassigned to a fresh ID, and incoming `depends_on` references to the renumbered test are updated automatically.
6. **Given** a generation run that respects the configured `id_start` floor, **When** the floor is higher than any existing ID, **Then** the first allocation begins at the floor, not at 1.

---

### User Story 2 - Stop a runaway operation cleanly (Priority: P2)

A user kicks off a long AI generation, criteria extraction, or coverage analysis from Copilot Chat or the terminal. Partway through, they realize the wrong suite was selected, the prompt was wrong, or it's simply taking too long. Today, their only option is to kill the integrated terminal — which orphans progress and result files in inconsistent states and gives them no idea how much work survived. They need a single command that stops the operation gracefully, preserves any partial work, updates the visible progress page to a clear "cancelled" state, and tells them exactly how many artifacts were written before stopping.

**Why this priority**: Users hit this every time an operation goes wrong, and the lack of a clean stop today actively erodes trust ("did it actually stop? did I lose everything?"). It also unblocks the SKILL surface: agents can offer to stop a run on the user's behalf without leaving the workspace in a half-broken state.

**Independent Test**: Start a generation of 15 tests. After 7 are written to disk, request cancellation. Verify the operation stops within a small grace window, the 7 already-written tests remain on disk, the result file reports cancelled status with the count of files that survived, and the progress page transitions to a terminal "Cancelled" state with auto-refresh disabled.

**Acceptance Scenarios**:

1. **Given** a generation operation has written 7 of 15 tests, **When** the user requests cancellation, **Then** the operation stops within ~5 seconds, the 7 tests remain on disk, and the result artifact reports `status: cancelled` with `tests_written: 7`.
2. **Given** the same in-flight operation, **When** the user requests forceful cancellation, **Then** the operation stops immediately without waiting for the cooperative grace window.
3. **Given** no SPECTRA operation is running, **When** the user requests cancellation, **Then** the command exits successfully with a clear "nothing was running" status — never an error.
4. **Given** a previous SPECTRA process crashed and left its tracking metadata behind, **When** the user requests cancellation, **Then** the stale metadata is detected and cleaned up automatically without false-killing an unrelated process.
5. **Given** the cancelled operation produced a progress page, **When** the user reloads it, **Then** the page shows the terminal "Cancelled" phase, lists what completed, and stops auto-refreshing.
6. **Given** a Copilot Chat user says "stop the generation", **When** the SKILL handles the request, **Then** it issues the cancel command and reports back the phase that was interrupted and the count of artifacts that survived.

---

### User Story 3 - Delete a single test or a small group of tests safely (Priority: P3)

A test author has a flaky, obsolete, or duplicated test case and wants it gone. Today they have to delete the file by hand and remember to update `_index.json` and any `depends_on` references in other tests — an error-prone manual job. They need a single command that previews exactly what will change, refuses to silently strand automation code that points at the test, cleans up incoming references, and trusts Git to be the undo mechanism.

**Why this priority**: Common operation, currently DIY. Less critical than ID correctness or cancellation because users have a manual workaround today, but it's high-value once the safer foundations are in place.

**Independent Test**: Delete a test that another test depends on and that has automation linked. Verify the operation refuses to proceed without an explicit override, and once overridden, the test file is gone, the index is updated, the dependent test's `depends_on` array no longer contains the deleted ID, and the automation file is reported as stranded but otherwise untouched.

**Acceptance Scenarios**:

1. **Given** a test with no automation links and no incoming dependencies, **When** the user runs delete with a preview flag, **Then** no files change and the preview shows the file path, title, suite, and an empty cleanup list.
2. **Given** the same test, **When** the user runs delete without preview, **Then** the test file is removed, its entry is removed from the suite index, and the suite's `test_count` is updated.
3. **Given** a test whose ID appears in another test's `depends_on` array, **When** the user deletes it, **Then** the deleted ID is automatically stripped from every dependent's `depends_on` list as part of the same atomic operation.
4. **Given** a test that has automation files linked to it, **When** the user attempts to delete it without an override, **Then** the operation refuses, lists the stranded files, and changes nothing on disk.
5. **Given** the same test, **When** the user supplies the override, **Then** the test is deleted, the result reports the stranded automation files, and the automation files themselves are not touched.
6. **Given** a list of multiple test IDs, **When** the user deletes them in one command, **Then** every requested test is processed atomically — either all succeed or the result clearly identifies which ones failed and why.
7. **Given** a non-existent test ID, **When** the user attempts to delete it, **Then** the operation exits with a clear "test not found" status and no other test is affected.

---

### User Story 4 - Rename or delete an entire suite (Priority: P3)

A team rebrands a feature area, retires a legacy module, or consolidates two suites. Today they have to rename a directory by hand and chase down references in `_index.json`, saved selections, and the config file. They need a single command for each operation that updates every reference atomically, validates the new name, refuses dangerous operations by default, and rolls back cleanly if anything goes wrong partway through.

**Why this priority**: Less frequent than single-test deletion but higher blast radius. Same priority bucket because the underlying machinery (atomic writes, dependency cleanup, automation guard) is shared with single-test delete and ships together.

**Independent Test**: Rename a suite that is referenced by a saved selection and a suite-specific config block. Verify the directory is renamed, the suite's index reflects the new name, the saved selection now points at the new name, the config block is rekeyed, and test IDs inside the suite are unchanged. For deletion: delete a suite whose tests have automation links and incoming cross-suite dependencies — verify the operation refuses without an override, and that an override produces a clean cascade of dependency cleanup.

**Acceptance Scenarios**:

1. **Given** a suite that exists and a target name that does not exist, **When** the user runs rename with a preview flag, **Then** the preview shows the directory move, the count of saved selections that will be updated, and the config block changes — but no files change on disk.
2. **Given** the same suite, **When** the user runs rename without preview, **Then** the directory is renamed, the suite index reflects the new name, every saved selection that referenced the old name now references the new name, and any per-suite config block is re-keyed. Test IDs inside the suite are unchanged.
3. **Given** a target name that already exists or violates the naming rules (lowercase alphanumeric, hyphens, underscores; must start with letter or digit), **When** the user runs rename, **Then** the operation refuses with a clear error and changes nothing.
4. **Given** a partial failure during rename (e.g., the directory rename succeeds but config rewrite fails), **When** the rollback runs, **Then** the directory is renamed back and the in-memory config snapshot is restored — no partial state is left on disk.
5. **Given** a suite whose tests have automation links or are depended on by tests in other suites, **When** the user attempts to delete it, **Then** the operation refuses without an override, and the report names the count of stranded automations and the count of incoming external dependencies.
6. **Given** the same suite with an override supplied, **When** deletion proceeds, **Then** every cross-suite `depends_on` reference to a test in the deleted suite is removed, the suite directory is recursively deleted, and the suite is removed from saved selections and the config block.
7. **Given** the user wants to see all suites at a glance, **When** they run the list command, **Then** the output includes every suite with its test count.

---

### Edge Cases

- A delete or suite operation is requested while a generation operation is mid-flight on the same `_index.json`. The lifecycle command must serialize against the in-progress write so that no half-written index is observed.
- A user deletes the last test in a suite. The suite directory and index file are kept (empty suite is a valid state); only `suite delete` removes the directory itself.
- A user requests cancellation of an operation that is already in its final commit phase (writing the result file). The operation must finish writing a consistent result file rather than tearing it apart mid-write.
- A user runs the diagnostic on a repo that has zero tests. The output must report zero duplicates, zero index mismatches, and a sensible "next ID" suggestion based on the configured floor.
- A user deletes a test that is referenced by automation code via a hardcoded ID attribute. The automation file remains untouched and the result report calls it out as stranded.
- The high-water-mark file is corrupted or written by an incompatible future version. The allocator must rebuild from the filesystem and refuse to silently downgrade — incompatible-version state is treated as recoverable, not fatal.
- A user runs cancel from a different working directory than the running operation. The command resolves the tracking metadata relative to the workspace, not the caller's CWD, so cross-directory cancellation works as long as both point at the same workspace.
- Two cancel commands are issued in quick succession. The second one observes that the first one already completed and reports "nothing was running" rather than killing an unrelated subsequent SPECTRA process.

## Requirements *(mandatory)*

### Functional Requirements

#### Test ID allocation (User Story 1)

- **FR-001**: System MUST guarantee that every newly created test ID is globally unique across all suites in the workspace, regardless of the state of any individual suite's index file.
- **FR-002**: System MUST serialize concurrent ID allocation requests so that two parallel generation operations cannot allocate overlapping ID ranges.
- **FR-003**: System MUST persist a monotonically non-decreasing high-water-mark for issued IDs, such that an ID once allocated is never reused even if its test is later deleted.
- **FR-004**: System MUST compute the next allocation as the maximum of: the persisted high-water-mark, the maximum ID found in any index file, the maximum ID found in any test file's frontmatter, and the configured `id_start` floor minus one.
- **FR-005**: System MUST honor the `tests.id_start` configuration value as a floor for the very first allocation in a fresh workspace.
- **FR-006**: System MUST provide a diagnostic command that reports total test count, count of unique IDs, every duplicate ID with its file locations and modification times, any mismatches between index entries and on-disk frontmatter, the current high-water-mark, and the next ID that would be allocated.
- **FR-007**: System MUST provide an opt-in repair operation that resolves duplicate IDs by keeping the older file and reassigning later occurrences to fresh IDs, with `depends_on` references and any in-source ID attributes updated on a best-effort basis. The repair MUST report any references it could not safely update.
- **FR-008**: System MUST NOT auto-renumber existing duplicate IDs as a side effect of upgrading or running any other command. Renumbering requires an explicit user action.
- **FR-009**: On first run after upgrade, system MUST seed the high-water-mark from the union of index and filesystem scan and emit a one-time informational log line stating the seeded value.
- **FR-010**: The high-water-mark file and the allocation lock file MUST be ignored by source control via the default ignore template generated at workspace initialization.

#### Cancellation (User Story 2)

- **FR-011**: System MUST provide a `cancel` command that requests termination of any in-progress long-running operation in the workspace.
- **FR-012**: System MUST cooperate with cancellation requests at every batch boundary in long-running operations (per-test in generation, per-doc in extraction and indexing, per-suite in coverage, per-step in dashboard build) such that work already committed to disk is preserved.
- **FR-013**: System MUST attempt cooperative shutdown first, allowing the running operation a short grace window (≤ 5 seconds) to finish its current batch and write a partial result before any forceful termination is attempted.
- **FR-014**: When the grace window elapses without cooperative shutdown, system MUST escalate to forceful termination of the running operation. A user-supplied force flag MUST skip the grace window entirely.
- **FR-015**: System MUST detect and clean up stale tracking metadata left by previously crashed operations, so that a cancel command never falsely terminates an unrelated process.
- **FR-016**: When no operation is in progress, the cancel command MUST exit successfully with a clear "no active run" status — this is not an error condition.
- **FR-017**: A cancelled operation's result artifact MUST report `status: cancelled`, the phase that was interrupted, the progress within that phase, the count of artifacts written, and the list of those artifacts.
- **FR-018**: A cancelled operation's progress page (when one was being written) MUST transition to a terminal "Cancelled" state, list what completed, and stop auto-refreshing.
- **FR-019**: System MUST clean up its own tracking metadata (PID file, sentinel file) at the end of every operation, whether it completed normally, was cancelled cooperatively, or was forcefully terminated.
- **FR-020**: Cancel-related sentinel and PID files MUST be ignored by source control via the default ignore template.
- **FR-021**: Existing long-running SKILLs MUST gain a recipe that handles user phrases such as "stop", "cancel", "kill it", "stop the analysis", "stop generating" by issuing the cancel command and reporting the outcome to the user.

#### Test deletion (User Story 3)

- **FR-022**: System MUST provide a delete command that accepts one or more test IDs and removes their files from disk.
- **FR-023**: Delete MUST resolve test files by consulting the suite indexes first and falling back to a filesystem scan if the index appears stale.
- **FR-024**: Delete MUST refuse by default to remove any test whose `automated_by` frontmatter is non-empty, exiting with a distinct status code and listing the automation files that would be stranded. A user-supplied override MUST be required to proceed.
- **FR-025**: Delete MUST remove the deleted test's ID from every other test's `depends_on` array atomically, in the same operation that removes the file and the index entry.
- **FR-026**: Delete MUST update the affected suite index's test count and generated-at timestamp.
- **FR-027**: Delete MUST support a preview mode that reports every change that would happen — file removal, index update, dependency cleanup, stranded automation — without modifying anything on disk.
- **FR-028**: Delete MUST present an interactive confirmation by default that names the test, file path, automation links, and dependents, and explicitly states that Git is the undo. Non-interactive mode and a force flag MUST both bypass the prompt.
- **FR-029**: Delete MUST NOT modify any automation source file, ever. Stranded automation is reported but never edited.
- **FR-030**: When a test ID does not exist, delete MUST exit with a distinct "not found" status and report which IDs were not found, without affecting any other tests.
- **FR-031**: Delete MUST emit a structured result artifact in non-interactive mode that lists the deleted tests with their metadata, the dependency-cleanup actions taken, any skipped IDs, and any errors.
- **FR-032**: A new SKILL MUST be bundled that handles user requests to delete tests by always running a preview first, presenting the impact to the user, and only proceeding after explicit chat confirmation.

#### Suite operations (User Story 4)

- **FR-033**: System MUST provide a `suite list` command that reports every suite in the workspace with its test count.
- **FR-034**: System MUST provide a `suite rename` command that takes an old name and a new name and atomically renames the suite directory, updates the suite name inside the index file, updates every saved selection that references the old name, and re-keys any per-suite config block.
- **FR-035**: Suite rename MUST refuse if the source does not exist, the target already exists, or the target name violates the naming rules (lowercase letters, digits, hyphens, underscores; must start with a letter or digit).
- **FR-036**: Suite rename MUST roll back to the original state on partial failure: directory rename reverted, in-memory config snapshot restored, index unchanged.
- **FR-037**: Suite rename MUST NOT change any test ID — IDs are global, not suite-prefixed.
- **FR-038**: System MUST provide a `suite delete` command that removes a suite directory and all its tests.
- **FR-039**: Suite delete MUST refuse by default if any test in the suite has automation links, or if any test in another suite has a `depends_on` reference into this suite. A user-supplied override MUST be required to proceed.
- **FR-040**: Suite delete MUST cascade dependency cleanup across all remaining suites, removing every `depends_on` reference to any test in the deleted suite, before the directory itself is removed.
- **FR-041**: Suite delete MUST remove the suite name from every saved selection's suite list and from any per-suite config block.
- **FR-042**: Both rename and delete MUST support a preview mode that reports every change without modifying anything on disk.
- **FR-043**: Both rename and delete MUST support an interactive confirmation by default and a force/non-interactive bypass.
- **FR-044**: A new SKILL MUST be bundled that handles user requests to rename or delete suites by running a preview first, summarizing the impact (test count, automation links, external dependencies, selections, config blocks), and only proceeding after explicit chat confirmation.

#### Cross-cutting

- **FR-045**: All new commands MUST follow the existing global flag conventions (`--no-interaction`, `--output-format json|human`, `--verbosity quiet`) and emit a structured result artifact in non-interactive mode that downstream SKILLs can read.
- **FR-046**: Result artifacts MUST use stable, documented status values: `completed`, `cancelled`, `no_active_run`, plus the existing error statuses.
- **FR-047**: All destructive commands (delete, suite delete, suite rename, doctor fix) MUST treat Git as the undo mechanism and MUST NOT maintain any internal trash, snapshot, or restore directory.
- **FR-048**: The CLI reference, project guidelines, and changelog MUST be updated to document every new command, its flags, exit codes, and the migration steps for existing workspaces with duplicate IDs.

### Key Entities *(include if feature involves data)*

- **Test case file**: A single Markdown file under `test-cases/<suite>/`. Holds frontmatter including a globally unique ID, optional `automated_by` paths, and an optional `depends_on` array of other test IDs.
- **Suite index**: Per-suite JSON file (`test-cases/<suite>/_index.json`) listing the tests in that suite with metadata such as title and ID. Authoritative *for that suite*; not a global source of truth.
- **High-water-mark record**: A small workspace-local JSON file that records the largest ID ever allocated, when it was allocated, and which command issued it. Monotonic; never decreases. Local-only, regenerable, gitignored.
- **Allocator lock**: A workspace-local file used as a cross-process mutex to serialize ID allocations. Local-only, gitignored.
- **PID/sentinel pair**: A workspace-local PID file announcing that a long-running command is in flight, plus a sentinel file written by the cancel command to request cooperative shutdown. Both are local-only and gitignored.
- **Result artifact**: A workspace-local JSON file written by every non-interactive command, consumed by SKILLs. Always reports a status, command name, and command-specific payload (e.g., the list of deleted tests, the count of survivors at cancellation, the duplicate-ID inventory).
- **Progress artifact**: A workspace-local HTML page written by long-running commands to give the user a live view of phases and progress. Gains a new terminal "Cancelled" phase as part of this feature.
- **Saved selection**: A named subset of suites stored in the workspace config, used by users to scope generation, analysis, and other operations. Updated automatically when suites are renamed or deleted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After this feature ships, no concurrent or sequential generation run produces duplicate test IDs across suites — verified by running two generation operations in parallel against a fresh workspace and confirming zero duplicates in the resulting tests.
- **SC-002**: A user can stop any long-running operation (generation, analysis, criteria extraction, coverage analysis, dashboard build) in under 6 seconds end-to-end (issue command → operation halted → result artifact written) in the cooperative path.
- **SC-003**: When an operation is cancelled mid-batch, 100% of artifacts already written to disk before cancellation remain on disk and are reported in the result artifact's survivors list.
- **SC-004**: A user can delete a test and its dependency references in a single command. After deletion, no other test in the workspace contains the deleted ID in its `depends_on`.
- **SC-005**: Suite rename completes with zero stale references: the directory, every saved selection, and any per-suite config block all reflect the new name. A simple grep for the old name across the workspace returns zero matches in tracked files.
- **SC-006**: A user can audit ID-collision risk on demand and receives a complete report (duplicates, index mismatches, current high-water-mark, suggested next ID) in under 5 seconds for a workspace of up to 10,000 test files.
- **SC-007**: Existing duplicate-ID workspaces can be brought to a clean state with a single command. After the repair, the diagnostic reports zero duplicates and zero index mismatches.
- **SC-008**: SKILL-driven users (Copilot Chat) can ask in natural language to "stop", "delete TC-NNN", "rename suite X to Y", or "delete suite Z" and receive a preview-then-confirm flow without ever leaving the chat surface and without any manual file edits.
- **SC-009**: All destructive operations support a preview mode that produces zero filesystem changes; this is verified by running every destructive command with the preview flag against a Git-clean workspace and observing that `git status` remains clean afterward.
- **SC-010**: All new commands emit machine-readable result artifacts in non-interactive mode using stable, documented status values, enabling SKILLs and CI pipelines to react reliably without parsing human output.

## Assumptions

- Git is the undo mechanism for every destructive operation. SPECTRA does not maintain an internal trash, snapshot, or restore feature.
- Cooperative cancellation is the preferred path; forceful termination is a fallback for processes that fail to respond to the sentinel within the grace window.
- Test IDs are global and not suite-prefixed; renaming a suite never changes a test's ID. This matches the existing project convention documented in the test format.
- Single-test deletion and suite deletion deliberately leave automation source files untouched. Removing stranded automation is a separate concern (a future `--auto-link --prune` capability) and is out of scope here.
- Users with existing duplicate IDs accept that fixing them via the repair operation will renumber some tests, with downstream impact on external trackers (Jira/ADO links, CI configs that hardcode IDs). This is documented in the migration guide; the repair updates in-source ID attributes on a best-effort basis.
- Cancellation of MCP execution runs is out of scope — that subsystem already has its own cancellation flow (`cancel_execution_run`).
- The new SKILLs follow the existing project SKILL conventions (`--no-interaction --output-format json --verbosity quiet`, structured result artifacts, preview-first behavior with explicit user confirmation in chat).

## Dependencies

- Spec 023 (criteria) — coverage and criteria pipelines are among the long-running operations that gain cancellation support.
- Spec 025 (universal SKILL progress) — the progress-page contract is extended with a new terminal "Cancelled" phase.
- Spec 027 (SKILL/agent deduplication) — new SKILLs (`spectra-delete`, `spectra-suite`) follow the deduplication conventions and are routed by the agent delegation table.

## Out of Scope

- Soft delete / trash / restore — Git is the undo.
- `spectra suite create <name>` — `mkdir test-cases/<name>` is sufficient today; revisit if scaffolding needs grow.
- `spectra suite merge <a> <b>` — non-trivial design problem (test ID renumbering, conflict resolution); separate spec.
- Pruning stranded automation source attributes after a test or suite is deleted — separate spec; touches automation code editing.
- Pause/resume of long-running operations — current cancellation is terminal-only.
- Cancellation of MCP execution runs — different subsystem with its own cancellation API.
