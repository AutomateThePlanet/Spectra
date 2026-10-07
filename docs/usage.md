---
title: Usage Guide
parent: User Guide
nav_order: 9
---

# SPECTRA Usage Guide

This guide shows how to use SPECTRA through Claude Code. Every workflow below shows what to say and what to expect. For configuration and customization details, see [Customization](customization.md).

## Prerequisites

1. [Claude Code](https://claude.com/claude-code), installed and signed in.
2. `spectra init` completed in your project (creates SKILL files in `.claude/skills/` and the critic subagent in `.claude/agents/`).
3. Documentation files present in `docs/` (the source material for test generation and acceptance criteria).

## Getting Started

Open Claude Code in your project and just ask. The bundled skills match natural-language requests to the right workflow automatically, so there's no menu to pick from. If you don't know where to start, ask "help me get started" and the bundled `spectra-quickstart` skill will walk you through the options.

---

## Generating Test Cases

The most common workflow. Three levels of specificity:

**Basic:**

> "Generate test cases for checkout"

**With count and focus:**

> "Generate 15 test cases for checkout focusing on error handling"

**Analysis only (review before generating):**

> "Analyze the checkout docs and tell me what test cases you'd generate"

### What to expect

1. The analysis phase identifies testable behaviors and shows existing coverage.
2. You approve the count, and generation begins as a turn in the same session.
3. Each generated test is verified by the `spectra-critic` subagent before it's accepted.
4. The results show the test cases created, verification verdicts, and file paths.
5. Optionally, suggestions for additional coverage areas are included.

### Follow-up prompts

After generating, you can continue:

> "Generate the suggested test cases"
> "Generate suggestions 1 and 3"
> "Create a test case for user session timeout after 30 minutes"

---

## Extracting Acceptance Criteria

> "Extract acceptance criteria from my documentation"
> "Re-extract all criteria (force full extraction)"

### What to expect

1. Each document is processed individually (no truncation), and only documents that changed since the last extraction are re-processed (SHA-256 content hashing).
2. Results are reported per document with criteria counts grouped by RFC 2119 level (MUST/SHOULD/MAY).
3. Files created: one criteria file per document, mirroring its path (`docs/a/login.md` → `docs/criteria/docs/a/login.criteria.yaml`), and a master `docs/criteria/_criteria_index.yaml`.
4. **IDs stay stable.** When a document changes, the assistant sees its existing criteria and keeps each criterion's ID when it still expresses the same requirement, even if reworded. A removed criterion's ID is never reused for something else.
5. **You see what changed.** Each document's refresh reports criteria added, modified (old → new text) and removed, and for each removed criterion the tests that still reference it.
6. **Deleted and moved documents.** A document moved without changes keeps its criteria and IDs. A deleted document is shown with the criteria that would be removed; its criteria are only retired after you confirm.
7. **Inconclusive extractions are retried, not cached.** If a document's extraction comes back unparseable or empty, it's retried once more with a short backoff. Documents that remain inconclusive after retries are reported under `failed_documents` and re-attempted on the next run instead of being silently skipped. A genuine "no criteria found" result (a real, valid empty list) is cached normally.
8. `--force` remains the full re-extraction escape hatch, ignoring the hash cache and re-extracting every document.
9. Two coverage guards help here: `spectra docs index` emits a prominent non-blocking warning when it indexed documents but produced 0 criteria across the corpus, naming the recovery command (`spectra ai analyze --extract-criteria`), and `spectra ai generate` attaches a non-blocking note to the result when the target suite has no matching acceptance criteria, so you know generated tests will not contribute to acceptance-criteria coverage. Neither guard blocks; both surface in the JSON result.

### Troubleshooting

If you see "Indexed but 0 criteria," `spectra docs index` finished but the corpus produced no acceptance criteria. This usually means every per-document extraction came back inconclusive, whether from transient glitches or output truncation. Run `spectra ai analyze --extract-criteria` to retry just the extraction phase on the documents that need it. The exit code on `docs index` is still success, since the warning is informational rather than a failure.

If you see "No acceptance criteria matched suite 'X'," `spectra ai generate` completed but no criteria matched the suite by component, source-doc, or file name. Generated tests have no criteria linkage, so acceptance-criteria coverage will not include them. If criteria are expected for this suite, run `spectra ai analyze --extract-criteria`. If criteria intentionally don't apply (for example, a quick `--from-description` test), the note is just informational.

### Follow-up prompts

> "List the high priority criteria for checkout"
> "Which MUST criteria don't have test cases yet?"

---

## Importing Criteria from External Tools

> "Import acceptance criteria from jira-export.csv"
> "Import criteria from sprint-42.yaml and replace existing"

Supported formats: CSV (with auto-detection of Jira/ADO column layouts), YAML, and JSON. Imports merge by default; pass replace mode if you want to overwrite the target file.

---

## Coverage Analysis

> "Show me coverage gaps"
> "Analyze coverage and link test cases to automation code"

Three dimensions are reported:

- Documentation coverage measures docs ↔ test cases linkage.
- Acceptance criteria coverage measures criteria ↔ test cases linkage.
- Automation coverage measures test cases ↔ code linkage via the `automated_by` frontmatter field.

The optional auto-link mode writes `automated_by` back into your test case files when it can resolve a match.

---

## Generating Dashboard

> "Generate a coverage dashboard"
> "Generate a dashboard preview with our branding"

Opens an interactive HTML dashboard with suite stats, coverage visualizations, and drill-down details. The preview mode renders sample data so you can verify branding and theme settings before running it on real data.

---

## Validating Test Cases

> "Validate my test case files"
> "Validate the checkout suite"

Checks: required frontmatter fields, unique test IDs, valid priority enum values, valid tags, dependency resolution.

---

## Updating Test Cases After Doc Changes

> "Update test cases for checkout since the payment docs changed"

SPECTRA keeps, for every test, a fingerprint of each doc section it references (`source_refs`, e.g.
`docs/checkout.md#Payment`) and each acceptance criterion it links (`criteria`), in
`test-cases/{suite}/_source-baseline.json`. They are recorded automatically when a test is generated
(`ingest-tests`) or edited (`ingest-update`). The `spectra-update` skill then runs:

1. **Triage** — `spectra ai update-triage --suite checkout` reports what changed since each test was written, with
   no model (git is only read, to find renamed docs). Every test lands in a bucket:
   - **RELINK** — a referenced doc file was renamed or moved, and git history says where to;
   - **LOAD** — a number, a modal word (must, should, may, cannot, required, only, not) or many words changed;
   - **CRITERIA_CHANGED** — a linked criterion was reworded;
   - **LIGHT** — a small wording change;
   - **COSMETIC** — markup, whitespace or case only; acknowledged automatically;
   - **REMOVED / CRITERIA_REMOVED** — the referenced section or criterion is gone;
   - **UNCHANGED**, and **UNBASELINED** for tests with no baseline yet.

   Tests that share the same changes are grouped, and each group says why (`numbers: 10gb→50gb`). Sections of a
   changed doc that no test references are listed as generation hints.
2. **Relink** — if there are RELINK tests, the assistant previews `spectra ai relink-sources --suite checkout
   --dry-run` (old → new ref per test), asks you, and only on your yes runs it for real, then triages again. Only the
   ref's path changes (the `#anchor` is kept); the baseline entry moves with its old fingerprint, so anything the
   rename commit also changed now shows as LOAD or LIGHT in the right section.
3. **Review** — the assistant shows the summary and lists REMOVED tests for you to fix (rename the ref, drop it, or
   delete the test if the feature is gone). It never edits those itself. A `not found; no rename found at 90%
   similarity` reason may still be a rename with heavier edits: `relink-sources --min-similarity 60 --dry-run` looks
   further.
4. **Choose** — LOAD only (recommended; includes CRITERIA_CHANGED), LOAD + LIGHT, chosen groups, or report only.
5. **Edit** — for each selected test the assistant edits it in-session through `compile-update-prompt` →
   `ingest-update`, which keeps the test's id and manual notes, fails loud on out-of-scope drift, and records the
   new baseline.
6. **Acknowledge** — tests you reviewed and kept as they are get `spectra ai update-ack --suite checkout --ids …`,
   so they leave triage. A change you neither edit nor acknowledge is reported again next time.

"Update all" is one `spectra ai update-triage --all-suites`.

**Existing workspaces.** A workspace from before this feature has no baselines, so every test shows as UNBASELINED.
Run `spectra ai source-baseline --init --all-suites` once: it adopts the current docs as the baseline.

**Adopting with history.** Adopting today's docs hides every doc change made since the tests were written. In a git
repository you can start the baselines in the past instead:

```bash
spectra ai relink-sources --all-suites --dry-run       # first: refs to docs that were renamed since
spectra ai relink-sources --all-suites
spectra ai source-baseline --init --all-suites --from-date 2026-03-01
spectra ai update-triage --all-suites                  # changes since 2026-03-01 show as LOAD / LIGHT
```

Relink first, so the bootstrap follows each doc back through its renames to the old content. Add
`--min-similarity 60` (50–100) to follow renames that came with heavier edits; if git's history can't be read, the
affected refs stay unbaselined with `git history unavailable: …` instead of being read from the wrong path. `--from-date` uses each
repository's (and submodule's) last commit at or before the date; `--from-ref <rev> --owner <path>` pins one
repository to a commit instead. It only fills missing entries; refs whose file or section didn't exist then stay
unbaselined, with the reason. RELINK shows up for tests without a baseline too, so a restructure made before the first
baseline is still relinkable.

**Docs in a submodule.** If the docs live in a git submodule and the clone was made without
`--recurse-submodules`, the submodule folder is empty. Triage then reports those refs as REMOVED with `not found;
submodule not checked out: <path> (run git submodule update --init)`, in one group per submodule, and adds a note
counting them; `source-baseline --init` warns the same way and leaves them out. It is a setup problem, not a doc
change: run `git submodule update --init` in the repository yourself, then triage again (the assistant won't offer a
baseline or a delete for them). SPECTRA never runs the command for you.

**Where are my docs?** `spectra docs topology` lists the repositories (including submodules) that own the referenced
docs, and the docs in none — the reason relinking or a history bootstrap isn't available for them. SPECTRA only ever
reads git; without git everything works as before and missing files are REMOVED with `not found; no git`.

**Repairs and broken files.** Critic repairs during generation or flagged-test review keep the test's baseline
(`ingest-update --keep-baseline`), so a pending doc change is still in triage afterwards. A test file that fails to
parse keeps its baseline entry: no command prunes a suite while any of its test files can't be read, and each says so.

**Commit the baseline.** Commit `test-cases/*/_source-baseline.json` and `.spectra/source-snapshots/` (the snapshots
let triage show word diffs); `.spectra/update-triage.json` and `.spectra/baseline.lock` are gitignored by
`spectra init`. Git is the undo for a wrong acknowledgement.

`spectra ai update` (the older word-overlap classifier) still works but is legacy; no skill uses it.

### Legacy: `spectra ai update`

`spectra ai update` classifies every test case in the suite as **UP_TO_DATE**, **OUTDATED**, **ORPHANED**, or **REDUNDANT** by word overlap with the *current* docs (deterministic, no model call). It has no record of what the docs said when a test was written, so a change that keeps the vocabulary (`10GB` → `50GB`) stays UP_TO_DATE — which is why triage replaced it. No skill uses it any more.

Classification never changes files with `--dry-run`, and in CI
`spectra ai update <suite> --no-interaction` also only classifies and reports. To mark orphaned tests
from a script, add `--no-review`; marking changes only a test's `status`, `orphaned_reason` and
`orphaned_date`. Tests are matched to their documents through `source_refs` such as
`docs/checkout.md#Payment` — the `#section` part is ignored when finding the file.

---

## Verdict Disposition

The critic assigns every generated test a verdict of **grounded**, **partial**, or **hallucinated**, and those verdicts are made durable and visible, not just used to accept/reject in the moment.

### What happens automatically (in-batch)

| Verdict | Automatic action |
|---------|-----------------|
| **grounded** | Condensed `grounding:` block written into the test's `.md` frontmatter (`verdict: grounded`, `score`, `verified_at`). Full JSON in `.spectra/verdicts/critic-verdict-{id}.json`. |
| **partial** | One bounded repair attempt (compile-repair → agent patches → re-critic). If upgraded to grounded: grounded block + `repaired: true`. If still partial: partial block with condensed findings + `flagged_for_review: true`. Batch continues without prompting. |
| **hallucinated** | Trail entry appended to `.spectra/dropped-tests.json` (`id`, `reason`, `contradicting_claim`, `timestamp`). Then clean three-phase delete (index entry removed, `depends_on` stripped, file deleted). |

The batch report shows four counts: kept-grounded, repaired-to-grounded, flagged-partial, dropped-hallucinated.

### Human review phase (for flagged tests)

After a batch run, review tests still marked `flagged_for_review: true`:

> "Review flagged tests for the checkout suite"

The `spectra-review-flagged` skill lists each flagged test with its condensed verdict and lets you choose per test:
- Accept as-is, which clears `flagged_for_review` and keeps the partial block as acknowledged.
- Retry repair, which runs one more bounded repair cycle.
- Delete, which writes a `user_decided` trail entry and then performs the clean three-phase delete.

To list flagged tests without an interactive session:

```bash
spectra ai review-flagged --suite checkout --no-interaction --output-format json
```

### What gets persisted where

| Store | grounded | partial | hallucinated |
|-------|----------|---------|--------------|
| `test-cases/<suite>/<id>.md` | block added | block added (flagged) | file deleted |
| `_index.json` entry | kept | kept | removed |
| `.spectra/verdicts/critic-verdict-{id}.json` | written | written | written (then test deleted) |
| `.spectra/dropped-tests.json` trail | — | — | entry appended |

`.spectra/verdicts/` and `.spectra/dropped-tests.json` are gitignored scratch, meaning they survive the run but are not committed.

### Repair Batch & Resume

When a batch has many partials, the `spectra-generate` skill runs a **manifest-driven** repair loop that completes reliably across sessions, batching the mechanical steps instead of looping per test:

1. `spectra ai compile-repair-batch --suite <s>` reads all partial verdict JSONs, filters to those still missing a grounding block, and compiles every repair prompt into one JSON manifest, deterministically and with no model call.
2. For each manifest entry, the assistant patches the test in-session and writes the corrected test JSON to `.spectra/updates/<suite>/updated-<id>.json`.
3. Once all repairs are staged, they're ingested in one batch call: `spectra ai ingest-update <suite> --all`.
4. Each repaired test is re-verified by the `spectra-critic` subagent, then all resulting verdicts are classified in one batch call: `spectra ai ingest-verdict --suite <s> --all`.
5. All resulting grounding blocks are written in one batch call: `spectra ai ingest-grounding --suite <s> --all --repaired`.
6. If interrupted or the session runs out, re-running from step 1 picks up exactly where it left off. The grounding block already written into a test's `.md` frontmatter is the resume checkpoint, so nothing is double-processed or restarted from scratch.

To check resume state or inspect grounding status directly:

```bash
spectra ai audit-grounding --suite smoke --output-format json   # resume checkpoint: grounding_written==false → still needs repair
spectra ai compile-repair-batch --suite smoke                   # re-run: emits only the remaining ungrounded partials
```

---

## Executing Tests

> "Run the smoke test selection"
> "Execute the checkout suite"

The execution skill orchestrates: it starts the run, launches the local web console, and hands you the URL, and you record each PASS/FAIL/SKIP/BLOCKED verdict in the browser console (see [Driving a Run from the Web Console](#driving-a-run-from-the-web-console) below); finalize then generates an HTML report. Saved selections (like `smoke`) live in `spectra.config.json` and let you run a curated subset without naming individual test IDs.

Execution is CLI-only (`spectra run`, driving `.execution/spectra.db`), so there is no server to run or configure.

---

## Driving a Run from the Web Console

> "Open the execution console"

For hands-on manual runs, `spectra run console` serves a **local** web page (in Spectra's report
styling) where you drive the run from the browser: click PASS / FAIL / BLOCKED, add a comment, drop or
paste a screenshot, and the page advances. SQLite is the source of truth, so a refresh loses nothing.

```bash
spectra run start checkout          # start (or use an active run)
spectra run console                 # prints a http://127.0.0.1:<port>/ URL — open it
# … record verdicts in the browser; click Finalize when done …
spectra run console --stop          # stop the detached server
```

No server config, no tokens, no network beyond localhost. The console runs detached (survives closing the
launching terminal) and the page is ephemeral/gitignored. See [CLI Reference](cli-reference.md) for the full command and
[Deployment](deployment.md) for how it differs from the static dashboard.

---

## Creating a Custom Profile

> "Create a test generation profile for our API team"
> "Set up a custom test format with request/response fields"

Generation profiles control test output format, including frontmatter fields, step structure, and custom keys. The bundled init flow seeds `profiles/_default.yaml`; you can copy it to a named profile and customize it. Set the active profile in `spectra.config.json` under `ai.profile`. See [Customization](customization.md) for the full reference.

---

## Indexing Documentation

> "Index all documentation"
> "Rebuild the docs index from scratch"

Catalogs every document in `docs/` with metadata (sections, entities, tokens, content hashes). Required before first generation, and re-run automatically before generation, but you can also run it on demand. The force flag rebuilds from scratch ignoring hashes.

---

## Customizing SPECTRA

> "How do I customize test generation quality?"
> "Show me how to focus on security testing"

There are four customization surfaces:

1. Prompt templates in `.spectra/prompts/*.md` control AI reasoning for analysis, generation, criteria extraction, critic verification, and updates.
2. Generation profiles in `profiles/` control test output format.
3. Behavior categories in `spectra.config.json` control what kinds of behaviors the analyzer considers.
4. Focus filtering lets you pass runtime focus via natural language, such as "focusing on security and error handling".

For the complete customization reference, see [Customization](customization.md).

---

## Troubleshooting

### A command appears stuck or waiting for input

All bundled SKILLs pass non-interactive flags. If you see a hang, your installed SKILL files may be out of date, so refresh them with `spectra update-skills`. Customizations you've made to SKILL files are preserved by hash tracking.

### Stale terminology in your installed SKILLs

Same fix: refresh installed SKILL files with `spectra update-skills`.

### Dashboard coverage section empty

Run a coverage analysis first, then re-generate the dashboard. The dashboard reads coverage data; without a recent run it has nothing to show.

### No acceptance criteria found

Index the documentation first, then extract criteria. The order matters because extraction reads the index.

### Test cases aren't linked to acceptance criteria

Re-generate after extracting criteria. Generation auto-loads related criteria as prompt context and writes the linkage into the test case frontmatter.

### Filter ignored / the whole suite ran

If you asked to run only high-priority (or tagged/component) tests but the whole suite started, you were almost certainly sending the filter in the wrong shape. Use the **canonical top-level arrays** shape:

```json
{ "suite": "checkout", "priorities": ["high"], "tags": ["smoke"], "components": ["payments"] }
```

Values within one array are OR'd; different arrays are AND'd. A misplaced field (top-level singular `priority`, or a nested `filters: { ... }` wrapper) no longer slips through silently. It returns an error that names the field and suggests the correct one, so the next attempt lands on the right shape. The legacy nested `filters` object is still accepted but deprecated.

> Keep in mind that run-mode tag matching is **OR-within-array** (any listed tag), so if you need "match all of these tags," split it into separate runs or filter the results afterward.

---

## Complete Pipeline: Start to Finish

For a brand-new project, the recommended sequence is:

1. Initialize SPECTRA by running `spectra init`, which bootstraps config, SKILLs, agents, and profiles.
2. Index the documentation to build the doc catalog.
3. Extract acceptance criteria to pull testable criteria out of the docs.
4. Generate test cases, asking the assistant to generate and review for each suite.
5. Validate the test cases to catch frontmatter and ID issues.
6. Ask for coverage to identify gaps across docs, criteria, and automation.
7. Generate a dashboard for a visual report.
8. Execute a suite with an interactive run that reports PASS/FAIL/SKIP/BLOCKED.

Each step is independent and can be re-run as the underlying docs and test cases evolve.
