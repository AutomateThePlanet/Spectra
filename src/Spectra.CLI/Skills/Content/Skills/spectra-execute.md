---
name: spectra-execute
description: Run manual test cases interactively from the CLI via `spectra run` — present a test, wait for the human's verdict, then advance. No MCP server required.
tools: [{{READONLY_TOOLS}}, Bash]
---

# SPECTRA Execute SKILL

You drive manual test execution through the `spectra run` CLI (one tool, no MCP server, no MCP
config). Run commands with the Bash tool. The engine is the authoritative state machine — you only
present tests and record the human's explicit verdict.

## Iron rules (human-in-the-loop)

- **Present ONE test at a time. WAIT for the human's verdict before advancing.**
- **NEVER auto-advance.** Never call `spectra run advance` until the human has given a result.
- **NEVER fabricate a result or a failure note.** For fail/blocked/skip, ask the human and use their exact words.
- **NEVER use dialog/popup tools.** Plain text only, so the user can paste screenshots.

## Workflow

**Step 1** — List runnable suites:
```
spectra run list-suites --output-format json
```

**Step 2** — Ask which suite (and any filters), then start the run:
```
spectra run start {suite} --output-format json
```
The result has `run_id` and the first test's `next_test.test_handle`.

**Step 3** — Show the current test's full details, then present it to the user:
```
spectra run show --output-format json
```
Present numbered steps, preconditions, and the expected result. End with: `Result? (pass/fail/blocked/skip)`.

**Step 4** — WAIT for the human's verdict. Then record it:
- pass → `spectra run advance --status pass --output-format json`
- fail → ask "What went wrong?", wait, then `spectra run advance --status fail --notes "{their words}" --output-format json`
- blocked → ask "What's blocking?", wait, then `spectra run advance --status blocked --notes "{their words}" --output-format json`
- skip → ask "Why skip?", wait, then `spectra run skip --reason "{their words}" --output-format json`

The result's `next_test` is the next test. Loop back to Step 3 until `next_test` is null.

**Step 5** — Finalize and show the report:
```
spectra run finalize --output-format json
```
Open the HTML report named in `reports.html` (under `.execution/reports/`). Offer to log bugs for failures.

## Result mapping

| User says | Command |
|-----------|---------|
| "passed", "yes", "p" | `spectra run advance --status pass` |
| "failed", "bug", "f" | ask why → `spectra run advance --status fail --notes "..."` |
| "blocked", "b" | ask what's blocking → `spectra run advance --status blocked --notes "..."` |
| "skip", "N/A", "s" | ask why → `spectra run skip --reason "..."` |

## Screenshots (failure evidence)

When the user pastes an image, attach it to the current test:
```
spectra run screenshot-clipboard --output-format json
```
Fallback if clipboard capture fails: `spectra run screenshot --file {path} --output-format json`.

## Other commands

- Status / where am I: `spectra run status --output-format json`
- Re-run a flaky test: `spectra run retest --test-id {id} --output-format json`
- Pause / resume: `spectra run pause` / `spectra run resume`
- Cancel: `spectra run cancel`

## Trigger phrases

- "Run the checkout tests", "execute the smoke suite", "let's test manually"
- "Start a test run", "I want to walk through the auth tests"

## Refuse to do

- Record a verdict the user did not give.
- Advance past a test the user has not judged.
- Invent failure/blocking/skip notes.
