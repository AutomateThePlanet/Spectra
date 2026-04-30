---
name: spectra-dashboard
description: Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Dashboard

You manage the dashboard by running CLI commands via runInTerminal. **NEVER use MCP tools for dashboard generation — always use the CLI commands below.**

## "generate the dashboard", "build the dashboard", "regenerate dashboard"

**Step 1** — Open the live progress page:
```
show preview .spectra-progress.html?nocache=1
```

**Step 2** — runInTerminal (deletes old site folder first, then regenerates):
```
rm -rf site && spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet && spectra dashboard --output ./site --no-interaction --output-format json --verbosity quiet
```
On Windows use: `if exist site rmdir /s /q site && spectra ai analyze ...`

**Step 3** — awaitTerminal. The progress page auto-refreshes — the user can watch live. Between runInTerminal and awaitTerminal, do NOTHING. No readFile, no listDirectory, no checking terminal output, no status messages.

**Step 4** — readFile `.spectra-result.json`

**Step 5** — ALWAYS open the dashboard after generation:
```
show preview site/index.html?nocache=1
```

Report: "Dashboard generated." Show suite count and test case count from the result JSON.

---

## "open the dashboard", "show me the dashboard"

show preview site/index.html

Report: "Say 'regenerate dashboard' to rebuild with latest data."

---

## Cancel the current run

If the user says "stop", "cancel", "kill it", "stop the analysis", "stop generating":

**Step 1** — runInTerminal:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`.

**Step 3** — Report what happened:
- `status: completed` with `shutdown_path: cooperative` → "Cancelled at phase {phase}. Tests/files written before stopping are preserved."
- `status: completed` with `shutdown_path: forced` → "Force-killed after grace window."
- `status: no_active_run` → "Nothing was running."

If the original command's progress page is still open, point the user at it — it now shows the "Cancelled" terminal phase.
