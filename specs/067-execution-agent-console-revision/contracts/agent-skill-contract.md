# Contract: Rewritten Agent/SKILL Content Assertions

The "interface" of this feature is the bundled prose. Its contract is what the two rewritten contract
tests assert against `AgentContent.ExecutionAgent` and `SkillContent.Execute`. This is the spec for
rewriting `ExecuteSkillTests.cs` and `ExecutionAgentPortTests.cs` (FR-008).

> Frozen oracle: `SkillsManifestTests.cs` is **not** changed and must stay green — see research R2 for the
> invariants the rewritten content must keep (≤200 agent lines, `spectra-update` + `spectra-quickstart`
> refs, no CLI code blocks in agents, count 15/3, no `### Tool call N`, no `terminalLastCommand`).

---

## ExecuteSkillTests.cs — new assertions

| Test | Asserts (MUST) | Replaces |
|---|---|---|
| `ExecuteSkill_IsBundledAndRegistered` | `SkillContent.All` has `spectra-execute`; non-empty; `name: spectra-execute` present | **keep as-is** |
| `ExecuteSkill_Orchestrates_StartLaunchConsole` | contains `spectra run start`, `spectra run console`, and `spectra run finalize` | replaces `ExecuteSkill_DrivesSpectraRunCli` (which required `spectra run advance`) |
| `ExecuteSkill_DoesNotDriveChatLoop` | does **NOT** contain the verdict-mapping loop: no `Result? (pass/fail/blocked/skip)`; no `--status pass` mapping table; `advance` is not presented as the agent's chat action | replaces `ExecuteSkill_EnforcesGuardrails` (which required in-chat `WAIT` loop) |
| `ExecuteSkill_StatesConsoleGuarantee` | contains a console-enforces-verdict statement (e.g. "console" + "verdict") and the no-fabrication rule (`fabricate`/`never record a verdict in chat`) | new — guarantee relocated, not deleted |
| `ExecuteSkill_OnCall_ReadsStatus` | contains `spectra run status` for on-call state | new (FR-006) |

> Note: the old `ExecuteSkill_EnforcesGuardrails` asserted the SKILL contains `WAIT` + `auto-advance`
> (the in-chat loop). After the rewrite the loop is gone, so that assertion is **inverted** — the SKILL
> must NOT instruct a chat WAIT-for-verdict loop. The integrity guarantee is asserted instead via
> `ExecuteSkill_StatesConsoleGuarantee`.

---

## ExecutionAgentPortTests.cs — new assertions

| Test | Action | Asserts (MUST) |
|---|---|---|
| `ExecutionAgent_HasNoCopilotIsm` (Theory) | **Keep unchanged** | no `model: GPT-4o`, `copilot_space`, `github/`, `runInTerminal`, `show preview`, … |
| `ExecutionAgent_DocLookup_IsNativeFileRead` | **Keep** | contains `source_refs`, `Read`, `Documentation lookup`; no `Copilot Spaces` |
| `ExecutionAgent_Orchestrates_StartLaunchConsole` | **Replace** `ExecutionAgent_DrivesSpectraRunCli` | contains `spectra run start`, `spectra run console`, `spectra run finalize`; **no** requirement for `spectra run show`/`advance` in the workflow |
| `ExecutionAgent_DoesNotDriveChatLoop` | **New** (replaces `ExecutionAgent_AsksBeforeRecording_NonPassOutcomes`) | does **not** contain the `Result Collection` table / `ask BEFORE running the command` / `Result? (pass/fail/blocked/skip)` chat-loop prose |
| `ExecutionAgent_LaunchesConsole_HandsOverUrl` | **New** | contains `spectra run console` and language handing the user the local URL |
| `ExecutionAgent_OnCall_ReadsStatusFromDb` | **New** (FR-006) | contains `spectra run status`; states current state comes from status/DB, not the page |
| `ExecutionAgent_NeverFabricatesNotes` | **Keep/relax** | contains a never-fabricate-verdict-in-chat statement (`fabricate`) |
| `ExecutionAgent_UsesPlainText_NotDialogTools` | **Keep** | contains `plain text`, `dialog/popup` |
| `ExecutionAgent_StatesConsoleGuarantee` | **New** | guardrail discipline framed as a console property (console + verdict/notes) |

> Removed assertions: `ExecutionAgent_AsksBeforeRecording_NonPassOutcomes` (required
> `ask BEFORE running the command` and `BLOCKED uses \`advance --status blocked\``) — these encode the
> chat loop that no longer exists. The screenshot assertions from `ExecutionAgent_DrivesSpectraRunCli`
> (`spectra run screenshot-clipboard`) may remain only as a fallback mention; the console is primary, so
> they are not required by the new contract.

---

## Invariants preserved (mapped to FRs)

- **FR-002 / FR-003**: agent contains `spectra run start` + `spectra run console`; the launch+hand-over flow.
- **FR-001**: no in-chat verdict loop in either file (no `Result?`/mapping table/WAIT-for-verdict).
- **FR-004 / SC-003**: console-guarantee statement + never-fabricate-in-chat present; guardrail **code** untouched (proved by `GuardrailTests` staying green unchanged — not in this change set).
- **FR-005**: agent keeps `source_refs` + `Documentation lookup` + native `Read`.
- **FR-006**: both reference `spectra run status` for on-call state.
- **R2 envelope**: `SkillsManifestTests` green unchanged (≤200 lines, refs kept, counts 15/3).
