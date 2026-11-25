---
title: Execution Agents
nav_order: 4
permalink: /execution-agents
description: "Run manual test cases with your AI agent: it starts the run and opens a browser console where you record pass, fail, blocked or skip for each test."
---

# Run tests with your agent

Your AI agent starts a test run and opens a console in your browser. The console shows one test at a time; you
perform it and record the result there. The agent stays available in chat for questions about a step.

## What to say

> "Run test cases for notification"
>
> "Run high priority test cases"
>
> "Run the smoke test cases"
>
> "Resume the last run"
>
> "Show run history"

All of these use the `spectra-execute` skill. If you don't name a suite, the agent helps you pick tests: ask
"What should I test?" for a suggestion based on what hasn't run or failed last time.

## What you'll see

1. **Active runs first.** If a run is still open, the agent offers to resume or cancel it.
2. **The console URL.** The agent starts the run and gives you a local address such as `http://127.0.0.1:5081/`.
3. **One test at a time.** The console shows the preconditions, steps and expected result. You click
   **PASS**, **FAIL**, **BLOCKED** or **SKIP**. A comment is required for everything except PASS. You can drop or
   paste a screenshot. The console never moves on by itself.

   {% include screenshot.html file="run-console" alt="SPECTRA execution console for the standard calculator suite: 3 of 236 executed, the current test TC-775 with steps, PASS, FAIL, BLOCKED and SKIP buttons, and the completed list" caption="The console during a run of the Calculator demo's standard calculator suite." %}

4. **Questions in chat.** Ask the agent "what does step 3 mean?" mid-run. It answers from the docs the test was
   written from. It never records a result for you: results come only from the console.
5. **Finish.** Say "finalize the run". The agent creates the report and opens it.

You can close the browser at any time: the run is saved, and refreshing or reopening the console loses nothing.

## Where the results are

| What | Where |
|---|---|
| Run state and history | `.execution/spectra.db` (keep it out of git) |
| Reports | `.execution/reports/` (JSON, Markdown and HTML per run) |

The dashboard's **Run History** tab lists past runs and opens each report:

{% include screenshot.html file="dashboard-runs" alt="Dashboard Run History tab: pass-rate trend, pass rate per suite and a table of three runs with their passed, failed, skipped and blocked counts" %}

{% include screenshot.html file="dashboard-run-report" alt="Dashboard report of a completed unit converter run: 8 passed, 1 failed, 1 skipped, and the result of each of the 10 tests" %}

## Automated execution

The console is for people. To have an automation tool perform the steps (for example BELLATRIX on a desktop app),
that tool needs its own skill; SPECTRA only records the results. In the Calculator demo, the
`bellatrix-desktop-execute-spectra-test` skill does this.

<details markdown="1">
<summary>Under the hood</summary>

The skill runs `spectra run start <suite>` (filters: `--priorities`, `--tags`, `--components`, `--test-ids`,
`--selection`) and `spectra run console`. Mid-run it reads `spectra run status` and `spectra run show`, never the
console page. Every step is also a plain CLI command, for scripts: see
[CLI reference](cli-reference#spectra-run-). Older versions used an MCP server for execution; it was removed in
2.4.0 ([Claude Code v2 migration](claude-code-v2-migration)).

</details>
