# Contract: Console HTTP Endpoints + CLI surface

The console exposes a tiny localhost HTTP API consumed only by its own page, plus two CLI verbs. Every
HTTP write-back maps 1:1 to an existing `ExecutionEngine` operation and replicates `RunHandler`
guardrails verbatim (FR-005). All responses are JSON unless noted. Bind: `http://127.0.0.1:{port}/`.

> **Parity contract**: for identical actions, the DB state after a console write-back MUST equal the
> state after the equivalent `RunHandler` / `ExecutionEngine`-direct call (status, notes, handle,
> attempt, blocking cascade, ordering). This is asserted by `ConsoleParityTests` against the
> `ParityTests` oracle.

---

## CLI surface

### `spectra run console [--port N]`
Launches the detached worker (research R2), prints the URL, writes `.execution/console.json`, returns
exit 0. If a live console already exists (valid marker), prints its URL and exits non-zero with
`CONSOLE_ALREADY_RUNNING` (FR-012) rather than double-binding.

### `spectra run console --stop`
Reads the marker, terminates the worker pid, deletes the marker, exit 0. If no live console (missing or
stale marker), prints "no running console" and exits 0 cleanly (FR-012).

### `spectra run console --serve --port N`  *(internal)*
The worker entry point (not for direct user use): runs the `HttpListener` loop in the foreground of the
detached process. Invoked by the launcher.

**Exit codes** reuse `ExitCodes` (Success/MissingArguments/ValidationError/NotFound/Error) consistent with
`RunHandler`.

---

## HTTP endpoints

### `GET /`  → `200 text/html`
The interactive page (borrowed `:root` tokens, inline JS). No state in the markup beyond the initial
projection; the page polls `/current` for liveness.

### `GET /current`  → `200 application/json`
The full page projection — the single read the page polls and re-fetches after every write.
```jsonc
{
  "runId": "run_…", "suite": "checkout", "runStatus": "active",
  "progress": { "total": 12, "completed": 5, "passed": 4, "failed": 1, "blocked": 0, "skipped": 0 },
  "current": {                               // null when no actionable test (run done / none active)
    "testHandle": "…", "testId": "TC-007", "title": "…", "priority": "high",
    "component": "…", "preconditions": "…", "steps": "…", "expectedResult": "…",
    "status": "InProgress", "notes": "…", "screenshotPaths": ["run_…/attachments/…webp"]
  },
  "results": [ { "testId": "TC-001", "status": "PASSED", "notes": "…" } /* … for the list/filters */ ]
}
```
- Source: `GetStatusAsync` + `GetQueueAsync` + `GetStatusCountsAsync` + test-case loader (`RunServices`).
- **No active run** → `{ "runStatus": "none", "current": null }` with a clear message (edge case).

### `POST /advance`  → `200` | `400`
Record a verdict and advance. Body: `{ "status": "pass|fail|blocked|skip", "notes": "…" }`.
**Guardrails (replicate `RunHandler.AdvanceAsync:204-211`)**:
- Missing/blank `status` → `400 { "error_code": "STATUS_REQUIRED" }` — records nothing.
- `status` not in enum → `400 { "error_code": "INVALID_STATUS" }`.
- `fail`/`blocked`/`skip` with blank `notes` → `400 { "error_code": "NOTES_REQUIRED" }` — records nothing.
- Valid → engine `AdvanceTestAsync(runId, handle, verdict, notes)`; response = refreshed `/current`
  projection plus `{ "recorded": {testId,status}, "blocked": [..ids..], "next": {…}|null }`.

The handle is resolved server-side from the in-progress/next test (as `RunHandler.ResolveHandleAsync`
does); the page never invents a handle. A pending target is auto-started (`StartTestAsync`) exactly as the
CLI path does.

### `POST /note`  → `200` | `400`
Body: `{ "note": "…" }`. Blank → `400 { "error_code": "NOTE_REQUIRED" }`. Valid → `AddNoteAsync`; status
unchanged. Response = refreshed projection.

### `POST /screenshot`  → `200` | `400`
Multipart form-data (`file`) **or** JSON `{ "dataUrl": "data:image/png;base64,…" }`. Server converts to
`byte[]` → `ScreenshotService.EncodeAndSaveAsync(reportsPath, runId, testId, existingCount, bytes)` →
`ResultRepository.AppendScreenshotPathAsync(handle, relativePath)`. Response includes the new
`screenshotPaths`. Decode/empty failure → `400 { "error_code": "SCREENSHOT_INVALID" }`; nothing attached
(edge case). **No authoritative copy is kept in the browser** (FR-006).

### `POST /finalize`  → `200` | `400`
Body: `{ "force": bool }`. → `FinalizeRunAsync`; response carries the report path. Used when the queue is
exhausted (the page surfaces a "finalize" affordance once `current` is null).

### (Optional, design-time) `POST /pause` · `/resume` · `/cancel`
Thin wrappers over `PauseRunAsync`/`ResumeRunAsync`/`CancelRunAsync` if the page exposes run controls;
same parity contract. Not required for the P1 MVP.

---

## Error envelope

All `4xx` responses share:
```json
{ "error_code": "STATUS_REQUIRED|INVALID_STATUS|NOTES_REQUIRED|NOTE_REQUIRED|SCREENSHOT_INVALID|NO_TEST|RUN_NOT_FOUND|RECONSTRUCTION_FAILED", "message": "human-readable" }
```
`RECONSTRUCTION_FAILED` surfaces a `QueueReconstructionException` distinctly from a benign
`RUN_NOT_FOUND`, mirroring the CLI/MCP central handling (spec 064). The error codes for verdict/notes
match the CLI exactly so `ConsoleGuardrailTests` can re-assert `GuardrailTests` at the HTTP boundary.

---

## Invariants the contract enforces (mapped to FRs)

- **FR-003 / R4**: every endpoint mutates SQLite through the engine; `/current` is a pure projection; no
  endpoint reads or writes browser storage.
- **FR-004 / FR-013**: write-backs leave DB state indistinguishable from `RunHandler` — the parity test.
- **FR-005**: `STATUS_REQUIRED` + `NOTES_REQUIRED` enforced server-side; the console never infers a verdict.
- **FR-006**: screenshot ingest reuses the existing service; no new storage model.
- **FR-002**: served by the console's own `HttpListener`, never the stdio MCP host.
