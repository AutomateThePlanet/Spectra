---
name: spectra-criteria
description: Extract, import, and browse acceptance criteria for test coverage analysis.
tools: [{{READONLY_TOOLS}}]
---

# SPECTRA Criteria SKILL

You help users manage acceptance criteria in SPECTRA. Run CLI commands with the Bash tool and present results readably.

## Extract acceptance criteria from documentation

**Step 1** — Open the live progress page: Open .spectra-progress.html?nocache=1

**Step 2** — Run with the Bash tool:
```
spectra ai analyze --extract-criteria --no-interaction --output-format json --verbosity quiet
```
For full re-extraction (ignore cache), add `--force`.

**Step 3** — Wait for the command to finish. The progress page auto-refreshes — the user can watch live. While it runs, do NOTHING — don't poll the terminal, list directories, or read files; just wait for it to complete.

**Step 4** — Read `.spectra-result.json`

**Step 5** — Show: documents processed, criteria extracted, new/updated/unchanged counts. Suggest next steps: "Run coverage analysis?" or "Generate test cases for uncovered criteria?"

---

## Import acceptance criteria from external file

**Step 1** — Run with the Bash tool:
```
spectra ai analyze --import-criteria {path} --no-interaction --output-format json --verbosity quiet
```

Supported formats: YAML (.yaml/.yml), CSV (.csv), JSON (.json).
Auto-detects format by extension. Use `--skip-splitting` to disable AI splitting.

**Step 2** — Wait for the command to finish.

**Step 3** — Read `.spectra-result.json`

**Step 4** — Show: imported count, split count, merge results. Suggest: "List imported criteria?" or "Run coverage analysis?"

---

## List acceptance criteria

**Step 1** — Run with the Bash tool:
```
spectra ai analyze --list-criteria --no-interaction --output-format json --verbosity quiet
```

Filter options: `--source-type`, `--component`, `--priority`

**Step 2** — Wait for the command to finish.

**Step 3** — Read `.spectra-result.json`

**Step 4** — Show criteria grouped by component with coverage status. Suggest: "Generate test cases for uncovered criteria?"

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
