---
title: Execution Agents
nav_order: 4
permalink: /execution-agents
---

# Execution Agents

How AI-driven test execution works in SPECTRA v2: a native Claude Code skill orchestrates
`spectra run`, and manual verdicts are recorded through a local browser console, not through any
MCP server or client-specific setup.

Related: [CLI Reference](cli-reference.md) (`spectra run …`)

---

## There is no MCP server for execution anymore

Pre-v2 SPECTRA shipped a separate `Spectra.MCP` global tool that any MCP-compatible client
(Claude Desktop, GitHub Copilot CLI/Chat, or a generic MCP client) could connect to over stdio.
**That server was removed entirely in v2.4.0.** There is nothing to `dotnet tool install`,
nothing to add to a client's MCP config, and no per-client setup page anymore. Execution is
CLI-only, and the agent that drives it is a native Claude Code skill
(`.claude/skills/spectra-execute/`), not something bolted on through MCP.

If you have an old `.vscode/mcp.json` entry or `mcp__spectra__*` permission from a pre-v2 install,
it's dead weight and safe to remove. `spectra init` no longer writes either.

## How it works now

1. You ask, in Claude Code, something like *"Run the checkout suite"* or *"Execute the high-priority auth
   tests."*
2. The execution skill/agent orchestrates rather than driving a per-test loop in chat:
   - Selects the tests (`spectra run start <suite> [--priorities …] [--tags …] [--components …]`)
   - Launches a local, detached web console (`spectra run console`)
   - Hands you the console's URL (`http://127.0.0.1:<port>/`) and stays on-call
3. You record verdicts in the browser (PASS / FAIL / BLOCKED, a comment, a screenshot), and
   the console's write-back endpoint enforces the same human-in-the-loop guardrails as the CLI
   (an explicit verdict is required; a comment is required for FAIL/BLOCKED/SKIP; nothing is ever
   auto-advanced or inferred).
4. The agent reads current state from `spectra run status` (the SQLite store) when you ask
   it something mid-run, never from the console page itself, so its answers can't drift from the
   source of truth.
5. Finally, `spectra run finalize` generates JSON, Markdown, and HTML reports under
   `.execution/reports/`.

SQLite (`.execution/spectra.db`) is the single source of truth throughout. The console is a view
and a write-back caller, never a store, so refreshing or reopening it loses nothing, and a
short-lived `spectra run` CLI invocation reconstructs the exact same state losslessly.

## Driving it directly from the CLI

You don't need the skill or the console at all: every operation is a plain `spectra run`
subcommand, useful for scripting or CI.

```bash
spectra run list-suites
spectra run start checkout --priorities high --tags smoke
spectra run status                              # active run if omitted
spectra run show                                # full steps/expected for the current test
spectra run advance --status pass|fail|blocked|skip [--notes "…"]
spectra run bulk-record --status pass --remaining
spectra run screenshot --file ./bug.png
spectra run finalize
```

See the [CLI Reference](cli-reference.md#spectra-run-) for the full command list, including
`pause`/`resume`/`cancel`/`cancel-all`, `retest`, `history`/`summary`/`selections`, and
`spectra run console --stop`.

## Answering questions mid-run

The agent answers tester questions about a step or expected result by reading the test case's
`source_refs` documentation directly with the Read tool, with no external service or configuration
required. It never auto-advances or fabricates a verdict; the human verdict pause is always
preserved.

## Storage

```
.execution/
├── spectra.db                # Execution state database (WAL mode)
├── console.json              # Console pid/port/url marker, when running
└── reports/
    └── a1b2c3d4-....json     # Execution reports (+ .md / .html)
```

Reports carry per-test enrichment sourced from the test case itself, including `priority`, `tags`,
`component`, linked acceptance-criteria IDs (`criteria`), and source-doc references
(`source_refs`), plus a run-level `timing` breakdown. All enrichment fields are optional and
backward compatible with older reports and JSON consumers (e.g. the dashboard).

## The SEPARATE BELLATRIX/Nova MCP

None of the above is related to BELLATRIX/Nova, the MCP server that drives your **system under
test** during automation. That MCP is a peer tool, unaffected by anything in this page; if
`spectra init` finds an existing `.vscode/mcp.json` with a BELLATRIX/Nova entry, it merges by key
and leaves it untouched.
