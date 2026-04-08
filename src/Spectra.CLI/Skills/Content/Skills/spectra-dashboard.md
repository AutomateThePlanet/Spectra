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

**Step 1** — runInTerminal:
```
spectra ai analyze --coverage --auto-link --no-interaction && spectra dashboard --output ./site --no-interaction
```

**Step 2** — awaitTerminal. Wait for the command to finish.

**Step 3** — show preview site/index.html

Report: "Dashboard generated." Show suite count and test count if visible in terminal output.

---

## "open the dashboard", "show me the dashboard"

show preview site/index.html

Report: "Say 'regenerate dashboard' to rebuild with latest data."
