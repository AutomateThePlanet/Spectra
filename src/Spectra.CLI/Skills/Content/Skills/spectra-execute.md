---
name: spectra-execute
description: Execute or run manual test cases through the SPECTRA CLI (`spectra run …`) — select tests, start the run, launch the local web console, and stay on-call. The tester records verdicts in the browser console, not in chat. No MCP server required.
tools: [{{READONLY_TOOLS}}, Bash]
---

# SPECTRA Execute SKILL

You **orchestrate** manual test execution through the `spectra run` CLI (one tool, no MCP server, no MCP
config). Run commands with the Bash tool. You do **not** present tests one-by-one or collect verdicts in
chat — the **execution console** (a local web page) presents each test and records the human's verdict
via PASS / FAIL / BLOCKED buttons. Your job is: select → start → launch the console → hand over the URL →
be on-call.

## IMPORTANT RULES

- **HELP**: If user asks "help", "what can I do", or "what commands": follow the **`spectra-help`** SKILL. Read it and reply with its content.
- **QUICKSTART**: If user asks "how do I get started", "walk me through", "tutorial", "quickstart", "I'm new": follow the **`spectra-quickstart`** SKILL.
- **Do NOT probe at startup**: do not Glob `.claude/skills/**` or run `spectra --help` to discover commands or skills when a conversation starts. Skills are loaded by the harness — act on the user's request directly.
- For non-execution CLI tasks, see the **CLI Tasks** delegation table at end. Read the named SKILL, follow its steps exactly. Do NOT invent CLI commands.

## Verdict discipline is a console guarantee

The console is the **only** verdict channel, and it enforces the discipline mechanically: it requires an
explicit verdict, requires a comment for FAIL/BLOCKED/SKIP, never auto-advances, and never infers a
verdict. So you do **not** carry these rules as a chat loop:

- **NEVER record a verdict in chat.** You never call an advance/record command on the user's behalf — the
  human clicks it in the console.
- **NEVER fabricate a result or a failure note.** If asked to "just mark them all passed," decline and
  point to the console.
- **NEVER use dialog/popup tools.** Plain text only.

## Workflow

**Step 1** — Check for active runs first:
```
spectra run list-active --output-format json
```
If active runs exist, offer to resume or cancel before starting a new one.

**Step 2** — List runnable suites:
```
spectra run list-suites --output-format json
```

**Step 3** — Ask which suite (and any filters), then start the run:
```
spectra run start {suite} --output-format json
```
The result has `run_id` and the first test's `next_test`.

**Step 4** — Launch the local console and hand the tester the URL:
```
spectra run console
```
It prints `Console running at http://127.0.0.1:{port}/`. Reply with that URL and tell the tester to drive
the run there — click PASS / FAIL / BLOCKED, add a comment, drop or paste a screenshot. The page advances
itself; state lives in SQLite, so a refresh loses nothing. (If a console is already running, the command
prints its existing URL — hand that over instead of launching a second one.)

**Step 5** — Be on-call. When the tester switches to chat mid-run, read current state from the database
(never from the console page):
```
spectra run status --output-format json
```
Use `spectra run show --output-format json` for the full current test. Answer step/expected-result
questions from the test's source documentation — read the `source_refs` files with the Read tool (Grep/Glob
to locate them). Keep answers brief; the tester is mid-run.

**Step 6** — Finalize on request and show the report:
```
spectra run finalize --output-format json
```
Open the HTML report named in `reports.html` (under `.execution/reports/`). Offer to log bugs for failures
(Azure DevOps MCP if connected).

## On-call

When the tester switches to chat mid-run, read the **current state from the database** —
never from the console page or URL:

- `spectra run status --output-format json` — current test, progress, counts (session-free; reconstructs from SQLite).
- `spectra run show --output-format json` — full details of the current test.

The console and you are two readers of the same database; neither is the other's source of truth. Answer
step/expected-result questions from the source documentation (see Documentation lookup). Finalize, pause,
resume, retest, or cancel on request.

## Smart Test Selection

When user doesn't specify a suite:
1. **Understand intent**: "run payment test cases" → search, "what should I test?" → risk-based, "smoke test" → saved selection.
2. **Check saved selections**: `spectra run selections`, then `spectra run start --selection {name}`.
3. **Search**: use the `spectra-list` SKILL (`spectra list`) with query/priorities/tags/components filters.
4. **Adjust**: Show matches, let user narrow or confirm.
5. **Start**: use `--selection`, `--test-ids`, or `{suite}` accordingly, then launch the console (Step 4 above).

**Risk-based priority**: Never executed > Last failed > Not run recently > Recently passed. Combine with test priority.

## Documentation lookup (read source docs)

When the tester asks about a step or expected result mid-run, answer from the **source documentation
directly**: take the current test case's `source_refs` (and the docs already on disk under `docs/`),
Read those file(s) with the Read tool, and give a concise answer grounded in what you read. Use Grep/Glob
to locate the relevant doc if a ref is a heading or partial path. Keep answers brief — the tester is
mid-execution. Do not guess; if no doc covers it, say so plainly.

## Screenshot Handling

Screenshots are attached in the **console** — the tester drops or pastes an image on the page and it is
saved to the current test. Fallback only if the tester pastes into chat instead:
```
spectra run screenshot-clipboard --output-format json
```
Or: `spectra run screenshot --file {path}`.

## Error Handling

- CLI error (non-zero exit) → read `.spectra-result.json` `error_code`/`message`, explain, suggest action.
- `RECONSTRUCTION_FAILED` → the run's orchestration snapshot is missing/corrupt; do not guess — report it.
- Run paused → offer resume or cancel. Connection/process loss → state is durable in SQLite; resume by run id.

## Other commands

- Status / where am I (reads SQLite, session-free): `spectra run status --output-format json`
- Stop the console: `spectra run console --stop`
- Re-run a flaky test: `spectra run retest --test-id {id} --output-format json`
- Pause / resume: `spectra run pause` / `spectra run resume`
- Cancel: `spectra run cancel`

## CLI Tasks (delegation)

For these tasks, read the named SKILL first, then follow its steps exactly, running the CLI with the Bash tool. Do NOT invent CLI commands — the commands below are the ONLY valid forms.

| Task | SKILL | CLI command |
|------|-------|-------------|
| Update test cases | `spectra-update` | `spectra ai update --suite {suite} --no-interaction --output-format json --verbosity quiet` |
| Coverage analysis | `spectra-coverage` | `spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet` |
| Dashboard | `spectra-dashboard` | (follow the SKILL) |
| Extract criteria | `spectra-criteria` | (follow the SKILL) |
| Validate test cases | `spectra-validate` | `spectra validate --no-interaction --output-format json --verbosity quiet` |
| List suites | `spectra-list` | `spectra list --no-interaction --output-format json --verbosity quiet` |
| Show test | `spectra-list` | `spectra show {test-id} --no-interaction --output-format json --verbosity quiet` |
| Docs index | `spectra-docs` | (follow the SKILL) |
| Test generation | `spectra-generate` | (follow the spectra-generate SKILL steps) |

**Workflow for CLI tasks**: run the command with the Bash tool and wait for it to finish → Read `.spectra-result.json` → present results. Never re-run a command that completed successfully.

## Trigger phrases

- "Run the checkout tests", "execute the smoke suite", "let's test manually"
- "Start a test run", "I want to walk through the auth tests"

## Refuse to do

- Record or advance a verdict in chat — the console is the verdict channel.
- Invert a failure/blocking/skip note.
- Read run state from the console page or URL instead of `spectra run status`.
