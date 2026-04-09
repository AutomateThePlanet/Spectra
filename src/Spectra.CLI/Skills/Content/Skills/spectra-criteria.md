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

**Step 1** — Open the live progress page:
```
show preview .spectra-progress.html?nocache=1
```

**Step 2** — runInTerminal:
```
spectra ai analyze --extract-criteria --no-interaction --output-format json --verbosity quiet
```
For full re-extraction (ignore cache), add `--force`.

**Step 3** — awaitTerminal. The progress page auto-refreshes — the user can watch live. Between runInTerminal and awaitTerminal, do NOTHING. No readFile, no listDirectory, no checking terminal output, no status messages.

**Step 4** — readFile `.spectra-result.json`

**Step 5** — Show: documents processed, criteria extracted, new/updated/unchanged counts. Suggest next steps: "Run coverage analysis?" or "Generate tests for uncovered criteria?"

---

## Import acceptance criteria from external file

**Step 1** — runInTerminal:
```
spectra ai analyze --import-criteria {path} --no-interaction --output-format json --verbosity quiet
```

Supported formats: YAML (.yaml/.yml), CSV (.csv), JSON (.json).
Auto-detects format by extension. Use `--skip-splitting` to disable AI splitting.

**Step 2** — awaitTerminal.

**Step 3** — readFile `.spectra-result.json`

**Step 4** — Show: imported count, split count, merge results. Suggest: "List imported criteria?" or "Run coverage analysis?"

---

## List acceptance criteria

**Step 1** — runInTerminal:
```
spectra ai analyze --list-criteria --no-interaction --output-format json --verbosity quiet
```

Filter options: `--source-type`, `--component`, `--priority`

**Step 2** — awaitTerminal.

**Step 3** — readFile `.spectra-result.json`

**Step 4** — Show criteria grouped by component with coverage status. Suggest: "Generate tests for uncovered criteria?"
