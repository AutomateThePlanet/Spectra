# Feature Specification: Lossless Execution-Queue Reconstruction

**Feature Branch**: `064-lossless-queue-reconstruction`  
**Created**: 2026-06-08  
**Status**: Draft — correctness prerequisite for the execution-surface consolidation spec  
**Input**: User description: "Reconstruct the in-memory execution TestQueue from SQLite without dropping DependsOn / Priority / ordering — so any short-lived process drives the engine identically."

## Overview

The execution engine records every test result durably and can rebuild its working test queue
from storage when the original in-memory queue is no longer available (for example after a
process restart, or in a fresh short-lived process). Today that rebuild is **lossy**: it discards
the dependency relationships between tests, each test's priority, and the original test ordering.
A run that is driven by anything other than the single original long-lived process therefore
silently loses dependency-blocking and reverts to alphabetical ordering — results are still
recorded correctly, but orchestration behaviour degrades without any error or warning.

This feature makes queue reconstruction **lossless and faithful**: a queue rebuilt from storage
behaves identically to the original in-memory queue for dependency-blocking, priority, and
ordering — or fails loudly if it cannot. This is the correctness foundation that lets the
execution surface be driven by short-lived processes in a later consolidation effort.

## Clarifications

### Session 2026-06-08

- Q: When reconstruction needs the lost orchestration data (dependencies, priority, order), where
  should it come from — re-read the on-disk index, or persist a snapshot? → A: **Persist an
  orchestration snapshot at run-build time** (each test's dependency relationships, priority, title,
  and original order captured onto durable storage when the run is created). Reconstruction reads
  **only** durable storage, making it DB-complete and immune to later index edits. "Identical to the
  original queue" is therefore defined against the run's **build-time** state. (Supersedes the
  earlier informed-guess default of re-reading the index, which a mid-run index edit could make
  diverge from the original queue.)
- Q: What counts as "cannot faithfully rebuild" and must fail loud? → A: A run has a recorded result
  for a test whose persisted orchestration snapshot is missing, incomplete, or internally
  inconsistent (e.g. a dependency edge points at a test absent from the snapshot), such that the
  queue cannot be rebuilt with its real dependencies/priority/order. (See Edge Cases.)
- Q: How does "fail loud" surface, and how is it kept distinct from a benign empty result? → A:
  Reconstruction **throws a distinct, typed error** on a broken/incomplete/inconsistent snapshot.
  This is separate from the benign "run has no results / run not found" signal (which remains a
  non-throwing absence). Consuming layers translate the thrown error onto their own error surface;
  a broken snapshot MUST NOT be reported as "run not found." (Consistent with the engine's existing
  throw-on-invalid-state behaviour.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dependency blocking survives reconstruction (Priority: P1)

An execution run has tests with declared dependencies (some tests must not start until others
have completed). The original process that built and held the queue is gone, so the queue is
rebuilt from storage in a new process. When a depended-upon test fails or is skipped, the tests
that depend on it must be blocked exactly as they would have been in the original queue.

**Why this priority**: This is the core correctness loss. Without it, a rebuilt queue silently
stops enforcing dependency blocking — the most dangerous failure because it is invisible: results
keep recording and nothing errors, but the orchestration guarantee is gone.

**Independent Test**: Build a run with a dependency relationship, drop/rebuild the queue from
storage, drive the depended-upon test to a non-passing terminal state, and confirm the dependent
tests are blocked identically to a run whose original queue was never dropped.

**Acceptance Scenarios**:

1. **Given** a run with persisted dependency relationships whose original in-memory queue is no
   longer available, **When** the queue is reconstructed in a new process, **Then** dependency
   blocking behaves identically to the in-memory-original case.
2. **Given** a reconstructed queue, **When** a test that others depend on reaches a non-passing
   terminal state, **Then** the dependent tests are blocked (not silently left runnable).

---

### User Story 2 - Priority and ordering survive reconstruction (Priority: P1)

An execution run's tests have assigned priorities and a defined execution order (priority-then-
dependency order). After the queue is rebuilt from storage, "the next test to run" and the overall
ordering must match the original queue — not fall back to alphabetical-by-id.

**Why this priority**: Equal-criticality companion to Story 1. Ordering and priority loss changes
which test is presented next and the sequence of the whole run, so a rebuilt queue would drive the
run in a different order than intended — again silently.

**Independent Test**: Build a run whose priority/dependency order differs from alphabetical order,
rebuild the queue from storage, and confirm the next-test selection and full ordering match the
original queue rather than alphabetical order.

**Acceptance Scenarios**:

1. **Given** tests with assigned priorities and a defined order, **When** the queue is
   reconstructed, **Then** priority and ordering are preserved exactly as in the original queue.
2. **Given** a run whose intended order differs from alphabetical-by-id, **When** the queue is
   reconstructed and the next pending test is requested, **Then** the selected test matches the
   original queue's selection, not alphabetical order.

---

### User Story 3 - Reconstruction fails loud when it cannot be faithful (Priority: P2)

If the data needed to faithfully rebuild the queue cannot be located or resolved, reconstruction
must fail loudly with a clear error rather than producing a silently degraded queue.

**Why this priority**: Lower than P1 because it is the safety net for the unhappy path, but
essential — the whole point of the feature is to eliminate *silent* degradation, so the failure
mode must be explicit.

**Independent Test**: Make the persisted orchestration snapshot unavailable/incomplete for a run
that has recorded results, attempt reconstruction, and confirm a loud, clear failure rather than a
degraded queue.

**Acceptance Scenarios**:

1. **Given** a run whose orchestration snapshot is missing, incomplete, or internally inconsistent,
   **When** reconstruction is attempted, **Then** it fails loudly with a clear error and does **not**
   return a degraded queue.
2. **Given** any reconstruction that cannot faithfully rebuild a field, **When** it runs, **Then**
   it does not infer or default the lost field silently.

---

### User Story 4 - Identical behaviour regardless of process lifetime (Priority: P1)

The same run, driven once by a long-lived process holding the original queue and once by a
fresh/short-lived process that rebuilds the queue, must produce identical orchestration behaviour
(blocking, priority, ordering, next-test selection) end to end.

**Why this priority**: This is the property the later consolidation work depends on. Stories 1 and
2 prove individual fields survive; this story asserts the *aggregate* behaviour is indistinguishable
between the two process models, which is the actual deliverable.

**Independent Test**: Run an identical scenario through both an original-queue path and a
rebuilt-queue path and assert the observable orchestration outcomes are identical.

**Acceptance Scenarios**:

1. **Given** identical runs, **When** one is driven by the original in-memory queue and the other
   by a queue rebuilt from storage, **Then** their dependency-blocking, priority, ordering, and
   next-test selection are identical.
2. **Given** a round-trip (build → persist → rebuild), **When** the rebuilt queue is driven through
   dependency, priority, and ordering scenarios, **Then** behaviour is identical to the original.

---

### Edge Cases

- **Snapshot missing/incomplete**: A run has recorded results but its persisted orchestration
  snapshot is absent or incomplete → reconstruction fails loud (FR-003), no degraded queue returned.
- **Snapshot internally inconsistent**: A persisted dependency edge points at a test not present in
  the snapshot → fail loud rather than fabricate or drop the dependency.
- **Mid-run index edit**: The on-disk index is regenerated/edited while a run is active → MUST have
  no effect on reconstruction, because reconstruction reads the build-time snapshot, not the index.
- **Run with no dependencies and default priorities**: Reconstruction must still preserve the
  original ordering (which may legitimately coincide with alphabetical); correctness is "matches the
  original queue," not "is non-alphabetical."
- **In-progress test at reconstruction time**: The rebuilt queue must restore the in-progress test
  as current (preserve existing behaviour) in addition to restoring orchestration fields.
- **Multiple recorded attempts for one test (retest history)**: Reconstruction must continue to
  resolve to the correct latest state per test while now also restoring that test's real
  dependencies, priority, and position.
- **Original in-memory queue still present**: When the original queue is still available it MUST be
  used unchanged; reconstruction is only the rebuild-from-storage path and must not alter the
  already-correct in-memory path.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Queue reconstruction from storage MUST preserve each test's dependency relationships,
  priority, and the original ordering with no loss — a reconstructed queue MUST be behaviorally
  identical to the original in-memory queue for dependency-blocking, priority, ordering, and
  next-test selection.
- **FR-002**: The system MUST persist, at run-build time, each test's orchestration data
  (dependency relationships, priority, title, and original order) onto durable storage, so that
  reconstruction is **DB-complete** — rebuildable from durable storage alone, with no read of the
  mutable on-disk index. Reconstruction MUST NOT infer, default, or re-derive lost fields from a
  source that may have changed since run-build time, and MUST NOT silently default any field.
- **FR-003**: Any reconstruction that cannot faithfully rebuild the queue (snapshot missing,
  incomplete, or internally inconsistent) MUST fail loud by **throwing a distinct, typed error**,
  and MUST NOT silently produce a degraded queue. This failure MUST be distinguishable from the
  benign "run has no results / run not found" signal (which remains a non-throwing absence); a
  broken snapshot MUST NOT be surfaced as "run not found."
- **FR-004**: Orchestration behaviour MUST be identical whether the engine is driven by a long-lived
  process (holding the original queue) or a short-lived process (rebuilding the queue from storage).
- **FR-005**: When the original in-memory queue is available, it MUST be used as-is; the
  reconstruction changes MUST NOT alter behaviour of the already-correct in-memory path.
- **FR-006**: Reconstruction MUST continue to honour existing per-test state semantics — resolving
  to the correct latest state per test across recorded attempts, and restoring any in-progress test
  as the current test — in addition to restoring dependency/priority/ordering.
- **FR-007**: Persisting the orchestration snapshot MUST be part of run creation; if the snapshot
  cannot be durably persisted at run-build time, run creation MUST fail loud rather than create a
  run that can never be faithfully reconstructed. (Derived from FR-002 — a run with no snapshot is
  an unreconstructable run.)

### Out of Scope (explicit)

- The new command-line execution surface, relocating the execution code into its own component, and
  database concurrency hardening (write-ahead logging / busy-timeout) — all belong to the follow-on
  consolidation spec and MUST NOT be attempted here.
- Removing or changing the existing remote execution server surface.
- Broad multi-client concurrency support.

### Reused Verbatim (do not modify)

- The execution engine's external contract, the durable result rows in storage, and the test
  state-machine. This feature corrects reconstruction only — not the engine, not result semantics.

### Key Entities *(include if feature involves data)*

- **Execution queue**: The ordered working set of tests for a run, carrying per-test dependency
  relationships, priority, title, ordering, and current-state/in-progress markers. The subject of
  reconstruction.
- **Persisted test result**: The durable per-test record (status, attempt, handle, timestamps,
  etc.) that survives process boundaries. Authoritative for *results*; today insufficient on its own
  for faithful *orchestration* rebuild.
- **Orchestration snapshot**: A durable, write-once-at-run-build record of each test's dependency
  relationships, priority, title, and original order, captured when the run is created. The
  authoritative source for faithful reconstruction; never re-read from the mutable on-disk index.
  Defines the "original queue" baseline that a reconstructed queue must match.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every scenario with dependency relationships, a queue rebuilt from storage blocks
  dependent tests in 100% of cases where the original in-memory queue would have blocked them (zero
  silent loss of dependency-blocking).
- **SC-002**: For every scenario, the next-pending-test selection and full ordering of a rebuilt
  queue match the original in-memory queue's selection and ordering in 100% of cases (no fallback to
  alphabetical ordering).
- **SC-003**: 100% of reconstruction attempts that cannot faithfully rebuild the queue throw a
  distinct, typed error (never returning a degraded queue and never reported as "run not found");
  0% return a silently degraded queue.
- **SC-004**: A defined parity scenario set (dependency, priority, ordering, round-trip) yields
  identical observable orchestration outcomes whether driven by a long-lived or a short-lived
  process — 0 behavioural differences.
- **SC-005**: The pre-existing regression nets — core models/persistence tests and the existing
  execution-engine tests proving long-lived behaviour — remain unchanged and green.

## Assumptions

- **Numbering**: The source request was drafted as "Spec 066"; the repository's next available
  number is **064**, so this feature is **064**. The follow-on consolidation spec (referenced as
  "067" in the source) is simply the next-after-this consolidation effort regardless of its
  eventual number.
- **Source of truth for reconstruction** (decided — see Clarifications): A durable orchestration
  snapshot is persisted at run-build time and is the **sole** source for reconstruction; the mutable
  on-disk index is never re-read during reconstruction. This accepts a small storage/schema addition
  in exchange for drift-immunity and a strict "DB-complete" rebuild — the only way to guarantee
  FR-001's "identical to the original queue" when the index can change mid-run. The exact storage
  shape (e.g. additional columns vs. a serialized snapshot record) is a planning/implementation
  decision, not a scope decision.
- **Status confirmed against the repo**: The lossy reconstruction is **still present** today (the
  rebuild path hard-codes default priority, null dependencies, and alphabetical ordering, and does
  not consult the test index). This feature is therefore real work, not a no-op.
- **Pre-existing latent defect in scope-by-consequence**: The divergence where one queue-access path
  rebuilds while another returns nothing (causing a retest operation to fail across process
  boundaries) is expected to resolve as a consequence of unifying reconstruction; it is noted here
  but the feature's acceptance is defined by FR-001–FR-006, not by that defect specifically.

## Dependencies

- Gated behind the completed migration series; this feature is a **hard prerequisite** for the
  follow-on execution-surface consolidation spec, which MUST NOT start until this one is merged and
  green — otherwise the consolidated path would silently lose dependency-blocking and ordering.
