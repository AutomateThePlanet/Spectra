---
name: spectra-docs
description: Index documentation, manage the v2 docs/_index/ manifest layout, and discover doc-suite IDs for analyzer filtering.
tools: [{{READONLY_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Docs Index SKILL

You help users build and maintain the SPECTRA documentation index.
The index catalogs all documentation files with metadata (sections, entities,
token counts, content hashes) for efficient test generation and coverage analysis.

**v2 layout (Spec 040, 1.51.0+):** the index lives at `docs/_index/_manifest.yaml` (always loaded, ~2-5K tokens) plus `docs/_index/groups/{suite}.index.md` (per-suite, lazy-loaded) plus `docs/_index/_checksums.json` (never sent to AI). On first run after upgrading from a release that wrote a single-file `docs/_index.md`, the indexer auto-migrates and preserves the legacy file as `docs/_index.md.bak` — no flag required.

## Index / reindex documentation

**Step 1** — Show the live progress page:
```
show preview .spectra-progress.html?nocache=1
```

**Step 2** — runInTerminal:
```
spectra docs index --no-interaction --output-format json --verbosity quiet
```

Incremental mode skips unchanged files (SHA-256 hash check). Use `--force` for a complete rebuild.

For a full rebuild (ignore hashes, re-process all files):
```
spectra docs index --force --no-interaction --output-format json --verbosity quiet
```

To skip acceptance criteria extraction (index only):
```
spectra docs index --skip-criteria --no-interaction --output-format json --verbosity quiet
```

**Step 3** — awaitTerminal. The progress page auto-refreshes — the user can watch live. Between runInTerminal and awaitTerminal, do NOTHING. No readFile, no listDirectory, no checking terminal output, no status messages.

**Step 4** — readFile `.spectra-result.json`. **Never re-run the command** — if result shows status "completed", present the results and stop.

From the JSON result, show:
- Documents indexed vs skipped vs total
- New, changed, and unchanged document counts
- Per-suite breakdown from `suites[]` (suite ID, doc count, tokens, skip-analysis flag)
- Migration record (if `migration.performed == true`): docs migrated, suites created, largest suite, legacy `.bak` path
- Acceptance criteria extracted (if any)
- Path to `docs/_index/_manifest.yaml`

---

## List doc-suites (Spec 040 / 1.51.0+)

When the user asks "what suites are available?" or "list doc suites", or you need to resolve a `--doc-suite` argument before running `spectra ai generate`:

**Step 1** — runInTerminal:
```
spectra docs list-suites --output-format json --no-interaction --verbosity quiet
```

**Step 2** — awaitTerminal, then read the JSON output:

```json
{
  "suites": [
    { "id": "cm_ug_topics", "document_count": 37, "tokens_estimated": 18232, "skip_analysis": false, ... },
    { "id": "POS_UG_Topics", "document_count": 89, "tokens_estimated": 29367, "skip_analysis": false, ... }
  ],
  "total_suites": 10,
  "total_documents": 539
}
```

Use this to pick the right `--doc-suite` for a generate run. Token estimates show which suites fit under the 96K pre-flight budget on their own.

For human display:
```
spectra docs list-suites --output-format json --no-interaction --verbosity quiet
```

---

## Show one suite's index (Spec 040 / 1.51.0+)

When the user asks "show me the index for suite X" or wants to inspect a specific suite's documents:

```
spectra docs show-suite {suite-id} --output-format json --no-interaction --verbosity quiet
```

Returns the per-suite Markdown index file content. Errors with exit 1 + the available-suites list if `{suite-id}` is unknown.

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

---

## Cancel the current run

If the user says "stop", "cancel", "kill it", "stop the analysis", "stop generating":

**Step 1** — runInTerminal:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — awaitTerminal, readFile `.spectra-result.json`.

**Step 3** — Report what happened:
- `status: completed` with `shutdown_path: cooperative` → "Cancelled at phase {phase}. Tests/files written before stopping are preserved."
- `status: completed` with `shutdown_path: forced` → "Force-killed after grace window."
- `status: no_active_run` → "Nothing was running."

If the original command's progress page is still open, point the user at it — it now shows the "Cancelled" terminal phase.
