---
name: spectra-dashboard
description: Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
tools: [{{READONLY_TOOLS}}]
---

# SPECTRA Dashboard

You manage the dashboard by running CLI commands with the Bash tool. **NEVER use MCP tools for dashboard generation — always use the CLI commands below.**

## "generate the dashboard", "build the dashboard", "regenerate dashboard"

**Step 1** — Run with the Bash tool:
```
spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet && spectra dashboard --clean --output ./site --no-interaction --output-format json --verbosity quiet
```
(`--clean` removes any existing `site/` folder before regenerating; no platform-specific deletion needed.)

**Step 2** — Wait for the command to finish. The progress page auto-refreshes — the user can watch live. While it runs, do NOTHING — don't poll the terminal, list directories, or read files; just wait for it to complete. Once it finishes, open `.spectra-progress.html` using the VS Code preview (IDE preview tool) to see the run summary. If an IDE preview is not available, run `spectra open .spectra-progress.html` to open it in the default browser.

**Step 3** — Read `.spectra-result.json`

**Step 4** — ALWAYS open the dashboard after generation: open `site/index.html` using the VS Code preview (IDE preview tool). If an IDE preview is not available, run `spectra open site/index.html` to open it in the default browser.

Report: "Dashboard generated." Show suite count and test case count from the result JSON.

---

## "open the dashboard", "show me the dashboard"

Open `site/index.html` using the VS Code preview (IDE preview tool). If an IDE preview is not available, run `spectra open site/index.html` to open it in the default browser.

Report: "Say 'regenerate dashboard' to rebuild with latest data."

---

## Cancel the current run

If the user says "stop", "cancel", "kill it", "stop the analysis", "stop generating":

**Step 1** — Run with the Bash tool:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — Wait for the command to finish, then Read `.spectra-result.json`.

**Step 3** — Report what happened:
- `status: completed` with `shutdown_path: cooperative` → "Cancelled at phase {phase}. Tests/files written before stopping are preserved."
- `status: completed` with `shutdown_path: forced` → "Force-killed after grace window."
- `status: no_active_run` → "Nothing was running."

If the original command's progress page is still open, point the user at it — it now shows the "Cancelled" terminal phase.
