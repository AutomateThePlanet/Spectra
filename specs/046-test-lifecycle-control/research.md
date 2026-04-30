# Research: Test Lifecycle & Process Control

**Phase**: 0 (Outline & Research)
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

The user-supplied spec resolved 8 open questions up front. This document captures the remaining design decisions that surfaced during the codebase walk and locks them down before Phase 1.

---

## Decision 1 — How to add persistence to the ID allocator without breaking existing call sites

**Decision**: Keep `Spectra.Core.Index.TestIdAllocator` as a pure in-memory, synchronous class. Add a new `Spectra.Core.IdAllocation.PersistentTestIdAllocator` that:

- Owns the file lock (`.spectra/id-allocator.lock`) and the high-water-mark file (`.spectra/id-allocator.json`).
- Performs the cross-process serialization, filesystem walk, and high-water-mark write.
- Constructs an internal `TestIdAllocator` seeded from the union of (HWM, index scan, filesystem-frontmatter scan, `id_start - 1`) and delegates the actual ID formatting to it.
- Exposes a single async API: `Task<IReadOnlyList<string>> AllocateAsync(int count, string idPrefix, int idStart, CancellationToken ct)`.

**Rationale**:
- The existing `TestIdAllocator` is a small, well-tested in-memory algorithm. Modifying it in place would force the ~30 sync test-allocation sites to become async, churning test infrastructure for no functional benefit.
- The new persistence + locking concerns are orthogonal to the allocation algorithm itself. Wrapping rather than rewriting matches the existing pattern (`TestIdAllocator.FromExistingTests`/`FromExistingIds` factory methods are kept).
- Single-responsibility: `TestIdAllocator` = "next number, given a known set"; `PersistentTestIdAllocator` = "next number, given the workspace state".

**Alternatives considered**:
- *Modify `TestIdAllocator` in place to be async + persistent*: rejected — touches every test, expensive churn, no upside.
- *Add a second sync class `LockedTestIdAllocator`*: rejected — file locks require holding a `FileStream` open across an `await`, which works cleanly only with async methods. Forcing sync would require sync-over-async lock acquisition, which deadlocks on UI-thread-like contexts and burns thread-pool threads.
- *Store the HWM inside `_index.json` instead of a separate file*: rejected — HWM is workspace-global, not suite-scoped. Spreading it across N suite indexes reintroduces the consistency problem we are solving. A dedicated `.spectra/id-allocator.json` is gitignored and cheaper to write atomically.

---

## Decision 2 — Cross-process file lock implementation

**Decision**: Use `new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)` to acquire an OS-level exclusive lock on `.spectra/id-allocator.lock`. Wrap in a `FileLockHandle : IDisposable` that closes the stream on disposal. Retry-with-backoff on `IOException` for up to 10 seconds; throw `TimeoutException` after.

**Rationale**:
- `FileShare.None` gives an OS-enforced exclusive handle on every platform .NET 8 supports (Windows/macOS/Linux).
- The OS automatically releases the handle if the holding process crashes — no stale lock recovery code needed.
- `IOException` (specifically `ERROR_SHARING_VIOLATION` on Windows, `EWOULDBLOCK` on Unix) is the documented signal that another process holds the file. Retry-with-backoff is the standard idiom.
- 10 s timeout: long enough to absorb a normal generation batch, short enough that a wedged caller surfaces as a clear error instead of hanging the user.

**Alternatives considered**:
- *Named mutex (`Mutex` with `Global\` prefix)*: rejected — works on Windows but `Mutex` semantics on .NET 8/Linux rely on `/tmp/.dotnet/shm/` which is process-uid-scoped, not workspace-scoped. Two users on the same box editing the same workspace would not coordinate.
- *SQLite-based lock*: rejected — overkill, drags a heavyweight dependency for the sole purpose of getting an mutex we already have via the file system.
- *Advisory lock via `FileStream.Lock(0, 0)` over a non-exclusive handle*: rejected — `FileStream.Lock` does not work the same way on Unix as on Windows in .NET 8; using `FileShare.None` at open time is the portable contract.

---

## Decision 3 — High-water-mark file format and corruption handling

**Decision**: Use the format already documented in the user spec:

```json
{
  "version": 1,
  "high_water_mark": 247,
  "last_allocated_at": "2026-04-30T14:30:00Z",
  "last_allocated_command": "ai generate"
}
```

On read:
- Missing file → seed from filesystem + index scan (one-time `[INFO] Initialized ID allocator: high water mark = TC-NNN` log line, FR-009).
- File present, parses OK, `version == 1` → use as-is.
- File present, fails to parse OR `version > 1` → log a warning, treat as if missing (re-seed). Do not throw. **Never silently downgrade**.

Atomic writes via temp + rename (write `.spectra/id-allocator.json.tmp`, `File.Move(tmp, target, overwrite: true)`).

**Rationale**:
- `version: 1` gives us a forward-compatibility hook without committing to a migration system today.
- Treating an unreadable HWM as "absent" is safe because the seed (filesystem + index union) always equals or exceeds it. The "deleted IDs never reused" guarantee is the only thing weakened, and only until the next allocation re-establishes a clean HWM. This is documented behavior per the spec.
- Temp-and-rename is the standard atomic-write idiom; `File.Move(overwrite: true)` is atomic on every supported platform.

**Alternatives considered**:
- *Throw on unreadable HWM*: rejected — turns a recoverable, gitignored, regenerable artifact into a fatal error. Bad UX.
- *Embed HWM in `spectra.config.json`*: rejected — that file is committed to Git, and per FR-010 the HWM must be gitignored (it is per-machine derived state).

---

## Decision 4 — Cancellation: cooperative-only or cooperative-then-force?

**Decision**: Three-layer as the spec describes:

1. **Cooperative token** — `CancellationManager.Token` plumbed into every long-running handler. Handlers call `ThrowIfCancellationRequested()` at every batch boundary (per-test in generation, per-doc in extraction/indexing, per-suite in coverage, per-step in dashboard build).
2. **Sentinel file** — `CancellationManager` polls `.spectra/.cancel` every 200 ms (a single `Task.Run` background poller registered with the same token); presence triggers `Cancel()` on the source.
3. **Hard kill** — only `spectra cancel` (the *peer* CLI process) escalates. After writing the sentinel and waiting up to 5 s, it calls `Process.Kill(entireProcessTree: true)`. After another 2 s, it gives up (logs that the kill didn't take — extremely unlikely on modern OSes).

**Rationale**:
- Cooperative-only would mean a wedged HTTP call to Copilot would never actually stop. The hard-kill fallback exists precisely for cases where the cooperative path is broken.
- The 5 s grace is a deliberate tradeoff: long enough for in-flight HTTP to complete and partial results to be written; short enough that an irritated user gets satisfaction quickly. `--force` skips the grace for users who already know cooperative shutdown won't work.
- Polling the sentinel every 200 ms is cheap (a single `File.Exists` check) and avoids spinning up a `FileSystemWatcher` (which has known cross-platform quirks with rapidly-created-then-deleted files).
- Process-tree kill (.NET 8 `Process.Kill(entireProcessTree: true)`) is portable and handles spawned subprocesses (e.g., `dotnet test`).

**Alternatives considered**:
- *FileSystemWatcher instead of polling*: rejected — overkill for a file that is checked at most a few times per second, and watchers historically misfire on macOS networked filesystems.
- *`Console.CancelKeyPress` exclusively (Ctrl+C)*: rejected — already partially in place; doesn't help SKILL/agent flows where the user is in Copilot Chat, not a terminal.
- *No hard kill at all*: rejected — would leave wedged Copilot SDK calls running forever.

---

## Decision 5 — Where the `CancellationManager` lives architecturally

**Decision**: Singleton in `Spectra.CLI/Cancellation/CancellationManager.cs`. Accessed via `CancellationManager.Instance`. Each handler calls `await CancellationManager.Instance.RegisterCommandAsync(commandName, externalToken)` at start (linking the external token in via `CreateLinkedTokenSource`) and `await CancellationManager.Instance.UnregisterCommandAsync()` in `finally`.

Multiple commands cannot register simultaneously inside one process. Registration throws if there is already an active registration (defensive — should not happen given how `Program.cs` dispatches one command per run).

**Rationale**:
- A SPECTRA process executes exactly one top-level command per invocation. The "current run" is unambiguous.
- Singleton is the minimum surface for the cross-handler-and-cross-process semantics we need; it does not preclude per-call cancellation tokens (the singleton's token is the linked source's token, so `OperationCanceledException` propagation just works).
- Living in `Spectra.CLI` (not `Spectra.Core`) is correct because PID-file-and-sentinel coordination is a CLI-process concern, not a library concern. `Spectra.Core` does not need to know about it.

**Alternatives considered**:
- *Static class instead of singleton*: equivalent for this case; chose `Instance` to keep the option of test-time replacement open.
- *Constructor injection of an `ICancellationManager`*: rejected — adds a DI surface for one consumer pattern that is fundamentally process-global. YAGNI per Constitution V.

---

## Decision 6 — How `CommandResult.Status` evolves to support `cancelled`

**Decision**: Status is already a free-form `string` (`CommandResult.Status` is `required string Status { get; init; }`). Existing in-use values: `success`, `failed`, `completed`, `analyzing`, `analyzed`, `analysis_failed`, `generating`, `verifying`. Add **two new values** documented as part of this feature:

- `cancelled` — operation halted by user request; partial results may be present.
- `no_active_run` — `spectra cancel` invoked when nothing was running.

Document the full enumeration in `contracts/result-json.md` and the CLI reference. No code-level enum is introduced (consistent with current convention; YAGNI).

**Rationale**:
- The current convention is documented strings, not a sealed enum. Introducing an enum now would require touching every existing `Status = "completed"` site for marginal type-safety gain.
- The two new values are additive; no consumer should break.

**Alternatives considered**:
- *Introduce `enum CommandStatus`*: rejected — would refactor 40+ existing call sites with no functional benefit; pure churn.
- *Use `cancelled_by_user` / `cancelled_by_force` distinct values*: rejected — the result payload already carries `cancelled_at`, `phase`, `phase_progress`, and a `force: true|false` flag would be a cleaner expression of that distinction without proliferating status strings.

---

## Decision 7 — Atomic delete + dependency cascade strategy

**Decision**: A single delete of one or more test IDs is implemented as:

1. **Resolve phase** — for each requested ID, locate file via suite indexes (with filesystem fallback). Build a plan: `(file_path, suite, automated_by, dependents)`. If any ID is `TEST_NOT_FOUND` or hits the automation guard, return early without writing anything.
2. **Pre-write check** — re-acquire the suite indexes for the affected suites and the all-suites scan for `depends_on` cleanup. Compute the new state in memory.
3. **Write phase** — rewrite each affected file atomically (temp + rename) in a single sequential pass:
   - The `_index.json` of each affected suite (entry removed, `test_count` decremented, `generated_at` updated).
   - Each dependent test file (its `depends_on` array filtered).
   - Finally, delete the test files.
4. **Result phase** — write `.spectra-result.json` with the deleted-tests, dependency-cleanup, skipped, and errors lists.

**Rationale**:
- Step 1 is read-only; if the resolve fails, nothing on disk is touched. This satisfies the "no partial state" expectation in FR-024 (automation guard) and FR-030 (not found).
- Sequential writes (not a transaction) are acceptable here because: (a) every write is atomic per file (temp + rename), (b) the order — `_index.json` first, then dependents, then file deletion — leaves the workspace in a recoverable state at any interruption point (e.g., a crash after rewriting `_index.json` but before deleting the file leaves an "orphan file" — detectable by `validate` and recoverable by `rebuild_indexes`), (c) Git is the undo per Constitution.
- A multi-test delete uses the same scheme: every affected file is rewritten exactly once, even when multiple deleted tests live in the same suite.

**Alternatives considered**:
- *True two-phase commit with a journal file*: rejected — Git is the undo (Constitution + FR-047). Adding a journal duplicates concerns and adds failure modes.
- *In-memory rollback if any write fails*: rejected — by the time a write fails (disk full, permissions), best-effort rollback is itself liable to fail. Better to surface the partial state in the result artifact and let the user `git status` and decide.

---

## Decision 8 — Suite rename rollback

**Decision**: Rollback is best-effort:

1. Snapshot `spectra.config.json` in memory before starting.
2. Rename directory `test-cases/<old>/` → `test-cases/<new>/` (atomic on every supported platform; same-volume `Directory.Move`).
3. Rewrite the new suite's `_index.json` (`suite` field updated). Atomic.
4. Rewrite `spectra.config.json` (selections + suite-config block). Atomic.

If step 4 fails:
- Revert step 3 (rewrite back to old suite name).
- Revert step 2 (rename directory back).
- Throw with a clear message naming what failed.

If step 3 fails:
- Revert step 2.
- Throw.

If step 2 fails:
- Throw.

**Rationale**:
- Each step is individually atomic via temp+rename. The rollback simply runs the inverse atomic op.
- We never "save and replay" a transaction log — we either complete forward or revert backward.
- If a rollback step *also* fails (extreme edge case: someone deleted the old directory mid-rename), the result artifact reports it and the user resolves via `git`.

**Alternatives considered**:
- *Skip rollback, leave partial state, document via result artifact*: rejected — rename is high-blast-radius (entire suite is in flight). Best-effort rollback is the right default.

---

## Decision 9 — `doctor ids --fix` ordering when there are duplicates

**Decision**: For each duplicate-ID group:

1. Order occurrences by Git history if available (`git log --diff-filter=A --reverse -- <path>` finds the file's add commit). Tie-break by filesystem `mtime` (older wins).
2. The oldest occurrence keeps the duplicated ID.
3. Each later occurrence is reassigned to a fresh ID at `currentHWM + 1`, then `+ 2`, etc.
4. After all reassignments, every other test's `depends_on` references to the renumbered ID are remapped (best-effort: only updates references to a renumbered ID, keyed by old → new).
5. `automated_by` source files are scanned for hardcoded `[TestCase("TC-NNN")]` patterns; updates are made best-effort with a regex that targets `"<oldId>"` literal strings inside C# attributes. Anything more complex (interpolations, indirect references) is reported as unfixable in the result artifact.

**Rationale**:
- Git-history-based "oldest wins" is the most defensible heuristic when multiple files claim the same ID. It minimizes change to externally-visible IDs (the file that has been around longest is more likely the one referenced externally).
- Reassigning at `HWM + 1` (not `existingMax + 1`) preserves the monotonic guarantee.
- Best-effort `automated_by` updates handle the 90% case (a literal `"TC-142"` string) without trying to be a code rewriter.
- The result artifact's "unfixable" list gives the user a clear punch list.

**Alternatives considered**:
- *Only update `depends_on`, never touch automation source*: rejected — leaves the user with a guaranteed-broken state in the common case. Best-effort literal-string update has minimal risk and high value.
- *Renumber all duplicates including the "oldest"*: rejected — pointlessly destabilizes IDs that have been working.

---

## Decision 10 — Where `.spectra/.pid` and `.spectra/.cancel` live; CWD vs workspace

**Decision**: All `.spectra/*` lifecycle files live under `<workspace-root>/.spectra/`. Workspace root is resolved exactly the same way the existing CLI resolves it (walk up from CWD looking for `spectra.config.json`; error if not found).

A `spectra cancel` invocation from any CWD inside the same workspace correctly targets the running process (FR per Edge Cases in spec).

**Rationale**:
- Workspace-relative is the only resolution that behaves correctly when the user runs `spectra cancel` from a subdirectory.
- Reuses the existing resolver — no new code path.

**Alternatives considered**:
- *CWD-relative*: rejected — breaks cross-directory cancellation, a concrete edge case in the spec.
- *Per-user temp dir (`%TEMP%`)*: rejected — multiple workspaces on one machine would collide.

---

## Decision 11 — Stale PID detection

**Decision**: Before any kill action, `CancelHandler` validates the recorded PID:

1. Read `.spectra/.pid` → `{ pid, command, started_at }`.
2. Probe `Process.GetProcessById(pid)`. If `ArgumentException` (no such process) → stale, delete the PID file, exit with `no_active_run`.
3. If process exists, check that `Process.ProcessName` is `spectra` (or `dotnet` in dev — accept the dev case via a configurable allow-list). If it's something else (PID was reused by an unrelated process) → stale, delete, exit `no_active_run`.

**Rationale**:
- Two-step validation (PID exists + is a SPECTRA process) is the standard defense against PID reuse on any modern OS.
- The dev-mode `dotnet` exception is a real operational concern: developers run `dotnet run --project src/Spectra.CLI -- ai generate` and the PID belongs to `dotnet`, not `spectra`. Allow-list keeps tests passing.

**Alternatives considered**:
- *Trust the PID file blindly*: rejected — first PID reuse incident kills the user's IDE.
- *Use process-creation-time fingerprinting*: rejected — overkill for a file that is rewritten on every command.

---

## Decision 12 — `.gitignore` additions

**Decision**: `InitHandler.UpdateGitIgnoreAsync()` (already in place at `src/Spectra.CLI/Commands/Init/InitHandler.cs:715`) gains four entries to its existing `# SPECTRA` block:

```
.spectra/.pid
.spectra/.cancel
.spectra/id-allocator.lock
.spectra/id-allocator.json
```

**Rationale**: All four are workspace-local, regenerable, per-developer derived state. None should be committed. Per spec FR-010, FR-020.

---

## Closed: source-spec items that are NOT NEEDS CLARIFICATION

The following were called out in the source spec as resolved and remain so — included here for traceability:

| Source-spec Q | Resolution (re-confirmed during research) |
|---|---|
| Soft delete vs. hard delete? | Hard delete only. Git is the undo. |
| Suite ops in `spectra-delete` SKILL or own? | Own SKILL (`spectra-suite`). |
| Dedicated `spectra-cancel` SKILL? | No — recipe in each long-running SKILL + workflow in quickstart. |
| BUG ID fix scope? | Cross-suite collisions only. |
| `automated_by` files when test deleted? | Refuse by default; `--force` proceeds; automation source files never touched. |
| `depends_on` references when test deleted? | Auto-remove. |
| Cancelling generation mid-batch — keep partial results? | Yes. |
| Suite rename — update test ID prefixes? | No. IDs are global, not suite-prefixed. |

No additional clarifications surfaced during research.
