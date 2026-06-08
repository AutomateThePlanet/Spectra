# Implementation Plan: Execution Agent / SKILL Revision

**Branch**: `067-execution-agent-console-revision` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/067-execution-agent-console-revision/spec.md`

## Summary

Rewrite the bundled execution **agent** (`spectra-execution.agent.md`) and **SKILL**
(`spectra-execute.md`) so the per-test verdict loop moves out of chat and into the Spec 066 console. The
agent becomes **orchestrator + on-call**: select tests → `spectra run start` → **`spectra run console`**
→ hand over the local URL → answer on-call questions by reading `spectra run status` (SQLite) + source
docs. The human-in-the-loop discipline is **restated as a console guarantee** (the console already
enforces explicit-status / notes-required / no-auto-advance / no-inferred-verdict at its HTTP boundary,
per 066); the agent simply never records verdicts in chat. The two skill/agent **contract tests**
(`ExecuteSkillTests`, `ExecutionAgentPortTests`) are rewritten to assert orchestrate-not-drive. Stale
loop prose in `docs/cli-reference.md` (and console pointers in `getting-started.md` /
`execution-agents.md`) are updated. **No engine/handler/guardrail code changes.**

## Technical Context

**Language/Version**: N/A — this feature is bundled **Markdown** prose (agent + SKILL) + xUnit test edits; no C# production code changes  
**Primary Dependencies**: The Spec 066 console (`spectra run console`, merged) the agent launches; the existing session-free `spectra run status` (RunHandler → GetStatusAsync, reconstructs from DB per 064); native Read/Grep/Glob doc lookup  
**Storage**: None changed — the agent *reads* run state from `.execution/spectra.db` via `spectra run status`; it writes nothing  
**Testing**: xUnit — rewrite `tests/Spectra.CLI.Tests/Skills/ExecuteSkillTests.cs` + `ExecutionAgentPortTests.cs`; everything else stays green unchanged  
**Target Platform**: bundled content shipped in `Spectra.CLI` (embedded resources) → installed to `.claude/`  
**Project Type**: Documentation/prose + test revision (single project — `Spectra.CLI`)  
**Performance Goals**: N/A  
**Constraints**: agent file MUST stay **≤200 lines** (`SkillsManifestTests.ExecutionAgent_LineCount_Within200`); agents MUST NOT contain fenced CLI code blocks for the delegation markers; SKILLs MUST NOT use `### Tool call N` or `terminalLastCommand`; agent MUST keep `spectra-update` + `spectra-quickstart` references; `SkillContent.All` count stays 15, `AgentContent.All` count stays 3 — all asserted by `SkillsManifestTests`, which **MUST stay green unchanged**  
**Scale/Scope**: 2 bundled markdown files rewritten, 2 test files rewritten, ~3 docs touched

### Resolved unknowns (see research.md)

1. **What may change** → only `spectra-execution.agent.md`, `spectra-execute.md`, the 2 contract tests, and stale docs. The guardrail **code** (`RunHandler.AdvanceAsync`, `ExecutionEngine`/`StateMachine`) is untouched.
2. **Test-rewrite boundary** → `SkillsManifestTests` is NOT rewritten; it is the constraint envelope the rewrite must satisfy. Only `ExecuteSkillTests` + `ExecutionAgentPortTests` move (FR-008).
3. **Console launch command** → `spectra run console` (Spec 066), detached; agent hands over the printed `http://127.0.0.1:<port>/` URL.
4. **On-call state source** → `spectra run status --output-format json` (DB), never the page/URL (FR-006).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|---|---|---|
| **I. GitHub as Source of Truth** | Agent/SKILL are versioned markdown in the repo; no external store. | ✅ Pass |
| **II. Deterministic Execution** | Guardrail/engine **code unchanged**; the deterministic state machine is untouched. The discipline's *enforcement* already lives in code (066 console + RunHandler) — this feature only relocates the *prose*. | ✅ Pass |
| **III. Orchestrator-Agnostic** | The revised agent still drives the same CLI; any orchestrator can launch `spectra run console`. No new vendor coupling. | ✅ Pass |
| **IV. CLI-First** | Orchestration is all named CLI commands (`run start`/`run console`/`run status`/`run finalize`). The console UI sits on top of the CLI. | ✅ Pass |
| **V. Simplicity (YAGNI)** | Pure prose + test revision; no new abstraction, no code, no dependency. Removes the chat loop (less surface). | ✅ Pass |

**Result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/067-execution-agent-console-revision/
├── plan.md              # This file
├── research.md          # Phase 0 — constraints + rewrite decisions
├── data-model.md        # Phase 1 — the prose artifacts + their "keep/rewrite/delete" map
├── quickstart.md        # Phase 1 — the new orchestrate flow + how to validate
├── contracts/
│   └── agent-skill-contract.md   # what the rewritten contract tests assert
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit.tasks)
```

### Source artifacts (repository root)

```text
src/Spectra.CLI/Skills/Content/
├── Agents/spectra-execution.agent.md   # REWRITE: loop-driver → orchestrator + on-call (≤200 lines)
└── Skills/spectra-execute.md           # REWRITE: remove show→wait→record loop + verdict table

tests/Spectra.CLI.Tests/Skills/
├── ExecuteSkillTests.cs                # REWRITE: assert orchestrate (start→console→on-call), no chat loop
├── ExecutionAgentPortTests.cs          # REWRITE: drop "ask-before-recording" loop asserts; keep no-Copilot + native-doc-lookup; add console + status-from-DB asserts
└── SkillsManifestTests.cs              # NOT TOUCHED (constraint envelope; must stay green)

docs/
├── cli-reference.md        # UPDATE: the `spectra-execute`/agent "present → wait → advance" line → orchestrate model
├── getting-started.md      # UPDATE: console pointer for manual execution
└── execution-agents.md     # UPDATE: console as the manual-run path (alongside MCP)
```

**Structure Decision**: No new projects or code files. The change set is two embedded-resource markdown
files, their two contract tests, and three docs. The guardrail/engine code and the rest of the test
corpus (including `SkillsManifestTests`, `ParityTests`, `GuardrailTests`, MCP corpus, Core) are untouched
and must stay green.

## Complexity Tracking

> No Constitution violations — table intentionally omitted.
