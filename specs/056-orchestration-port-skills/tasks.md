# Tasks: Authoring Orchestration Port (skills → `.claude/skills/`)

**Input**: Design documents from `/specs/056-orchestration-port-skills/`
**Prerequisites**: plan.md, spec.md, research.md (D1–D7), data-model.md, contracts/ (4)

**Tests**: Included — the spec's Tests section requests net-new install/manifest, no-Copilot-ism,
mandates-critic, and loop-choreography tests.

**Scope reminder (additive, precedent-matched)**: ports the **authoring** set (generation agent + 13
skills) to `.claude/`, de-Copilots the content, mandates the critic step. The **execution agent** is
untouched (next spec) and the **literal in-process C# model-call removal** is deferred to the
provider-retirement spec. The `SkillResourceLoader`/`SkillsManifest` mechanism, the model-free CLI
verbs, and `Spectra.Core` are reused unchanged.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Confirm baseline green: `dotnet build` and `dotnet test` pass on `056-orchestration-port-skills` before any change (records the regression-net starting point).
- [X] T002 Pin the port set in a scratch note: authoring = `Content/Agents/spectra-generation.agent.md` + the 13 `Content/Skills/*.md` (exclude `spectra-execution.agent.md`; the critic subagent is reused, not re-shipped). No code change.

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: the shared tool-model resolution both the install (US1) and the content (US2) depend on.

**⚠️ CRITICAL**: complete before US1/US2 so resolved skills carry Claude Code tools, not Copilot tools or unexpanded placeholders.

- [X] T003 In `src/Spectra.CLI/Skills/SkillResourceLoader.cs`, repoint the tool-list constants for the ported set to Claude Code tool names (D2): `{{READONLY_TOOLS}}` → `Read, Grep, Glob, Bash`; `{{GENERATE_TOOLS}}`/`{{GENERATION_TOOLS}}` → `Read, Write, Edit, Bash, Glob, Grep, Task`. Leave `{{EXECUTION_TOOLS}}` unchanged (execution agent excluded). Ensure resolution still strips every `{{…}}` token.

**Checkpoint**: placeholders resolve to Claude Code tools; no Copilot tool name or unexpanded token can ship.

---

## Phase 3: User Story 1 — Install as `.claude/skills/<name>/SKILL.md` (Priority: P1) 🎯 MVP

**Goal**: `init`/`update-skills` write the authoring set to `.claude/`, the critic subagent to `.claude/agents/`, via the unchanged pipeline; execution agent stays on `.github/`.

**Independent Test**: run install → `.claude/skills/*/SKILL.md` exist, 0 authoring skills under `.github/`, execution agent still under `.github/`.

### Tests for User Story 1

- [X] T004 [P] [US1] New `tests/Spectra.CLI.Tests/Skills/ClaudeSkillsInstallTests.cs`: install (via the handler) produces `.claude/skills/<name>/SKILL.md` for the authoring set and `.claude/agents/spectra-critic.agent.md`; asserts 0 authoring skills under `.github/skills/`, manifest tracks the `.claude/` paths by hash, and `spectra-execution.agent.md` remains under `.github/agents/` (FR-001, SC-001, SC-005).
- [X] T005 [P] [US1] Rewrite the `.github/`-path assertions in `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs` to assert the `.claude/skills/` + `.claude/agents/` targets (execution agent still `.github/`).

### Implementation for User Story 1

- [X] T006 [US1] In `src/Spectra.CLI/Commands/Init/InitHandler.cs`: retarget `CreateBundledSkillFilesAsync` so authoring skills write to `.claude/skills/<name>/SKILL.md`, the generation agent maps to `.claude/skills/spectra-generation/SKILL.md`, and the critic agent writes to `.claude/agents/spectra-critic.agent.md`; keep the execution-agent install (`InstallAgentFilesAsync`, the `ExecutionAgentPath`/`ExecutionSkillPath` consts) on `.github/`. Update the const paths and the `_logger` "Created:" lines accordingly.
- [X] T007 [US1] In `src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs`: retarget the SKILL loop to `.claude/skills/<name>/SKILL.md`; split the agent loop so the critic agent goes to `.claude/agents/` and the generation agent is handled as a `.claude/skills/spectra-generation/SKILL.md`, while the execution agent stays on `.github/agents/`. Manifest/hash/skip logic unchanged.
- [X] T008 [US1] Run T004/T005; confirm install targets and exclusion are green.

**Checkpoint**: authoring orchestration installs under `.claude/`; execution agent untouched.

---

## Phase 4: User Story 2 — No Copilot-isms remain (Priority: P1)

**Goal**: every ported skill drops `model: GPT-4o` + `disable-model-invocation`, resolves `{{…TOOLS}}`, and expresses terminal/confirmation discipline in Claude Code terms.

**Independent Test**: inspect the ported set → 0 `model: GPT-4o`, 0 `disable-model-invocation`, 0 unexpanded `{{…TOOLS}}`, 0 `runInTerminal`/`awaitTerminal`.

### Tests for User Story 2

- [X] T009 [P] [US2] New `tests/Spectra.CLI.Tests/Skills/NoCopilotIsmsTests.cs`: across the resolved authoring set (`SkillContent.All` + `AgentContent.GenerationAgent`), assert 0 `model: GPT-4o`, 0 `disable-model-invocation`, 0 `{{`, 0 `runInTerminal`/`awaitTerminal`/`show preview`, and that `tools` lists contain Claude Code names (FR-002, SC-002). Explicitly assert the critic subagent is exempt (keeps `model: claude-sonnet-4-6` + `disable-model-invocation`).
- [X] T010 [P] [US2] Update `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs` assertions that pin Copilot phrasing (e.g. `UpdateSkill_ContainsDoNothingInstruction`, `UpdateSkill_ToolsListContainsBrowserOpenBrowserPage`, any `runInTerminal`/`terminalLastCommand`/`browser/openBrowserPage` checks) to their Claude Code translations; preserve the CLI-flag assertions (`--no-interaction`, `--output-format json`, `--verbosity quiet`, `.spectra-result.json`) unchanged.

### Implementation for User Story 2

- [X] T011 [P] [US2] Translate the frontmatter of the 13 authoring skills in `src/Spectra.CLI/Skills/Content/Skills/*.md` (`spectra-coverage`, `-criteria`, `-dashboard`, `-delete`, `-docs`, `-help`, `-init-profile`, `-list`, `-prompts`, `-quickstart`, `-suite`, `-update`, `-validate`): remove `model: GPT-4o`, remove `disable-model-invocation`, keep `name`/`description`/`tools: [{{…TOOLS}}]`.
- [X] T012 [P] [US2] Translate the bodies of those 13 skills: replace `runInTerminal`/`awaitTerminal` + "do NOTHING while waiting" with the Claude Code Bash-tool discipline (D5), replace `show preview …` with the Claude-Code-neutral progress-page affordance; keep CLI command blocks + flags + `.spectra-result.json` reads verbatim.
- [X] T013 [US2] Translate the frontmatter of `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`: remove `model: GPT-4o` + `disable-model-invocation`; keep `name`/`description`/`tools: [{{GENERATION_TOOLS}}]`. (Its body loop/critic content is US3.)
- [X] T014 [US2] Run T009/T010; confirm 0 Copilot-isms across the ported set.

**Checkpoint**: ported set is Copilot-ism-free; critic subagent correctly exempt.

---

## Phase 5: User Story 3 — Generation skill drives the loop & mandates the critic (Priority: P1)

**Goal**: the generation skill compiles → generates → ingests/validates → regenerates on fail-loud error (bounded), and invokes the critic subagent as a mandatory explicit step before persistence.

**Independent Test**: inspect `spectra-generation` SKILL → a required, non-skippable critic step before persistence; a bounded compile→generate→ingest→regenerate loop using the specific error.

### Tests for User Story 3

- [X] T015 [P] [US3] New `tests/Spectra.CLI.Tests/Skills/GenerationSkillContractTests.cs`: assert the generation skill text (a) references invoking the `spectra-critic` subagent as a mandatory step before persistence with no skip/auto-invoke branch (FR-004), (b) drives the compile→generate→ingest loop and regenerates on a fail-loud error bounded by the retry limit, stopping at the limit (FR-003), and (c) its resolved `tools` include `Task`.

### Implementation for User Story 3

- [X] T016 [US3] In `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`: add/translate the procedure so it drives the model-free loop (compile prompt → generated content → ingest/validate boundary → regenerate-with-specific-error, bounded by the existing retry limit, stop+surface at the limit) and includes a mandatory, explicit, non-skippable step invoking the `spectra-critic` subagent before persistence (D4/D6). Translate the "do NOT ask clarifying questions about count or scope" line to Claude Code's confirmation model (D5).
- [X] T017 [US3] In `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`: align the generation skill body with the mandated-critic + bounded-loop choreography (frontmatter de-Copilot done in T011; here ensure the body's critic/loop steps match T016 and reference `spectra ai ingest-verdict` / exit codes 4/5/6).
- [X] T018 [US3] Run T015; confirm the mandate + bounded loop are present and inspectable.

**Checkpoint**: generation skill mandates the critic and drives the bounded loop.

---

## Phase 6: User Story 4 — Retire the runtime declaration; refresh docs (Priority: P2)

**Goal**: `CLAUDE.md` no longer names Copilot as the runtime; `.claude/skills/` is the documented path of record; superseded docs corrected.

**Independent Test**: `grep -c "sole AI runtime" CLAUDE.md` → 0; docs describe the `.claude/skills/` surface.

### Implementation for User Story 4

- [X] T019 [US4] In `CLAUDE.md`: remove "GitHub Copilot SDK (sole AI runtime)" (line 6 area) and describe the Claude-Code-only runtime; name `.claude/skills/` as the authoring path of record. Keep the file under the 40K-char budget.
- [X] T020 [P] [US4] Update the factually-wrong docs (skills-integration; getting-started/quickstart — add the `.claude/settings.json` allowlist **setup step**, not its content): describe the `.claude/skills/` surface and the Claude Code setup. Per the Documentation Impact section.
- [X] T021 [P] [US4] Update the stale docs (customization; copilot-chat / copilot-cli / copilot-spaces-setup): mark deprecated or replace with the Claude Code equivalent. Do **not** describe the `mcp__spectra__*` allowlist content (next spec).

**Checkpoint**: runtime declaration and authoring docs reflect the Claude-Code-only model.

---

## Phase 7: Polish & Cross-Cutting

- [X] T022 Run the full suite: `dotnet test`. Confirm the regression net (`Spectra.Core` + the CLI-verb tests for the commands the skills call) is unchanged and green; rewritten `InitCommandTests` + net-new tests pass (SC-007).
- [X] T023 Execute `specs/056-orchestration-port-skills/quickstart.md` checks 1–4 in a scratch workspace (install lands under `.claude/`, 0 Copilot-isms, generation skill mandates critic, CLAUDE.md clean).
- [X] T024 Note for downstream: the two demo projects must re-run `spectra update-skills` after this lands (per repo memory) — they will pick up the `.claude/` targets; record this in the PR/commit body (no code change here).

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → no deps.
- **Foundational (T003)** → blocks US1 + US2 (resolved tools must be Claude Code before install/content are verified).
- **US1 (T004–T008)** → after T003. Install retarget; independently testable.
- **US2 (T009–T014)** → after T003. Content de-Copilot; independently testable. Independent of US1 (content tests inspect bundled content directly).
- **US3 (T015–T018)** → after US2's generation-agent frontmatter (T013), since it edits the same file's body. Sequential with T013/T011 on shared files.
- **US4 (T019–T021)** → independent; can run anytime after Setup.
- **Polish (T022–T024)** → after all desired stories.

### Within stories

- Tests written alongside; verify the FR they pin fails before the content/handler change, passes after.
- Same-file edits are sequential (e.g. T013 then T016/T017 on the generation files); `[P]` marks distinct files.

---

## Parallel Opportunities

- T004 ∥ T005 (different test files). T009 ∥ T010. T011 ∥ T012 are sequential on the same 13 files — do per-file (frontmatter+body together) to avoid churn; across the suite they are independent of US1/US4.
- T020 ∥ T021 (different doc pages).
- US1, US2, US4 can proceed in parallel after T003; US3 follows US2's generation-file frontmatter.

---

## Implementation Strategy

**MVP** = Setup + Foundational + US1 + US2 + US3 (all P1): authoring orchestration installs under
`.claude/` with no Copilot-isms and a mandated critic. US4 (P2) is the runtime-declaration/doc
consolidation. Stop-and-validate at each checkpoint; keep the regression net green throughout.

## Notes

- `[P]` = different files, no dependency. `[Story]` maps to spec user stories.
- The critic subagent and execution agent are **not** edited (reused / out of scope).
- The literal in-process C# model-call removal is **deferred** to the provider-retirement spec.
- Commit per story/logical group.
