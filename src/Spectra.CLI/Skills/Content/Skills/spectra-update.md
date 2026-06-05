---
name: spectra-update
description: Update existing test cases after documentation or acceptance criteria changes.
tools: [{{READONLY_TOOLS}}]
---

# SPECTRA Update SKILL

You help users update existing test cases when documentation or acceptance
criteria have changed. The update command classifies each test as UP_TO_DATE,
OUTDATED, ORPHANED, or REDUNDANT — then rewrites affected test cases to match
the current documentation.

## Update test cases for a specific suite

**Step 1** — Open `.spectra-progress.html?nocache=1`

**Step 2** — Run with the Bash tool:
```
spectra ai update --suite <suite> --no-interaction --output-format json --verbosity quiet
```

Replace `<suite>` with the suite name the user mentions (e.g., "checkout", "login", "payments").

**Step 3** — Wait for the command to finish. The progress page auto-refreshes — the user can watch live. While it runs, do NOTHING — don't poll the terminal, list directories, or read files; just wait for it to complete.

**Step 4** — Read `.spectra-result.json`. **Never re-run the command** — if result shows status "completed", present the results and stop.

From the JSON result, show:
- Total test cases analyzed
- Classification breakdown: UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT counts
- Test cases updated (rewritten)
- Test cases flagged for manual review (if any)

**Step 5** — Suggest next steps based on results.

## Preview changes without applying

When the user wants to see what would change before committing:

```
spectra ai update --suite <suite> --diff --no-interaction --output-format json --verbosity quiet
```

The `--diff` flag shows proposed changes without writing files. Present the diff to the user and ask if they want to proceed. If yes, run the command again without `--diff`.

## Update all suites

When the user says "update all test cases" or "update everything":

First, list available suites:
```
spectra list --no-interaction --output-format json --verbosity quiet
```

Then update each suite sequentially, showing progress for each.

---

## Classification meanings

When presenting results, explain classifications:
- **UP_TO_DATE** — test case matches current documentation, no changes needed
- **OUTDATED** — documentation or linked acceptance criteria changed, test case rewritten
- **ORPHANED** — source documentation or criteria were removed, test case flagged for review
- **REDUNDANT** — test case duplicates another test case's coverage, flagged for review

---

## Suggest next steps

After updating:
- "Run coverage analysis?" → use spectra-coverage SKILL
- "Validate the updated test cases?" → use spectra-validate SKILL
- "Generate additional test cases for new coverage gaps?" → use spectra-generate SKILL
- "View the updated test cases?" → use spectra-list SKILL

---

## Example user requests

- "Update test cases for the checkout suite"
- "My docs changed, update the test cases"
- "Check if any test cases are outdated"
- "Preview what would change in the login test cases"
- "Update all test suites"
- "Are there any orphaned test cases?"
- "Refresh test cases after the API docs update"
- "Show me outdated test cases in payments"

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
