# SPECTRA — Architecture v2 (Claude Code only, interactive, subscription-first)

> **Status:** High-level shape, not a spec. Seeds the investigation MDs and `/speckit.specify` specs that follow.
> **Date:** 2026-06-04
> **Why now:** GitHub Copilot's usage-based billing broke the cost model. v2 re-homes all model work onto the *user's own interactive Claude Code session* and keeps everything else deterministic in the CLI. **No local models, no embeddings, no API providers — Claude Code + CLI, nothing else.**

---

## Governing constraint

Two rules shape everything:

1. **A human always drives, and everything runs on the interactive subscription path.** Never `claude -p`, the Agent SDK, GitHub Actions, or SPECTRA-as-a-third-party-app authenticating via subscription — as of the **June 15, 2026** billing split, those are *metered* at full API rates. The interactive Claude Code session in the IDE is the one path that stays on subscription limits.
2. **All intelligence lives in Claude Code. Nothing else runs a model.** No local model on the GPU, no embedding API, no OpenAI/Anthropic API provider. Every step that needs judgment is an interactive Claude turn on the user's subscription.

Consequences:
- The **only client is Claude Code in VS Code** (not Claude Desktop — no project/terminal/git context, no advantage for either role).
- Inference draws from the **user's own subscription**, which they already pay for daily dev work. SPECTRA sells scaffolding + a deterministic engine, not tokens.
- Marginal cost per generated test case ≈ zero *within the user's existing limits* — the only thing that survives the billing change for real users.

---

## The layers

There is no "local layer." Everything is one of three things: deterministic CLI, interactive Claude, or stateful MCP.

1. **Deterministic core — model-free CLI (C#).** Orchestration + persistence: prompt-compilation, file I/O (reading docs is plain file reads, *not* a model call), validation, indexing, coverage. The reliability backbone and the single place silent failures are caught. Model-free in the sense that *it* runs no model — the thinking happens in the agent.
2. **All model work — interactive Claude, main session.** Generation, criteria extraction, and any documentation judgment are interactive Claude turns. Subscription. **Correction from an earlier draft:** extraction and doc analysis are *not* free/offloaded — they consume the same subscription pool as generation. Anything that reads and reasons over a document costs subscription budget.
3. **Critic — Sonnet 4.6 subagent, fresh context, always runs.** Empirically confirmed (team testing) to give more useful critique than the cross-family alternative for *these* prompts and test cases. Subscription. **Model is config-driven, not hard-coded** — this is exactly the decision that needs regression-checking on every model/prompt change.
4. **Execution — Spectra.MCP.** Stateful state machine, opaque handles, human-in-the-loop, same VS Code client.

> **Note on the dual-model principle.** "Different families" was always a means, not the end; the end is reliable, uncorrelated judgment. Team testing showed same-family Sonnet critique reads as more useful. The residual risk a same-family critic does *not* remove is **correlated blind spots** (a defect both generator and critic silently miss). Measurable, not theoretical — a defect-injection bake-off measuring *catch rate against seeded defects* settles it. Until then, config-driven critic keeps the door open.

---

## Two surfaces: CLI and MCP (not "either/or")

Rule: **stateless transformation → CLI; stateful session → MCP.**

- **Authoring path → CLI** (Playwright-style, model-free): `generate-prompt`, `write-test`, `validate`, `coverage`, `list`, criteria scaffold. Transparent and debuggable ("run this command, show the output"), same surface in Claude Code, a bare terminal, and CI.
- **Execution → MCP, only here.** A state machine with a lifecycle *is* a session by definition; forcing it into stateless CLI calls would mean re-inventing MCP's session model.

Maps cleanly onto the demo: authoring = CLI, live driving of the Calculator via BELLATRIX / Nova = MCP. **Do not unify.**

---

## Coverage and index stay lexical — by design, not compromise

All three coverage dimensions are **identifier / exact-field matching, never semantic**, and this is the right model:

- **Documentation:** `source_refs` in test frontmatter matched against files in `docs/`.
- **Acceptance criteria:** `criteria` field cross-referenced against IDs in `_criteria_index.yaml` (`AC-042` vs `AC-042`).
- **Automation:** test IDs matched via regex scan patterns (`[TestCase("TC-001")]`).

Coverage counts links the generator already wrote explicitly at creation time. **Embeddings would be a regression here** — they introduce non-determinism and approximate "85% similar" matches into something currently exact and explainable. That fuzzy band is precisely where things silently pass as "covered" without being. Hard ID matching either finds the link or visibly shows it's missing.

Semantic judgment *does* exist in the pipeline — but at **generation** ("which criteria does this doc cover", "what edge cases are missing"), where it is the interactive Claude's job. Coverage only reports the result afterward. So semantics live where they belong (the model, at generation), not in reporting.

Net: index + coverage need **nothing** beyond the deterministic CLI — no embeddings, no API, no local model.

---

## The generation handoff inversion

The largest conceptual change in v2.

The CLI **stops calling a model** and **starts compiling a prompt.** From `doc + criteria + config` it deterministically assembles the grounded prompt — now a **versioned, diffable, unit-testable artifact** testable without burning tokens. The interactive agent does *only* the generative part. The CLI takes content back, **validates hard at the boundary**, then persists + indexes.

Because there is no programmatic `response_format`:
- The retry loop moves **out of the CLI and into SKILL / agent choreography**: invalid output → SKILL instructs the agent to redo with the *specific* validation error.
- Boundary validation is the only net — it must **fail loud, never silently repair.**

Cost lever: **the tighter the document context the prompt-compiler feeds, the less each test case weighs against the subscription limit.** With embeddings off the table, this is done by deterministic selection — feeding only the doc sections behind the given criteria, not whole files. Context discipline is now the primary cost control, since there is no retrieval layer to lean on.

Payoff for known bug classes: if `criteriaContext` is null (cf. Spec 045), the prompt-compiler refuses to emit the prompt *at that point*, instead of the bug surfacing three layers downstream.

**Implementation status (Spec 053, conceptual 052).** The two model-free halves are extracted and shipped as CLI commands:

- `spectra ai compile-prompt <suite>` → `Spectra.CLI.Generation.PromptCompiler`. Deterministic, token-free; emits the grounded prompt to stdout, writes nothing, and **refuses to emit** (exit 4, naming the missing input) when criteria/count/user-prompt are absent.
- `spectra ai ingest-tests <suite>` → `Spectra.CLI.Generation.GeneratedTestIngestor`. The fail-loud boundary: parses (no truncation salvage), validates with `TestValidator`, and persists valid batches via the unchanged `TestPersistenceService`. On any failure it persists nothing and returns a machine-readable `error_code` (`EMPTY_CONTENT`/`MALFORMED_JSON`/`TRUNCATED`/`NO_TESTS`/`SCHEMA_INVALID`) — the payload the retry skill (Spec 055) re-prompts against.

`CopilotGenerationAgent.BuildFullPrompt` now delegates to `PromptCompiler.Assemble`, so there is a single source of prompt truth. The literal removal of the model call at `GenerationAgent.cs:239` and the rewire of the in-CLI `ai generate` handler paths is deferred to the Spec 055 skill-wiring increment (the replacement compile→ingest surface exists first, by design).

---

## Orchestration layer

Copilot `.github/agents/` translates to Claude Code **skills** — target the current format `.claude/skills/<name>/SKILL.md` (the legacy `.claude/commands/` still works but is deprecated; skills give `/name` invocation *plus* optional auto-invocation, controlled by frontmatter). Two roles, not to be conflated:

- **Generation + authoring skills** — invoked into the *current* session (`/generate`, the authoring verbs). No isolation; they run in the main context.
- **Critic — a skill that runs in a subagent via `context: fork`** (fresh, isolated context window so it sees only the artifact + criteria, never the generator's reasoning). **The critic is invoked *explicitly as a mandatory step inside the generation skill's own procedure* — never via auto-invocation.** Auto-invocation is Claude's discretion and unreliable for an "always runs" guarantee; the guarantee comes from the parent skill mandating the call. (Confirmed: investigation MD §1.)

Discipline rules port with full force — same class as the existing `runInTerminal → awaitTerminal` rule:
- "ask BEFORE calling the tool, wait for the user's exact words, never fabricate notes"
- "do NOTHING between runInTerminal and awaitTerminal"

### Execution loop porting

Ports **conceptually 1:1**, because **state lives in the MCP server, not the agent.** `start_execution_run` creates the run, opaque handles hold position, `advance_test_case` records the verdict, the run survives lost connections (pause/resume/`get_execution_status`). The agent is effectively stateless between turns — so the multi-step pass/fail loop is just *conversational turns with a tool call between them*, Claude Code's native mode.

Real risk is the **opposite** of the obvious one: not that Claude Code can't wait, but that an eager agentic model **guesses a verdict and calls `advance_test_case` itself** instead of pausing for the human. The existing guardrails address exactly this and must carry over verbatim in spirit.

**Two setup requirements (confirmed in investigation MD §2–3):**
- **Pre-approve the MCP server** with `allowedTools: ["mcp__spectra__*"]` in `.claude/settings.json`. Without it, Claude Code's default prompts fire on *every* MCP tool call and shred the loop. This is orthogonal to the intentional human-verdict pause — the pause is the agent asking a text question and waiting (controlled by the skill's instructions), not a permission prompt. Pre-approval removes the friction without removing the pause. This is also what replaces Copilot's `askQuestion` / `askForConfirmation`: plain-text question + wait, so the user answers with a verdict, notes, or a screenshot path.
- **Screenshots: standardize on MCP-saves-to-file + path reference.** The existing `save_clipboard_screenshot` (capture → write file → return path) sidesteps Claude Code's terminal-paste UI entirely; the agent reads the image by path. Native clipboard paste works on **macOS and native Windows** (both in use); it is fragile only under **WSL**, which is not part of the target setup. So the file-path approach is the standard and clipboard paste is an unneeded fallback.

---

## Cost / usage reality (corrected)

With no local offload, **every intelligent step hits the one subscription pool**: extraction, any doc-reasoning, generation, the always-on critic, and execution turns. The earlier "doc analysis is local / free" framing is wrong and removed. Implications:

- Usage is shared with the user's normal Claude Code dev work — SPECTRA competes for the same window.
- Per-test-case weight is **not constant**; it scales with how much document context the prompt-compiler feeds. Tight context = cheaper cases.
- The binding constraint for sustained work is the **weekly cap**, not the 5-hour window.
- The honest number comes from measurement, not estimation: instrument a small batch via `debug.enabled` token telemetry (e.g. 10 cases with critic, narrow vs wide doc context) and calibrate a real "cases per session" curve per plan.

---

## Verified mechanics (was: open questions) — see investigation MD

All four gating mechanics were checked against current Claude Code docs (`INVESTIGATION-claude-code-mechanics.md`):

1. **Subagents & skills — GREEN.** `.claude/skills/` is the current format; `context: fork` gives subagent isolation; "always runs" = explicit mandatory invocation from the generation skill.
2. **Screenshot / image paste — YELLOW → handled.** File-path reference is the robust, cross-platform method; `save_clipboard_screenshot` already does this. Native paste fine on macOS + Windows; WSL fragility is out of scope.
3. **Permission model — GREEN.** Pre-approve `mcp__spectra__*`; the human-verdict pause is separate from permission prompts.
4. **Interactive MCP billing — GREEN (inferred).** The MCP server burns zero Claude tokens; cost is the interactive agent's turns = subscription. Holds only while genuinely interactive/human-driven (which the governing constraint enforces). No explicit doc guarantee — re-verify if Anthropic publishes MCP billing language.

Known gap with no equivalent: **Copilot Spaces** docs lookup (`get_copilot_space`, `execution.copilot_space`). Needs a conscious replacement (file reads / docs index). Not a blocker, but a feature loss if ignored.

---

## What does NOT port (Copilot-isms)

- `model: GPT-4o` in agent frontmatter → Claude via config.
- `askQuestion` / `askForConfirmation` prohibition → translate to Claude Code's confirmation model, don't copy.
- `github/get_copilot_space`, `list_copilot_spaces` → no equivalent (see gap above).

---

## Sequencing (investigation-first)

1. ~~**Doc verification** of the four Claude Code mechanics~~ — **DONE** (`INVESTIGATION-claude-code-mechanics.md`). All four GREEN/handled.
2. **Investigation MD** on the real generation path in code (`Spectra.CLI` / `Spectra.Core`) — where the critic is injected today, what the current model-invocation seam looks like — grounded in file + line evidence. May require Anton to point at specific files not in the project docs. **← next.**
3. **Only then** specs (`/speckit.specify`). No spec written against memory or assumptions.

## Target environment

- Anton: **native Windows** (standard, no WSL). Other users: also **macOS** (standard). Both handle screenshots and clipboard natively; WSL — the only fragile case — is not in the target set. The MCP-saves-to-file screenshot path works identically across both regardless.

---

## One-line summary

Deterministic model-free CLI + interactive Claude (generation, extraction, all doc reasoning) + always-on Sonnet critic subagent + MCP execution + lexical coverage — everything on the user's interactive Claude Code subscription, one VS Code client, no local models, no embeddings, no API, nothing metered by default.
