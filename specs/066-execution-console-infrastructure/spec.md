# Feature Specification: Execution Console Infrastructure

**Feature Branch**: `066-execution-console-infrastructure`  
**Created**: 2026-06-08  
**Status**: Draft  
**Input**: User description: "A local, detached HTTP server (`spectra run console`) serving an ephemeral web page where a QA engineer drives a manual run — current test, PASS/FAIL/BLOCKED, comment, screenshot — with SQLite (via the execution engine) as the single source of truth."

## Overview

A QA engineer running a manual test suite today drives the loop from the terminal (`spectra run …`) or
through an AI agent that relays one test at a time. This feature adds a third way to drive the *same*
run: a small **local web console**. The engineer starts a run, opens a page in their browser (e.g. the
VS Code Simple Browser), sees the current test in Spectra's familiar report styling, and records each
verdict by clicking **PASS / FAIL / BLOCKED**, typing a comment, and optionally attaching a screenshot.
The page advances to the next test.

The defining constraint: **the execution database is the single source of truth.** The browser is a
*view + write-back caller* — it never stores run state. Every click is a server call that records the
verdict in the same database the terminal and agent already use. Closing the tab, refreshing, or even
ending the session that launched the console loses nothing, because nothing of value lives in the
browser.

This is a *new transport over an engine that already exists* — the execution engine, its database, its
guardrails, and its screenshot handling are all reused unchanged. No model, no tokens, no network beyond
localhost.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record verdicts through the console (Priority: P1)

A QA engineer has started an execution run. They launch the console, open the local URL in their
browser, and see the current test rendered in Spectra's report style. They click **PASS** and the page
records the verdict and advances to the next test. For a failing test they click **FAIL**, type what
went wrong, and submit — the verdict and comment are saved and the run advances.

**Why this priority**: This is the core of the feature — driving a run from the browser, with results
landing in the same database the rest of the system reads. Without it nothing else matters. It is a
complete, demonstrable MVP on its own: a human can run an entire suite from the page.

**Independent Test**: Start a run, open the console, and record a PASS and a FAIL-with-comment for two
tests. Verify (via the existing terminal `spectra run status`/`show` or the database directly) that the
recorded statuses, notes, and ordering are identical to what the terminal path would have produced.

**Acceptance Scenarios**:

1. **Given** a started run, **When** the engineer opens the console URL, **Then** the current test
   renders with its title, steps, and expected results in Spectra's report styling.
2. **Given** the current test is shown, **When** the engineer clicks PASS, **Then** the verdict is
   recorded in the database through the engine and the page advances to the next test.
3. **Given** the current test is shown, **When** the engineer clicks FAIL or BLOCKED and supplies a
   comment, **Then** the verdict and comment are recorded and the page advances.
4. **Given** the engineer clicks FAIL or BLOCKED **without** a comment, **Then** the console rejects the
   action with a clear "notes required" message and records nothing — exactly the same rule the terminal
   path enforces.
5. **Given** a verdict that blocks dependent tests, **When** it is recorded, **Then** the dependent
   tests are marked blocked in the database, identical to the terminal path.

---

### User Story 2 - Lose nothing on refresh or restart (Priority: P1)

Mid-run, the engineer accidentally closes the browser tab, or the machine's browser reloads the page.
They reopen the URL and the run is exactly where they left it — same current test, same recorded
results, same counts. Nothing was kept in the browser; everything was already in the database.

**Why this priority**: The "no browser store" invariant is the whole point of building on the engine
rather than re-implementing a local-storage app. If a refresh could lose progress, the console would be
an unreliable parallel store instead of a faithful view. This is co-equal P1 with Story 1.

**Independent Test**: Record several verdicts, hard-refresh the page (and separately close/reopen the
browser), and confirm the page rebuilds the same state from the database with no loss and no duplicate
or phantom results.

**Acceptance Scenarios**:

1. **Given** several verdicts have been recorded, **When** the page is refreshed, **Then** it shows the
   same current test, results, and counts, reconstructed from the database.
2. **Given** the console page is open, **When** the engineer inspects browser storage, **Then** no run
   state is held there (no localStorage/sessionStorage state, no export/import-as-state).
3. **Given** a run is in progress, **When** the engineer opens the console on the same machine after
   previously closing it, **Then** the run resumes from its persisted state with no re-entry of prior
   results.

---

### User Story 3 - Attach a screenshot to a result (Priority: P2)

While reviewing a test, the engineer captures the screen and drops the image onto the console page (or
pastes it). The screenshot is uploaded, saved alongside the run's other attachments, and linked to that
test's result — visible in the eventual report just like screenshots attached through the terminal or
agent.

**Why this priority**: Screenshots are essential evidence for manual QA, but a run is fully recordable
without them, so this sits just below the core verdict loop. It reuses the existing screenshot handling,
so the only new surface is the browser-to-server upload.

**Independent Test**: With a test in progress, upload an image through the console and verify it is saved
to the run's attachments and appears in that result's screenshot list — with no copy held in the
browser.

**Acceptance Scenarios**:

1. **Given** a test is in progress, **When** the engineer uploads a screenshot via the console, **Then**
   it is saved using the same encode-and-store path as the terminal/agent and linked to that test's
   result.
2. **Given** a screenshot has been uploaded, **When** the result is later viewed (report or status),
   **Then** the screenshot is present and correctly associated with the test.
3. **Given** a screenshot upload, **When** it completes, **Then** the only stored copy is the one in the
   run's attachments — the browser retains no authoritative copy.

---

### User Story 4 - Console outlives the launching session (Priority: P2)

The engineer (or an AI agent on their behalf) launches the console. Later the launching context goes
away — the terminal closes, or an agent session is compacted or ends. The console keeps running and the
engineer can keep working in the browser. The console stops only when the engineer explicitly stops it.

**Why this priority**: Without detachment, an agent-launched console would die when the agent's session
turns over, which is the exact scenario the follow-on agent revision depends on. It is below the core
loop because a human launching from a terminal they keep open gets value even without detachment, but it
is required for the intended long-running workflow.

**Independent Test**: Launch the console, end the launching process, and confirm the page is still served
and functional; then issue the explicit stop command and confirm the server ends.

**Acceptance Scenarios**:

1. **Given** the console has been launched, **When** the launching process exits, **Then** the console
   keeps serving the page and recording verdicts.
2. **Given** a running console, **When** the engineer issues the explicit stop command, **Then** the
   server shuts down and the URL stops responding.
3. **Given** a running console, **When** the engineer queries for it, **Then** they can discover that it
   is running and on which address/port (via a recorded marker), so they can reach or stop it.

---

### Edge Cases

- **No active run when the console is opened**: the page communicates that there is no run to drive
  rather than rendering a blank or broken test panel.
- **Run reaches its end**: after the last test is recorded, the page reflects a completed/finalizable
  state instead of presenting a non-existent "next test".
- **Two consoles for the same run**: the feature targets one human at a time; if a second console is
  launched against the same run, the behavior must not corrupt state (database remains the arbiter), and
  the launch should surface that a console is already running.
- **Stop when nothing is running**: issuing the stop command with no live console reports cleanly rather
  than erroring.
- **Stale marker**: if a recorded console marker points at a process that no longer exists, launching a
  new console or stopping must not be blocked by the stale marker.
- **Corrupt or partial screenshot upload**: a failed upload reports an error and does not attach a
  broken file or partially-write the result.
- **Database temporarily busy** (a concurrent terminal writer): writes wait briefly and succeed rather
  than failing, consistent with the engine's existing concurrent-writer safety.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a `spectra run console` command that starts a local web server and
  serves an interactive page for driving an execution run. The server MUST dispatch every operation to
  the existing execution engine and MUST NOT re-implement run, queue, verdict, or persistence logic — it
  is a sibling of the existing terminal and agent transports.
- **FR-002**: The console MUST NOT be hosted as part of the agent/MCP integration surface; it is a
  distinct local web transport.
- **FR-003**: The browser MUST act only as a view and a write-back caller. Page state MUST always be a
  projection of the run's persisted state, refetched from the server. The browser MUST NOT hold
  authoritative run state — no local/session storage of results, no export/import as the store.
- **FR-004**: Every verdict, comment, skip, note, and screenshot recorded in the console MUST be
  persisted to the same execution database used by the terminal and agent transports, leaving the
  database in a state indistinguishable from those transports for the same actions (status, notes,
  handle, blocking cascade, and ordering all identical).
- **FR-005**: The verdict write-back MUST enforce the same mechanical human-in-the-loop guardrails the
  terminal path enforces: an explicit status is required (the console never infers a verdict), and a
  comment/reason is required for FAIL, BLOCKED, and SKIP. Actions missing a required status or required
  notes MUST be rejected and MUST record nothing.
- **FR-006**: The console MUST let the engineer attach a screenshot to the current test by uploading an
  image from the browser; the upload MUST be stored through the existing screenshot encode-and-store path
  and linked to the result's screenshot list. No new screenshot storage model may be introduced and the
  browser MUST NOT retain the authoritative copy.
- **FR-007**: The page MUST present the current test and run state in Spectra's existing report visual
  styling (its established color palette, cards, and layout), so the console is visually consistent with
  the reports — and MUST NOT adopt an unrelated external styling.
- **FR-008**: The page MUST keep itself current by re-fetching run state from the server on an interval
  and after every write-back action, so the view reflects the database without manual reload.
- **FR-009**: The server MUST run detached from the process/session that launched it: it MUST survive the
  launching process exiting and MUST NOT be terminated by the launching agent session being compacted or
  ending.
- **FR-010**: The system MUST provide an explicit `spectra run console --stop` action that shuts the
  server down, and the server MUST stop only on that explicit action (not implicitly when the launcher
  goes away). The launch MUST record a discoverable marker (e.g. address/port and process identity) so a
  running console can be found and stopped.
- **FR-011**: The console page and any per-run runtime files it produces MUST be treated as ephemeral,
  local-only artifacts that are excluded from version control and never committed.
- **FR-012**: When there is no active run, the run has ended, or a console is already running for the
  target, the console MUST surface that condition clearly rather than rendering a broken page or
  silently corrupting state.
- **FR-013**: The feature MUST be additive: it MUST NOT change the behavior of the execution engine, its
  storage, the screenshot handling, the report styling source, or the existing terminal/agent transports.

### Key Entities *(include if feature involves data)*

- **Execution Run**: The unit of work the console drives — an ordered queue of tests with per-test
  results (status, notes, screenshots) and overall counts. Owned and persisted by the execution engine;
  the console only projects and mutates it through the engine.
- **Test Result**: One test's recorded outcome within a run — status (passed/failed/blocked/skipped),
  notes/comment, and an associated list of screenshot references. The target of every console write-back.
- **Console Server Marker**: A small runtime record produced at launch identifying the running console
  (its address/port and process identity) so it can be discovered and explicitly stopped. Ephemeral and
  local-only.
- **Screenshot Attachment**: An image linked to a test result, stored in the run's attachments via the
  existing storage path; referenced (not embedded) by the result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A QA engineer can drive an entire run from the console — recording PASS, FAIL-with-comment,
  and BLOCKED-with-comment verdicts — with zero terminal interaction after launch, and the resulting
  database state is identical to driving the same run from the terminal.
- **SC-002**: After any page refresh or browser close-and-reopen mid-run, 100% of previously recorded
  results are present and correct, and the page resumes at the correct current test, with no duplicate or
  phantom results.
- **SC-003**: Attempting to record a FAIL, BLOCKED, or SKIP without a comment is rejected 100% of the
  time and records nothing, matching the terminal guardrail.
- **SC-004**: A screenshot uploaded through the console is attached to the correct test result and
  appears in that result's report, with no authoritative copy retained in the browser.
- **SC-005**: The console keeps serving and recording after the launching process exits and stops only
  when the explicit stop action is issued.
- **SC-006**: The existing engine, state-machine, reconstruction, parity, guardrail, concurrency, agent
  integration, and core test suites remain unchanged and passing; the feature adds new console-parity and
  guardrail-at-the-boundary coverage without disturbing the existing net.

## Assumptions

- **Single user, local only**: the console serves one QA engineer at a time on `localhost`; multi-user
  and remote access are out of scope.
- **A run already exists or can be started**: the primary scenarios assume the engineer starts (or has
  started) a run; the console's job is to *drive* it. (Starting a run from the console is supported by the
  same engine but the recording loop is the focus.)
- **Spectra's report styling is the canonical visual language** for the page; the RMH/Next.js runner
  informed only the interaction model (per-test buttons, comment, screenshot), not the look.
- **Polling is sufficient** for liveness for a single local user; no streaming/push transport is needed.
- **Primary platform is Windows** (macOS secondary); the detached-launch mechanism must be validated on
  the primary platform during planning.
- **Concurrent-writer safety already exists** in the storage layer, so a detached console and a
  short-lived terminal writer can coexist without contention failures.

## Out of Scope

- The execution agent / SKILL revision that re-points the agent at launching this console (a separate,
  dependent effort).
- Bug-description-by-agent and any Copilot-Spaces clarification work (owned separately).
- Streaming/push liveness (SSE/websockets).
- Multi-user or remote console access.
- Any browser-side store of run state (local storage, export/import as the source of truth).
- Changes to the execution engine, storage, screenshot service, or report styling source beyond
  borrowing the visual tokens.

## Dependencies

- **Execution-surface consolidation (repo 065, merged)**: provides the transport-neutral execution engine
  the console builds on, plus lossless cross-process state reconstruction and concurrent-writer safety.
  This gate is satisfied.
- The dependent agent/SKILL revision is sequenced *after* this feature and is not required for the
  console to be usable.
