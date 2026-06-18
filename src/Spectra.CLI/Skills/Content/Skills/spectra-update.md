---
name: spectra-update
description: Update existing test cases after documentation or acceptance criteria changes.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Update SKILL

You help users update existing test cases when documentation or acceptance
criteria have changed. The CLI classifies each test as UP_TO_DATE, OUTDATED,
ORPHANED, or REDUNDANT (deterministic, no model). For OUTDATED tests you then
**edit the affected parts in this session** through a deterministic seam — the
CLI compiles an edit prompt, *you* edit the test in your own context, and the CLI
ingests it. The CLI never rewrites a test for you; the targeted edit happens
in-session.

The seam, per OUTDATED test:

```
compile-update-prompt (CLI, deterministic)  →  you edit in-session  →  ingest-update (CLI, fail-loud)
```

`ingest-update` deterministically protects invariants so you don't have to: it keeps
the **original id** (never a new one), re-asserts any pre-existing **manual verdict /
notes**, and **fails loud on drift** — if your edit changes a field the doc change did
not implicate (priority, component, tags) it rejects the edit and persists nothing.

## Update test cases for a suite

**Step 0** — Set up the live progress monitor.

Run with the Bash tool:
```
spectra ai init-seam-progress
```

Write `.spectra/progress.json` with the Write tool:
```json
{"phases":["Step 1 — Classify tests","Step 2 — Edit OUTDATED tests","Step 3 — Report"],"active":0}
```

**Step 1** — Classify and find the OUTDATED tests.

Run with the Bash tool:
```
spectra ai update --suite <suite> --diff --no-interaction --output-format json --verbosity quiet
```
Replace `<suite>` with the suite the user mentions (e.g., "checkout", "login", "payments").
The progress page auto-refreshes — the user can watch live. While it runs, do NOTHING —
don't poll the terminal, list directories, or read files; just wait for it to complete. Once it finishes, open `.spectra-progress.html` using the VS Code preview (IDE preview tool) to see the run summary. If an IDE preview is not available, run `spectra open .spectra-progress.html`.

Then Read `.spectra-result.json`. Collect the ids classified **OUTDATED**.

- UP_TO_DATE → leave untouched.
- ORPHANED / REDUNDANT → present for the user's review; the edit seam does not touch these.
- OUTDATED → continue to Step 2 for each.

Update `.spectra/progress.json` — N = number of OUTDATED tests:
```json
{"phases":["Step 1 — Classify tests","Step 2 — Edit OUTDATED tests","Step 3 — Report"],"active":1,"loop":{"current":0,"total":N,"label":"starting"}}
```

**Step 2** — For each OUTDATED test (i = 1..N), update progress.json first:
```json
{"phases":["Step 1 — Classify tests","Step 2 — Edit OUTDATED tests","Step 3 — Report"],"active":1,"loop":{"current":i,"total":N,"label":"test i/N: <id>"}}
```

Then compile its edit prompt with the Bash tool:
```
spectra ai compile-update-prompt --suite <suite> --test-id <id>
```
- Exit `0` → the edit prompt is on stdout (the existing test + the changed source/criteria + edit instructions).
- Exit `4` → a required input is missing (no such test, or no changed source/criteria to reconcile against). Skip this test and tell the user why.

**Step 3** — Edit in-session. Read the prompt. Edit **only** the parts the
documentation/criteria change requires. Keep the id, the structure, the priority,
component, tags, and any manual notes. Write the whole edited test as a JSON array
with a single element to `.spectra/updated.json`.

**Step 4** — Ingest (fail-loud), with the Bash tool:
```
spectra ai ingest-update <suite> --test-id <id> --from .spectra/updated.json --output-format json --verbosity quiet
```
- Exit `0` → persisted; the id is unchanged and the index was regenerated.
- Exit `5` → content invalid **or** `DRIFT_DETECTED`. Read the specific error.
  - `DRIFT_DETECTED` → you changed an out-of-scope field; redo the edit touching ONLY what the doc change requires.
  - otherwise → fix the malformed/truncated JSON.
- Exit `6` → schema invalid. Read the error and fix the offending field.

**Bounded retry:** on exit `5`/`6`, re-edit and re-ingest **at most twice** (3 attempts
total) for the same test. If it still fails, stop and report the specific error — do not
loop. On any non-zero exit, nothing was persisted; the original test and indexes are untouched.

**Step 5** — Update progress.json to mark complete, then report.
```json
{"phases":["Step 1 — Classify tests","Step 2 — Edit OUTDATED tests","Step 3 — Report"],"active":2}
```
For the suite, summarize: total analyzed, classification breakdown
(UP_TO_DATE / OUTDATED / ORPHANED / REDUNDANT), tests edited (with ids), and any tests
that failed ingest after retries or were flagged for manual review.

Write `.spectra/progress.json`: `{"active":3}` (terminal — seam-progress page renders Complete).

## Update all suites

When the user says "update all": list suites with
`spectra list --no-interaction --output-format json --verbosity quiet`, then run the
loop above for each suite sequentially.

---

## Classification meanings

When presenting results, explain classifications:
- **UP_TO_DATE** — test matches current documentation, left untouched
- **OUTDATED** — documentation or linked criteria changed; the test is **edited in-session** through the seam, preserving its id and manual fields
- **ORPHANED** — source documentation or criteria were removed; flagged for review (not edited)
- **REDUNDANT** — duplicates another test's coverage; flagged for review (not edited)

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
- "Edit the outdated login test cases to match the new docs"
- "Update all test suites"
- "Are there any orphaned test cases?"
- "Refresh test cases after the API docs update"

---

## Cancel the current run

If the user says "stop", "cancel", "kill it", "stop the analysis", "stop updating":

**Step 1** — Run with the Bash tool:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — Wait for the command to finish, then Read `.spectra-result.json`.

**Step 3** — Report what happened:
- `status: completed` with `shutdown_path: cooperative` → "Cancelled at phase {phase}. Tests/files written before stopping are preserved."
- `status: completed` with `shutdown_path: forced` → "Force-killed after grace window."
- `status: no_active_run` → "Nothing was running."
