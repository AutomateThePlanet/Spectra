---
name: spectra-execute
description: Orchestrate a manual test run from the CLI — select tests, start the run, launch the local web console, and stay on-call. The tester records verdicts in the browser console; you never drive a per-test loop in chat. No MCP server required.
tools: [{{READONLY_TOOLS}}, Bash]
---

# SPECTRA Execute SKILL

You **orchestrate** manual test execution through the `spectra run` CLI (one tool, no MCP server, no MCP
config). Run commands with the Bash tool. You do **not** present tests one-by-one or collect verdicts in
chat — the **execution console** (a local web page) presents each test and records the human's verdict
via PASS / FAIL / BLOCKED buttons. Your job is: select → start → launch the console → hand over the URL →
be on-call.

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

**Step 1** — List runnable suites:
```
spectra run list-suites --output-format json
```

**Step 2** — Ask which suite (and any filters), then start the run:
```
spectra run start {suite} --output-format json
```
The result has `run_id` and the first test's `next_test`.

**Step 3** — Launch the local console and hand the tester the URL:
```
spectra run console
```
It prints `Console running at http://127.0.0.1:{port}/`. Reply with that URL and tell the tester to drive
the run there — click PASS / FAIL / BLOCKED, add a comment, drop or paste a screenshot. The page advances
itself; state lives in SQLite, so a refresh loses nothing. (If a console is already running, the command
prints its existing URL — hand that over instead of launching a second one.)

**Step 4** — Be on-call. When the tester switches to chat mid-run, read current state from the database
(never from the console page):
```
spectra run status --output-format json
```
Use `spectra run show --output-format json` for the full current test. Answer step/expected-result
questions from the test's source documentation — read the `source_refs` files with the Read tool (Grep/Glob
to locate them). Keep answers brief; the tester is mid-run.

**Step 5** — Finalize on request and show the report:
```
spectra run finalize --output-format json
```
Open the HTML report named in `reports.html` (under `.execution/reports/`). Offer to log bugs for failures.

## Screenshots (failure evidence)

Screenshots are attached in the **console** — the tester drops or pastes an image on the page and it is
saved to the current test. Fallback only if the tester pastes into chat instead:
```
spectra run screenshot-clipboard --output-format json
```

## Other commands

- Status / where am I (reads SQLite, session-free): `spectra run status --output-format json`
- Stop the console: `spectra run console --stop`
- Re-run a flaky test: `spectra run retest --test-id {id} --output-format json`
- Pause / resume: `spectra run pause` / `spectra run resume`
- Cancel: `spectra run cancel`

## Trigger phrases

- "Run the checkout tests", "execute the smoke suite", "let's test manually"
- "Start a test run", "I want to walk through the auth tests"

## Refuse to do

- Record or advance a verdict in chat — the console is the verdict channel.
- Invent a failure/blocking/skip note.
- Read run state from the console page or URL instead of `spectra run status`.
