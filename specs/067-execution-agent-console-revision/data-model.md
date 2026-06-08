# Phase 1 Data Model: Execution Agent / SKILL Revision

This feature has **no data entities** — it introduces no schema, storage, or model. The "entities" here
are the two bundled prose artifacts being rewritten and the run state the agent *reads* (unchanged). This
document records the keep/rewrite/delete map (from `B-agent-skill-revision.md` B10) that drives the
rewrite.

---

## Artifact 1 — Execution Agent (`spectra-execution.agent.md`)

The agent definition (YAML frontmatter + prose). Rewritten from loop-driver to orchestrator + on-call.
Must stay **≤ 200 lines** and keep its delegation + quickstart references (R2).

| Section (current) | Action | Notes |
|---|---|---|
| Frontmatter (`name`, `description`, tools Read/Bash/Glob/Grep) | **Keep** (tweak `description` to "orchestrates … launches the console") | tools unchanged |
| IMPORTANT RULES — dialog/popup ban, never fabricate, never auto-advance | **Rewrite** | keep "plain text only / never use dialog tools"; replace auto-advance/fabricate-in-loop with "the console enforces verdict discipline; never record/fabricate a verdict in chat" |
| Execution Workflow steps 1-4 (list-active, list-suites, ask, start) | **Keep** | the orchestrator's selection + start |
| Execution Workflow step 5 (per-test show→present→collect loop) | **Replace** | → "launch `spectra run console`, hand over the URL; the tester drives the run in the browser" |
| Execution Workflow steps 6-8 (finalize, report, bug logging) | **Keep** | finalize on request; report; bug logging unchanged |
| Test Presentation format block | **Delete** | presentation is the console's job |
| Result Collection table + CRITICAL ask-before-running | **Delete** | verdict collection is the console's buttons |
| Screenshot Handling | **Re-home** | "screenshots attach via the console drop; chat-paste fallback: `spectra run screenshot-clipboard`" |
| Proactive Behavior (summary every 5 tests, etc.) | **Rewrite/trim** | no per-test cadence; keep "on request, `spectra run summary`/`status`" |
| Error Handling (RECONSTRUCTION_FAILED, paused, resume-by-id) | **Keep** | orchestrator still surfaces these |
| Documentation lookup (native Read/Grep/Glob from `source_refs`) | **Keep** | the on-call job (FR-005) |
| Smart Test Selection | **Keep** | selection is the orchestrator's job |
| CLI Tasks delegation table (incl. `spectra-update`) | **Keep** | pinned by `SkillsManifestTests` |
| New: "On-call" subsection | **Add** | read `spectra run status` (DB) for current state, never the console page (FR-006) |

---

## Artifact 2 — Execution SKILL (`spectra-execute.md`)

The step-by-step SKILL the agent follows. Rewritten to remove the loop. Must contain `spectra`, no
`### Tool call N`, no `terminalLastCommand` (R2).

| Section (current) | Action | Notes |
|---|---|---|
| Frontmatter (`name: spectra-execute`, tools) | **Keep** (refresh `description` to orchestrate) | count stays 15 |
| Iron rules (present one, WAIT, never auto-advance, never fabricate) | **Rewrite → server guarantee** | "the console enforces verdict discipline; you never record a verdict in chat" |
| Step 1 list-suites | **Keep** | |
| Step 2 start | **Keep** | |
| Step 3 show + present + "Result? (pass/fail/blocked/skip)" | **Replace** | → "launch `spectra run console`; hand over the URL" |
| Step 4 WAIT + pass/fail/blocked/skip command mapping | **Delete** | verdicts come from the console |
| Step 5 finalize + report | **Keep** (renumber) | finalize on request |
| Result mapping table | **Delete** | console's buttons |
| Screenshots (clipboard/file) | **Re-home** | console drop primary; chat fallback brief |
| Other commands (status, retest, pause/resume/cancel) | **Keep** | on-call + lifecycle |
| Trigger phrases | **Keep** | |
| Refuse to do (record/advance/invent) | **Rewrite** | "never record a verdict in chat / never fabricate notes — the console is the verdict channel" |
| New: "On-call" step | **Add** | `spectra run status` from DB; answer from source docs |

---

## Run state (read-only — unchanged)

The agent reads, never writes, run state for on-call answers:
- **Current test / progress** ← `spectra run status` / `spectra run show` → `RunHandler` → `GetStatusAsync`
  (reconstructs from `.execution/spectra.db`, session-free per 064).
- **Source docs** ← the test's `source_refs` + `docs/`, via native Read/Grep/Glob.

No field, schema, or storage changes. The verdict-write path (`AdvanceTestAsync` →
`ResultRepository.UpdateStatusAsync`) is exercised only by the console, not the agent.
