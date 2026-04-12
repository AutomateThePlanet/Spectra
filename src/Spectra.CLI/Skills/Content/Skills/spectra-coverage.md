---
name: spectra-coverage
description: Analyzes test coverage across documentation, acceptance criteria, and automation.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Coverage

You analyze test coverage by running a CLI command via runInTerminal.

## Run coverage analysis

**Step 1** — Open the live progress page:
```
show preview .spectra-progress.html?nocache=1
```

**Step 2** — runInTerminal:
```
spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet
```

**Step 3** — awaitTerminal. The progress page auto-refreshes — the user can watch live. Between runInTerminal and awaitTerminal, do NOTHING. No readFile, no listDirectory, no checking terminal output, no status messages.

**Step 4** — readFile `.spectra-result.json`

**Step 5** — Show the three coverage sections from the result:
- **Documentation coverage**: X% (N/M documents) — list uncovered docs
- **Acceptance criteria coverage**: X% (N/M criteria) — list untested acceptance criteria
- **Automation coverage**: X% (N/M test cases) — list unlinked test cases

If the user asks to improve coverage, suggest generating test cases for uncovered areas.
