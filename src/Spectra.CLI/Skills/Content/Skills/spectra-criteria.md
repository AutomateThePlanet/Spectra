---
name: spectra-criteria
description: Extract, import, and browse acceptance criteria for test coverage analysis.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Criteria SKILL

You manage acceptance criteria in SPECTRA. Extraction runs through a deterministic CLI seam — the CLI never
calls a model. It **compiles a grounded prompt** per document, the `spectra-extractor` subagent performs the
extraction turn in its own context (the compiled prompt carries the document text), and the CLI **ingests** and
validates what it produced. The seam, per document:

```
docs changed (CLI) → per changed doc: spectra-extractor subagent
                       (compile-extraction-prompt → extract → ingest-criteria, fail-loud) → its outcome + changeset lines
```

**Context budget (Spec 092):** this session never reads a changed document or a compiled extraction prompt —
the subagent does, in its own context, and returns a few lines. Moves, retirements and migration stay here,
with the user's confirmation. Report every line a subagent returns, as returned — the user doesn't see a
subagent's own output, so this session's report is how they learn what changed.

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

### Step 2 — Per changed document: the extractor subagent

For EACH `new` or `changed` entry (i = 1..N), in order — one at a time, never in parallel (one criteria lock,
one shared `.spectra/criteria.json`) — update progress.json first:
```json
{"phases":["Step 1 — Find changed docs","Step 2 — Extract criteria","Step 3 — Summarize"],"active":1,"loop":{"current":i,"total":N,"label":"doc i/N: <path>"}}
```

Then invoke the `spectra-extractor` subagent with the Task tool, passing `doc: <path>` and
`component: <component>` from the entry — nothing else. It compiles the extraction prompt, extracts,
ingests (with its own bounded retries), and returns:
- one outcome line: `<path> | extracted`, `<path> | unchanged`, `<path> | rejected` or
  `<path> | failed <code>`;
- a `note:` line (e.g. an empty document);
- the changeset summary lines, verbatim: `Criteria changeset for <path>`, then `+` (added), `~` (modified,
  old → new), `-` (removed, with `still referenced by N tests: <ids>`), `*` (metadata) or `No changes.`;
- `Warning: criterion id … is defined more than once` and other `warning:` lines;
- a `stop: …` line if it hit something no single command covers.

Report every line, as returned, to the user. A `failed` document is reported and the next document runs. A
`stop:` line → report it and continue with the next document. A document with no outcome line is reported
as "outcome unknown"; do not read files to find out.

Update `.spectra/progress.json`: `{"active":2}` after the loop completes.

### Step 3 — Summarize

Report one combined changeset summary, built from the returned lines:
- per document: its outcome and its changeset lines — criteria added / modified / removed, and metadata
  changes, exactly as returned;
- every **removed** criterion id that tests still reference (the `still referenced by` lines), with those test
  ids — these links are now broken and the tests need attention;
- every duplicate-id `Warning:` and other warning;
- moves applied (1a), documents retired (1b), deleted documents the user chose to keep for now;
- documents skipped as unchanged (from Step 1) and any per-document failures (the `failed` lines, with their codes).

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
