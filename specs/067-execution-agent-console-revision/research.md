# Phase 0 Research: Execution Agent / SKILL Revision

This feature is a prose + test revision. There are no technology unknowns; the "research" is the exact
constraint envelope the rewrite must satisfy and the decisions that follow. Grounded in the live files
(read 2026-06-08) and `docs/investigation/console/B-agent-skill-revision.md`.

---

## R1. What changes vs what is frozen

**Decision**: The change set is exactly: `spectra-execution.agent.md`, `spectra-execute.md`,
`ExecuteSkillTests.cs`, `ExecutionAgentPortTests.cs`, and three docs. **No engine/handler/guardrail code.**

**Rationale**: The verdict discipline is already enforced in code — `RunHandler.AdvanceAsync`
(`RunHandler.cs:204-211`: `STATUS_REQUIRED` / `INVALID_STATUS` / `NOTES_REQUIRED`) and the Spec 066
console endpoint replicate it; the engine validates the transition (`StateMachine.Transition`). So moving
the *prose* home requires touching no code (FR-010, SC-006). If a `GuardrailTest` or `ParityTest` breaks,
the rewrite reached into code it must not.

**Alternatives considered**: Folding a "verdict source" assertion into the engine — rejected: out of
scope, and the console click already satisfies the source promise (066). Deleting the SKILL entirely —
rejected: the agent still needs an execution SKILL for the orchestrate flow; `SkillContent.All` count
must stay 15.

---

## R2. The constraint envelope — `SkillsManifestTests` must stay green UNCHANGED

**Decision**: Treat `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs` as a frozen oracle the
rewrite must satisfy (it is NOT in the permitted-to-change set — only `ExecuteSkillTests` +
`ExecutionAgentPortTests` are). Concretely the rewrite MUST keep:

| Assertion | Constraint on the rewrite |
|---|---|
| `SkillContent_HasAllSkills` | `SkillContent.All.Count == 15`; `spectra-execute` still present. Rewrite in place — don't add/remove skills. |
| `AgentContent_HasAllAgents` | `AgentContent.All.Count == 3`; `spectra-execution.agent.md` still present. |
| `ExecutionAgent_LineCount_Within200` | Agent file **≤ 200 lines**. |
| `AllSkills_ContainSpectraCommand` | Execute SKILL contains `spectra`. |
| `AllSkills_UseStepNFormat_NotToolCallN` | No `### Tool call N` headings. |
| `AllSkills_DoNotUse_TerminalLastCommand` | No `terminalLastCommand` in the SKILL body. |
| `ExecutionAgent_ContainsUpdateDelegation` | Agent still references `spectra-update` (keep the CLI Tasks delegation table). |
| `ExecutionAgent_References_QuickstartSkill` | Agent still references `spectra-quickstart`. |
| `Agents_DoNotContain_DuplicatedCliCodeBlocks` / `Agents_DoNotContain_UpdateCliCodeBlocks` | No fenced code blocks containing the delegation CLI markers (e.g. `spectra ai analyze --coverage`, `spectra ai update`) in any agent. |
| `spectra-execute` excluded from `--no-interaction` / `--output-format json` / `--verbosity quiet` per-line checks | `spectra run …` command lines need NOT carry those flags (they already don't). Safe to keep `--output-format json` on run commands, but not required. |

**Rationale**: These assertions encode bundling invariants unrelated to the loop→orchestrate shift;
preserving them proves the rewrite stayed within scope. They are the reason the agent rewrite must stay
compact (≤200 lines) and keep the delegation/quickstart references.

---

## R3. Console launch + hand-off

**Decision**: After `spectra run start`, the agent runs **`spectra run console`** (Spec 066) and hands
the tester the printed local URL (`http://127.0.0.1:<port>/`). It tells the tester to drive the run in
the browser. If a console is already running, 066 prints the existing URL (`CONSOLE_ALREADY_RUNNING`) —
the agent hands that over rather than double-launching.

**Rationale**: 066 shipped the detached console; this is the new orchestration step (FR-003). The console
is local/ephemeral and survives the agent session ending — so the agent can step back.

---

## R4. On-call status from the database, not the page

**Decision**: When the tester asks mid-run, the agent reads **`spectra run status --output-format json`**
(and `spectra run show` for full detail), which reconstructs from SQLite (session-free, spec 064). It
answers doc questions from the test's `source_refs` via native Read/Grep/Glob. It NEVER scrapes the
console page/URL for state.

**Rationale**: The console holds no authoritative state (066 FR-002); the DB does. Agent and console are
two readers of the same DB (FR-006, B12). Keeps answers correct even in a fresh agent session.

---

## R5. Guardrail prose → server-guarantee restatement

**Decision**: Replace the SKILL "Iron rules" / "Refuse to do" loop instructions and the agent's
`NEVER auto-advance` / `Result Collection` rules with a short statement: *the console enforces verdict
discipline (explicit status; notes required for fail/blocked/skip; no auto-advance; no inferred verdict);
the agent never records or fabricates a verdict in chat.* Keep "never use dialog/popup tools; plain text."

**Rationale**: B11 — the guarantee becomes a property of the transport. The agent's residual rule is the
single negative: don't record verdicts in chat. Less prose, same integrity.

---

## R6. Screenshot re-home; keep doc-lookup; keep selection/lifecycle

**Decision**: Screenshots are attached via the console drop/paste (066); the agent's
`screenshot-clipboard` guidance is demoted to a brief chat-paste fallback. KEEP: the Documentation-lookup
block (native reads), Smart Test Selection, `selections`/`--selection`, `start`, `status`, `finalize`,
pause/resume/cancel, resume-by-run-id, the CLI Tasks delegation table, help/quickstart references.

**Rationale**: B10 keep/rewrite/delete map. These are the orchestrator's remaining job and several are
pinned by `SkillsManifestTests` (R2).

---

## R7. Stale docs

**Decision**: Update `docs/cli-reference.md:529-530` (the "`spectra-execute` SKILL and the execution agent
drive this loop … present → wait for verdict → advance" line) to the orchestrate model, and add a console
pointer to `getting-started.md` and `execution-agents.md` for the manual-run path. `copilot-spaces-setup.md`
is out of scope (Copilot Spaces).

**Rationale**: FR-009; the cli-reference line is the only main-doc text that asserts the chat-driven loop.
Broader MCP-centric staleness in `execution-agents.md` is acknowledged but only the manual-run path is in
scope here.

---

## Summary of decisions

| # | Topic | Decision |
|---|---|---|
| R1 | Change scope | 2 markdown + 2 tests + 3 docs; zero code |
| R2 | Constraint envelope | `SkillsManifestTests` frozen; agent ≤200 lines, keep update/quickstart refs, no CLI code blocks in agents |
| R3 | Console launch | `spectra run console` after `start`; hand over local URL |
| R4 | On-call state | `spectra run status` (DB), never the page |
| R5 | Guardrail prose | restate as console guarantee; agent's only rule: never record a verdict in chat |
| R6 | Keep/re-home | screenshots → console drop; keep selection/lifecycle/doc-lookup/delegation |
| R7 | Docs | fix `cli-reference.md:529-530`; console pointers in getting-started + execution-agents |
