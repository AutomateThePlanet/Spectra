# Feature Specification: Repair-Orchestration Hardening & Inspection Surface

**Feature Branch**: `072-repair-batch-inspect`
**Created**: 2026-06-19
**Status**: Draft

## Problem Context

When Spectra's critic assigns a `partial` verdict to a generated test, the test must be repaired — patched against the unverified claims, re-critiqued, and grounded or flagged. This repair loop currently runs as prose-orchestrated per-test steps inside the agent session. For 15 partial tests that means 135 sequential operations; the session exhausts before completing any test end-to-end. There is also no resume checkpoint, so a re-run after interruption restarts from scratch.

Separately, the agent must read three state surfaces — which tests have grounding blocks, what a test's file path is, and what the active config contains — by improvising raw shell commands. Each improvisation is an allowlist prompt that halts an unattended run.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Grounding State Is Inspectable Without Shell Improvisation (Priority: P1)

A developer or agent running repair wants to know which tests have been grounded and which still need attention — without resorting to directory listings, JSON parsing scripts, or direct file reads. They run a single Spectra command and get an accurate per-test summary of grounding state.

**Why this priority**: Inspection unblocks every other story. Without a reliable grounding-state oracle, you cannot know where to resume, cannot verify a repair completed, and cannot audit the corpus. It is also independent of the batch-compile work — it can be built and validated immediately against the 15 already-stuck partial tests.

**Independent Test**: Run the grounding audit command against a suite with known mixed grounding state (the 15 existing partials from the Spec 071 run). Verify that ungrounded tests report `action_needed: repair` and that tests with grounding blocks report `action_needed: none`. No shell improvisation required.

**Acceptance Scenarios**:

1. **Given** a suite with partial verdict files on disk and some tests with grounding blocks and some without, **When** the user runs the grounding audit command for that suite, **Then** the output lists every test's grounding state with a per-test action recommendation and summary counts.
2. **Given** the same suite with JSON output mode requested, **When** the audit runs, **Then** the output is machine-readable structured data the agent can consume without additional parsing.
3. **Given** a test that already has a grounding block written, **When** the audit runs, **Then** that test is reported as complete (`action_needed: none`) and would be excluded from any batch repair manifest.
4. **Given** a test without a grounding block and a partial verdict on disk, **When** the audit runs, **Then** that test is reported as pending (`action_needed: repair`).
5. **Given** the agent needs a test's file path, **When** it runs the test-detail command for that test ID, **Then** the output includes the `file` field — no index-parsing improvisation needed.

---

### User Story 2 — Repair Batch Compiles Deterministically and Resumes Across Sessions (Priority: P2)

A developer running repair on a batch of partial tests wants all repair prompts compiled in a single deterministic command that returns a manifest they can hand to the agent. If the session exhausts mid-batch, a re-run picks up from where it stopped — tests that already have grounding blocks are automatically skipped.

**Why this priority**: This is the core orchestration fix. Pulling batch compile into a single CLI call drops per-test agent operations from 9 to approximately 5, and the grounding-block checkpoint eliminates restart-from-scratch. Depends on US1 for the checkpoint oracle.

**Independent Test**: Compile a repair manifest for the 15 existing partials. Verify the manifest includes only ungrounded tests. Process a subset, then re-run the compile command and confirm the manifest now contains only the remaining tests.

**Acceptance Scenarios**:

1. **Given** a suite with N partial verdict files on disk (some with grounding blocks, some without), **When** the batch compile command runs, **Then** a JSON manifest is emitted containing one entry per ungrounded partial (id, repair prompt, source refs, file path) — grounded tests excluded.
2. **Given** a manifest produced by the batch compile command, **When** the agent processes it test-by-test, **Then** each test requires approximately 5 agent operations — not 9. The compile and batch-filter steps have already happened.
3. **Given** a repair session interrupted after 8 of 15 partials, **When** repair is resumed in a new session, **Then** re-running the batch compile command produces a manifest of the remaining 7. The 8 completed tests are never re-queued.
4. **Given** a partial test that is still partial after the repair attempt, **When** the loop processes it, **Then** it is flagged for human review and the loop continues to the next entry — the batch never halts on a single stubborn partial.
5. **Given** an empty suite (no partial verdicts on disk), **When** the batch compile command runs, **Then** an empty manifest is emitted with a clear message and a successful exit — no error.

---

### User Story 3 — Full Generate+Repair Cycle Completes Unattended (Priority: P3)

A developer running a full unattended generate+repair cycle wants the entire cycle to complete — or cleanly resume after interruption — without a single allowlist prompt. Every agent action is either writing to a Spectra-authored path or running a `spectra` CLI command.

**Why this priority**: This is the proof that the whole system works end-to-end. Depends on US1 and US2. The non-stop contract is only verifiable after the inspection surface and batch compile are in place.

**Independent Test**: Run a complete generate+repair cycle with no-interaction mode. Observe that no allowlist prompts appear — all state reads go through Spectra commands.

**Acceptance Scenarios**:

1. **Given** a generate run that produced partial-verdict tests, **When** the full repair cycle runs with no-interaction mode, **Then** it completes (or checkpoints) with zero raw-shell improvisation — no directory listings, index-parsing scripts, or direct config-file reads.
2. **Given** a resumed repair cycle after prior interruption, **When** the cycle runs again, **Then** it correctly skips all already-grounded tests and converges — never re-processes a completed test.
3. **Given** the agent needs the resolved configuration, **When** it fetches config state, **Then** it uses the existing config-display command and never reads the config file directly.

---

### Edge Cases

- What happens when `compile-repair-batch` is run with no partial verdicts on disk? (Expected: empty manifest, exit 0, clear message.)
- What happens when all partials already have grounding blocks? (Expected: empty manifest, exit 0 — "nothing to repair".)
- What happens when a verdict file exists but the corresponding test `.md` file is missing? (Expected: fail-loud on that entry with clear error; continue to the next entry.)
- What if the session exhausts mid-manifest? (Expected: tests with grounding blocks are skipped on re-run; resume is automatic via the checkpoint filter.)
- What if a hallucinated-verdict test accidentally ends up in the partial filter? (Expected: the filter is verdict-type-specific — only `partial` verdicts are included; hallucinated tests have already been dropped via the separate delete path.)

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A single CLI command MUST compile all partial-verdict tests for a suite into a JSON manifest in one deterministic, model-free pass, filtering out tests that already have grounding blocks.
- **FR-002**: The batch compile command MUST reuse the single-test repair prompt builder internally — no duplicate prompt-building logic.
- **FR-003**: The batch compile command MUST emit one manifest entry per ungrounded partial, containing: test id, repair prompt (or path to it for large prompts), source refs, and target file path.
- **FR-004**: A single CLI command MUST report per-test grounding state for a suite: id, verdict, score, grounding_written (bool), flagged_for_review (bool), action_needed. Both human-readable and JSON output modes required.
- **FR-005**: The grounding audit command MUST serve as the canonical checkpoint for the repair loop — the batch compile command uses its logic to filter already-done tests.
- **FR-006**: The test-detail command MUST include the `file` path field in its structured output.
- **FR-007**: The skill's repair step MUST be rewritten as a numbered manifest-driven procedure with an explicit resume note stating that re-running the batch compile step automatically skips completed tests.
- **FR-008**: The skill MUST state the flag-and-continue rule: a test still partial after one repair attempt is flagged for human review and the loop continues — the batch never halts on a single stubborn partial.
- **FR-009**: Skill and documentation MUST note that the existing config-display command (with `--raw` flag) returns the resolved configuration, replacing the raw-file-read improvisation.
- **FR-010**: Repair intermediate files MUST use per-test naming (`repaired-{id}.json`) to prevent overwrite collisions when multiple tests are processed.

### Key Entities

- **Repair Manifest**: A JSON structure listing one entry per ungrounded partial test — the handoff artifact between deterministic batch compile and the agent-driven critic loop.
- **Grounding Block**: The condensed verdict block written to a test's frontmatter by `ingest-grounding`; its presence is the "done" marker for the resume checkpoint.
- **Verdict File**: Per-test file written by the critic subagent; the source data for both grounding audit and batch compile.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A batch of N partial tests compiles to a repair manifest in one command invocation — zero per-test agent compile calls required.
- **SC-002**: Per-test agent operations in the repair loop drop from 9 to approximately 5 (critic + persist-update + persist-grounding, with compile and batch-filter moved to deterministic CLI).
- **SC-003**: Interrupting a repair batch and re-running produces a manifest containing only the remaining incomplete tests — zero already-grounded tests are re-processed.
- **SC-004**: The grounding audit command returns accurate per-test grounding state for suites up to 100 tests without requiring any external shell tools or file parsing by the caller.
- **SC-005**: A full generate+repair cycle with no-interaction mode produces zero allowlist prompts for state reads — all inspection actions use Spectra commands.
- **SC-006**: The test-detail command returns the test's file path in its structured output — zero index-parsing workarounds required by agents or humans.

---

## Assumptions

- The critic subagent remains a separate model-family subagent (context-fork, agent-driven). Making it a pure CLI step is a structural non-goal — the independent-judgment principle is preserved.
- Repair intermediate files are gitignored; no change to their accepted schema beyond what the existing persist-update command already handles.
- The existing persist-update command's file-path flag is unchanged and continues to accept any caller-supplied path.
- The existing config-display command's raw-output flag requires no code changes — only skill/doc awareness needs updating.
- Resume is multi-session by design: the expectation is not that all operations fit in one session, but that re-running in a new session picks up cleanly from the grounding-block checkpoint.
