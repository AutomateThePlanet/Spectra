# Tasks: Execution Agent Port (independent pista)

**Input**: Design documents from `/specs/057-execution-agent-port/`
**Prerequisites**: plan.md, spec.md, research.md (D1–D6), data-model.md, contracts/ (4)

**Tests**: Included — the spec's Tests section requests file-reads-replace-Spaces, allowlist-present,
verdict-pause, and no-Copilot-ism tests, plus a config back-compat test.

**Scope reminder (complete port, no deferral; zero MCP change)**: re-author the **one** canonical
execution agent for Claude Code, consolidate away the two redundant copies, relocate the install to
`.claude/`, remove the dead Spaces config, and add the client-side `mcp__spectra__*` allowlist. The
entire `Spectra.MCP` server and its test corpus are the **untouched regression net**.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Confirm baseline green: `dotnet build` and `dotnet test` pass on `057-execution-agent-port` before any change (records the regression-net starting point, especially `Spectra.MCP.Tests`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the install routing + settings writer the user stories depend on.

- [X] T002 In `src/Spectra.CLI/Skills/SkillInstallLayout.cs`, change the agent-routing so `spectra-execution.agent.md` → `.claude/skills/spectra-execution/SKILL.md` (replace the `.github/` default for execution with an explicit mapping; generation → `.claude/skills/spectra-generation/SKILL.md` and critic → `.claude/agents/` mappings unchanged).
- [X] T003 [P] Add `src/Spectra.CLI/Skills/ClaudeSettingsInstaller.cs` — a pure, idempotent helper that, given the current `.claude/settings.json` content (or none), returns JSON whose `permissions.allow` array contains `"mcp__spectra__*"` while preserving any existing entries (no duplicate on re-apply).

**Checkpoint**: execution routes to `.claude/skills/`; the allowlist merge helper exists.

---

## Phase 3: User Story 1 — Drive the unchanged MCP engine; one install under `.claude/` (Priority: P1) 🎯 MVP

**Goal**: the execution agent installs as a single `.claude/skills/spectra-execution/SKILL.md`, the duplicate `.github/` copies are gone, and `Spectra.MCP` is provably untouched.

**Independent Test**: `git diff` shows 0 changes under `src/Spectra.MCP/`; install produces one `.claude/skills/spectra-execution/SKILL.md` and no `.github/` execution copies; `Spectra.MCP.Tests` green.

### Tests for User Story 1

- [X] T004 [P] [US1] Rewrite the execution-agent/skill assertions in `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs` (`HandleAsync_InstallsExecutionAgentFile`, `HandleAsync_InstallsExecutionSkillFile`, `HandleAsync_WithExistingAgentFile_*`, `HandleAsync_AgentFilesHaveVersionComment`) to assert the single `.claude/skills/spectra-execution/SKILL.md` target (skip/force/content preserved via the bundled install helper); drop assertions of the retired `.github/agents` + `.github/skills/spectra-execution` paths.

### Implementation for User Story 1

- [X] T005 [US1] In `src/Spectra.CLI/Commands/Init/InitHandler.cs`: retire `InstallAgentFilesAsync` (remove the call at HandleAsync, the method, and the `ExecutionAgentPath` / `ExecutionSkillPath` consts) and remove the corresponding `_logger` "Created:" lines; the execution agent now installs solely via `CreateBundledSkillFilesAsync` → `SkillInstallLayout` (T002).
- [X] T006 [US1] Delete the redundant duplicates: `src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md`, `src/Spectra.CLI/Agent/Resources/SKILL.md`, and `src/Spectra.CLI/Agent/AgentResourceLoader.cs`; remove their two `EmbeddedResource` lines from `src/Spectra.CLI/Spectra.CLI.csproj`. Confirm no remaining references to `AgentResourceLoader` compile-break.
- [X] T007 [US1] Build; run T004 + the full `Spectra.MCP.Tests` corpus; confirm `git diff --name-only` shows 0 files under `src/Spectra.MCP/` (SC-001) and one canonical execution install (SC-005 install half).

**Checkpoint**: one execution skill under `.claude/`; duplicates gone; MCP untouched and green.

---

## Phase 4: User Story 2 — De-Copilot the agent; doc-lookup via native file reads (Priority: P1)

**Goal**: the execution agent carries no GPT-4o pin, no `disable-model-invocation`, no Copilot Spaces tools/section; doc-lookup reads `source_refs` files directly; dead config removed.

**Independent Test**: inspect the single execution agent → 0 Copilot-isms, file-read doc-lookup present; `ExecutionConfig` has no `copilot_space*` fields and legacy configs still deserialize.

### Tests for User Story 2

- [X] T008 [P] [US2] New `tests/Spectra.CLI.Tests/Skills/ExecutionAgentPortTests.cs`: across `AgentContent.ExecutionAgent`, assert 0 `model: GPT-4o`, `disable-model-invocation`, `get_copilot_space`, `list_copilot_spaces`, `copilot_space`, `runInTerminal`, `awaitTerminal`, `show preview`; assert the doc-lookup guidance references reading `source_refs` / documentation files directly (FR-002/FR-003, SC-002/SC-003).
- [X] T009 [P] [US2] New `tests/Spectra.Core.Tests/...` config back-compat test: an `ExecutionConfig` / `SpectraConfig` JSON carrying `execution.copilot_space` + `copilot_space_owner` deserializes without error (unknown keys ignored) (SC-004).

### Implementation for User Story 2

- [X] T010 [US2] Re-author `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`: drop `model: GPT-4o` + `disable-model-invocation`; remove `github/get_copilot_space` / `github/list_copilot_spaces` from `tools` (add `read` if not present); replace the "Copilot Spaces Documentation" section with a "Documentation lookup" section that reads the test's `source_refs` doc file(s) directly; translate `runInTerminal`/`awaitTerminal`/`show preview` to Claude Code (Bash tool, "open"); keep MCP tool names + screenshot tools unchanged. (Verdict-pause preservation is verified in US4.)
- [X] T011 [US2] In `src/Spectra.Core/Models/Config/ExecutionConfig.cs`: remove `CopilotSpace` and `CopilotSpaceOwner`. (Class may become empty — keep it as a valid bind target for the `execution` config block.)
- [X] T012 [US2] Run T008/T009; confirm 0 Copilot-isms and config back-compat.

**Checkpoint**: the single execution agent is Copilot-ism-free; doc-lookup is native file reads; dead config gone.

---

## Phase 5: User Story 3 — Add the client-side MCP allowlist (Priority: P2)

**Goal**: `spectra init` ensures `.claude/settings.json` allows `mcp__spectra__*`; the Spectra repo dogfoods it; distinct from `Bash(spectra-mcp:*)`.

**Independent Test**: after init, `.claude/settings.json` `permissions.allow` contains `mcp__spectra__*`, distinct from `Bash(spectra-mcp:*)`; re-running init does not duplicate it.

### Tests for User Story 3

- [X] T013 [P] [US3] New `tests/Spectra.CLI.Tests/Skills/McpAllowlistTests.cs`: `ClaudeSettingsInstaller` adds `mcp__spectra__*` to an empty/absent settings, preserves existing `permissions.allow` entries, is idempotent on re-apply, and the result is distinct from `Bash(spectra-mcp:*)`; an end-to-end `init` produces `.claude/settings.json` with the entry (FR-005, SC-005).

### Implementation for User Story 3

- [X] T014 [US3] In `InitHandler`, call `ClaudeSettingsInstaller` (creating/merging `.claude/settings.json` in the working directory) during install; register the file path in the skills manifest if appropriate (or leave unmanaged like `.vscode/mcp.json`).
- [X] T015 [US3] Add the Spectra repo's own `.claude/settings.json` with `permissions.allow: ["mcp__spectra__*"]` (dogfood), kept distinct from the `Bash(spectra-mcp:*)` entry in `.claude/settings.local.json`.
- [X] T016 [US3] Run T013; confirm the allowlist is present, idempotent, and distinct.

**Checkpoint**: the MCP allowlist is installed and dogfooded; the loop runs without per-call prompts.

---

## Phase 6: User Story 4 — Preserve the human-verdict pause (Priority: P1)

**Goal**: the re-authored agent still presents a result, asks in plain text, waits, and never auto-advances or fabricates a verdict/notes.

**Independent Test**: inspect the execution agent → the no-fabrication / no-auto-advance / ask-before-recording rules and the plain-text "use plain text" guidance are present.

### Tests for User Story 4

- [X] T017 [P] [US4] Extend `ExecutionAgentPortTests.cs` (or a focused `ExecutionVerdictPauseTests.cs`): assert the agent text instructs presenting the result and waiting for the plain-text verdict; forbids fabricating notes/verdicts and auto-advancing; for FAIL/BLOCKED/SKIP asks the reason before recording; BLOCKED uses `advance_test_case` (not `skip_test_case`); the dialog/popup-tool ban reads as "use plain text" (FR-004, SC-006).

### Implementation for User Story 4

- [X] T018 [US4] Verify the re-authoring in T010 preserved the verdict-pause guardrails verbatim in spirit; restore/adjust any guardrail wording the de-Copilot edits weakened. Run T017.

**Checkpoint**: the human-verdict pause and guardrails survive the port.

---

## Phase 7: Polish & Cross-Cutting

- [X] T019 [P] Update the factually-wrong docs: the `execution-agents` / execution-agent page(s) describing the Copilot Spaces section and `github/*` tools, and any `execution.copilot_space` config reference → describe native file-read doc-lookup and the Claude Code execution skill.
- [X] T020 [P] Update the stale deployment/setup docs: add the `mcp__spectra__*` allowlist setup step (note it is distinct from `Bash(spectra-mcp:*)`).
- [X] T021 Run the full suite: `dotnet test`. Confirm `Spectra.MCP.Tests` is unchanged and green (SC-001), and the rewritten `InitCommandTests` + net-new execution/allowlist/config tests pass.
- [X] T022 Execute `quickstart.md` checks 1–6 in a scratch workspace (engine untouched, one `.claude/` skill, 0 Copilot-isms, dead config gone, allowlist present+distinct, verdict pause preserved).

---

## Dependencies & Execution Order

- **Setup (T001)** → no deps.
- **Foundational (T002, T003)** → T002 blocks US1 install; T003 blocks US3.
- **US1 (T004–T007)** → after T002. Install relocation + consolidation + MCP-untouched proof.
- **US2 (T008–T012)** → independent of US1 (content + config); can run in parallel after Setup.
- **US3 (T013–T016)** → after T003.
- **US4 (T017–T018)** → after US2's T010 (same file; verifies the pause survived).
- **Polish (T019–T022)** → after the desired stories.

### Parallel Opportunities

- US2 (content/config) ∥ US1 (install/delete) ∥ US3 (allowlist) after Foundational — they touch
  disjoint files (execution `.md` + config vs InitHandler/csproj/resources vs settings writer).
- T019 ∥ T020 (different doc pages).

---

## Implementation Strategy

**MVP** = Setup + Foundational + US1 + US2 + US4 (all P1): the execution agent is de-Copilot'd, installs
as one `.claude/` skill, preserves the verdict pause, and the MCP engine is provably untouched. US3 (P2)
adds the friction-removing allowlist. Keep `Spectra.MCP.Tests` green at every checkpoint — a break there
is a regression, not a test to update.

## Notes

- `[P]` = different files, no dependency. `[Story]` maps to spec user stories.
- **Zero `Spectra.MCP` changes** — if a task seems to need one, the approach is wrong.
- The legacy `test-generation` skill install (`.github/skills/test-generation/`) is out of scope and
  untouched.
- Commit per story/logical group.
