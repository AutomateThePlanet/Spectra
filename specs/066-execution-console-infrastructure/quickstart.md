# Quickstart: Execution Console

A local, human-driven web console for driving a manual execution run — PASS / FAIL / BLOCKED, comment,
screenshot — with SQLite as the single source of truth. One human, localhost only, no model, no tokens.

## Prerequisites

- A Spectra workspace (`test-cases/{suite}/` with `_index.json`).
- The `spectra` CLI (this tool). No MCP server and no network beyond localhost required.

## Drive a run from the console

```bash
# 1. Start a run (or use an already-active one)
spectra run start checkout

# 2. Launch the console (detached — survives this terminal/session)
spectra run console
#   → prints e.g.  Console running at http://127.0.0.1:7878/  (pid 12345)

# 3. Open the URL in your browser (e.g. the VS Code Simple Browser).
#    - The current test renders in Spectra's report styling.
#    - Click PASS to record and advance.
#    - Click FAIL/BLOCKED, type a comment, submit → recorded + advance.
#    - Drag-drop or paste a screenshot to attach it to the current test.
#    - Refresh anytime — nothing is lost (state is in SQLite).

# 4. When the queue is exhausted, click Finalize on the page (or):
spectra run finalize

# 5. Stop the console when done
spectra run console --stop
```

The console writes every verdict, comment, and screenshot straight to `.execution/spectra.db` through the
same engine the terminal (`spectra run advance …`) and the MCP tools use — so you can mix surfaces freely.

## What the console guarantees

- **Refresh-safe**: closing/reopening or reloading the page loses nothing; it re-projects from SQLite.
- **Guardrails**: FAIL / BLOCKED / SKIP require a comment; a verdict is never inferred — the same rules
  the CLI enforces, enforced again at the HTTP boundary.
- **Detached**: the server keeps running after the launching terminal or agent session ends; it stops
  only on `spectra run console --stop`.
- **Ephemeral**: the page and the `.execution/console.json` marker are local-only and gitignored — never
  committed.

## Validate the feature (developer)

```bash
dotnet build
dotnet test                                   # full suite — regression net (C13) must stay green

# Targeted new coverage
dotnet test --filter "FullyQualifiedName~Commands.Run.Console"
#   ConsoleParityTests      — write-back leaves the same DB state as RunHandler/engine-direct
#   ConsoleGuardrailTests   — STATUS_REQUIRED / NOTES_REQUIRED at the HTTP boundary
#   ConsoleScreenshotTests  — upload → ScreenshotService → screenshot_paths; no browser store
#   ConsoleLifecycleTests   — marker read/write, stale-marker handling, --stop, detached survival
```

> **Detached-launch prototype gate (T001)**: before the lifecycle code is locked, a prototype confirms
> the worker survives the launching process exiting **and** an agent-tracked job closing (research R2). If
> the `cmd /c start /B` pattern fails the job-close check, the fallback is `CREATE_BREAKAWAY_FROM_JOB`.

## Troubleshooting

- **"Console already running"**: a live marker exists. Reuse the printed URL, or `spectra run console
  --stop` first.
- **Stale marker** (pid gone): a fresh `spectra run console` overwrites it; `--stop` reports "no running
  console" cleanly.
- **Port in use**: pass `--port N`, or let the launcher pick a free port (printed at launch).
