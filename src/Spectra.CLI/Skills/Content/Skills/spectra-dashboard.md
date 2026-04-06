---
name: SPECTRA Dashboard
description: Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Dashboard

You generate the dashboard by running CLI commands. Follow these steps:

### Tool call 1: runInTerminal
First link automation and generate the dashboard:
```
spectra ai analyze --coverage --auto-link --verbosity normal && spectra dashboard --output ./site --output-format json --verbosity quiet
```

### Tool call 2: awaitTerminal

### Tool call 3: terminalLastCommand
Parse the JSON output. Report suites and tests included.

### Tool call 4: runInTerminal
Open the dashboard in the default browser:
```
start ./site/index.html
```

### Your response:
- "Dashboard generated and opened in your browser."
- Show suite count and test count from the JSON result.
