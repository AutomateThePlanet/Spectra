# Contract: Ported Execution Skill (de-Copilot + file reads + verdict pause)

**Surface**: the canonical `Skills/Content/Agents/spectra-execution.agent.md` (installed as
`.claude/skills/spectra-execution/SKILL.md`).

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | the ported execution skill | inspect frontmatter | no `model: GPT-4o`, no `disable-model-invocation`; `tools` carry no `github/get_copilot_space` / `github/list_copilot_spaces` |
| 2 | the ported skill body | inspect | no "Copilot Spaces Documentation" section; no `get_copilot_space` / `list_copilot_spaces` references; no `execution.copilot_space` reference |
| 3 | a doc question mid-run | the agent resolves it | it reads the test's `source_refs` documentation file(s) directly (native Read) and answers concisely |
| 4 | the ported skill body | inspect for Copilot verbs | no `runInTerminal` / `awaitTerminal` / `show preview`; the wait discipline is expressed in Claude Code terms |
| 5 | a PASS verdict | the tester says pass | the agent calls `advance_test_case` only after the tester gives the outcome — never preemptively |
| 6 | a FAIL / BLOCKED / SKIP verdict | recording | the agent asks for the reason in plain text and waits for the tester's exact words before recording; never fabricates notes |
| 7 | any result | presentation | the agent presents and **pauses** for the plain-text verdict; it never auto-advances or guesses a verdict |

## Invariants

- The MCP tool names (`start_execution_run`, `get_test_case_details`, `advance_test_case`,
  `skip_test_case`, `finalize_execution_run`, `add_test_note`, `save_screenshot`,
  `save_clipboard_screenshot`, …) are referenced **unchanged** — no server contract change.
- Screenshot capture stays on the path-based `save_screenshot` / `save_clipboard_screenshot` tools
  (FR-006) — unchanged.
- BLOCKED still uses `advance_test_case` with status BLOCKED (not `skip_test_case`) — the existing
  guardrail is preserved.
