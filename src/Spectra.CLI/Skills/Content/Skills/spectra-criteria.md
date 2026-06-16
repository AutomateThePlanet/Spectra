---
name: spectra-criteria
description: Extract, import, and browse acceptance criteria for test coverage analysis.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Criteria SKILL

You manage acceptance criteria in SPECTRA. Extraction runs **in this session** through a deterministic
CLI seam — the CLI never calls a model. It **compiles a grounded prompt** per document, *you* perform the
extraction turn in your own context (reading the document with your file tools), and the CLI **ingests**
and validates what you produced. The seam, per document:

```
docs changed (CLI) → compile-extraction-prompt (CLI) → you extract in-session → ingest-criteria (CLI, fail-loud)
```

There is **no** `spectra ai analyze --extract-criteria` model call anymore. Do not look for one.

## Extract acceptance criteria from documentation

**Progress setup** — before Step 1, initialize the live monitor:
```
spectra ai init-seam-progress
```
Open `.spectra/seam-progress.html` using the VS Code preview (IDE preview tool). If an IDE preview is not available, run `spectra open .spectra/seam-progress.html`.

Write `.spectra/progress.json` with the Write tool:
```json
{"phases":["Step 1 — Find changed docs","Step 2 — Extract criteria","Step 3 — Summarize"],"active":0}
```

### Step 1 — Find the work-list (deterministic, no model)

```
spectra docs changed --output-format json
```

Read the `changed` array. Each entry is `{ path, component, status, current_hash, indexed_hash }` with
`status` = `new` or `changed`. **If `changed` is empty, report "Acceptance criteria are up to date" and
STOP** — do not extract anything. (Unchanged documents are skipped here, so they never reach a model turn.
Use `--force`-style full re-extraction by passing `--include-unchanged` and processing every doc only when
the user explicitly asks to re-extract everything.)

Update `.spectra/progress.json` — N = number of changed docs:
```json
{"phases":["Step 1 — Find changed docs","Step 2 — Extract criteria","Step 3 — Summarize"],"active":1,"loop":{"current":0,"total":N,"label":"starting"}}
```

### Step 2 — Per changed document: compile → extract in-session → ingest

For EACH entry in `changed` (i = 1..N), update progress.json first:
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
- Exit 4 (refused) → show the `message` and skip that doc.

**2b. Extract IN-SESSION**: Read the compiled prompt. Use your file tools to read `{path}` and identify
every testable acceptance criterion exactly as the prompt specifies. Produce ONLY the JSON array the
prompt describes. **Write it to `.spectra/criteria.json`** with the Write tool.

**2c. Ingest (fail-loud)**:
```
spectra ai ingest-criteria --doc {path} --component {component} --from .spectra/criteria.json --output-format json
```
- Exit 0 → persisted; record `persisted` count + `ids`.
- Exit 5 (empty) or exit 6 (unparseable JSON array) → **fail loud, nothing persisted.** Re-read the
  compiled prompt, regenerate stricter JSON, rewrite `.spectra/criteria.json`, and re-ingest. **Bounded:
  at most 2 attempts** per document; then record that document as failed and continue with the rest.

Update `.spectra/progress.json`: `{"active":2}` after the loop completes.

### Step 3 — Summarize

Report: documents processed, criteria persisted (new/updated), documents skipped as unchanged (from
Step 1), and any per-document failures. Suggest next steps: "Run coverage analysis?" or "Generate test
cases for uncovered criteria?"

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
