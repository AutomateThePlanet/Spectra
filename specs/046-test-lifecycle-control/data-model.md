# Data Model: Test Lifecycle & Process Control

**Phase**: 1 (Design & Contracts)
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

This document describes the entities, file formats, and state transitions introduced (or modified) by Spec 040. Existing entities (test case files, per-suite `_index.json`, `spectra.config.json`) are referenced where they are touched but not redefined.

---

## 1. High-water-mark record (`.spectra/id-allocator.json`)

**Purpose**: Persistent, monotonic record of the largest test-ID number that has ever been allocated in this workspace.

**Location**: `<workspace-root>/.spectra/id-allocator.json`

**Lifecycle**: Created on first allocation in a fresh workspace (or on first run after upgrade). Read at the start of every allocation. Written atomically (temp + rename) at the end of every allocation. Gitignored.

**Format**:

```json
{
  "version": 1,
  "high_water_mark": 247,
  "last_allocated_at": "2026-04-30T14:30:00Z",
  "last_allocated_command": "ai generate"
}
```

**Field reference**:

| Field | Type | Required | Notes |
|---|---|---|---|
| `version` | int | yes | Schema version. `1` today. Future bumps are non-breaking forward; readers treat unknown versions as "absent" (re-seed from filesystem). |
| `high_water_mark` | int | yes | Largest ID number ever issued (numeric suffix only — e.g., `247` for `TC-247`). Monotonic. |
| `last_allocated_at` | string (ISO 8601 UTC) | yes | Timestamp of the most recent allocation. Diagnostic only. |
| `last_allocated_command` | string | yes | Name of the command that performed the most recent allocation (`ai generate`, `ai update`, `doctor ids --fix`, etc.). Diagnostic only. |

**Invariants**:

- `high_water_mark` never decreases.
- After any allocation of `count` IDs, `new_high_water_mark = max(old_high_water_mark, indexMax, filesystemMax, idStart - 1) + count`.

**Corruption handling**: If parse fails or `version` is unrecognized, treat as absent and re-seed from the union of (per-suite `_index.json` scan, filesystem frontmatter scan, `id_start - 1`). Log a warning. Do not throw.

---

## 2. Allocator lock (`.spectra/id-allocator.lock`)

**Purpose**: Cross-process mutex that serializes ID allocations.

**Location**: `<workspace-root>/.spectra/id-allocator.lock`

**Lifecycle**: Created on first lock acquisition. Held open with `FileShare.None` for the duration of the critical section. The OS releases the handle on process exit (including crash). Gitignored. Empty file (0 bytes) — its existence and exclusive handle are the lock; contents are irrelevant.

**Acquisition**: `new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)`. On `IOException` (lock held by another process), retry with exponential backoff (start 50 ms, double up to 1 s) for up to 10 s total. After 10 s, throw `TimeoutException` with a message naming the lock path.

---

## 3. PID record (`.spectra/.pid`)

**Purpose**: Announce that a long-running command is in flight in this workspace, so a peer `spectra cancel` invocation knows whom to signal.

**Location**: `<workspace-root>/.spectra/.pid`

**Lifecycle**: Written by `CancellationManager.RegisterCommandAsync()` at command start. Deleted by `UnregisterCommandAsync()` in the `finally` block of the command. Also deleted by `CancelHandler` after a forceful kill, and by the next `CancelHandler` invocation if found stale. Gitignored.

**Format**:

```json
{
  "pid": 12345,
  "command": "ai generate",
  "started_at": "2026-04-30T14:23:11Z",
  "process_name": "spectra"
}
```

**Field reference**:

| Field | Type | Required | Notes |
|---|---|---|---|
| `pid` | int | yes | OS process ID of the running SPECTRA command. |
| `command` | string | yes | Name of the in-flight command (`ai generate`, `ai analyze`, etc.). |
| `started_at` | string (ISO 8601 UTC) | yes | Command start time. |
| `process_name` | string | yes | Process executable name (`spectra` in production, `dotnet` in dev mode). Used by stale-PID detection. |

**Stale detection**: A PID record is stale if `Process.GetProcessById(pid)` throws `ArgumentException`, OR the live process's `ProcessName` is not in the allowed set (`spectra`, `dotnet`, `Spectra.CLI`).

---

## 4. Cancel sentinel (`.spectra/.cancel`)

**Purpose**: Cross-process signal from `spectra cancel` (the peer process) to the running command, requesting cooperative shutdown.

**Location**: `<workspace-root>/.spectra/.cancel`

**Lifecycle**: Created by `CancelHandler` when a cancel request arrives. Deleted by `CancellationManager` on the running command's way out (whether the command completes normally, is cancelled cooperatively, or is force-killed and `CancelHandler` cleans up afterward). Also deleted by `RegisterCommandAsync()` at command start (defensive — clears any leftover sentinel from a previous crashed run before the new command begins). Gitignored.

**Format**: Empty file. Existence is the signal; contents are ignored.

---

## 5. Lifecycle result models (Spectra.Core/Models/Lifecycle/)

All inherit from `Spectra.CLI.Results.CommandResult` and follow the existing `[JsonPropertyName]` conventions.

### 5a. `DeleteResult`

```jsonc
{
  "command": "delete",
  "status": "completed",      // or "failed"
  "timestamp": "2026-04-30T14:30:00Z",
  "deleted": [
    {
      "id": "TC-142",
      "suite": "checkout",
      "file": "test-cases/checkout/TC-142.md",
      "title": "Checkout with expired card",
      "automated_by": ["tests/e2e/CheckoutTests.cs"],
      "stranded_automation": ["tests/e2e/CheckoutTests.cs"]
    }
  ],
  "dependency_cleanup": [
    { "test_id": "TC-150", "removed_dep": "TC-142" }
  ],
  "skipped": [
    { "id": "TC-999", "reason": "TEST_NOT_FOUND" }
  ],
  "errors": [],
  "dry_run": false
}
```

### 5b. `SuiteListResult`

```jsonc
{
  "command": "suite list",
  "status": "completed",
  "suites": [
    { "name": "checkout", "test_count": 42, "directory": "test-cases/checkout" },
    { "name": "auth",     "test_count": 18, "directory": "test-cases/auth" }
  ]
}
```

### 5c. `SuiteRenameResult`

```jsonc
{
  "command": "suite rename",
  "status": "completed",
  "old_name": "checkout",
  "new_name": "payments",
  "directory_renamed": true,
  "index_updated": true,
  "selections_updated": ["smoke", "regression"],
  "config_block_renamed": true,
  "dry_run": false
}
```

### 5d. `SuiteDeleteResult`

```jsonc
{
  "command": "suite delete",
  "status": "completed",
  "suite": "checkout",
  "tests_removed": 42,
  "stranded_automation_count": 12,
  "external_dependency_cleanup": [
    { "test_id": "TC-300", "suite": "orders", "removed_deps": ["TC-142", "TC-143"] }
  ],
  "selections_updated": ["smoke"],
  "config_block_removed": true,
  "dry_run": false
}
```

### 5e. `CancelResult`

```jsonc
{
  "command": "cancel",
  "status": "completed",     // or "no_active_run"
  "timestamp": "2026-04-30T14:30:00Z",
  "target_pid": 12345,        // null when no_active_run
  "target_command": "ai generate",
  "shutdown_path": "cooperative",  // "cooperative" | "forced" | "none"
  "elapsed_seconds": 1.4,
  "force": false
}
```

### 5f. `DoctorIdsResult`

```jsonc
{
  "command": "doctor ids",
  "status": "completed",
  "total_tests": 247,
  "unique_ids": 245,
  "duplicates": [
    {
      "id": "TC-142",
      "occurrences": [
        { "file": "test-cases/checkout/TC-142.md", "title": "...", "mtime": "2026-03-15T..." },
        { "file": "test-cases/auth/TC-142.md",     "title": "...", "mtime": "2026-04-20T..." }
      ]
    }
  ],
  "index_mismatches": [
    { "suite": "checkout", "id": "TC-200", "in_index": false, "on_disk": true }
  ],
  "high_water_mark": 247,
  "next_id": "TC-248",
  "fix_applied": false,
  "renumbered": [],
  "unfixable_references": []
}
```

When `--fix` is applied, `fix_applied` becomes `true`, `renumbered` lists `{ from, to, file }`, and `unfixable_references` lists `{ file, reference, reason }`.

### 5g. Cancelled-command result (extension to existing per-command results)

When any of the six long-running commands is cancelled, the existing result type for that command (e.g., `GenerateResult`) is emitted with these conventions:

- `status: "cancelled"`
- New common fields appended (added to the relevant result class):
  - `cancelled_at` (ISO 8601)
  - `phase` (the in-flight phase name when cancellation hit)
  - `phase_progress` (`{ "current": int, "total": int }`)
  - `tests_written` / `docs_processed` / etc. — count of artifacts that survived (named per command, e.g., `tests_written` for generate, `docs_extracted` for criteria)
  - `files` — list of paths actually written
  - `message` — human-readable summary (e.g., `"Generation cancelled by user. 7 of 15 tests written."`)

---

## 6. Modified entity: per-suite `_index.json`

**No schema change.** Behavior changes:

- `delete` and `suite delete` rewrite the file atomically (temp + rename), updating `test_count` and `generated_at`.
- The cascade dependency cleanup may rewrite `_index.json` in *other* suites if a dependent test's metadata is index-cached (today the index does not cache `depends_on`; if that changes in future, this contract holds).

---

## 7. Modified entity: `spectra.config.json`

**No schema change.** The `selections` map and any `suites.<name>` config block become rewrite targets for `suite rename` and `suite delete`. Writes are atomic (temp + rename) and rollback-safe (in-memory snapshot taken before the first rewrite).

---

## 8. Modified entity: progress page (`.spectra-progress.html`)

**Existing**: `ProgressPageWriter.WriteProgressPage(htmlPath, jsonData, isTerminal, title?)` writes a self-contained HTML with embedded JSON.

**Change**:
- New phase value `"Cancelled"` is recognized by the page's embedded JS as a terminal phase (alongside `"Completed"`).
- When terminal, auto-refresh stops.
- When `phase == "Cancelled"`, the phase strip renders the canceled phase with a striped/yellow style and shows `phase_progress` and the survivors count.

No new artifact — same file, extended state machine.

---

## 9. State transitions

### 9a. Cancellation state machine (per long-running command)

```text
[Not Registered]
      │
      │ RegisterCommandAsync()  — write .pid, clear .cancel, init linked CTS
      ▼
[Running] ───────────────────────────────────────────────────────┐
   │                                                              │
   │ batch boundary reached + sentinel detected OR token signaled │
   ▼                                                              │
[Cancelling] ── write .spectra-result.json (status=cancelled)     │
   │            ── update .spectra-progress.html (phase=Cancelled)│
   │                                                              │
   │ UnregisterCommandAsync() — delete .pid, delete .cancel       │
   ▼                                                              │
[Done]                                                            │
                                                                  │
   command finishes normally ─────────────────────────────────────┘
                              ── write .spectra-result.json (status=completed)
                              ── update .spectra-progress.html (phase=Completed)
                              ── UnregisterCommandAsync()
                              ▼
                          [Done]
```

### 9b. Cancel-peer state machine (the `spectra cancel` process)

```text
[Start]
   │
   │ read .spectra/.pid
   ▼
[Have PID?] ── no ──> exit { status: "no_active_run" }
   │ yes
   ▼
[Validate live + named "spectra"]
   │     │
   │     stale ──> delete .pid, exit { status: "no_active_run" }
   │ live and valid
   ▼
[Write .spectra/.cancel]
   │
   │ --force? ── yes ──> Process.Kill(entireProcessTree: true)
   │ no
   ▼
[Poll .pid every 200 ms for ≤ 5 s]
   │
   ├ pid file gone OR process exited ──> exit { status: "completed", shutdown_path: "cooperative" }
   │
   │ still alive after 5 s
   ▼
[Process.Kill(entireProcessTree: true)]
   │
   │ wait 2 s
   ▼
[Process gone?] ── yes ──> delete .pid, delete .cancel, exit { status: "completed", shutdown_path: "forced" }
   │
   │ no (extreme — uncommon)
   ▼
[Log: kill did not take], exit code 1
```

### 9c. ID allocation flow (single allocation)

```text
AllocateAsync(count, prefix, idStart)
   │
   │ acquire FileLockHandle on .spectra/id-allocator.lock (≤ 10 s)
   ▼
read HWM (or seed from indexMax + filesystemMax + idStart-1)
   │
   ▼
effective = max(HWM, indexMax, filesystemMax, idStart-1)
ids = [effective+1 .. effective+count]
   │
   ▼
write HWM = effective + count (atomic temp+rename)
   │
   ▼
release lock, return ids
```

---

## 10. Validation rules (informational, derived from FRs)

These are properties the implementation must preserve; they are not new schema fields:

| Rule | Source | Where enforced |
|---|---|---|
| Every newly-allocated ID is unique across all suites | FR-001 | `PersistentTestIdAllocator.AllocateAsync` |
| HWM monotonic, never decreases | FR-003 | `HighWaterMarkStore.WriteAsync` (asserts new ≥ old) |
| `id_start` floor honored on first allocation | FR-005 | `AllocateAsync` (max() includes `idStart - 1`) |
| Delete refuses with `automated_by` non-empty unless `--force` | FR-024 | `DeleteHandler.PreflightAsync` |
| Delete strips deleted ID from every dependent's `depends_on` | FR-025 | `DeleteHandler.ExecuteAsync` cascade pass |
| Suite rename leaves test IDs unchanged | FR-037 | `SuiteRenameHandler` does not touch test files |
| Suite name matches `^[a-z0-9][a-z0-9_-]*$` | FR-035 | `SuiteRenameHandler.ValidateName` |
| Cancel grace ≤ 5 s | FR-013 | `CancelHandler` poll loop |
| `cancelled` result preserves all on-disk artifacts | FR-012, FR-017 | Each handler's `OperationCanceledException` catch block |
| Stale PID detected and cleaned | FR-015 | `CancelHandler.ValidatePid` |
