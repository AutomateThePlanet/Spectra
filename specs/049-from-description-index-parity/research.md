# Phase 0 Research: From-Description Write & Index Parity

**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This phase has **no unresolved NEEDS CLARIFICATION items** — the spec was unambiguous on scope and acceptance. The decisions below capture design choices that are not externally constrained but should be settled before tasks are generated.

---

## Decision 1 — Where `TestPersistenceService` lives

**Decision**: `src/Spectra.CLI/IO/TestPersistenceService.cs` (CLI project, IO namespace).

**Rationale**:
- The service composes two existing primitives: `Spectra.CLI.IO.TestFileWriter` and `Spectra.Core.Index.IndexWriter`/`IndexGenerator`. Composition belongs in the CLI layer because the orchestration concern ("when you write a test, also update the suite index") is a CLI-level invariant, not a core domain primitive — `Spectra.Core` legitimately exposes both primitives independently for tools that only need one.
- Co-locating with `TestFileWriter` makes the central write path discoverable: a contributor opening `src/Spectra.CLI/IO/` sees both `TestFileWriter` (low-level format) and `TestPersistenceService` (orchestrated write+index) and naturally calls the latter.
- Avoids inflating `Spectra.Core` with a service it would not consume.

**Alternatives considered**:
- *Place in `Spectra.Core/Index/`*: Rejected. Would force `Spectra.Core` to take a dependency on `Spectra.CLI.IO.TestFileWriter`, which is the wrong direction.
- *Move `TestFileWriter` into `Spectra.Core` first*: Rejected as scope creep. The two-line file write is fine where it is; this spec does not need to relocate it.
- *New `Spectra.CLI/Persistence/` directory*: Rejected. We have a perfectly good `IO/` directory for exactly this kind of file. YAGNI on a new namespace.

---

## Decision 2 — Service shape: instance class vs. static helper

**Decision**: Instance class with three private dependencies (`TestFileWriter`, `IndexGenerator`, `IndexWriter`) injected via constructor.

**Rationale**:
- Existing code in `GenerateHandler` and `IndexHandler` constructs `TestFileWriter`, `IndexGenerator`, and `IndexWriter` directly (no DI container is used for these). The new service follows the same convention: constructed at the handler level, dependencies passed in.
- Instance shape lets the service be a single test seam: a `TestPersistenceServiceTests` fixture can construct it once per test and assert against both filesystem and parsed index.
- Three dependencies is fine — they are all stateless primitives. Constructor signature stays self-documenting.

**Alternatives considered**:
- *Static class with method `TestPersistenceService.PersistAsync(...)`*: Rejected. Statics complicate the "single test seam" property and resist substitution in unit tests, and there's no perf or simplicity gain here.
- *Make it implement an interface for DI*: Rejected as premature. There is exactly one implementation. If a second emerges (e.g. a dry-run wrapper), introduce the interface then.

---

## Decision 3 — Persist signature and "full set" parameter

**Decision**:

```csharp
public Task PersistAsync(
    string testsPath,
    string suite,
    IReadOnlyList<TestCase> testsToWrite,
    IReadOnlyList<TestCase> allTestsForIndex,
    CancellationToken ct);
```

The caller is responsible for providing `allTestsForIndex` = existing on-disk tests + new tests (deduplicated by id). The service does not load existing tests itself.

**Rationale**:
- Matches the batch path's existing shape (it already maintains `existingTests + allWrittenTests` for the per-batch index update). Refactor is mechanical.
- Keeps the service free of filesystem-discovery responsibility — it does not parse `.md` files; that is `TestCaseParser`'s job. Composition is clean.
- Lets callers choose how to obtain the existing set: the batch path already loaded it; the from-description path will load via the existing `GenerateHandler.LoadExistingTestsAsync` private helper (line 1948).
- The index generator and writer already handle "regenerate full index from full set" — this is the operation we want.

**Alternatives considered**:
- *Pass only `testsToWrite` and have the service load existing-on-disk tests itself*: Rejected. Adds an internal `TestCaseParser` dependency, duplicates the loading the batch path already does, and gives the service two unrelated responsibilities (write + discover).
- *Use `IndexGenerator.Update(existing, newTests)` to incrementally update*: Rejected. Goes against the existing batch convention (which regenerates from full set per batch). Mixing two strategies is the kind of inconsistency that produced this bug in the first place.

---

## Decision 4 — Write ordering and failure surfacing (FR-011)

**Decision**: Write test files first, then regenerate-and-write the index. If any step throws, the exception propagates to the caller; the service does not catch.

**Rationale**:
- The spec (FR-011) requires failures to be surfaced, not swallowed. Letting exceptions propagate is the simplest correct implementation.
- Test-file-then-index ordering means a mid-operation crash leaves a test file with no index entry — which is the same observable state as a pre-spec-049 from-description test, and is *recoverable* via `spectra index --rebuild`. The reverse ordering would risk an index entry pointing at a nonexistent file, which is the more dangerous corruption.
- The system already tolerates "file on disk without index entry" — `IndexHandler` parses and indexes it on next run. So the failure mode is benign and self-healing via the existing rebuild command.

**Alternatives considered**:
- *Two-phase commit / write-to-temp-then-rename*: Rejected as YAGNI. The index file is already overwritten atomically-ish by `File.WriteAllTextAsync` on the platforms we target, and full-suite regeneration means the index is consistent in itself even if individual file writes failed mid-batch.
- *Catch and log, return a failure result*: Rejected. CLI handlers already have failure-result plumbing at their boundaries; the service should not pre-empt them.

---

## Decision 5 — Index rebuild verification scope

**Decision**: `IndexHandler.ExecuteAsync` already parses `.md` files of record and regenerates each suite's index from them (verified by reading `src/Spectra.CLI/Commands/Index/IndexHandler.cs:84–155`). The `--rebuild` path simply bypasses the timestamp short-circuit (`if (!rebuild && writer.Exists(indexPath))` at line 97). No code change is required to `IndexHandler`; only a regression test asserting the unindexed-recovery scenario.

**Rationale**:
- The existing code is already correct for the spec's requirement: it scans `suitePath` for `*.md` excluding `_*` (line 99–101), parses each via `TestCaseParser`, and writes the regenerated index.
- Failed parses already increment an error counter and continue (lines 202–211 sequential, 242–250 parallel) — satisfying FR-007.
- Adding a regression test guards against future regressions in this load-bearing path.

**Alternatives considered**:
- *Refactor `IndexHandler` to share code with `TestPersistenceService`*: Rejected as scope creep. The rebuild's responsibility (read everything, regenerate everything) differs from the persistence service's (write new + regenerate one suite). Sharing would force one to grow toward the other's needs.

---

## Decision 6 — Suite-existing-tests loader for the from-description path

**Decision**: Reuse the existing private `GenerateHandler.LoadExistingTestsAsync(suitePath, testsPath, ct)` helper (line 1948). No new public loader is introduced.

**Rationale**:
- Already used at two call sites in the same handler (lines 388 and 1200). Adding a third call site at the from-description path is the smallest possible change.
- The helper already filters `_*` prefixed files, handles parse errors, and returns the parsed `TestCase` set the persistence service needs.
- Extracting it to its own class is not required by this spec; if a future spec needs it cross-handler, it can be promoted then.

**Alternatives considered**:
- *Extract a `SuiteTestLoader` class*: Rejected as premature. One handler, three call sites — private helper is fine.
- *Have `TestPersistenceService` load existing tests*: Rejected per Decision 3.

---

## Decision 7 — Test fixture strategy

**Decision**: Use the existing `tests/TestFixtures/` sample data plus in-test `Directory.CreateDirectory` + file writes for ad-hoc scenarios. CLI integration tests invoke the handler via its public API (not the full CLI parser), matching the existing `Spectra.CLI.Tests` convention.

**Rationale**:
- The repo's test conventions are well-established — follow them rather than inventing a new harness.
- `TestPersistenceServiceTests` is a true unit test (no CLI parsing, no AI) and is fast.
- The `FromDescription_*` tests need the handler but can stub the generator out via the existing `UserDescribedGenerator` seam (verify this when implementing — if no seam exists, the simplest fix is a `Func<...>` factory injected into the handler; this is a small scope addition and was already implied by the spec's "ExecuteFromDescriptionAsync" reference).

**Alternatives considered**:
- *Drive tests through `Program.Main` end-to-end*: Rejected as slow and brittle for unit-level guarantees. End-to-end coverage exists at the MCP-integration layer (`FromDescriptionDiscoveryTests`).
- *Snapshot-test the index JSON*: Considered as a Batch-regression mechanism. Reasonable. Will use a structural assertion (entry count + each entry's fields) rather than a string snapshot to avoid brittleness against `GeneratedAt` timestamps.

---

## Decision 8 — Concurrency model

**Decision**: Inherit the existing concurrency posture. The persistent ID allocator (Spec 046) prevents id collisions across concurrent generation runs. Index writes are last-writer-wins; because each invocation regenerates the full index from the full set, two concurrent successful writers converge on a consistent index (no partial-merge state). No new locking introduced.

**Rationale**:
- The system is single-user CLI per workspace in practice. Concurrent generation into the same suite is an edge case already handled by Spec 046's filesystem lock for id allocation.
- A coarser "index write lock" would only protect against the (already-safe) last-write-wins case; the cost (locking discipline, deadlock risk) is not justified.

**Alternatives considered**:
- *Suite-level write lock around `PersistAsync`*: Rejected. Pure cost, no observed concurrency hazard.

---

## Resolved Clarifications

None. The spec contained no `[NEEDS CLARIFICATION]` markers.
