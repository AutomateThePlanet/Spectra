---
name: spectra-criteria
description: Extract, import, and browse acceptance criteria for test coverage analysis.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Criteria SKILL

You manage acceptance criteria in SPECTRA. Extraction runs **in this session** through a deterministic
CLI seam — the CLI never calls a model. It **compiles a grounded prompt** per document, *you* perform the
extraction turn in your own context (the compiled prompt already carries the document text), and the CLI **ingests**
and validates what you produced. The seam, per document:

```
docs changed (CLI) → compile-extraction-prompt (CLI) → you extract in-session → ingest-criteria (CLI, fail-loud)
```

There is **no** `spectra ai analyze --extract-criteria` model call anymore. Do not look for one.

## Extract acceptance criteria from documentation

**Step 0 — Initialize live progress.** Run this FIRST, before anything else:

Run with the Bash tool:
```
spectra ai init-seam-progress
```

Write `.spectra/progress.json` with the Write tool:
```json
{"phases":["Step 1 — Find changed docs","Step 2 — Extract criteria","Step 3 — Summarize"],"active":0}
```

### Step 1 — Find the work-list (deterministic, no model)

```
spectra docs changed --output-format json
```

Read the `changed` array. Each entry is `{ path, component, status, current_hash, indexed_hash }` where
`status` is one of:
- `new` / `changed` → extract it in Step 2;
- `moved` (with `from`) → the same content now lives at `path`; handled in 1a, never extracted;
- `deleted` (with `criteria_count`) → the document is gone; handled in 1b.

Also show the user every entry of `warnings` (e.g. duplicate criterion ids, or that criteria storage still
uses the old layout — criteria writes migrate it automatically).

**If `changed` is empty, report "Acceptance criteria are up to date" and STOP** — do not extract anything.
(Unchanged documents are skipped here, so they never reach a model turn. Use `--force`-style full
re-extraction by passing `--include-unchanged` and processing every doc only when the user explicitly asks
to re-extract everything.)

**1a. Moved documents** (`status: moved`). The content is unchanged, so its criteria and their IDs are kept
and nothing is extracted. Apply the move:
```
spectra docs apply-move --from {from} --to {path} --output-format json
```
- Exit 0 → record "moved {from} → {path}, {criteria_count} criteria kept".
- Exit 4 → show `errors[0]` to the user and continue.
- Exit 1 → an error; nothing was written. On `LOCK_TIMEOUT` (another SPECTRA command is writing criteria),
  retry the same command once. Otherwise — or if the retry also fails — report the `error_code`
  (`CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH`; `CONFIG_MISSING` / `CONFIG_INVALID` for a config problem — fix `spectra.config.json` first) with
  `errors[0]` to the user, stop for that document, and continue with the rest.

**1b. Deleted documents** (`status: deleted`). Never retire anything on your own. For each, preview:
```
spectra docs retire --doc {path} --dry-run --output-format json
```
Show the user the `changeset` from stdout: every criterion that would be removed and, for each, its
`referencing_tests` / `test_ids` — the tests that still link to it. Ask the user whether to retire this
document's criteria. Only on an explicit yes, run:
```
spectra docs retire --doc {path} --output-format json
```
Otherwise leave it; it stays reported as `deleted` and nothing is removed.
- Exit 1 from either retire call → an error; nothing was written. On `LOCK_TIMEOUT` (another SPECTRA command
  is writing criteria), retry the same command once. Otherwise — or if the retry also fails — report the
  `error_code` (`CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH`; `CONFIG_MISSING` / `CONFIG_INVALID` for
  a config problem — fix `spectra.config.json` first) with `errors[0]` to the user, stop for that document, and continue with the rest.

The Step 2 work-list is the `new` and `changed` entries only.

Update `.spectra/progress.json` — N = number of `new` + `changed` docs:
```json
{"phases":["Step 1 — Find changed docs","Step 2 — Extract criteria","Step 3 — Summarize"],"active":1,"loop":{"current":0,"total":N,"label":"starting"}}
```

### Step 2 — Per changed document: compile → extract in-session → ingest

For EACH `new` or `changed` entry (i = 1..N), update progress.json first:
```json
{"phases":["Step 1 — Find changed docs","Step 2 — Extract criteria","Step 3 — Summarize"],"active":1,"loop":{"current":i,"total":N,"label":"doc i/N: <path>"}}
```

Then, using its `path` and `component`:

**2a. Compile the extraction prompt** (Bash, capture stdout):
```
spectra ai compile-extraction-prompt --doc {path} --component {component} --output-format json
```
- Exit 0 with a prompt on stdout → proceed to 2b.
- Exit 0 with `{ "short_circuit": true, "outcome": "Extracted", "criteria": [] }` → the document is
  empty; nothing to extract. Skip it (no model turn, no ingest needed).
- Exit 4 (refused, `error_code` `MISSING_INPUT`) → show `errors[0]` and skip that doc.
- Exit 1 (`DOCUMENT_NOT_FOUND`, `DOCUMENT_UNREADABLE`, or a config problem: `CONFIG_MISSING` / `CONFIG_INVALID`) → report the
  `error_code` and `errors[0]` to the user and skip that doc.

**2b. Extract IN-SESSION**: Read the compiled prompt — it already contains the full document text; do not
read `{path}` separately — and identify every testable acceptance criterion exactly as the prompt
specifies. Produce ONLY the JSON array the prompt describes. **Write it to `.spectra/criteria.json`** with the Write tool.

**2c. Ingest (fail-loud)**:
```
spectra ai ingest-criteria --doc {path} --component {component} --from .spectra/criteria.json --output-format json
```
- Exit 0 → persisted; record `persisted`, `ids` and the `changeset` from stdout (`added`, `modified`
  with old/new text, `removed` with `referencing_tests` / `test_ids`, `metadata`). A human summary of the
  same changeset is printed on stderr — you do not need to parse it.
- Exit 5 (empty) or exit 6 (unparseable JSON array) → **fail loud, nothing persisted.** Re-read the
  compiled prompt, regenerate stricter JSON, rewrite `.spectra/criteria.json`, and re-ingest. **Bounded:
  at most 2 attempts** per document; then record that document as failed and continue with the rest.
- Exit 7 (`INVALID_CRITERION_ID`) → you returned an `id` that is not one of this document's existing
  criteria, or used one `id` twice. Nothing was persisted. Regenerate using only ids from the prompt's
  "Existing criteria" list (or omit `id` for new requirements), rewrite `.spectra/criteria.json`, and
  re-ingest — same 2-attempt bound.
- Exit 1 → an error; nothing was persisted. On `LOCK_TIMEOUT` (another SPECTRA command is writing criteria),
  retry the same command once. Otherwise — or if the retry also fails — report the `error_code`
  (`CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH`; `CONFIG_MISSING` / `CONFIG_INVALID` for a config problem — fix `spectra.config.json` first) with
  `errors[0]` to the user, record that document as failed, stop for that document, and continue with the rest.
  Do not regenerate the JSON — the response was not the problem.

Update `.spectra/progress.json`: `{"active":2}` after the loop completes.

### Step 3 — Summarize

Report one combined changeset summary:
- per document: criteria added / modified / removed (from each ingest's `changeset`);
- every **removed** criterion id that tests still reference, with those test ids — these links are now
  broken and the tests need attention;
- moves applied (1a), documents retired (1b), deleted documents the user chose to keep for now;
- documents skipped as unchanged (from Step 1) and any per-document failures.

Suggest next steps: "Update the tests that reference removed criteria?", "Run coverage analysis?" or
"Generate test cases for uncovered criteria?"

Write `.spectra/progress.json`: `{"active":3}` (terminal — seam-progress page renders Complete).

---

## Import acceptance criteria from external file

Import is fully deterministic — no model call, no AI splitting.

**Step 1** — Run with the Bash tool:
```
spectra ai analyze --import-criteria {path} [--replace] --no-interaction --output-format json --verbosity quiet
```
Supported formats: YAML (.yaml/.yml), CSV (.csv), JSON (.json); auto-detected by extension. `--replace`
overwrites existing criteria instead of merging.

**Step 2** — Read `.spectra-result.json`.

**Step 3** — Show: imported count, merge results. Suggest: "List imported criteria?" or "Run coverage analysis?"

---

## List acceptance criteria

**Step 1** — Run with the Bash tool:
```
spectra ai analyze --list-criteria --no-interaction --output-format json --verbosity quiet
```

Filter options: `--source-type`, `--component`, `--priority`

**Step 2** — Read `.spectra-result.json`.

**Step 3** — Show criteria grouped by component with coverage status. Suggest: "Generate test cases for uncovered criteria?"
