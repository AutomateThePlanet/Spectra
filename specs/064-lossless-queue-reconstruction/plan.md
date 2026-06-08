# Implementation Plan: Lossless Execution-Queue Reconstruction

**Branch**: `064-lossless-queue-reconstruction` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/064-lossless-queue-reconstruction/spec.md`

## Summary

Today the in-memory `TestQueue` is reconstructed from SQLite **lossily**: `TestQueue.AddFromResult`
hard-codes `Title = TestId`, `Priority = Medium`, `DependsOn = null`, and `ExecutionEngine.ReconstructQueue`
re-orders alphabetically by `TestId`. Any process that does not hold the original in-memory queue
therefore silently loses dependency-blocking and ordering.

**Approach (decided in Clarifications):** persist a durable **orchestration snapshot** at run-build
time — one row per queued test capturing `title`, `priority`, `depends_on`, and `order_index` — in a
new `queue_snapshot` table. Reconstruction reads **only** durable storage (snapshot + latest result
status per test), making it DB-complete and immune to mid-run index edits. Reconstruction that cannot
faithfully rebuild (snapshot missing/incomplete/inconsistent) throws a distinct typed
`QueueReconstructionException`, kept separate from the benign "run not found" (null) signal. The
divergent queue-access paths (`GetQueueAsync` reconstructs; `GetStatusAsync`/`RetestAsync`/
`FinalizeRunAsync`/`Advance`/`Bulk` read `_queues` directly) are unified to route through the
reconstruct-aware `GetQueueAsync`, which collapses the B-column tools and fixes the `retest`
cross-process latent bug as a consequence.

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: Microsoft.Data.Sqlite, System.Text.Json (both already in use; no new deps)  
**Storage**: SQLite at `.execution/spectra.db` — additive new table `queue_snapshot` (created via
`CREATE TABLE IF NOT EXISTS`, same pattern as `runs`/`test_results`)  
**Testing**: xUnit (`Spectra.MCP.Tests`)  
**Target Platform**: cross-platform (Windows/macOS/Linux) — CLI + MCP server over .NET 8  
**Project Type**: single solution, multi-project (`Spectra.Core` domain, `Spectra.MCP` execution/storage, test projects)  
**Performance Goals**: no regression; reconstruction is O(n) over a run's tests with one extra indexed
read (`queue_snapshot` by `run_id`) on a cold rebuild only — the warm path (queue already in `_queues`) is unchanged  
**Constraints**: MUST NOT modify `Spectra.Core` tests or `TestPersistenceService` tests; existing
`Spectra.MCP` execution-engine tests proving long-lived behaviour MUST stay green; no new CLI surface,
no engine-contract change, no concurrency/WAL hardening (all deferred to the consolidation spec)  
**Scale/Scope**: tens–hundreds of tests per run; single-user sequential execution loop today

No `NEEDS CLARIFICATION` remain — the two material decisions (snapshot vs. index re-read; throw vs.
typed-result for fail-loud) were resolved in `/speckit.clarify` and recorded in the spec.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Assessment | Verdict |
|---|---|---|
| I. GitHub as Source of Truth | The snapshot stores **runtime execution state** (a point-in-time capture for one run) in `.execution/spectra.db`, alongside existing `test_results` — not a test definition. Test definitions remain authoritative in Git. The snapshot deliberately decouples a run's orchestration from the mutable index, which is the whole point. | PASS |
| II. Deterministic Execution | Directly **strengthens** "same inputs MUST produce the same execution queue": reconstruction becomes deterministic and identical to the original queue, and an unreconstructable state fails loud instead of silently degrading. The engine remains the authoritative state machine. | PASS |
| III. Orchestrator-Agnostic | No change to tool response shape beyond one additional, self-contained error code for the fail-loud case. Tools stay minimal and self-contained. | PASS |
| IV. CLI-First | No CLI surface added or changed here (explicitly out of scope; that is the follow-on spec). No regression to existing commands. | PASS |
| V. Simplicity (YAGNI) | One new table + one repository (mirrors the existing `runs`/`RunRepository`, `test_results`/`ResultRepository` pattern — not a new abstraction) + one exception type. The lossy `AddFromResult` primitive is **removed** rather than kept as a shim — killing the footgun this spec exists to fix. The rejected alternative (re-read index) is documented. | PASS |

**Result: GATE PASSED.** No violations → Complexity Tracking table omitted.

## Project Structure

### Documentation (this feature)

```text
specs/064-lossless-queue-reconstruction/
├── plan.md              # This file
├── spec.md              # Feature spec (with Clarifications)
├── research.md          # Phase 0 — decisions & rationale
├── data-model.md        # Phase 1 — entities, schema, validation
├── quickstart.md        # Phase 1 — how to verify the fix
├── contracts/
│   └── reconstruction.md # Phase 1 — schema + reconstruction behavioural contract
└── checklists/
    └── requirements.md   # From /speckit.specify
```

### Source Code (repository root)

```text
src/
  Spectra.Core/
    Models/Execution/
      QueueSnapshotEntry.cs        # NEW — per-test orchestration snapshot record
      QueuedTest.cs                # (unchanged) reconstruction target
      TestIndexEntry.cs            # (unchanged) source of build-time orchestration
  Spectra.MCP/
    Storage/
      ExecutionDb.cs               # MODIFY — add queue_snapshot table to schema init
      QueueSnapshotRepository.cs   # NEW — CreateManyAsync (txn) + GetByRunIdAsync (ordered)
    Execution/
      ExecutionEngine.cs           # MODIFY — persist snapshot at StartRun (FR-007);
                                   #          snapshot-driven + fail-loud ReconstructQueue;
                                   #          route Status/Advance/Bulk/Retest/Finalize via GetQueueAsync
      TestQueue.cs                 # MODIFY — remove lossy AddFromResult; add lossless reconstruction builder
      QueueReconstructionException.cs # NEW — distinct typed fail-loud error
    Tools/                          # MODIFY — surface QueueReconstructionException as a distinct
                                   #          error code (NOT "run not found")
    Program.cs                     # MODIFY — construct QueueSnapshotRepository; pass to engine

tests/
  Spectra.MCP.Tests/
    Storage/
      QueueSnapshotRepositoryTests.cs     # NEW
    Execution/
      QueueReconstructionParityTests.cs   # NEW — long-lived == short-lived parity, fail-loud
      TestQueueReconstructionTests.cs      # MODIFY — migrate 8 tests off removed AddFromResult
    Integration/
      ReconstructedExecutionFlowTests.cs   # NEW — advance/skip/retest/finalize across a fresh engine
```

**Structure Decision**: Single existing solution; all changes land in `Spectra.Core` (one new model)
and `Spectra.MCP` (storage + execution + tool error surfacing + DI). This matches the investigation's
"smallest type set" and keeps the engine contract intact. No project added, no dependency added.

## Phase 0 — Research

See [research.md](./research.md). All decisions resolved; no open unknowns.

## Phase 1 — Design & Contracts

See [data-model.md](./data-model.md), [contracts/reconstruction.md](./contracts/reconstruction.md),
and [quickstart.md](./quickstart.md).

## Complexity Tracking

No constitution violations — table intentionally omitted.
