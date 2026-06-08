# Execution Console — Investigation Summary

**Subject:** a local, human-driven **execution console** — a small detached local server (conceptually
`spectra run console`) serving an ephemeral, gitignored web page where a QA engineer drives a manual
run: sees the current test, clicks PASS / FAIL / BLOCKED, adds a comment and a screenshot. **SQLite
(via the extracted execution engine) is the single source of truth — never browser storage.** One
human at a time, local-only.

**Output of this investigation:** evidence only (Markdown under `docs/investigation/console/`). No
production code, specs, configs, or skills were written or modified. Every claim about current
behaviour cites `path:line`. **No spec numbers are proposed** (the spec script's auto-detect is
authoritative).

**UX reference (not copied):** a colleague's RMH QA Suite Runner (Next.js) established the manual-run
UX — per-test status buttons, per-test comment, screenshot attach + lightbox, clickable charts that
filter the list, status filters, editable suite title, autosave-on-refresh, tree selection. We want
that UX **in Spectra's report styling** and on the **engine/SQLite source of truth**, not RMH's
`localStorage` + export/import model.

**Out of scope** (the user owns these separately): bug-description-by-agent and Copilot-Spaces
clarification. *(Note only: if clarification ever attaches here it must use native file reads, since
Copilot Spaces was retired in spec 057 — but that is not this investigation's subject.)*

---

## Gating: both specs sit behind the execution-surface consolidation (spec 065) — which is MERGED

The console is a **third transport over an engine that already exists**. Spec 065 (execution-surface
consolidation) is already merged on `claude-code-v2` (`a899779`, merge `ce44004`). The transport-neutral
library `Spectra.Execution` exists and is referenced by both the CLI (`spectra run`) and MCP
transports:

- Engine: `src/Spectra.Execution/Execution/ExecutionEngine.cs`
- Storage: `src/Spectra.Execution/Storage/{ExecutionDb,RunRepository,ResultRepository,QueueSnapshotRepository}.cs`
- Reports: `src/Spectra.Execution/Reports/{ReportGenerator,ReportWriter}.cs`
- Screenshots: `src/Spectra.Execution/Screenshots/ScreenshotService.cs`

Two prerequisites for short-lived / concurrent HTTP handlers are also already in place:
- **Lossless cross-process queue reconstruction** (spec 064): `ExecutionEngine.GetQueueAsync`
  (`ExecutionEngine.cs:144`) → `ReconstructQueue` (`ExecutionEngine.cs:173`) rebuilds the queue
  DB-complete from `queue_snapshot` + `test_results`.
- **Concurrent-writer safety**: `ExecutionDb` opens with `PRAGMA journal_mode=WAL; busy_timeout=5000`
  (`ExecutionDb.cs:49-52`).

**Conclusion:** the gate is satisfied. Both specs below can proceed against the merged architecture.

---

## The two specs

### Independence / gating between them

- **Spec 1 (console infrastructure) is independent.** It can ship a usable console even while the agent
  still drives the loop manually, because the console talks straight to the same engine + DB.
- **Spec 2 (agent/SKILL revision) HARD-GATES on Spec 1.** The agent's new role is "start run → **launch
  the detached console** → be on-call." It cannot launch a console that does not exist. Sequence Spec 1
  before Spec 2.

### Spec split table

Columns: **Concern · Owning seam · Exists vs Missing · Regression net it must not disturb.**

#### Spec 1 — Console infrastructure

| Concern | Owning seam | Exists vs Missing | Must-not-disturb |
|---|---|---|---|
| Server host | NEW `spectra run console` over `RunServices`/`ExecutionEngine` (`RunServices.cs:39-54`); **not** a route on the MCP host (MCP is stdio JSON-RPC, `McpServer.cs:12-13,42,57`) | **Missing** (the subcommand + HTTP listener) — engine it wraps **Exists** | MCP transport tests, `ParityTests` |
| Write-back endpoints | `ExecutionEngine` public methods (see **A2**) | **All Exist** — 1:1 with MCP tools; nothing missing engine-side | engine + reconstruction tests |
| Source-of-truth invariant | `AdvanceTestAsync` → `ResultRepository.UpdateStatusAsync` → SQLite (`ExecutionEngine.cs:287-291`, `ResultRepository.cs:165`) | **Exists** — browser is view + write-back caller only | `ParityTests`, reconstruction family |
| Screenshot ingest | `ScreenshotService.EncodeAndSaveAsync` (`ScreenshotService.cs:28`) + `ResultRepository.AppendScreenshotPathAsync` (`:263`) | Service **Exists**; a thin **browser→bytes ingest endpoint is Missing** | screenshot/report tests |
| Page styling | Inline in `ReportWriter.cs:412-724` (`:root` tokens, navy + status palette) | **Exists** — console borrows the tokens | report-render tests |
| Interactive render | NEW page (JS events, write-back `fetch`, autosave-by-refetch) | **Missing** — `ReportWriter` HTML is a static end-of-run artifact | report-render tests |
| Live update | poll `/current` (lowest-complexity; no HTTP/SSE/websocket exists today) | **Missing** | — |
| Detached lifecycle | launch/stop inside `spectra run console`; Windows fire-and-forget (`INFERRED`) | **Missing** — no detached-server pattern in repo | — |

#### Spec 2 — Execution agent / SKILL revision

| Concern | Owning seam | Exists vs Missing | Must-not-disturb |
|---|---|---|---|
| Agent prompt | `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` | **Exists** — drives present→verdict→advance (`:29-38`, `:59-68`); must be **rewritten** to orchestrate | `ExecutionAgentPortTests`, `ExecuteSkillTests` |
| SKILL | `src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md` | **Exists** — "Iron rules" (`:13-18`), loop steps 3-5 (`:33-51`); must be **rewritten** | `ExecuteSkillTests` |
| Guardrail home | prose (`spectra-execute.md:13-18`, agent `:24-26`) + code (`RunHandler.AdvanceAsync:199-215`) | **Exists in code already**; with the console it becomes a **server property** (console write-back endpoint must replicate the checks) | `GuardrailTests` |
| Status surfacing | `spectra run status` → `RunHandler.StatusAsync:120-148` → `GetStatusAsync` → DB reconstruct | **Exists** — already session-free per 064 | reconstruction family |

---

## Regression net both specs must keep GREEN and untouched

A console is a **NEW transport over the SAME engine**. If any of these need to change, that is a signal
the console altered engine behaviour it should not (the trigger for spec C13):

- `tests/Spectra.CLI.Tests/Commands/Run/ParityTests.cs` — `spectra run` handler leaves the same DB
  state as driving `ExecutionEngine` directly.
- `tests/Spectra.CLI.Tests/Commands/Run/GuardrailTests.cs` — mechanical human-in-the-loop enforcement.
- `tests/Spectra.Execution.Tests/Storage/WalConcurrencyTests.cs` — WAL/busy_timeout under concurrent
  short-lived writers.
- `tests/Spectra.MCP.Tests/Execution/*` — `ReconstructionParityTests`, `ReconstructionFailLoudTests`,
  `ReconstructionBlockingParityTests`, `ReconstructionOrderingParityTests`, `TestQueueReconstructionTests`,
  `StateMachineTests`, `DependencyResolverTests`, `TestQueueFilterTests`.
- The ~56-file MCP test corpus in `tests/Spectra.MCP.Tests` (transport + tool + integration), which
  spec 065 proved passes byte-unchanged.
- All of `tests/Spectra.Core.Tests`.
- `tests/Spectra.CLI.Tests/Skills/ExecuteSkillTests.cs` (Spec 2 will edit this **on purpose** — see C).

See `C-regression-surface.md` for the full enumeration and the per-spec read.

---

## Stale-text log (for the spec that touches each file to fix)

- `spectra-execution.agent.md` (`:29-38`, `:40-68`) and `spectra-execute.md` (`:13-51`, `:82-86`) both
  describe the **agent-driven per-test loop**. Once the console owns that loop, this text is stale —
  **Spec 2's job** to rewrite (loop → orchestrate; verdict-collection mapping → console buttons).
- No other stale execution-loop text was found in the searched paths.

---

## Files in this investigation

- `00-summary.md` — this file (split table, gating, independence, regression net).
- `A-console-infrastructure.md` — sections A1–A8 (server host, write-back surface, source-of-truth,
  screenshots, render reuse, live update, detached lifecycle, styling).
- `B-agent-skill-revision.md` — sections B9–B12 (current agent + SKILL, role shift, guardrail
  migration, status surfacing).
- `C-regression-surface.md` — section C13 (the green-and-untouched test net + the additive net Spec 1
  should bring).

**Start-here for the reader:** A2 (write-back surface) and A3 (source-of-truth invariant) — together
they prove the console is a thin transport, not hidden new state.
