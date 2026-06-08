---
description: "Task list for Execution Agent / SKILL Revision"
---

# Tasks: Execution Agent / SKILL Revision

**Input**: Design documents from `/specs/067-execution-agent-console-revision/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/agent-skill-contract.md, quickstart.md

**Tests**: INCLUDED — the spec (FR-008) explicitly rewrites the two contract tests `ExecuteSkillTests` and
`ExecutionAgentPortTests`. No other test may change.

**Organization**: By user story (US1–US4). Note: this is a **prose-centric** feature — the two bundled
markdown files (agent + SKILL) are rewritten holistically in the Foundational phase (they deliver all
four stories' *content* at once); each story phase then rewrites the specific *test assertions* that
verify its slice. Tasks editing the same file are sequential (not `[P]`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: different files, no dependency on incomplete tasks
- **[Story]**: US1–US4 (maps to spec.md)

## Path Conventions

Bundled content: `src/Spectra.CLI/Skills/Content/{Agents,Skills}/`. Tests:
`tests/Spectra.CLI.Tests/Skills/`. Docs: `docs/`. **No engine/handler/guardrail code changes** (FR-010).
`SkillsManifestTests.cs` is a **frozen oracle** — it must stay green unchanged (research R2).

---

## Phase 1: Setup (Baseline)

**Purpose**: Capture the green baseline and the assertions that will invert.

- [X] T001 Run `dotnet test --filter "FullyQualifiedName~Skills"` and record the current pass state of `ExecuteSkillTests`, `ExecutionAgentPortTests`, and `SkillsManifestTests` (so the rewrite's inversions and the frozen-oracle invariants are known going in)

---

## Phase 2: Foundational (Holistic prose rewrite — BLOCKS all story verification)

**Purpose**: Rewrite both bundled markdown artifacts from loop-driver to orchestrator + on-call. These
two rewrites deliver the *content* for US1–US4; the story phases then assert each slice.

**⚠️ CRITICAL**: Must stay within the `SkillsManifestTests` envelope (research R2) — verified by T004.

- [X] T002 Rewrite `src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md` (data-model Artifact 2): **remove** the Step 3 show→present block, the Step 4 WAIT→record mapping, and the "Result mapping" table; **add** a "launch `spectra run console` and hand over the URL" step and an "On-call" step (`spectra run status` from the DB); **restate** the Iron rules / Refuse-to-do as a console guarantee ("the console enforces verdict discipline; never record a verdict in chat; never fabricate notes"); **re-home** screenshots to the console drop (keep a brief `screenshot-clipboard` fallback); **keep** list-suites, start, finalize, status/retest/pause/resume/cancel, trigger phrases. Constraints: contains `spectra`, no `### Tool call N`, no `terminalLastCommand`.
- [X] T003 Rewrite `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` (data-model Artifact 1): **remove** Execution Workflow step 5 (per-test loop), the "Test Presentation" block, and the "Result Collection" table; **replace** with a "launch `spectra run console` → hand over local URL" step and an "On-call" subsection (read `spectra run status` from SQLite, never the page — FR-006); **rewrite** IMPORTANT RULES so verdict discipline is a console guarantee and the agent never records/fabricates a verdict in chat (keep "plain text only / no dialog tools"); **re-home** screenshot guidance to the console; **KEEP** start/list-active/list-suites, finalize, Smart Test Selection, `selections`/`--selection`, Error Handling (RECONSTRUCTION_FAILED/resume-by-id), the Documentation-lookup block (`source_refs` + native `Read`), the CLI Tasks delegation table (incl. `spectra-update`), and the `spectra-quickstart` reference. Constraints: **≤200 lines**, no fenced CLI code blocks containing delegation markers, no Copilot-isms.
- [X] T004 Verify the constraint envelope is intact: run `dotnet test --filter "FullyQualifiedName~Skills.SkillsManifestTests"` and confirm it is **green unchanged** (agent ≤200 lines; `SkillContent.All`==15, `AgentContent.All`==3; `spectra-update` + `spectra-quickstart` refs present; no forbidden CLI code blocks). If any fails, the rewrite left the allowed scope — fix the prose, do not edit `SkillsManifestTests`.

**Checkpoint**: Both artifacts are rewritten and still satisfy the frozen oracle.

---

## Phase 3: User Story 1 - Agent orchestrates instead of driving the loop (Priority: P1) 🎯 MVP

**Goal**: The agent/SKILL flow is select → start → launch console → hand over URL, with no in-chat
per-test loop.

**Independent Test**: `dotnet test --filter "FullyQualifiedName~Skills.ExecuteSkillTests|FullyQualifiedName~Skills.ExecutionAgentPortTests"` — orchestrate assertions pass; no chat-loop text remains.

- [X] T005 [US1] Rewrite the orchestrate assertions in `tests/Spectra.CLI.Tests/Skills/ExecuteSkillTests.cs` (contract: agent-skill-contract.md): keep `ExecuteSkill_IsBundledAndRegistered`; replace `ExecuteSkill_DrivesSpectraRunCli` → `ExecuteSkill_Orchestrates_StartLaunchConsole` (asserts `spectra run start`, `spectra run console`, `spectra run finalize`); replace `ExecuteSkill_EnforcesGuardrails` → `ExecuteSkill_DoesNotDriveChatLoop` (asserts the SKILL does **not** contain `Result? (pass/fail/blocked/skip)` or a `--status pass` mapping table)
- [X] T006 [US1] Rewrite the orchestrate assertions in `tests/Spectra.CLI.Tests/Skills/ExecutionAgentPortTests.cs`: keep the `ExecutionAgent_HasNoCopilotIsm` Theory, `ExecutionAgent_DocLookup_IsNativeFileRead`, and `ExecutionAgent_UsesPlainText_NotDialogTools`; replace `ExecutionAgent_DrivesSpectraRunCli` → `ExecutionAgent_Orchestrates_StartLaunchConsole` (asserts `spectra run start`, `spectra run console`, `spectra run finalize`); add `ExecutionAgent_LaunchesConsole_HandsOverUrl` (asserts `spectra run console` + URL hand-off language); replace `ExecutionAgent_AsksBeforeRecording_NonPassOutcomes` → `ExecutionAgent_DoesNotDriveChatLoop` (asserts no `Result Collection` / `ask BEFORE running the command` / `Result? (pass/fail/blocked/skip)`)

**Checkpoint**: US1 verified — the agent orchestrates; no chat loop in either artifact.

---

## Phase 4: User Story 2 - Human-in-the-loop becomes a server guarantee (Priority: P1)

**Goal**: Verdict discipline is stated as a console property; the agent never records/fabricates a verdict
in chat. Guardrail **code** unchanged.

**Independent Test**: the guarantee assertions pass; `GuardrailTests` stays green unchanged (proves no code moved).

- [X] T007 [US2] Add guarantee assertions (same two test files, sequential after T005/T006): `ExecuteSkill_StatesConsoleGuarantee` and `ExecutionAgent_StatesConsoleGuarantee` (assert the console-enforces-verdict statement: contains `console` + `verdict`); keep/relax `ExecutionAgent_NeverFabricatesNotes` (asserts a never-fabricate-verdict-in-chat statement). Confirm neither file instructs the agent to record a verdict in chat.

**Checkpoint**: US2 verified — discipline relocated to the console, integrity preserved.

---

## Phase 5: User Story 3 - On-call clarification reads the database (Priority: P2)

**Goal**: On-call, the agent reads current state from `spectra run status` (DB), not the page.

**Independent Test**: the status-from-DB assertions pass.

- [X] T008 [US3] Add on-call assertions (same two test files): `ExecuteSkill_OnCall_ReadsStatus` and `ExecutionAgent_OnCall_ReadsStatusFromDb` (assert `spectra run status` is the on-call state source; the agent keeps `source_refs` + native `Read` for doc lookup). Confirm no assertion expects the agent to read the console page/URL for state.

**Checkpoint**: US3 verified — agent and console are two readers of the same DB.

---

## Phase 6: User Story 4 - Finalize, resume, cancel on request (Priority: P2)

**Goal**: Orchestration lifecycle controls are retained after the loop is removed.

**Independent Test**: the lifecycle assertions pass; agent/SKILL still reference finalize + resume-by-run-id.

- [X] T009 [US4] Confirm lifecycle retention in the rewritten artifacts and tests: `spectra run finalize` is asserted (already in T005/T006 orchestrate assertions) and the agent prose still covers resume-by-run-id / pause / resume / cancel. Add a small assertion if not already covered (e.g. agent contains `finalize` and `resume`).

**Checkpoint**: US4 verified — lifecycle intact; only the per-test verdict loop was removed.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T010 [P] Update `docs/cli-reference.md` (the line "the `spectra-execute` SKILL and the execution agent drive this loop with human-in-the-loop guardrails (present → wait for verdict → advance)") to the orchestrate model: the agent starts the run + launches `spectra run console`; verdicts are recorded in the console; guardrails are a console property
- [X] T011 [P] Add a manual-run console pointer to `docs/getting-started.md` and `docs/execution-agents.md` (the agent launches `spectra run console` and hands over the URL; the browser drives the run) without conflating it with the static dashboard
- [X] T012 Run the full `dotnet test` suite and confirm the regression net is **green and unchanged** — `GuardrailTests`, `ParityTests`, `RunLoopSmokeTests`, `WalConcurrencyTests`, the MCP corpus, `Spectra.Core.Tests`, **and `SkillsManifestTests`** (SC-005/SC-006). Confirm the diff touches **no** engine/handler/guardrail code (FR-010). If a GuardrailTest breaks, the rewrite reached into code it should not have — stop and investigate.
- [X] T013 Run the `quickstart.md` validation: confirm the rewritten agent/SKILL describe select → start → launch console → hand over URL → on-call (no in-chat verdict loop), and that the filtered `ExecuteSkillTests`/`ExecutionAgentPortTests` pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: none — run first to capture baseline.
- **Foundational (T002–T004)**: the two prose rewrites BLOCK all story verification (the tests assert the rewritten content). T004 gates on T002+T003.
- **User Stories (T005–T009)**: all depend on Foundational. They edit the **same two test files**, so they are sequential (T005/T006 → T007 → T008 → T009), not parallel.
- **Polish (T010–T013)**: T010/T011 (docs) are `[P]` with each other; T012/T013 run last, after all content + tests are in.

### Within the rewrite

- T002 (SKILL) and T003 (agent) are independent files → could be `[P]`, but T004 needs both.
- The story phases are verification of the holistic rewrite; if an assertion fails, fix the **prose** (T002/T003), never `SkillsManifestTests`.

### Parallel Opportunities

- T002 and T003 (different files) may run in parallel; T004 joins them.
- T010 and T011 (different docs) are `[P]`.
- The test-file edits (T005–T009) are NOT parallel — they touch the same two files sequentially.

---

## Implementation Strategy

### MVP (US1)

1. T001 baseline → T002/T003 rewrite both artifacts → T004 envelope check.
2. T005/T006 rewrite the orchestrate assertions.
3. **STOP and VALIDATE**: the agent orchestrates (start → console → URL) with no chat loop — demonstrable MVP.

### Incremental

1. Foundational rewrite (content for all stories).
2. + US1 (orchestrate asserts) → MVP.
3. + US2 (server-guarantee asserts) → integrity proven relocated.
4. + US3 (status-from-DB asserts) → on-call correctness.
5. + US4 (lifecycle asserts) → controls retained.
6. Polish: docs + full green (incl. frozen oracle + guardrail net).

---

## Notes

- **Frozen oracle**: `SkillsManifestTests.cs` must stay green unchanged — it is the constraint envelope
  (agent ≤200 lines, counts 15/3, `spectra-update`/`spectra-quickstart` refs, no CLI code blocks in
  agents). If it breaks, fix the prose, not the test.
- **No code**: if any task tempts an edit to `RunHandler`/`ExecutionEngine`/guardrail code, stop — this
  feature only moves the prose home (FR-010, SC-006).
- The two markdown rewrites are wholesale; the per-story test phases verify slices of that single rewrite.
- Commit after the foundational rewrite and after each story's assertions pass.
