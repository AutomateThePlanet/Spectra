---
name: spectra-docs
description: Index documentation and manage the docs/_index.md metadata index.
tools: [execute/runInTerminal, execute/awaitTerminal, execute/getTerminalOutput, read/readFile, read/terminalLastCommand, read/problems, search/listDirectory, search/textSearch, browser/openBrowserPage]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Docs Index SKILL

You help users build and maintain the SPECTRA documentation index.
The index catalogs all documentation files with metadata (sections, entities,
token counts, content hashes) for efficient test generation and coverage analysis.

## Index / reindex documentation

**Step 1** — Show the live progress page:
```
show preview .spectra-progress.html
```

**Step 2** — runInTerminal:
```
spectra docs index --no-interaction --output-format json --verbosity quiet
```

For a full rebuild (ignore hashes, re-process all files):
```
spectra docs index --force --no-interaction --output-format json --verbosity quiet
```

To skip acceptance criteria extraction (index only):
```
spectra docs index --skip-criteria --no-interaction --output-format json --verbosity quiet
```

**Step 3** — awaitTerminal. Wait for the command to finish. Between runInTerminal and awaitTerminal, do NOTHING. No readFile, no listDirectory.

**Step 4** — readFile `.spectra-result.json`

From the JSON result, show:
- Documents indexed vs skipped vs total
- New, changed, and unchanged document counts
- Acceptance criteria extracted (if any)
- Path to `docs/_index.md`

---

## Suggest next steps

After indexing:
- "Generate test cases?" -> use spectra-generate SKILL
- "Extract acceptance criteria?" -> use spectra-criteria SKILL
- "Run coverage analysis?" -> use spectra-coverage SKILL

---

## Example user requests

- "Index the docs"
- "Reindex all documentation"
- "Rebuild the docs index"
- "Update the documentation index"
- "Refresh the doc catalog"
- "Index again the docs"
