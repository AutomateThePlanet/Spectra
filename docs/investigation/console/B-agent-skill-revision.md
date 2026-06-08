# B. Execution Agent / SKILL Revision (Spec 2)

Evidence for the second spec: how the shipped execution agent + SKILL change once the console owns the
per-test loop. **Hard-gates on Spec 1** (the agent cannot launch a console that does not exist).

---

## B9. Current execution agent + SKILL — they drive present → verdict → advance today

**Agent:** `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`
- It executes the loop itself. **Execution Workflow** step 5 (`:35`): *"For each test: run `spectra run
  show --output-format json`, present it, collect the result (see below), show progress."*; finalize at
  step 6 (`:36`).
- **Result Collection** table (`:59-68`) maps the human's words to commands —
  `spectra run advance --status pass` immediately; for fail/blocked/skip "Reply … — wait — `advance
  --status …`". The **CRITICAL** line (`:68`): *"For FAIL/BLOCKED/SKIP, ask BEFORE running the command.
  Never invent notes."*
- Test presentation format at `:40-57` (numbered steps, "Result? (pass/fail/blocked/skip)").

**SKILL:** `src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md`
- **Iron rules (human-in-the-loop)** (`:13-18`): *"Present ONE test at a time. WAIT for the human's
  verdict before advancing. NEVER auto-advance … NEVER fabricate a result or a failure note …"*
- **Workflow** (`:20-51`): Step 3 (`:33-37`) `spectra run show` + present; Step 4 (`:39-43`) "WAIT for
  the human's verdict. Then record it" → pass/fail/blocked/skip command mapping; loop "back to Step 3
  until `next_test` is null" (`:45`); Step 5 finalize (`:47-51`).
- **Refuse to do** (`:82-86`): *"Record a verdict the user did not give. Advance past a test the user
  has not judged. Invent failure/blocking/skip notes."*

So today **the agent/SKILL IS the per-test loop driver.**

---

## B10. Role shift — from loop-driver to orchestrator + on-call

When the console exists, the per-test loop moves to the **browser buttons**. The agent's job becomes
three things:

1. **Select tests + start the run.** Keep: `spectra run list-active` (`agent:31`),
   `list-suites` (`:32`), `start {suite} --priorities …` (`:34`), and the **Smart Test Selection**
   block (`agent:99-108`) + `spectra run selections` / `--selection` (`agent:103`).
2. **Launch the detached console.** New: after `start`, launch `spectra run console` (the Spec-1
   subcommand, A1/A7) and hand the user the local URL.
3. **Be on-call.** When the user opens chat mid-run, read the current state with `spectra run status`
   (B12) rather than driving the loop; answer doc questions from `source_refs` via native file reads
   (the agent's **Documentation lookup** block, `agent:91-97`, is already native Read/Grep/Glob and
   stays); finalize on request.

**Rewrite vs delete vs keep:**

| Text | Action | Why |
|---|---|---|
| SKILL Workflow steps 3-4 (`spectra-execute.md:33-45`) — show → WAIT → record loop | **Rewrite** | The console now presents the test and records the verdict via buttons. |
| SKILL "Result mapping" table (`:53-60`) | **Delete** | Verdict collection is the console's buttons, not chat. |
| SKILL "Iron rules" / "Refuse to do" (`:13-18`, `:82-86`) | **Rewrite → restate as server guarantees** | The discipline still holds, but it's now enforced by the server (B11), not by the agent in a loop. |
| Agent "Execution Workflow" step 5 (`agent:35`) + "Result Collection" (`:59-68`) + presentation (`:40-57`) | **Rewrite/Delete** | Per-test loop and verdict collection move to the console. |
| Agent/SKILL `start`, `finalize`, `selections`, smart selection, status, resume-by-run-id | **Keep** | These are the orchestrator's remaining job. |
| Screenshot guidance (`agent:70-72`, `skill:62-68`) | **Keep but re-home** | Screenshots are now attached via the console drop (A4); chat-paste fallback may remain. |

*(Stale-text note for the spec: both files currently assert the agent drives the loop — that prose is
stale the moment the console ships. Logged in `00-summary.md`.)*

---

## B11. Guardrail migration — from prose-in-the-loop to a property of the server

**Where the guardrails live today — BOTH prose and code:**
- **Prose:** `spectra-execute.md:13-18` (Iron rules) and `:82-86` (Refuse to do);
  `spectra-execution.agent.md:24-26` (*"NEVER use dialog/popup tools. NEVER fabricate failure notes …
  NEVER auto-advance …"*).
- **Code (already mechanical):** `RunHandler.AdvanceAsync` (`src/Spectra.CLI/Commands/Run/RunHandler.cs:199-215`)
  rejects, deterministically, before any DB write:
  - no `--status` → `STATUS_REQUIRED` (`:204-205`) — *"The loop never advances without an explicit
    verdict."*
  - unparseable status → `INVALID_STATUS` (`:207-208`)
  - fail/blocked/skipped without notes → `NOTES_REQUIRED` (`:210-211`)
  - `SkipAsync` requires `--reason` (`:221-222`).
  - The engine also validates the state transition: `AdvanceTestAsync` calls `StateMachine.Transition`
    and throws on an illegal move (`ExecutionEngine.cs:281-285`).

  Note: the engine validates verdict **syntax and transition legality, not the verdict's *source*** — it
  trusts that whoever calls it supplied a real human verdict. Today the SKILL/agent prose carries the
  "a human actually said this" promise.

**After the console:** the **buttons are the only path to a verdict.** The guardrail therefore becomes a
**property of the server**, not the prompt:
- The console write-back endpoint must replicate the same mechanical checks as `RunHandler.AdvanceAsync`
  (explicit status; notes/reason required for fail/blocked/skip; never infer a verdict). A click is the
  human action that satisfies the "source" promise the prose used to carry.
- The agent prose shrinks to "the console enforces the verdict discipline; never fabricate a verdict on
  the user's behalf in chat."

This is the central design move of Spec 2: **the human-in-the-loop guarantee stops being an instruction
to a model and becomes an invariant of the transport.**

---

## B12. Status surfacing — the agent reads SQLite via `spectra run status`, never the page/URL

The SKILL already reads run state session-free: *"Status / where am I: `spectra run status
--output-format json`"* (`spectra-execute.md:72`); the agent relies on the same and on durability:
*"Connection/process loss → state is durable in SQLite; resume by run id."* (`spectra-execution.agent.md:89`).

The path is session-free and lossless: `RunHandler.StatusAsync` (`RunHandler.cs:120-148`) builds a fresh
`RunServices`, resolves the active run (`ResolveRunIdAsync` → `GetActiveRunAsync`, `RunHandler.cs:599-604`),
and calls `Engine.GetStatusAsync` (`ExecutionEngine.cs:127`), which reconstructs the queue from the DB on
a cold process (spec 064). The `RunHandler` header documents the contract: *"Each method is stateless …
A short-lived process reconstructs the queue from the DB (Spec 064), so behavior is identical to the
long-lived MCP server."* (`RunHandler.cs:14-18`).

**Finding:** when the agent is on-call, it gets "what's the current test" from `spectra run status`
(SQLite), **not** from the console page or its URL. The console and the agent are two readers of the same
DB; neither is the other's source of truth.
