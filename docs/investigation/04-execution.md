# 04 — MCP execution engine (Area D)

> **Purpose.** Inventory the MCP execution surface, confirm it is client-agnostic (ports to
> Claude Code unchanged), and isolate the few Copilot/agent-specific bits that do not port —
> grounding ARCHITECTURE-v2 §90–98 (execution loop, allowlist, screenshots).
>
> Investigation only. Confirmed claims cite `file:line`; risks under **Findings**.

---

## 1. The decisive evidence: the server has no model and no Copilot coupling

A repo-wide search for `GitHub.Copilot.SDK`, `CopilotService`, `CopilotClient`,
`copilot_space`, and `CopilotSpace` across **`src/Spectra.MCP`** returns **no files**. The
execution server runs no model and has no Copilot dependency. The only `copilot_space`
binding lives in the shared config model (`Spectra.Core/Models/Config/ExecutionConfig.cs:10–14`),
not in the server. The server is therefore client-agnostic by construction: it speaks
JSON-RPC and holds state itself.

---

## 2. Tool inventory (25 tools)

All tools are registered in `src/Spectra.MCP/Program.cs:135–166` into a `ToolRegistry`
(`src/Spectra.MCP/Server/ToolRegistry.cs:9`) — a case-insensitive `Dictionary<string,
IMcpTool>` (`ToolRegistry.cs:11`) dispatched by bare method name in `InvokeAsync`
(`ToolRegistry.cs:43–73`). The registered method names are the bare names below; a Claude
Code client addresses them as `mcp__spectra__<name>`.

### Run management (`Tools/RunManagement/`, registered `Program.cs:135–143`)
| Tool | Reg line | Server-side state / handle |
|------|----------|----------------------------|
| `start_execution_run` | `:135` | Creates `Run` + builds in-memory `TestQueue`; returns `runId` handle |
| `get_execution_status` | `:136` | Read-only over `runId` |
| `pause_execution_run` | `:137` | `Run.Status → Paused` (state machine) |
| `resume_execution_run` | `:138` | `Run.Status → Running` |
| `cancel_execution_run` | `:139` | `Run.Status → Cancelled` |
| `finalize_execution_run` | `:140` | `Run.Status → Completed`; emits JSON/MD/HTML report |
| `list_available_suites` | `:141` | Read-only |
| `list_active_runs` | `:142` | Read-only (discovery) |
| `cancel_all_active_runs` | `:143` | Bulk cancel |

### Test execution (`Tools/TestExecution/`, registered `Program.cs:146–154`)
| Tool | Reg line | Server-side state / handle |
|------|----------|----------------------------|
| `get_test_case_details` | `:146` | Read-only; `testHandle` resolves position |
| `advance_test_case` | `:147` | Records verdict on `testHandle` → DB result row |
| `skip_test_case` | `:148` | `→ Skipped` with reason |
| `bulk_record_results` | `:149` | Batch verdicts over `runId` |
| `add_test_note` | `:150` | Appends note to `testHandle` |
| `retest_test_case` | `:151` | Resets to pending, increments attempt |
| `save_screenshot` | `:153` | Writes attachment file, returns relative path |
| `save_clipboard_screenshot` | `:154` | Captures clipboard → delegates to `save_screenshot` |

### Reporting (`Tools/Reporting/`, registered `Program.cs:157–158`)
`get_run_history` (`:157`), `get_execution_summary` (`:158`) — both read-only over the DB.

### Data (`Tools/Data/`, registered `Program.cs:161–166`)
`validate_tests` (`:161`), `rebuild_indexes` (`:162`), `analyze_coverage_gaps` (`:163`),
`find_test_cases` (`:164`), `get_test_execution_history` (`:165`),
`list_saved_selections` (`:166`) — read-only / deterministic over `_index.json` and the DB;
these reuse the same model-free core covered in `03`.

(`Tools/ActiveRunResolver.cs` is a shared helper, not a registered tool — 25 tools total.)

### Opaque-handle + persistence mechanics
`runId` keys an in-memory `TestQueue` held by `ExecutionEngine`; `testHandle` resolves to a
DB result row; run state persists in SQLite (`.execution/spectra.db`) so a run survives a lost
connection (pause/resume/`get_execution_status`). This is exactly ARCHITECTURE-v2 §92's
premise: **state lives in the server, not the agent**, so the multi-step pass/fail loop is
just conversational turns with a tool call between them — Claude Code's native mode. (The
in-memory-queue + DB-result split is asserted by the agent map in CLAUDE.md and the §1 grep;
the precise `ExecutionEngine` field is INFERRED — see §5.)

---

## 3. Client-agnostic — ports to Claude Code unchanged

- No Copilot/model dependency in the server (§1).
- Dispatch is by string method name with a generic `IMcpTool` contract
  (`ToolRegistry.cs:55, 92–110`) — any JSON-RPC client drives it identically.
- Error handling is structured and client-neutral: `MethodNotFound` (`ToolRegistry.cs:47`),
  `INVALID_PARAMS` (`:62`), `InternalError` (`:68`).

**Conclusion:** the entire tool surface + engine is reused verbatim by Claude Code. No code
change is required on the server to switch clients.

---

## 4. Copilot/agent-specific bits that do NOT port

These are all in the **orchestration agent**, not the server:

- `model: GPT-4o` — `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md:13`.
- `disable-model-invocation: true` — `spectra-execution.agent.md:14`.
- `github/get_copilot_space`, `github/list_copilot_spaces` in the tool list —
  `spectra-execution.agent.md:6–7`; used in the "Copilot Spaces Documentation" section
  (`:91–93`) which reads `execution.copilot_space` from config. **No Claude Code equivalent**
  — this is the known gap (ARCHITECTURE-v2 §122); replacement is file reads / the docs index.
- `askQuestion` / `askForConfirmation` prohibition — `spectra-execution.agent.md:25`. This
  is a Copilot-ism by reference: the *ban* ports in spirit (the human-verdict pause is a
  plain-text question + wait), but the named tools do not exist in Claude Code, so the line
  becomes "use plain text," which the agent already says.
- `runInTerminal` / `awaitTerminal` discipline — `spectra-execution.agent.md:110, 125`
  ("do NOTHING while waiting"). Ports conceptually to Claude Code's terminal model.
- Config fields `execution.copilot_space` / `copilot_space_owner` —
  `ExecutionConfig.cs:10–14` — dead once the Spaces lookup is replaced.

---

## 5. Where the allowlist and screenshot-by-path slot in

- **Allowlist:** `ToolRegistry.InvokeAsync` (`ToolRegistry.cs:43`) currently invokes any
  registered tool — there is **no server-side allowlist**. The v2 `mcp__spectra__*`
  pre-approval (ARCHITECTURE-v2 §97) is a **client-side** setting in `.claude/settings.json`,
  not a server change; it would gate the same 25 method names. A server-side allowlist, if
  ever wanted, would be a guard inserted before the lookup at `ToolRegistry.cs:45` — recorded
  as the slot, not proposed.
- **Screenshot-by-path:** `Tools/TestExecution/SaveScreenshotTool.cs` already implements
  capture→write→return-path: accepts `file_path` (reads bytes, `:131`) or base64 `image_data`
  (`:143`), compresses to WebP via ImageSharp (`:175–189`), writes to
  `reports/{run_id}/attachments/` (`:165–200`), and returns a **relative path** (`:203`) plus
  a test note (`:207`). `save_clipboard_screenshot` (`Program.cs:154`) delegates to it. This
  is precisely the "MCP-saves-to-file + path reference" method ARCHITECTURE-v2 §98 wants to
  standardize for Claude Code — already present, no change needed.

---

## 6. INFERRED

- **INFERRED:** `runId` maps to an in-memory `TestQueue` owned by `ExecutionEngine`, while
  verdicts persist to SQLite via the result repository — so the agent is effectively stateless
  between turns. *Confirming evidence:* read `Execution/ExecutionEngine.cs`,
  `Execution/TestQueue.cs`, `Storage/ResultRepository.cs` (not opened in this pass; the claim
  rests on the §1 grep, the registration wiring in `Program.cs:135–166`, and CLAUDE.md's
  description).
- **INFERRED:** state-machine transitions (`pause/resume/cancel/finalize`) are validated by a
  `StateMachine` type. *Confirming evidence:* `Execution/StateMachine.cs` referenced in
  CLAUDE.md project structure; not opened here.

---

## 7. Findings (recorded, not fixed)

- **F-1 — No server-side tool allowlist.** Every registered tool is invokable
  (`ToolRegistry.cs:45`). Fine today (the client controls exposure), but worth noting that the
  v2 pre-approval is purely client-side and the server enforces nothing.
- **F-2 — Copilot Spaces is a real feature loss, not just a Copilot-ism.** The execution
  agent's doc-lookup (`spectra-execution.agent.md:91–93`) has no Claude Code equivalent; the
  `execution.copilot_space*` config (`ExecutionConfig.cs:10–14`) becomes dead. Needs a
  conscious replacement (docs index / file reads), per ARCHITECTURE-v2 §122 — recorded, not
  designed here.

---

## 8. Conclusion

The MCP execution engine and all 25 tools are **client-agnostic and reused verbatim**: no
model, no Copilot coupling (§1), generic JSON-RPC dispatch (§3), and the screenshot-by-path
mechanism v2 wants already exists (`SaveScreenshotTool.cs:203`). What does not port is confined
to the **execution agent file** (`spectra-execution.agent.md`) and two config fields
(`ExecutionConfig.cs:10–14`): the GPT-4o model pin, the `github/*` Copilot Spaces tools, and
the `askQuestion` ban — all translation/replacement work in orchestration (`05`), not in the
server.
