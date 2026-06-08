# Feature Specification: Execution Agent / SKILL Revision

**Feature Branch**: `067-execution-agent-console-revision`  
**Created**: 2026-06-08  
**Status**: Draft — HARD-GATES on the Execution Console (Spec 066, merged)  
**Input**: User description: "Rewrite the execution agent + SKILL so the agent stops driving the per-test loop and instead starts the run, launches the detached console, and is on-call — the human-in-the-loop guarantee moves from prompt instruction to a property of the console server."

## Overview

Today the SPECTRA execution **agent** and its **SKILL** *are* the manual-run loop: they present one test
at a time in chat, wait for the tester's spoken verdict, and record it. With the execution console
(Spec 066) now shipped, that loop belongs in the browser — the tester clicks PASS/FAIL/BLOCKED, types a
comment, and attaches a screenshot on the page, with the database as the source of truth.

This feature **re-points the agent and SKILL** to match. The agent's role shifts from **loop-driver** to
**orchestrator + on-call**:

1. Select the tests (by suite, filter, saved selection, or smart/risk-based intent).
2. Start the run.
3. Launch the detached console and hand the tester the local URL.
4. Step back. When the tester opens chat mid-run ("what does step 3 mean?"), answer from the source
   documentation — reading current state from the database, never from the browser page.
5. Finalize, resume, or cancel on request.

Critically, the **human-in-the-loop guarantee stops being a prompt instruction and becomes a property of
the console**. The console already refuses a verdict with no explicit status, requires a comment for
FAIL/BLOCKED/SKIP, never auto-advances, and never infers a verdict (Spec 066). So the agent no longer
needs to *promise* that discipline in prose — it simply never records verdicts in chat at all. The
guardrail enforcement code is unchanged; only its *home* moves from "instructions to a model" to "a
behavior of the server."

This is a **prose + test change**: it rewrites the agent file, the SKILL file, and the two skill/agent
contract tests, and updates stale workflow docs. It changes **no** engine, handler, or guardrail code.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Agent orchestrates instead of driving the loop (Priority: P1)

A tester tells the agent which tests to run ("run the checkout smoke tests"). The agent selects the
tests, starts the run, launches the console, and replies with the local URL — and then stops. It does
**not** present tests one at a time or ask "pass or fail?" in chat. The tester drives the run in the
browser.

**Why this priority**: This is the whole point of the revision — moving the loop out of chat and into the
console. Without it, the agent and the console would both claim to drive the run, defeating Spec 066. It
is the minimum viable change and is independently demonstrable.

**Independent Test**: Ask the agent to run a suite; confirm its instructions/flow are "select → start →
launch console → hand over URL," with no per-test presentation or verdict-collection step, and that the
revised SKILL/agent content no longer contains the show→wait→record loop or the verdict-mapping table.

**Acceptance Scenarios**:

1. **Given** a request to run a suite, **When** the agent acts, **Then** it selects tests, starts the
   run, launches the console, and returns the local URL — and does not present tests or collect verdicts
   in chat.
2. **Given** the agent has started a run, **When** it reports back, **Then** it directs the tester to the
   browser console as the place to record results.
3. **Given** the tester did not name a suite, **When** the agent helps choose, **Then** it still uses the
   existing selection capabilities (saved selections, filters, smart/risk-based intent) before starting.

---

### User Story 2 - Human-in-the-loop becomes a server guarantee (Priority: P1)

The tester records every verdict by clicking in the console; the agent never records a verdict on the
tester's behalf in chat. The discipline that used to be enforced by careful agent prose — explicit
verdict required, comment required for non-pass, no auto-advance, no fabricated notes — is now guaranteed
by the console, and the agent simply refuses to record verdicts in conversation.

**Why this priority**: The verdict-source guarantee ("a human actually said this") is the integrity
property of the whole system. The revision must preserve it end-to-end while relocating where it is
enforced. Co-equal P1 with Story 1 — moving the loop is only safe if the guarantee provably survives the
move.

**Independent Test**: Confirm the revised agent/SKILL state the verdict discipline as a property of the
console (not as a chat loop the model runs), instruct the agent never to fabricate or record a verdict in
chat, and that the guardrail enforcement code is untouched (its tests stay green unchanged).

**Acceptance Scenarios**:

1. **Given** any verdict, **When** it is recorded, **Then** it is recorded through the console, never by
   the agent in chat.
2. **Given** the tester asks the agent to "just mark them all passed" in chat, **When** the agent
   responds, **Then** it declines to record verdicts in chat and points to the console.
3. **Given** the revised prose, **When** the guardrail rules are stated, **Then** they are framed as
   console guarantees (explicit verdict, required notes, no auto-advance, no fabrication), and the
   underlying enforcement code is unchanged.

---

### User Story 3 - On-call clarification reads the database, not the page (Priority: P2)

Mid-run, the tester switches to chat and asks the agent to explain a step or expected result. The agent
reads the current test from the run's persisted state (the same source the terminal reads), looks up the
relevant source documentation by its references, and gives a short, grounded answer — then the tester
returns to the browser to continue.

**Why this priority**: On-call clarification is the agent's main job once it is out of the loop, and it
must read authoritative state, not scrape the browser page (which holds no authoritative state). It is
below the core orchestration shift because a run is drivable without it, but it defines the agent's
steady-state value.

**Independent Test**: Ask the agent about the current test mid-run; confirm it reads current state from
the run-status command (database) — not the console page or URL — and answers from source documentation
via direct file reads.

**Acceptance Scenarios**:

1. **Given** an active run, **When** the tester asks about the current test, **Then** the agent reads
   current state from `spectra run status` (the database), not the console page.
2. **Given** a question about a step's meaning, **When** the agent answers, **Then** it reads the test's
   source documentation references directly and answers from what it read, saying so plainly if no doc
   covers it.
3. **Given** the console and the agent are both open, **When** both report state, **Then** they agree
   because both read the same database.

---

### User Story 4 - Finalize, resume, and cancel on request (Priority: P2)

When the tester is done (or wants to stop, pause, or resume), they ask the agent, which runs the existing
lifecycle commands and reports the outcome (e.g., the generated report). The agent retains these
orchestration controls; only the per-test verdict loop is gone.

**Why this priority**: Run lifecycle control is part of orchestration and must remain after the loop is
removed, but it reuses existing commands and is lower-risk than the loop shift itself.

**Independent Test**: Ask the agent to finalize/resume a run; confirm it uses the existing lifecycle
commands and reports the result, with no per-test loop involved.

**Acceptance Scenarios**:

1. **Given** a completed or in-progress run, **When** the tester asks to finalize, **Then** the agent
   finalizes via the existing command and surfaces the report.
2. **Given** a paused or interrupted run, **When** the tester asks to resume, **Then** the agent resumes
   by run id (state is durable), without re-presenting prior tests.

---

### Edge Cases

- **Tester pastes a screenshot into chat** instead of the console: the primary path is the console's
  drop/paste; a chat-paste fallback to attach via the existing command may remain, but the console is the
  documented place.
- **Tester insists the agent record a verdict in chat**: the agent declines and redirects to the console
  (the guarantee is now a server property; chat is not a verdict channel).
- **No console is running** when the tester asks about progress: the agent can still read run state from
  the database and can (re)launch the console.
- **Tester asks the agent to drive the old loop** ("show me each test here"): the agent explains the
  console now owns the loop and hands over the URL.
- **Help/quickstart and non-execution CLI delegation** (update, coverage, validate, etc.) are unaffected
  and remain available.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The execution SKILL MUST be rewritten so the present→wait→record per-test loop and the
  verdict-mapping table are **removed**. The SKILL MUST describe the console as where tests are presented
  and verdicts recorded.
- **FR-002**: The execution agent MUST be rewritten so the per-test loop step, the in-chat test
  presentation format, and the in-chat result-collection mapping are **removed or replaced** by
  orchestration. The agent MUST retain test selection (including saved selections and smart/risk-based
  intent), run start, status, finalize, and resume-by-run-id.
- **FR-003**: After starting a run, the agent MUST launch the execution console and hand the tester the
  local URL as the place to drive the run.
- **FR-004**: The human-in-the-loop rules MUST be restated as **console guarantees** (explicit verdict
  required, comment required for FAIL/BLOCKED/SKIP, no auto-advance, no fabricated verdict). The agent
  MUST never record or fabricate a verdict in chat. The guardrail enforcement **code MUST NOT change** —
  only the prose home moves.
- **FR-005**: The agent MUST retain the documentation-lookup capability (reading source documentation
  directly) for on-call clarification.
- **FR-006**: When on-call, the agent MUST read current run state from the run-status command (the
  database), never from the console page or URL. The console and the agent are two readers of the same
  authoritative state.
- **FR-007**: Screenshot guidance MUST be re-homed: screenshots are attached via the console; a chat-paste
  fallback through the existing command MAY remain, but the console is the primary path.
- **FR-008**: The two skill/agent contract tests MUST be rewritten to assert the orchestrate-not-drive
  behavior (start → launch console → on-call; no in-chat verdict collection; on-call status from the
  database). No other test in the regression net may be modified.
- **FR-009**: Stale workflow documentation that depicts the chat-driven per-test loop (getting-started and
  execution-workflow snippets) MUST be updated to the orchestrate model.
- **FR-010**: This feature MUST NOT change any engine, handler, storage, or guardrail code; it is limited
  to agent/SKILL prose, the two contract tests, and documentation.

### Key Entities *(include if feature involves data)*

- **Execution Agent definition**: The bundled prose that defines the agent's role and workflow — the
  primary artifact rewritten from loop-driver to orchestrator + on-call.
- **Execution SKILL definition**: The bundled step-by-step prose the agent follows — rewritten to remove
  the verdict loop and point at the console.
- **Run state (read-only here)**: The persisted run/test state the agent reads for on-call answers; owned
  by the engine, unchanged by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a run request, the agent's flow is select → start → launch console → hand over URL, with
  **zero** in-chat test presentations or verdict prompts.
- **SC-002**: The agent records **zero** verdicts in chat; 100% of verdicts are recorded through the
  console.
- **SC-003**: The verdict discipline (explicit verdict, required notes for non-pass, no auto-advance, no
  fabrication) is preserved end-to-end and is stated as a console guarantee.
- **SC-004**: Mid-run clarification answers are derived from the database (run status) and source
  documentation, never from the console page; the agent and console always report consistent state.
- **SC-005**: Only the two skill/agent contract tests change; the engine/state-machine/reconstruction,
  parity, guardrail, concurrency, and core test suites remain **unchanged and green**.
- **SC-006**: No engine/handler/guardrail code is modified (verifiable by diff scope and by the guardrail
  tests passing unchanged).

## Assumptions

- The execution console (Spec 066) is merged and available, so the agent can launch it and rely on its
  guardrails. (Hard dependency — see Dependencies.)
- The agent launches the console on the **local** machine (the tester's machine), consistent with the
  console's local-only, one-human model.
- The agent reads run state through the existing session-free status command, which reconstructs from the
  database — so on-call answers are correct even in a fresh agent session.
- Help/quickstart and the non-execution CLI delegation table remain part of the agent and are out of scope
  for rewriting beyond what the loop removal requires.
- "Launch the console" means invoking the console start command; if a console is already running, the
  agent reuses/hands over its URL rather than double-launching.

## Out of Scope

- The execution console infrastructure itself (Spec 066 — this feature depends on it).
- Any change to the guardrail enforcement code in the run handler or engine (only the prose home moves).
- Bug-description-by-agent and any Copilot-Spaces clarification work (owned separately; if clarification
  ever attaches, it uses native file reads).
- Changes to other agents/SKILLs beyond the execution agent and execution SKILL (and the stale workflow
  doc snippets).
- Any change to the regression test net other than the two named skill/agent contract tests.

## Dependencies

- **HARD-GATES on the Execution Console (Spec 066, merged)**: the agent cannot launch a console that does
  not exist. The console's write-back guardrails are the enforcement home this feature relocates the
  discipline to.
- Gated behind the execution-surface consolidation (repo 065, merged) — satisfied.
- The guardrail discipline is preserved across the move: it shifts from prose-in-loop to a property of the
  console transport, with the enforcement code unchanged.
