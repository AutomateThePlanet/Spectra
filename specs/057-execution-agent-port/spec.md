# Feature Specification: Execution Agent Port (independent pista)

**Feature Branch**: `057-execution-agent-port`
**Created**: 2026-06-05
**Status**: Draft
**Input**: User description: "Spec 056 — Execution agent port. Re-author the execution agent for Claude Code: drop the GPT-4o pin and Copilot Spaces, replace doc-lookup with native file reads, add the client-side MCP allowlist — the 25-tool MCP engine reused verbatim, no server change."

> **Series note**: This is migration spec **5 of 6** (the 052–057 series) and the **independent
> pista** — it has **no dependency on pista A** (the four preceding generation/critic/orchestration
> specs) and could have been built in parallel from day one. The conceptual numbering in the source
> material calls this "Spec 056"; in the repository it lives in directory `057-` because of the
> established one-step offset (conceptual spec *N* → directory *N+1*). Unlike the pista-A specs, this
> one is **not** additively deferred: the execution agent is pure orchestration prose with **no
> in-process model call to remove**, and the MCP engine is already client-agnostic, so this spec
> **completes** its surface in one pass — the port is done here, nothing is left for a later spec.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The execution agent drives the unchanged MCP engine from Claude Code (Priority: P1)

A tester runs an interactive execution session in Claude Code. The agent starts a run, presents one
test at a time, records the tester's verdict, and advances — all by calling the **existing** MCP
tools (`start_execution_run`, `get_test_case_details`, `advance_test_case`, `finalize_execution_run`,
…). The 25-tool MCP execution engine, its state machine, the in-memory run queue, the SQLite result
store, and the path-based screenshot tools are reused **verbatim** — there is no server-side change.
Because state lives in the server (keyed by run and test handles), the agent is stateless between
turns and the pass/fail loop is just conversational turns with a tool call between them.

**Why this priority**: This is the load-bearing premise of the whole port — that the execution
surface is client-agnostic and Claude Code can drive it as-is. If anything on the server had to
change, the "independent pista, engine untouched" claim would be false. It is P1 because every other
story assumes the engine is reused unchanged.

**Independent Test**: Confirm the execution agent's procedure calls the existing MCP tool names with
no server-side modification, and that the entire `Spectra.MCP` engine/tool test corpus passes
unchanged (a break signals a regression in something that must not change).

**Acceptance Scenarios**:

1. **Given** the ported execution agent, **When** it drives a run, **Then** it calls the MCP tools
   unchanged (`start_execution_run`, `get_test_case_details`, `advance_test_case`, …) with no
   server-side change.
2. **Given** a failed step with a screenshot, **When** evidence is captured, **Then** it goes through
   the existing path-based `save_screenshot` / `save_clipboard_screenshot` tools — unchanged.
3. **Given** a lost connection mid-run, **When** the tester resumes, **Then** the server-held run
   state is intact (pause/resume/status work) with no agent-side state.

---

### User Story 2 - The agent is de-Copilot'd; doc-lookup is native file reads (Priority: P1)

A maintainer inspects the ported execution agent and finds no GitHub Copilot coupling. The `model:
GPT-4o` pin and `disable-model-invocation` are gone (the interactive session selects the model). The
Copilot Spaces tools (`github/get_copilot_space`, `github/list_copilot_spaces`) and the "Copilot
Spaces Documentation" section are removed; when a tester asks about a step or expected result during
execution, the agent **reads the relevant documentation file(s) directly** rather than querying a
Copilot Space. The now-dead `execution.copilot_space` / `copilot_space_owner` configuration is
removed. The Copilot terminal/confirmation discipline (`runInTerminal`/`awaitTerminal`,
`askQuestion`/`askForConfirmation` ban) is translated to Claude Code in spirit.

**Why this priority**: This is the substance of the port — removing the three classes of Copilot-ism
and replacing the one genuine feature loss (Spaces doc-lookup) with a native equivalent (file reads).
Without it, the agent is non-functional or misleading under Claude Code (a GPT-4o pin and `github/*`
tools mean nothing there). P1 because it is the core re-authoring the spec exists to deliver.

**Independent Test**: Inspect the ported agent: 0 occurrences of `model: GPT-4o`,
`disable-model-invocation`, `github/get_copilot_space`, `github/list_copilot_spaces`, and the Copilot
Spaces section; the doc-lookup guidance instructs direct file reads. Confirm the
`execution.copilot_space*` config fields no longer exist.

**Acceptance Scenarios**:

1. **Given** the ported agent, **When** its frontmatter is inspected, **Then** it contains no `model:
   GPT-4o` pin and no `disable-model-invocation`.
2. **Given** the ported agent, **When** its tool list and body are inspected, **Then** the Copilot
   Spaces tools and the Spaces doc-lookup section are gone.
3. **Given** a doc question during execution, **When** the agent resolves it, **Then** it reads the
   relevant documentation file(s) directly — no Copilot Space.
4. **Given** the configuration model, **When** it is inspected, **Then** the
   `execution.copilot_space` / `copilot_space_owner` fields no longer exist.

---

### User Story 3 - The MCP allowlist removes per-call permission prompts (Priority: P2)

A tester runs a long execution session and is not interrupted by a permission prompt on every MCP
tool call. The client-side `mcp__spectra__*` allowlist is installed in `.claude/settings.json`
(`permissions.allow`) so the multi-step pass/fail loop runs smoothly — while the intentional
human-verdict pause (US4) still happens. This allowlist is a client setting only; the server enforces
nothing. It is distinct from the existing `Bash(spectra-mcp:*)` entry, which pre-approves a bash
command name, not the MCP tool namespace.

**Why this priority**: Without the allowlist, every one of the dozens of MCP calls in a run would
prompt for permission, making the human-in-the-loop loop unusable. It is P2 rather than P1 because it
is a friction-removal setting layered on top of the functional port (US1/US2) — the agent works
without it, just with prompts — and because the allowlist is a small, self-contained settings change.

**Independent Test**: Confirm `.claude/settings.json` contains an `mcp__spectra__*` entry under
`permissions.allow`, and that it is a distinct entry from the existing `Bash(spectra-mcp:*)` (the two
are not conflated).

**Acceptance Scenarios**:

1. **Given** the installed allowlist, **When** the agent calls an MCP tool during a run, **Then** no
   per-call permission prompt fires.
2. **Given** the settings file, **When** it is inspected, **Then** the `mcp__spectra__*` allowlist
   entry is present under `permissions.allow` and is distinct from `Bash(spectra-mcp:*)`.
3. **Given** the allowlist is present, **When** a test result is presented, **Then** the intentional
   human-verdict pause still happens — the allowlist removes tool-permission prompts, not the
   deliberate wait for the tester's verdict.

---

### User Story 4 - The human-verdict pause and guardrails are preserved (Priority: P1)

A tester relies on the agent to never decide pass/fail for them. After the port, the agent presents a
result, asks for the verdict in plain text, and **waits** — it MUST NOT fabricate a verdict, invent
failure notes, or call `advance_test_case` / `skip_test_case` on its own. For FAIL / BLOCKED / SKIP it
asks first and waits for the tester's exact words before recording. This human-in-the-loop guardrail
is carried over verbatim in spirit from the Copilot agent; the `askQuestion`/`askForConfirmation` ban
becomes "use plain text" (which the agent already does), and the "do NOTHING while waiting" discipline
ports to Claude Code's model.

**Why this priority**: The verdict pause is the integrity guarantee of manual execution — a tester
must be the one who decides each outcome. If the port let the agent auto-advance or guess a verdict,
the recorded results would be untrustworthy. P1 because it protects the core meaning of a manual test
run.

**Independent Test**: Inspect the ported agent: it instructs presenting the result and waiting for the
tester's plain-text verdict; it forbids fabricating a verdict/notes and auto-advancing; for
FAIL/BLOCKED/SKIP it asks before recording.

**Acceptance Scenarios**:

1. **Given** a presented test result, **When** the agent awaits the outcome, **Then** it pauses for
   the tester's plain-text verdict and does not call `advance_test_case` itself.
2. **Given** a FAIL / BLOCKED / SKIP outcome, **When** it is recorded, **Then** the agent first asks
   for the reason and waits for the tester's exact words — it never invents notes.
3. **Given** the ported agent, **When** its guardrails are inspected, **Then** the no-fabrication and
   no-auto-advance rules are present and the dialog/popup-tool ban reads as "use plain text."

---

### Edge Cases

- **Two execution-agent copies**: the execution agent ships from more than one bundled source (the
  `Skills/Content/Agents/` bundled file and the `Agent/Resources/` legacy resource). Both must be
  ported consistently — a port that leaves one copy carrying Copilot Spaces / GPT-4o is a defect.
- **Spaces feature loss, not just cosmetics**: Copilot Spaces doc-lookup has no Claude Code
  equivalent. Replacing it with "read the file directly" is a deliberate functional substitution, not
  a no-op deletion — the agent must still be able to answer a step/expected-result question, just by
  reading the source doc.
- **Allowlist confusion**: `mcp__spectra__*` (the MCP tool namespace) must not be conflated with
  `Bash(spectra-mcp:*)` (a bash command name). Adding one is not adding the other.
- **Server must not change**: any change under `Spectra.MCP` is out of bounds — if a port edit seems
  to require a server change, the design is wrong (the engine is already client-agnostic).
- **Allowlist removes prompts, not the pause**: the allowlist must not be written so broadly that it
  suppresses the intentional human-verdict wait — it gates tool-permission prompts only.
- **Config removal is safe**: removing `execution.copilot_space*` must not break config
  deserialization for existing config files that still carry those keys (unknown keys are ignored).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The MCP execution engine and all 25 tools MUST be reused verbatim — there MUST be no
  server-side change in `Spectra.MCP` (engine, tool registry, state machine, result store, screenshot
  tools).
- **FR-002**: The execution agent MUST be re-authored for Claude Code: remove `model: GPT-4o` and
  `disable-model-invocation`; the interactive session selects the model.
- **FR-003**: Copilot Spaces MUST be replaced with native file reads: remove the
  `github/get_copilot_space` / `github/list_copilot_spaces` tools and the Copilot Spaces doc-lookup
  section; the agent reads documentation file(s) directly to answer step/expected-result questions.
  The dead `execution.copilot_space` / `copilot_space_owner` configuration fields MUST be removed.
- **FR-004**: The human-verdict pause MUST be preserved: the agent presents a result, asks in plain
  text, and waits; it MUST NOT fabricate a verdict or notes and MUST NOT auto-advance
  (`advance_test_case` / `skip_test_case` are called only after the tester gives the outcome, and for
  FAIL/BLOCKED/SKIP only after the reason is given). The `askQuestion`/`askForConfirmation` ban
  becomes "use plain text"; the `runInTerminal`/`awaitTerminal` "do NOTHING while waiting" discipline
  carries over in spirit.
- **FR-005**: The client-side `mcp__spectra__*` allowlist MUST be added to `.claude/settings.json`
  (`permissions.allow`) so MCP tool calls run without per-call permission prompts. This is a client
  setting only — the server enforces nothing — and it MUST be a distinct entry from the existing
  `Bash(spectra-mcp:*)` (a bash command name, not the MCP namespace).
- **FR-006**: Screenshot capture MUST continue through the existing path-based `save_screenshot` /
  `save_clipboard_screenshot` tools — no change (this is already the target method).
- **FR-007**: The execution agent MUST install as a Claude Code artifact under `.claude/` (completing
  the relocation the preceding orchestration spec deliberately left on `.github/` for the execution
  agent), consistent with how the other ported authoring artifacts install.

### Reused Verbatim *(must not be modified)*

- **The entire `Spectra.MCP` server**: the tool registry, all 25 tools, the execution engine, the
  in-memory run queue, the state machine, the SQLite result store, and the path-based screenshot
  tools. Confirmed client-agnostic — reused unchanged.
- **The MCP tool contract and method names**: the agent addresses the same bare method names
  (surfaced to the client as `mcp__spectra__<name>`); the wire contract is unchanged.
- **The existing `Bash(spectra-mcp:*)` settings entry**: left as-is; the new `mcp__spectra__*` entry
  is added alongside it, not in place of it.

### Key Entities

- **Execution agent**: The orchestration artifact (prose `.md`) that drives an interactive execution
  session by calling MCP tools — re-authored for Claude Code (no model pin, no Copilot Spaces, native
  file reads, preserved verdict pause).
- **MCP execution engine + 25 tools**: The client-agnostic server surface (run management, test
  execution, reporting, data) reused verbatim — the agent's only execution mechanism.
- **MCP allowlist entry**: The client-side `mcp__spectra__*` pre-approval in `.claude/settings.json`
  that removes per-call permission prompts for the run loop; distinct from `Bash(spectra-mcp:*)`.
- **Human verdict**: The tester's plain-text pass/fail/blocked/skip outcome (plus reason for the
  non-pass cases) that the agent must wait for and never fabricate.
- **Execution config**: The configuration block losing its dead `copilot_space` / `copilot_space_owner`
  fields once Spaces is replaced by file reads.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The MCP engine and 25 tools are unchanged: 0 server-side modifications in `Spectra.MCP`,
  and 100% of the `Spectra.MCP` engine/tool/state-machine/screenshot test corpus passes without
  modification.
- **SC-002**: The ported execution agent contains 0 occurrences of `model: GPT-4o`,
  `disable-model-invocation`, `github/get_copilot_space`, `github/list_copilot_spaces`, and the
  Copilot Spaces doc-lookup section — across **every** bundled copy of the agent.
- **SC-003**: A doc question during execution is answered by a direct file read in 100% of cases — 0
  Copilot Space lookups remain in the agent.
- **SC-004**: The `execution.copilot_space` / `copilot_space_owner` config fields no longer exist (0
  occurrences in the configuration model), and configuration deserialization still succeeds for files
  that carry the (now-ignored) legacy keys.
- **SC-005**: `.claude/settings.json` contains the `mcp__spectra__*` allowlist entry under
  `permissions.allow`, distinct from `Bash(spectra-mcp:*)`; with it installed, an execution run issues
  0 per-call MCP permission prompts while the human-verdict pause still occurs.
- **SC-006**: The verdict-pause guardrails are present: the ported agent never fabricates a verdict or
  notes and never auto-advances — the no-fabrication / no-auto-advance / ask-before-recording rules
  are all present and inspectable.
- **SC-007**: Screenshot capture is unchanged: evidence is captured via the existing path-based
  `save_screenshot` / `save_clipboard_screenshot` tools (0 changes to that path).

## Assumptions

- **Complete port, no deferral**: Unlike the pista-A specs (which shipped additively and deferred a
  literal in-process model-call removal), the execution agent has **no in-process model call** — it is
  orchestration prose over a client-agnostic engine. This spec therefore **completes** the execution
  surface in one pass; there is nothing to defer to a later spec.
- **Two bundled copies are reconciled together**: the execution agent exists in more than one bundled
  source (the `Skills/Content/Agents/` bundled file and the `Agent/Resources/` legacy resource). Both
  are ported in this spec so no copy ships a Copilot-ism; reconciling or de-duplicating them is an
  implementation detail handled here, not deferred.
- **Allowlist scope**: the `mcp__spectra__*` entry pre-approves the Spectra MCP tool namespace broadly
  enough to cover the 25 tools' run loop; it is added to the project settings the consuming workspace
  uses (`.claude/settings.json` `permissions.allow`).
- **File-read doc-lookup uses what exists**: the native-file-read replacement for Spaces relies on the
  test's existing source references / the documentation already on disk; it does not introduce a new
  index or lookup service.
- **No server allowlist**: a *server-side* tool allowlist is explicitly not built — the v2 allowlist is
  purely client-side (the server enforces nothing today and continues not to).

## Out of Scope

- **Everything in pista A** (the generation handoff, criteria extraction, critic subagent, and
  authoring-orchestration port) — this is the independent pista.
- **Provider chain / generation config retirement** — the final spec in the series.
- **A server-side tool allowlist** — recorded as a possible future guard slot in the tool registry,
  but not proposed; the v2 allowlist is client-side only.
- **Any change to the `Spectra.MCP` server, the 25 tools, the state machine, or the screenshot tools.**
- **Re-homing the execution-side documentation lookup onto a new index/service** — the replacement is
  direct file reads using existing on-disk docs, not a new lookup mechanism.

## Dependencies

- **None in pista A**: this is the independent pista. The execution MCP surface is self-contained and
  the engine is reused verbatim, so this spec could be (and per the series framing, was designed to be)
  written and implemented in parallel with the four pista-A specs from day one.
- **The reused MCP engine**: the 25-tool server is a prerequisite only in that it already exists and is
  client-agnostic — this spec depends on it remaining unchanged, not on any new work in it.

## Documentation Impact

- **Factually wrong (must fix)**: the execution-agent documentation page describing the Copilot Spaces
  doc-lookup section and the `github/*` tools; any reference to `execution.copilot_space` configuration.
- **Stale (update)**: the deployment / setup pages where execution setup is described — add the
  `mcp__spectra__*` allowlist setup step (and note it is distinct from `Bash(spectra-mcp:*)`).

## Tests

- **Rewrite (covers old agent content)**: any test asserting the execution-agent `.md` content that
  pins the GPT-4o model, the Copilot Spaces tools, or the Spaces doc-lookup section — rewritten to
  assert the Claude Code form.
- **Do not touch (regression net)**: all `Spectra.MCP` engine/tool tests, the state-machine tests, and
  the screenshot tests. They prove the engine is client-agnostic — if one breaks during this spec, that
  is a regression in something that must not have changed, not a test to update.
- **Net-new**:
  - File-reads-replace-Spaces test: the doc-lookup guidance resolves via direct file read; no Copilot
    Spaces tool is referenced; the `execution.copilot_space*` config fields are gone (FR-003).
  - Allowlist-present test: `mcp__spectra__*` is present in `.claude/settings.json` under
    `permissions.allow` and is distinct from `Bash(spectra-mcp:*)` (FR-005).
  - Verdict-pause guardrail test: the ported agent does not auto-advance or fabricate a verdict — the
    no-fabrication / no-auto-advance / ask-before-recording rules are present (FR-004).
  - No-Copilot-ism test across every bundled execution-agent copy: 0 `model: GPT-4o`,
    `disable-model-invocation`, or `github/*copilot_space*` occurrences (FR-002, SC-002).
