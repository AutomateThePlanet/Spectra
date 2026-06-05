# Phase 1 Data Model: Execution Agent Port

No new persisted data model, no DB change, **no `Spectra.MCP` change**. The "entities" are the artifact
taxonomy of the port plus the one config-model trim. Documented so contracts and tasks share a stable
vocabulary.

---

## Entity: Canonical Execution Skill

`.claude/skills/spectra-execution/SKILL.md` — the single execution-agent artifact after consolidation,
sourced from `Skills/Content/Agents/spectra-execution.agent.md`.

| Field (frontmatter) | Rule |
|---|---|
| `name` | `spectra-execution` (unchanged). |
| `description` | One line (unchanged or lightly edited). |
| `tools` | Claude Code tool list — **no** `github/get_copilot_space` / `github/list_copilot_spaces`; includes the MCP execution tools + `Read` (for native doc-lookup). |
| `model` | **Absent** (drop `GPT-4o` — session selects the model). |
| `disable-model-invocation` | **Absent** (dropped). |

**Body rules**: no `runInTerminal`/`awaitTerminal`/`show preview` Copilot verbs; no "Copilot Spaces
Documentation" section; a "Documentation lookup (read source docs)" section that reads the test's
`source_refs` files directly; the **verdict pause preserved** (present result → ask in plain text →
wait; no fabrication; no auto-advance; FAIL/BLOCKED/SKIP ask-first). MCP tool calls
(`start_execution_run`, `get_test_case_details`, `advance_test_case`, `skip_test_case`,
`finalize_execution_run`, `save_clipboard_screenshot`, …) are referenced **unchanged**.

---

## Entity: Deleted Duplicate Copies (removed)

- `Agent/Resources/spectra-execution.agent.md` — **deleted** (redundant 389-line duplicate).
- `Agent/Resources/SKILL.md` — **deleted** (redundant skill stub).
- `AgentResourceLoader` (`src/Spectra.CLI/Agent/AgentResourceLoader.cs`) — **deleted** (only
  `InitHandler` referenced it).
- csproj `EmbeddedResource` entries for the two resource files — **removed**.

Listed to pin the consolidation: after removal there is exactly **one** execution-agent artifact.

---

## Entity: MCP Allowlist Entry

A `permissions.allow` string `"mcp__spectra__*"` in `.claude/settings.json`.

| Property | Rule |
|---|---|
| Location | `.claude/settings.json` (committed project settings), `permissions.allow` array. |
| Value | `mcp__spectra__*` (one wildcard covering all 25 MCP tool method names). |
| Idempotency | Adding when already present is a no-op; existing `permissions.allow` entries are preserved. |
| Distinctness | NOT the same as `Bash(spectra-mcp:*)` (a bash command name in `settings.local.json`). |
| Scope | Client-side only — the server enforces nothing; removes per-call permission prompts, not the human-verdict pause. |

---

## Entity: Execution Config (trimmed)

`ExecutionConfig` (`src/Spectra.Core/Models/Config/ExecutionConfig.cs`).

| State | Fields |
|---|---|
| Before | `CopilotSpace` (`copilot_space`), `CopilotSpaceOwner` (`copilot_space_owner`) |
| After | **(removed)** — 0 fields; deserialization of legacy configs carrying those keys still succeeds (unknown keys ignored) |

---

## Entity: Reused MCP Engine (unchanged)

The entire `Spectra.MCP` server — `ToolRegistry`, the 25 tools, `ExecutionEngine` / `TestQueue` /
state machine, `SaveScreenshotTool`, the SQLite result store. **No fields, no behavior, no test
changes.** Listed only to pin the boundary: the port touches none of it.

---

## State transitions

No runtime state machines change. The only lifecycle touched is install/update, and only in the
execution artifact's destination + the added settings-merge:

```
(not installed) --init--> (.claude/skills/spectra-execution/SKILL.md written;
                           .claude/settings.json ensured to allow mcp__spectra__*)
(installed, unmodified) --update-skills, bundled changed--> (rewritten, hash updated)
(installed, user-modified) --update-skills--> (skipped, preserved)
```

The MCP run state machine (`pause/resume/cancel/finalize`) is unchanged and server-owned.
