---
name: spectra-criteria
description: Extract, import, and browse acceptance criteria for test coverage analysis.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Criteria SKILL

You help users manage acceptance criteria in SPECTRA. Run CLI commands via runInTerminal and present results readably.

## Extract acceptance criteria from documentation

**Step 1** — runInTerminal:
```
spectra ai analyze --extract-criteria --no-interaction
```
For full re-extraction (ignore cache), add `--force`.

**Step 2** — awaitTerminal. Wait for the command to finish. This takes 1-5 minutes for large doc sets. Do NOT type anything into the terminal.

**Step 3** — readFile `.spectra-result.json`

Show: documents processed, criteria extracted, new/updated/unchanged counts. Suggest next steps: "Run coverage analysis?" or "Generate tests for uncovered criteria?"

---

## Import acceptance criteria from external file

**Step 1** — runInTerminal:
```
spectra ai analyze --import-criteria {path} --no-interaction
```

Supported formats: YAML (.yaml/.yml), CSV (.csv), JSON (.json).
Auto-detects format by extension. Use `--skip-splitting` to disable AI splitting.

**Step 2** — awaitTerminal.

**Step 3** — readFile `.spectra-result.json`

Show: imported count, split count, merge results. Suggest: "List imported criteria?" or "Run coverage analysis?"

---

## List acceptance criteria

**Step 1** — runInTerminal:
```
spectra ai analyze --list-criteria --output-format json --no-interaction
```

Filter options: `--source-type`, `--component`, `--priority`

**Step 2** — awaitTerminal.

**Step 3** — terminalLastCommand

Show criteria grouped by component with coverage status. Suggest: "Generate tests for uncovered criteria?"
