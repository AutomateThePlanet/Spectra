# Quickstart: Verifying Lossless Queue Reconstruction

This is an internal correctness fix — there is no new command to run. "Verification" is the test
suite plus an optional manual cross-process check.

## Build & test

```bash
dotnet build
dotnet test tests/Spectra.MCP.Tests        # all execution/storage tests, incl. the new parity tests
dotnet test                                 # full suite — regression gate must stay green
```

Expected: all green, including the pre-existing long-lived-behaviour tests
(`BlockingCascadeTests`, `RetestFlowTests`, `ExecutionFlowTests`) and the untouched
`Spectra.Core` / `TestPersistenceService` tests.

## What "fixed" looks like

1. **Parity** — a queue rebuilt from `queue_snapshot` + `test_results` in a *fresh* `ExecutionEngine`
   blocks dependents, orders, prioritises, and selects "next" identically to the original in-memory
   queue. (`QueueReconstructionParityTests`)
2. **Cross-process flow** — `advance` / `skip` / `retest` / `finalize` driven through a fresh engine
   (no cached `_queues`) behave like the long-lived path: dependency cascades fire, `retest` no longer
   returns `RUN_NOT_FOUND`, `finalize` honours the pending guard. (`ReconstructedExecutionFlowTests`)
3. **Fail-loud** — a run with recorded results but a missing/incomplete/inconsistent snapshot throws
   `QueueReconstructionException` (surfaced as `RECONSTRUCTION_FAILED`), never a silently degraded
   queue and never `RUN_NOT_FOUND`. A run that genuinely has no results still returns `null`.

## Manual cross-process smoke (optional)

The true short-lived-process path is exercised by the consolidation spec (CLI). Until then, the
parity tests simulate it by constructing a second `ExecutionEngine` over the *same* `.execution/
spectra.db` with an empty `_queues`, forcing the reconstruction path.

## Upgrade note (legacy in-flight runs)

Runs created before this feature have no `queue_snapshot` rows. If such a run is still active when you
upgrade and its in-memory queue is gone, reconstruction will **fail loud** (correct behaviour). The
`.execution/spectra.db` holds local, ephemeral execution state — **finish or `cancel` in-flight runs
before upgrading**, or simply start a fresh run.
