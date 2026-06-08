# Feature Specification: Execution Surface Consolidation (CLI run + MCP-as-adapter)

**Feature Branch**: `065-execution-surface-consolidation`
**Created**: 2026-06-08
**Status**: Draft
**Input**: User description: "Make the execution engine first-class through the spectra CLI, keep Spectra.MCP as a thin optional adapter over the same engine — one install, 25 tool schemas out of context."

## Overview

Today, running a test-execution loop with Spectra requires a **second** global tool (`Spectra.MCP`) on top of the `spectra` CLI, plus per-client MCP configuration (`.vscode/mcp.json`, `.mcp.json`, `claude_desktop_config.json`), and it loads **25 MCP tool schemas** into the model's context every session. Generation already runs through the `spectra` CLI; execution does not.

This feature makes the deterministic execution engine drivable directly through the `spectra` CLI as a first-class surface (`spectra run …`), so a single `dotnet tool install -g Spectra` provides **both** generation and execution with **zero** per-client MCP configuration and the 25 tool schemas out of context. `Spectra.MCP` is retained as a thin optional adapter over the same engine, so networked/remote clients keep working unchanged.

This is **spec 2 of 2** in the MCP→CLI consolidation series. Its hard prerequisite — lossless queue reconstruction, so a short-lived CLI process drives the engine identically to a long-lived server — shipped as **Spec 064** (merged into the integration branch and green). Without that fix the CLI path would silently lose dependency-blocking and ordering; with it, every execution operation is a stateless, DB-backed call.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One install drives execution from the CLI (Priority: P1)

A user installs Spectra once (`dotnet tool install -g Spectra`) and runs a full execution loop — start a run, view status, advance/skip/record results, finalize — entirely through `spectra run …` invoked from their agent's shell tool. No separate `Spectra.MCP` install, no `.vscode/mcp.json` / `.mcp.json` / `claude_desktop_config.json`, and the 25 MCP tool schemas no longer consume model context.

**Why this priority**: This is the headline value of the feature — the single-tool, zero-config, low-context execution surface. It is the reason the work exists; everything else exists to make this safe.

**Independent Test**: On a clean machine with only `spectra` installed and no MCP configuration present, drive a run from `start` through `finalize` purely via `spectra run …` and confirm results are persisted to `.execution/spectra.db` and reportable — with no MCP server running.

**Acceptance Scenarios**:

1. **Given** a clean install of only the `spectra` CLI and no MCP config files, **When** the user starts a run, advances every test to a terminal verdict, and finalizes via `spectra run …`, **Then** the run completes and its results are durable and reportable with no MCP server involved.
2. **Given** an agent session using the CLI execution path, **When** the session begins, **Then** no MCP tool schemas are loaded into the model context for execution.
3. **Given** a single `dotnet tool install -g Spectra`, **When** the user runs both a generation command and an execution command, **Then** both work from the one installed tool with no additional install step.

---

### User Story 2 - CLI execution behaves identically to the MCP path (Priority: P1)

An execution operation invoked through `spectra run …` produces exactly the same engine behavior and the same persisted state as the equivalent MCP tool call — same ordering, same dependency-blocking, same status counts, same handles, same terminal outcomes — because both surfaces call one shared engine over the one SQLite database.

**Why this priority**: Without behavioral parity the consolidation is unsafe — users (and the dependency-blocking discipline that protects them) cannot trust the CLI path. Parity is what lets the CLI replace the MCP path rather than diverge from it.

**Independent Test**: For each operation, run it once through the MCP tool and once through the matching `spectra run` subcommand against the same database/run, and assert the observable engine result and resulting DB state are equivalent.

**Acceptance Scenarios**:

1. **Given** a run with persisted `DependsOn` relationships, **When** the user advances tests via `spectra run …`, **Then** dependent tests are blocked/unblocked exactly as they are under the MCP path.
2. **Given** tests with differing priority and a defined order, **When** the user asks for the next test via `spectra run …`, **Then** the selection matches the MCP path's selection (priority-then-topological, not alphabetical).
3. **Given** the same operation expressed as an MCP tool call and as a `spectra run` subcommand, **When** each is executed against the same run, **Then** the persisted run/test state is equivalent afterward.

---

### User Story 3 - Networked MCP clients keep working unchanged (Priority: P1)

A client that drives execution over MCP (e.g. a remote/networked agent) continues to call the existing tools and gets unchanged behavior, because `Spectra.MCP` is retained as a thin adapter over the same extracted engine.

**Why this priority**: Dropping MCP would delete the transport-test safety net and risk breaking unknown external MCP consumers. Keeping MCP as a thin adapter is strictly dominant: both surfaces call one engine, networked execution is preserved, and the transport tests stay green — which is itself the signal that the extraction did not change behavior.

**Independent Test**: Run the existing MCP transport/tool test corpus against the engine after extraction; all must pass unchanged.

**Acceptance Scenarios**:

1. **Given** the extracted engine, **When** a client invokes any existing MCP execution tool, **Then** the tool behaves exactly as before and the MCP transport test corpus passes unchanged.
2. **Given** the consolidation is complete, **When** the MCP adapter and the CLI both operate on the same run, **Then** they share one engine and one database with no behavioral divergence.

---

### User Story 4 - Short-lived processes are safe under concurrent SQLite access (Priority: P2)

Because CLI invocations are short-lived processes (each opens and closes its own database connection) rather than one long-lived server, concurrent or rapid-succession `spectra run …` invocations that touch the database do not fail with lock errors.

**Why this priority**: The single-user sequential loop works without this, so it is not part of the MVP — but the per-command process model removes the long-lived single-connection guarantee, so without per-process SQLite safety, any concurrency (or rapid retries) can surface lock failures. It hardens the surface for real use.

**Independent Test**: Launch concurrent short-lived processes that each open the database and perform a write against the same workspace; confirm none fail with a database-locked error.

**Acceptance Scenarios**:

1. **Given** two short-lived processes opening the same database, **When** both attempt a write within the contention window, **Then** neither fails with a database-locked error (writes serialize and succeed).
2. **Given** a rapid sequence of `spectra run …` invocations, **When** each opens and closes its own connection, **Then** all complete without lock-related failures.

---

### User Story 5 - The CLI execution loop preserves human-in-the-loop guardrails (Priority: P2)

An agent driving execution through the new CLI loop follows the same discipline as the MCP execution agent: present the current test to the human, wait for the human's verdict, then advance — never fabricating a result and never auto-advancing past a test the human has not judged.

**Why this priority**: The guardrails are the difference between assisted manual execution and a model silently rubber-stamping a test run. They are essential to trust, but they ride on top of the mechanical surface (US1–US3), so they follow it.

**Independent Test**: Exercise the CLI execution loop against scripted human inputs and confirm the loop blocks for a verdict at each test, never records a verdict the human did not give, and never advances past an unjudged test.

**Acceptance Scenarios**:

1. **Given** the CLI execution loop is presenting a test, **When** no human verdict has been supplied, **Then** the loop does not advance and does not record any result.
2. **Given** a human supplies a verdict, **When** the loop records it, **Then** it records exactly that verdict (never a fabricated or inferred one) and only then advances to the next actionable test.

---

### Edge Cases

- **Missing/incomplete orchestration snapshot**: when a CLI process cannot faithfully rebuild the queue from the database, the operation fails loud (surfacing the Spec 064 reconstruction failure) rather than silently degrading — the CLI surface must propagate that failure with a clear, distinct error, not a generic one.
- **No active run / unknown run id**: a `spectra run …` operation against a nonexistent run reports a clear "run not found" outcome, distinct from a reconstruction failure.
- **Host-bound operations from the CLI** (e.g. clipboard screenshot capture): the local CLI process is the host, so these operate against the local machine; this is expected and is an improvement over a possibly-remote MCP server reading the wrong machine's clipboard.
- **Concurrent finalize / advance on the same run from two processes**: must not corrupt state or lose a recorded result; the database write-first discipline plus per-process SQLite safety governs the outcome.
- **Stale or lost handle in the CLI loop**: the CLI path recovers the in-progress or next-pending handle the same way the MCP path does, rather than failing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The execution engine and its supporting types currently residing in the `Spectra.MCP` assembly MUST be relocated into a transport-neutral execution assembly referenced by both the CLI and MCP, behavior-preserving, with no MCP/transport coupling in the relocated code.
- **FR-002**: The CLI MUST expose a `spectra run` command group whose subcommands are thin adapters over the shared engine and address the same execution operations the MCP tools expose, so a full execution loop (start, status, advance, skip, record, note, retest, screenshot, finalize, cancel, pause/resume, discovery/reporting as applicable) is drivable from the CLI.
- **FR-003**: `Spectra.MCP` MUST be retained as a thin adapter over the relocated engine, with its existing tool handlers behavior-unchanged, so networked/remote-client execution is preserved and the MCP transport test corpus stays green.
- **FR-004**: Database access MUST be safe for short-lived, possibly-concurrent processes — concurrent or rapid invocations that write to the database MUST NOT fail with lock errors under normal single-user contention.
- **FR-005**: The feature MUST ship a CLI execution-loop SKILL and an execution agent prompt that port the human-in-the-loop guardrails of the existing MCP execution agent — present the current test, wait for the human verdict, then advance; never fabricate a result; never auto-advance past an unjudged test.
- **FR-006**: After this feature, a single `dotnet tool install -g Spectra` MUST provide both generation and execution; execution MUST be runnable via `spectra run …`; the 25 MCP tool schemas MUST be absent from the model context for the CLI execution path; and no per-client MCP configuration MUST be required for that path.
- **FR-007**: The CLI execution path MUST produce engine behavior and persisted state equivalent to the MCP path for the same operation against the same run (ordering, dependency-blocking, status counts, handle semantics, terminal outcomes).
- **FR-008**: When the shared engine fails loud (e.g. an un-reconstructable queue), the CLI surface MUST surface that failure with a clear, distinct outcome — never silently degrade and never conflate it with a benign "run not found".
- **FR-009**: Documentation that asserts execution requires a separate MCP install and per-client MCP configuration MUST be corrected, and getting-started/architecture docs MUST be updated to describe the one-install + `spectra run` path alongside the retained MCP adapter.

### Key Entities *(include if feature involves data)*

- **Shared execution assembly**: a transport-neutral home for the engine, queue, dependency resolver, state machine, reconstruction-failure type, and the SQLite repositories (run, result, queue-snapshot) plus the database accessor — referenced by both the CLI and MCP, depending only on the core domain models.
- **`spectra run` command group**: the CLI surface for execution; each subcommand is a thin adapter mapping CLI arguments to one engine operation and rendering the engine's domain result, mirroring an existing MCP tool one-to-one.
- **MCP adapter (retained)**: the existing MCP tool handlers, now thin adapters over the relocated engine; the contract external clients see is unchanged.
- **CLI execution SKILL + agent prompt**: the human-in-the-loop driver for the CLI loop, carrying the present→wait-for-verdict→advance discipline.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can complete a full execution loop (start → advance all tests → finalize) on a clean machine with only the `spectra` CLI installed and zero MCP configuration files present.
- **SC-002**: For every execution operation, the `spectra run` subcommand and its MCP tool counterpart produce equivalent engine results and equivalent persisted database state when run against the same run.
- **SC-003**: The MCP transport/tool test corpus (~14 transport-coupled test files) passes unchanged after the engine is relocated.
- **SC-004**: The `Spectra.Core` test corpus and the `TestPersistenceService` tests are unchanged and pass.
- **SC-005**: Concurrent short-lived processes writing to the same database under normal single-user contention complete with zero database-locked failures.
- **SC-006**: The CLI execution loop never advances past a test without a human verdict and never records a verdict the human did not supply, across the guardrail test scenarios.
- **SC-007**: Setup/getting-started/architecture documentation no longer requires a separate MCP install or per-client MCP config for the execution path, and describes `spectra run` as the first-class surface with MCP as the retained adapter.

## Assumptions

- **Numbering**: the repository's next available feature number is **065** (highest existing spec is `064`). The originating draft labeled this "Spec 067" under the assumption that `060` provider-retirement and `061–065` were already consumed; in this repository provider-retirement is `058` and `065` is the next free slot, so this feature is **065**. The series narrative (consolidation spec 2 of 2; pairs with a future multi-client spec that should land after this one despite any lower number it might receive) is preserved.
- **Prerequisite is satisfied**: the hard dependency — lossless queue reconstruction so short-lived == long-lived behavior — shipped as **Spec 064** (the draft's "Spec 066"), merged into the integration branch and green. This feature is therefore unblocked.
- **Extraction set**: the draft cited "seven execution types." Post-064 the relocation set is **nine** types — `ExecutionEngine`, `TestQueue`, `DependencyResolver`, `StateMachine`, `QueueReconstructionException` (execution) plus `ExecutionDb`, `RunRepository`, `ResultRepository`, `QueueSnapshotRepository` (storage) — because Spec 064 added the reconstruction-exception and queue-snapshot types. Supporting types the engine constructor depends on (identity resolution, execution config) are relocated or made available to the neutral assembly as needed; the move is mechanical and behavior-preserving.
- **Integration branch**: the base is the `claude-code-v2` integration branch.
- **Tooling defaults**: standard managed-CLI cold-start cost is acceptable for the interactive execution loop; no ahead-of-time/ReadyToRun work is in scope (the loop is human-paced, not throughput-bound).
- **SQLite safety mechanism**: per-process safety is achieved with write-ahead logging plus a bounded busy-timeout at connection open; a cross-process file lock is added only if true concurrent writers prove necessary (not assumed for the single-user model).

## Dependencies

- **Hard-gated behind Spec 064** (lossless queue reconstruction) — merged and green. Without it the CLI path silently loses dependency-blocking and ordering.
- **Gated behind the v2 migration (Specs 053–059)** — done.
- **Pairs with the future multi-client spec**: a CLI-only execution surface makes per-client support trivial, so this feature should land before multi-client breadth is implemented, even if multi-client receives a lower spec number.

## Out of Scope

- Dropping MCP entirely. Keeping it as a thin adapter is strictly dominant: both surfaces call one engine, transport tests stay green, networked execution is preserved, and removal would delete ~14 transport tests and risk unknown external MCP consumers.
- Multi-client breadth (Codex / Gemini / Copilot client support) — that is the separate multi-client spec.
- Signing or authenticating opaque handles.
- Remote-execution behavior changes.
- Semantic matching of tests or criteria.
- Cold-start performance optimization (ahead-of-time compilation, trimming, ReadyToRun).
