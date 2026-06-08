# Tasks: Lossless Execution-Queue Reconstruction

**Input**: Design documents from `/specs/064-lossless-queue-reconstruction/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/reconstruction.md

**Tests**: Test tasks ARE included — the spec's `## Tests` section explicitly requests net-new
reconstruction-parity, fail-loud, and round-trip tests. Per the constitution, tests MAY be written
after implementation; here they are the definition of "done" for each story.

**Organization**: Tasks are grouped by user story. Because this is a single shared-root-cause
correctness fix, the **Foundational phase delivers the faithful-reconstruction machinery wired
end-to-end through the engine**; each user-story phase is then an independently testable verification
slice (US3 also adds the tool-surface error code).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 (user-story phases only)

## Path Conventions

Single existing solution: `src/Spectra.Core/`, `src/Spectra.MCP/`, `tests/Spectra.MCP.Tests/`.

---

## Phase 1: Setup

**Purpose**: Establish a clean baseline before changing the reconstruction path.

- [X] T001 Capture a green baseline on branch `064-lossless-queue-reconstruction`: run `dotnet build` then `dotnet test`, and record that the regression-net projects (`tests/Spectra.Core.Tests`, `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs`) and the MCP execution/integration tests pass before any change.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The single shared fix — persist an orchestration snapshot at run-build, reconstruct the
queue losslessly from the DB (fail loud when impossible), and route every engine queue-access path
through the reconstruct-aware `GetQueueAsync`. ALL user stories depend on this phase.

**⚠️ CRITICAL**: No user-story verification can pass until this phase is complete.

- [X] T002 [P] Create `QueueSnapshotEntry` immutable record (RunId, TestId, Title, Priority string, DependsOn?, OrderIndex int; JSON attributes matching sibling models) in `src/Spectra.Core/Models/Execution/QueueSnapshotEntry.cs`
- [X] T003 [P] Add the `queue_snapshot` table + `idx_queue_snapshot_run` index (schema in data-model.md) to `InitializeSchemaAsync` in `src/Spectra.MCP/Storage/ExecutionDb.cs` using `CREATE TABLE IF NOT EXISTS` (additive, same style as `runs`/`test_results`)
- [X] T004 [P] Create `QueueReconstructionException` (sealed, carries `RunId`, distinct from benign not-found) in `src/Spectra.MCP/Execution/QueueReconstructionException.cs`
- [X] T005 Create `QueueSnapshotRepository` with `CreateManyAsync(IEnumerable<QueueSnapshotEntry>)` (single transaction, throws on failure) and `GetByRunIdAsync(runId)` (returns rows ordered by `order_index`) in `src/Spectra.MCP/Storage/QueueSnapshotRepository.cs` (depends on T002, T003)
- [X] T006 In `src/Spectra.MCP/Execution/TestQueue.cs`: remove the lossy `AddFromResult`; add a lossless reconstruction builder that takes an ordered `QueueSnapshotEntry` plus the latest result's status+handle and produces a `QueuedTest` with real Title/Priority(`ParsePriority`)/DependsOn, setting `_currentIndex` to the in-progress test if present (preserves FR-006 behaviour) (depends on T002)
- [X] T007 Inject `QueueSnapshotRepository`: construct it in `src/Spectra.MCP/Program.cs` (next to `resultRepo`) and add it as a constructor parameter/field on `ExecutionEngine` in `src/Spectra.MCP/Execution/ExecutionEngine.cs` (depends on T005)
- [X] T008 In `ExecutionEngine.StartRunAsync` (`src/Spectra.MCP/Execution/ExecutionEngine.cs`): after `TestQueue.Build`, derive snapshot entries from the ordered `queue.Tests` (order_index = position) and persist via `QueueSnapshotRepository.CreateManyAsync` before returning; let persistence failure propagate so run creation fails loud (FR-007) (depends on T006, T007)
- [X] T009 Rewrite `ExecutionEngine.ReconstructQueue` + `GetQueueAsync` (`src/Spectra.MCP/Execution/ExecutionEngine.cs`) to be snapshot-driven: load snapshot (ordered) + latest result per test_id, apply the data-model validation rules (benign null when no results & no snapshot; **throw `QueueReconstructionException`** on snapshot missing/incomplete/inconsistent), and build the faithful queue via the T006 builder (depends on T004, T005, T006)
- [X] T010 Route the divergent queue-access paths through `GetQueueAsync` in `src/Spectra.MCP/Execution/ExecutionEngine.cs`: make `GetStatusAsync` reconstruct (not return null when `_queues` is cold), and replace the direct `_queues.TryGetValue(...)` reads in `AdvanceTestAsync`, `BulkRecordResultsAsync`, `RetestAsync`, and `FinalizeRunAsync`'s pending-guard with the reconstruct-aware queue (warm path unchanged — FR-005) (depends on T009)
- [X] T011 Migrate `tests/Spectra.MCP.Tests/Execution/TestQueueReconstructionTests.cs` off the removed `AddFromResult` to the new lossless builder; preserve the queue-mechanics assertions (GetNext / MarkCompleted / CurrentIndex / CompletedCount) and add assertions that Title/Priority/DependsOn are restored (depends on T006)

**Checkpoint**: `dotnet build` green; `dotnet test tests/Spectra.MCP.Tests` green (incl. migrated T011 and existing long-lived `BlockingCascadeTests`/`RetestFlowTests`); reconstruction is now faithful and wired end-to-end.

---

## Phase 3: User Story 1 - Dependency blocking survives reconstruction (Priority: P1) 🎯 MVP

**Goal**: A queue rebuilt from the DB blocks dependents exactly as the original in-memory queue would.

**Independent Test**: Reconstruct a run with a dependency edge in a fresh engine; drive the
depended-upon test to a non-passing terminal state; confirm dependents are blocked identically.

**Implementation note**: No new production code — the restored `DependsOn` (T006/T009) + the rewired
advance path (T010) deliver this. This phase verifies the slice.

- [X] T012 [P] [US1] Add `tests/Spectra.MCP.Tests/Execution/ReconstructionBlockingParityTests.cs`: (a) `DependencyResolver.PropagateBlocks` on a reconstructed queue blocks the same dependents (incl. transitive) as on the original built queue; (b) via a fresh `ExecutionEngine` over the same `.execution` DB (cold `_queues`), `AdvanceTestAsync(blocker, Failed/Skipped)` cascades blocks to dependents in the DB — matching the long-lived case (depends on T010)

**Checkpoint**: Dependency blocking proven identical across process boundaries.

---

## Phase 4: User Story 2 - Priority and ordering survive reconstruction (Priority: P1)

**Goal**: A rebuilt queue preserves priority and the original (priority-then-topological) order; "next
test" matches the original, never alphabetical.

**Independent Test**: Build a run whose intended order differs from alphabetical-by-id; reconstruct in
a fresh engine; assert full ordering, `Priority`, and `GetNext` selection match the original.

**Implementation note**: No new production code — restored `Priority`/`OrderIndex` ordering (T006/T009)
deliver this.

- [X] T013 [P] [US2] Add `tests/Spectra.MCP.Tests/Execution/ReconstructionOrderingParityTests.cs`: reconstructed queue's `Tests` order, each `Priority`, and `GetNext()` selection equal the original built queue's, using a fixture where dependency/priority order ≠ alphabetical order (guards against the old `OrderBy(test_id)` regression) (depends on T010)

**Checkpoint**: Priority and ordering proven identical across process boundaries.

---

## Phase 5: User Story 3 - Reconstruction fails loud when it cannot be faithful (Priority: P2)

**Goal**: A run with results but a broken snapshot fails loud (distinct from "run not found"), never a
silently degraded queue.

**Independent Test**: Make a run's snapshot missing/incomplete/inconsistent; confirm
`QueueReconstructionException` (surfaced as `RECONSTRUCTION_FAILED`) and that a genuinely empty run
still returns null.

- [X] T014 [US3] In the execution-path tool handlers, catch `QueueReconstructionException` and return a distinct structured error `RECONSTRUCTION_FAILED` (never `RUN_NOT_FOUND`): `src/Spectra.MCP/Tools/TestExecution/AdvanceTestCaseTool.cs`, `SkipTestCaseTool.cs`, `BulkRecordResultsTool.cs`, `RetestTestCaseTool.cs`, `GetTestCaseDetailsTool.cs`, and `src/Spectra.MCP/Tools/RunManagement/GetExecutionStatusTool.cs`, `FinalizeExecutionRunTool.cs` (prefer a single catch in the `McpServer` dispatch path if one exists — check `src/Spectra.MCP/Server/McpServer.cs` first) (depends on T009)
- [X] T015 [P] [US3] Add `tests/Spectra.MCP.Tests/Execution/ReconstructionFailLoudTests.cs`: assert `GetQueueAsync` throws `QueueReconstructionException` for (a) results-but-no-snapshot, (b) result-without-snapshot-row, (c) snapshot-row-without-result, (d) dangling `depends_on`; and returns `null` for a run with no results and no snapshot (benign) (depends on T009)
- [X] T016 [P] [US3] Add a tool-surface test asserting a corrupt-snapshot run yields error_code `RECONSTRUCTION_FAILED` (not `RUN_NOT_FOUND`) in `tests/Spectra.MCP.Tests/Tools/ReconstructionErrorSurfaceTests.cs` (depends on T014)

**Checkpoint**: Fail-loud guaranteed and distinguishable from benign absence (SC-003).

---

## Phase 6: User Story 4 - Identical behaviour regardless of process lifetime (Priority: P1)

**Goal**: Aggregate orchestration behaviour is indistinguishable between a long-lived engine (original
queue) and a fresh engine (reconstructed queue) — the property the consolidation spec depends on.

**Independent Test**: Run identical scenarios through original-queue and rebuilt-queue paths; assert
identical blocking, priority, ordering, next-test selection; round-trip build→persist→reconstruct.

**Implementation note**: The enabling rewire is T010 (Foundational). This phase verifies the aggregate.

- [X] T017 [P] [US4] Add `tests/Spectra.MCP.Tests/Execution/ReconstructionParityTests.cs`: for dependency/priority/order/round-trip fixtures, assert a fresh-engine reconstructed queue is behaviorally identical to the original (next-test, full order, block propagation, pending/completed counts) — the SC-004 aggregate parity (depends on T010)
- [X] T018 [P] [US4] Add `tests/Spectra.MCP.Tests/Integration/ReconstructedExecutionFlowTests.cs`: drive `advance`/`skip`/`retest`/`finalize` through a fresh `ExecutionEngine` over the same DB and assert parity with the long-lived flow — specifically that `retest` no longer returns `RUN_NOT_FOUND` after a cold start and `finalize`'s pending-guard is honoured from a reconstructed queue (depends on T010)

**Checkpoint**: Long-lived == short-lived behaviour proven (FR-004); the 6→0 B-column collapse realized.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T019 [P] Update any execution-engine/architecture docs that assert the queue is fully reconstructable today or misstate state ownership (per spec `## Documentation`); search `docs/` and `docs/specs/ARCHITECTURE-v2.md` for queue/reconstruction/state-ownership claims and correct them to the snapshot model
- [X] T020 Run quickstart validation: full `dotnet test` green, explicitly confirming the regression net (`Spectra.Core` tests + `TestPersistenceServiceTests`) is unchanged and green (SC-005)
- [X] T021 [P] Add a concise `064-lossless-queue-reconstruction` entry to the "Recent Changes" section of `CLAUDE.md` (snapshot table + lossless reconstruction + fail-loud), keeping the file compact

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: depends on Setup; **BLOCKS all user stories**.
- **User Stories (Phases 3–6)**: all depend on Foundational (specifically T010). Given the shared
  fix, the value of US1/US2/US4 is delivered by Foundational; their phases are verification and are
  mutually independent. US3 adds the only story-specific production code (T014).
- **Polish (Phase 7)**: depends on all desired stories complete.

### Within Foundational (ordering)

T002/T003/T004 [P] → T005 → T006 → T007 → T008, and T009 (needs T004/T005/T006) → T010. T011 needs T006.
T009 and T008 both follow T006/T007 and can proceed in parallel only if they touch disjoint regions of
`ExecutionEngine.cs` — in practice they edit the same file, so sequence T008 then T009 then T010.

### User Story Dependencies

- **US1 (P1)**, **US2 (P1)**, **US4 (P1)**: independent of each other; each needs T010. (MVP = US1.)
- **US3 (P2)**: T014 needs T009; tests T015 need T009, T016 needs T014. Independent of US1/US2/US4.

### Parallel Opportunities

- Foundational kickoff: **T002, T003, T004** in parallel (distinct new files).
- After T010: **T012, T013, T015, T017, T018** are all new, distinct test files → fully parallel.
  (T014 is production code; T016 depends on it.)
- Polish: **T019, T021** parallel; T020 after stories.

---

## Parallel Example: after Foundational completes

```bash
# All verification slices write to distinct new files — run together:
Task: "US1 blocking parity → tests/.../Execution/ReconstructionBlockingParityTests.cs"
Task: "US2 ordering parity → tests/.../Execution/ReconstructionOrderingParityTests.cs"
Task: "US3 fail-loud → tests/.../Execution/ReconstructionFailLoudTests.cs"
Task: "US4 aggregate parity → tests/.../Execution/ReconstructionParityTests.cs"
Task: "US4 flow → tests/.../Integration/ReconstructedExecutionFlowTests.cs"
```

---

## Implementation Strategy

### MVP (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational (the actual fix) → 3. Phase 3 US1 → **STOP & VALIDATE**:
dependency blocking is lossless across a process boundary. This already proves the core correctness
claim; US2/US3/US4 harden ordering, fail-loud, and aggregate parity.

### Incremental Delivery

Foundational → US1 (blocking) → US2 (ordering) → US3 (fail-loud + tool surface) → US4 (aggregate parity
+ flow) → Polish. Each story is a green-test increment; none regresses the protected test net.

---

## Notes

- The genuine fix lives in Foundational; the per-story phases are deliberately test-heavy because the
  spec is a correctness fix whose acceptance IS behavioural parity.
- DO NOT modify `Spectra.Core` tests or `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs`
  (regression gate). The only existing test file changed is `TestQueueReconstructionTests.cs` (T011),
  which is an MCP unit test explicitly flagged in the investigation as the reconstruction-path test.
- Commit after each task or logical group.
