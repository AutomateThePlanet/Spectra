---
description: "Task list for Execution Console Infrastructure"
---

# Tasks: Execution Console Infrastructure

**Input**: Design documents from `/specs/066-execution-console-infrastructure/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/console-http.md, quickstart.md

**Tests**: INCLUDED — the spec's Tests section explicitly enumerates net-new coverage
(`ConsoleParityTests`, `ConsoleGuardrailTests`, `ConsoleScreenshotTests`, `ConsoleLifecycleTests`).

**Organization**: Tasks grouped by user story (US1–US4) for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1, US2, US3, US4 (maps to spec.md user stories)

## Path Conventions

Single project (`Spectra.CLI`). Source under `src/Spectra.CLI/Commands/Run/WebConsole/`; tests under
`tests/Spectra.CLI.Tests/Commands/Run/WebConsole/`. **No changes** to `Spectra.Execution`, `Spectra.Core`,
or MCP projects — the console reuses `ExecutionEngine`, `RunServices`, `RunHandler`, `ScreenshotService`,
and `ReportWriter` styling verbatim.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folders and the command stub everything hangs off.

- [X] T001 Create source folder `src/Spectra.CLI/Commands/Run/WebConsole/` and test folder `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/` (empty placeholders, ready for the transport files)
- [X] T002 Add `BuildConsole()` to `src/Spectra.CLI/Commands/Run/RunCommand.cs` and `AddCommand(BuildConsole())` — a `console` subcommand with options `--port <int>`, `--stop`, and the internal `--serve` flag, dispatching to a new `ConsoleCommand` (handlers may be stubs at this point)
- [X] T003 [P] Confirm `.execution/console.json` is covered by the existing `.execution/` `.gitignore` entry (FR-011); add an explicit clarifying comment in `.gitignore` noting the console marker + ephemeral page are local-only

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The HTTP host + read-only projection + page skeleton that ALL user stories sit on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Implement `ConsoleServer` in `src/Spectra.CLI/Commands/Run/WebConsole/ConsoleServer.cs` — a `System.Net.HttpListener` host bound to `http://127.0.0.1:{port}/` (research R1) that constructs **one long-lived** `RunServices` (`RunServices.cs:39`), owns a route table (method + path → handler), dispatches requests to `ConsoleEndpoints`, serializes JSON responses, and supports graceful start/stop. Bind default port `7878` with fallback to a free ephemeral port; expose the bound port.
- [X] T005 Implement `ConsoleEndpoints.GetCurrentAsync` (`GET /current`) in `src/Spectra.CLI/Commands/Run/WebConsole/ConsoleEndpoints.cs` — project run state from `Engine.GetActiveRunAsync`/`GetStatusAsync` + `GetQueueAsync` + `GetStatusCountsAsync` + the `RunServices` test-case loader into the contract JSON shape (`contracts/console-http.md`), including the `{ "runStatus": "none", "current": null }` no-active-run shape (edge case). Surface `QueueReconstructionException` as `RECONSTRUCTION_FAILED`, distinct from `RUN_NOT_FOUND` (spec 064).
- [X] T006 Implement `ConsolePage.Render` in `src/Spectra.CLI/Commands/Run/WebConsole/ConsolePage.cs` — an interactive HTML page that borrows `ReportWriter.cs:413-431` `:root` tokens (navy `#1B2A4A`/`#2D3F5E`, per-status palette, card CSS at `:485-509`) into an inline `<style>` (research R6), renders the current test read-only (optionally reusing `ReportWriter.RenderTestContent`), and includes inline JS that polls `GET /current` on a ~1.5–2 s interval and re-renders (research R5). No client-side run state.
- [X] T007 Implement the `spectra run console --serve --port N` foreground worker path in `src/Spectra.CLI/Commands/Run/WebConsole/ConsoleCommand.cs` — run the `ConsoleServer` loop in-process until cancelled. This is the entry point the detached launcher (US4) invokes and the one tests drive directly.

**Checkpoint**: A foreground console serves a live, read-only, self-refreshing page over the engine.

---

## Phase 3: User Story 1 - Record verdicts through the console (Priority: P1) 🎯 MVP

**Goal**: Drive a full run from the browser — PASS / FAIL-with-comment / BLOCKED-with-comment — with
every verdict landing in SQLite identically to the terminal path, and the page advancing.

**Independent Test**: Start a run, drive `/advance` for a PASS and a FAIL-with-comment, and verify (via
`spectra run status`/DB) that status, notes, handle, attempt, and blocking cascade match the
engine/`RunHandler` reference.

### Tests for User Story 1 ⚠️

- [X] T008 [P] [US1] `ConsoleParityTests` in `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleParityTests.cs` — drive `POST /advance` through `ConsoleServer`/`ConsoleEndpoints` and assert the resulting `test_results` row state (status/notes/attempt), dependency-blocking cascade, and ordering are identical to the `ParityTests.cs` engine-direct oracle (FR-004/FR-013, SC-001)
- [X] T009 [P] [US1] `ConsoleGuardrailTests` in `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleGuardrailTests.cs` — assert `POST /advance` returns `400 STATUS_REQUIRED` (missing status), `400 INVALID_STATUS`, and `400 NOTES_REQUIRED` (fail/blocked/skip without notes) and **records nothing** in each rejection, re-asserting `GuardrailTests.cs` at the HTTP boundary (FR-005, SC-003)

### Implementation for User Story 1

- [X] T010 [US1] Implement `POST /advance` in `ConsoleEndpoints.cs` — replicate `RunHandler.AdvanceAsync:204-211` guardrails (explicit status; notes required for fail/blocked/skip; never infer), resolve the handle server-side (mirror `ResolveHandleAsync`), auto-start a pending target (`StartTestAsync`), call `Engine.AdvanceTestAsync(runId, handle, verdict, notes)`, and return the refreshed `/current` projection plus `{ recorded, blocked[], next }`
- [X] T011 [US1] Implement `POST /note` in `ConsoleEndpoints.cs` — `NOTE_REQUIRED` guard → `Engine.AddNoteAsync`; status unchanged; return refreshed projection
- [X] T012 [US1] Implement `POST /finalize` in `ConsoleEndpoints.cs` — `{ force }` → `Engine.FinalizeRunAsync`; return report path; used when the queue is exhausted (`current == null`)
- [X] T013 [US1] Add PASS / FAIL / BLOCKED buttons, a comment textarea, and advance-then-refetch fetch logic to `ConsolePage.cs` JS; show a "Finalize" affordance once `/current.current` is null
- [X] T014 [US1] Add the shared `4xx` error envelope (`{ error_code, message }`) handling in `ConsoleEndpoints.cs` and surface it inline on the page, keeping `RECONSTRUCTION_FAILED` distinct from `RUN_NOT_FOUND`

**Checkpoint**: A human can drive an entire run to completion from the browser; DB state == terminal path.

---

## Phase 4: User Story 2 - Lose nothing on refresh or restart (Priority: P1)

**Goal**: Closing/reopening or refreshing the page loses nothing — the page is a pure projection of
SQLite and holds no authoritative state.

**Independent Test**: Record several verdicts (via engine or `/advance`), then fetch a fresh `/current`
(simulating refresh) and confirm identical state and correct current test; inspect the page for any
browser-side run store.

### Tests for User Story 2 ⚠️

- [X] T015 [P] [US2] Refresh/no-store test in `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleParityTests.cs` (or a sibling `ConsoleProjectionTests.cs`) — after recording verdicts, assert a fresh `GetCurrentAsync` re-projects identical results/counts/current-test with no duplicates or phantoms (SC-002), and assert the rendered `ConsolePage` markup/JS contains **no** `localStorage`/`sessionStorage` run-state usage (FR-003)

### Implementation for User Story 2

- [X] T016 [US2] Make `ConsolePage.cs` JS fully stateless — every render derived solely from the latest `/current` payload, with no client-side accumulation or storage; add a code comment documenting the "browser is a view + write-back caller, never a store" invariant (FR-002/FR-003, research R4)

**Checkpoint**: Refresh and close/reopen are provably lossless; no browser store exists.

---

## Phase 5: User Story 3 - Attach a screenshot to a result (Priority: P2)

**Goal**: Upload (drop/paste) an image in the browser and have it attached to the current test's result
via the existing screenshot path — no browser-side copy.

**Independent Test**: With a test in progress, upload an image through `/screenshot` and verify it lands
in the run's attachments and in that result's `screenshot_paths`.

### Tests for User Story 3 ⚠️

- [X] T017 [P] [US3] `ConsoleScreenshotTests` in `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleScreenshotTests.cs` — assert a multipart upload AND a base64 data-URL upload each route through `ScreenshotService.EncodeAndSaveAsync` + `ResultRepository.AppendScreenshotPathAsync` into the result's `screenshot_paths`, that bad/empty bytes yield `400 SCREENSHOT_INVALID` with nothing attached, and that no authoritative copy is retained browser-side (FR-006, SC-004)

### Implementation for User Story 3

- [X] T018 [US3] Implement `POST /screenshot` in `ConsoleEndpoints.cs` — parse multipart form-data (`file`) OR JSON `{ dataUrl }` → `byte[]`, derive `existingCount` from the current result's `screenshot_paths` length, call `ScreenshotService.EncodeAndSaveAsync(reportsPath, runId, testId, existingCount, bytes)` then `ResultRepository.AppendScreenshotPathAsync(handle, relativePath)`, and return the updated `screenshotPaths`
- [X] T019 [US3] Add drag-drop + clipboard-paste image capture in `ConsolePage.cs` wired to `POST /screenshot`, refreshing the test-detail panel from the response (no browser store of the image)

**Checkpoint**: Screenshots attach through the existing path; visible on the result/report.

---

## Phase 6: User Story 4 - Console outlives the launching session (Priority: P2)

**Goal**: The server runs detached — surviving the launching terminal/agent session exiting — and stops
only on `spectra run console --stop`, discoverable via a marker.

**Independent Test**: Launch the console, end the launching process, confirm the page still serves; then
`--stop` and confirm the server ends.

- [X] T020 [US4] **PROTOTYPE GATE** (research R2) — prototype the detached worker launch and confirm it (a) survives the launching process exiting and (b) survives an agent-tracked Windows **Job Object** with kill-on-close closing. If `cmd.exe /c start "" /B …` fails (b), adopt the `CREATE_BREAKAWAY_FROM_JOB` `CreateProcess` P/Invoke fallback. Record the chosen mechanism in `research.md` (R2). **Blocks T023.**

### Tests for User Story 4 ⚠️

- [X] T021 [P] [US4] `ConsoleLifecycleTests` in `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleLifecycleTests.cs` — assert `ConsoleMarker` write/read round-trip, stale-marker detection (dead/mismatched pid → treated as absent), `--stop` deletes the marker and reports cleanly when none is running, and a second launch against a live marker is refused with `CONSOLE_ALREADY_RUNNING` (FR-010/FR-012, SC-005)

### Implementation for User Story 4

- [X] T022 [US4] Implement `ConsoleMarker` in `src/Spectra.CLI/Commands/Run/WebConsole/ConsoleMarker.cs` — read/write `.execution/console.json` (`pid`, `port`, `url`, `startedUtc`, optional `runId`) with stale detection by pid liveness + start-time consistency (data-model.md)
- [X] T023 [US4] Implement detached launch in `ConsoleCommand.cs` for `spectra run console` — spawn the hidden `--serve` worker via the T020-confirmed mechanism, wait for the bound port, write the marker, print the URL, and refuse with `CONSOLE_ALREADY_RUNNING` if a live marker exists (depends on T020, T022)
- [X] T024 [US4] Implement `spectra run console --stop` in `ConsoleCommand.cs` — read the marker, terminate the worker pid, delete the marker; report "no running console" cleanly on a missing/stale marker (FR-012)
- [X] T025 [US4] Implement port handling in `ConsoleServer`/`ConsoleCommand` — honor `--port`, else bind the default and fall back to a free ephemeral port; write the actual bound port into the marker and the launch output

**Checkpoint**: Server survives launcher exit; `--stop` ends it; marker discovery + stale handling work.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T026 [P] Update execution-workflow + deployment docs to distinguish the console (local ephemeral server) from the dashboard (static + GitHub OAuth on Cloudflare Pages) — do not conflate the two deployment models (spec Documentation section)
- [X] T027 [P] Add a `spectra run console [--port N] [--stop]` entry to `docs/usage.md` and the CLI reference (mirrors the quickstart)
- [X] T028 Run the full `dotnet test` suite and confirm the C13 regression net is **green and byte-unchanged** — `ParityTests`, `GuardrailTests`, `RunLoopSmokeTests`, `WalConcurrencyTests`, `Spectra.MCP.Tests/Execution/*`, the MCP corpus, and all of `Spectra.Core.Tests` (SC-006). If any required a change, STOP — the console altered engine behavior it should not.
- [X] T029 Run `quickstart.md` end-to-end (start run → launch console → record PASS/FAIL + screenshot → refresh → finalize → `--stop`) to validate the feature

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. **BLOCKS all user stories** (server + `/current` + page skeleton + `--serve`).
- **User Stories (Phase 3–6)**: All depend on Foundational. US1/US2/US3 are independent of each other; US4 is independent of US1–US3 but **T023 depends on the T020 prototype gate**.
- **Polish (Phase 7)**: Depends on the desired user stories being complete (T028/T029 after all).

### User Story Dependencies

- **US1 (P1)**: After Foundational. The core write-back loop. No dependency on other stories.
- **US2 (P1)**: After Foundational. Verifies/locks the no-store invariant; testable independently of US1 (can record via engine, then re-project).
- **US3 (P2)**: After Foundational. Independent ingest endpoint + UI.
- **US4 (P2)**: After Foundational. Detached lifecycle; internally T020 → T023.

### Within Each User Story

- Tests written first and expected to FAIL before implementation.
- Endpoints (`ConsoleEndpoints.cs`) before the page wiring (`ConsolePage.cs`) that calls them.
- US4: prototype gate (T020) before detached launch (T023); marker (T022) before launch/stop (T023/T024).

### Parallel Opportunities

- T003 in Setup is [P].
- Foundational (T004–T007) is largely sequential (server → endpoint → page → worker) — one file each but `/current`, page, and worker build on the server.
- Test tasks across stories (T008, T009, T015, T017, T021) are [P] — distinct files.
- US1, US3, US4 implementation can proceed in parallel by different developers once Foundational is done (different files), with US4 honoring its internal T020→T023 order.
- Polish T026 and T027 are [P] (different docs).

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together (distinct files):
Task: "ConsoleParityTests in tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleParityTests.cs"
Task: "ConsoleGuardrailTests in tests/Spectra.CLI.Tests/Commands/Run/WebConsole/ConsoleGuardrailTests.cs"
```

## Parallel Example: Across stories (post-Foundational, multi-developer)

```bash
Developer A: US1 (T008–T014)   # /advance, /note, /finalize, page buttons
Developer B: US3 (T017–T019)   # /screenshot ingest + drop/paste UI
Developer C: US4 (T020 → T021–T025)  # prototype gate first, then marker + detached launch + --stop
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (server + `/current` + page + `--serve`).
2. Phase 3 US1 (`/advance` + guardrails + buttons + finalize).
3. **STOP and VALIDATE**: drive a full run from the (foreground) console; DB state == terminal path.
4. Demo — a usable console even before detachment (US4) lands, exactly as the spec's independence note allows.

### Incremental Delivery

1. Setup + Foundational → read-only live page.
2. + US1 → drive verdicts (MVP). 
3. + US2 → lossless-refresh invariant locked.
4. + US3 → screenshots.
5. + US4 → detached lifecycle (enables the later agent revision, Spec 2).
6. Polish (docs + full-regression verify + quickstart).

---

## Notes

- [P] = different files, no dependency on incomplete tasks.
- The console reuses `ExecutionEngine`/`RunServices`/`RunHandler`/`ScreenshotService`/`ReportWriter`
  **unmodified** — if a task tempts you to edit those, stop: the console must not change engine behavior
  (C13 / SC-006).
- HTTP layer is BCL `System.Net.HttpListener` — **no new `PackageReference`** in `Spectra.CLI.csproj`.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
