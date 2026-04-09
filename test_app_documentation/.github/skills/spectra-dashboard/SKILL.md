---
name: spectra-dashboard
description: Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
tools: [execute/runInTerminal, execute/awaitTerminal, read/readFile, read/problems, search/listDirectory, search/textSearch, browser/openBrowserPage]
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

**Step 2** — runInTerminal:
```
spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet && spectra dashboard --output ./site --no-interaction --output-format json --verbosity quiet
```

**Step 3** — awaitTerminal. The progress page auto-refreshes — the user can watch live. Between runInTerminal and awaitTerminal, do NOTHING. No readFile, no listDirectory, no checking terminal output, no status messages.

**Step 4** — readFile `.spectra-result.json`

**Step 5** — show preview site/index.html

Report: "Dashboard generated." Show suite count and test count from the result JSON.

---

## "open the dashboard", "show me the dashboard"

show preview site/index.html

Report: "Say 'regenerate dashboard' to rebuild with latest data."
