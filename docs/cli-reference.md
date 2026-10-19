---
title: CLI Reference
parent: User Guide
nav_order: 1
---

# CLI Reference

All SPECTRA CLI commands organized by function.

Related: [Getting Started](getting-started.md) | [Configuration](configuration.md)

---

## Global Options

| Option | Short | Description |
|--------|-------|-------------|
| `--verbosity` | `-v` | Output level: `quiet`, `minimal`, `normal`, `detailed`, `diagnostic` |
| `--dry-run` | | Preview changes without executing |
| `--no-review` | | Skip interactive review (CI mode) |
| `--output-format` | | Output format: `human` (default) or `json` for structured output |
| `--no-interaction` | | Disable interactive prompts; fail with exit code 3 if required args missing |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Command failed (runtime error, missing config) |
| 2 | Validation errors found |
| 3 | Missing required arguments in non-interactive mode |
| 130 | Cancelled by user |

### Configuration

The criteria commands and the critic/repair prompt compilers load `spectra.config.json` by one rule. **No config
section is ever required.** For write commands, the required config *file* is the protection against writing into
the wrong folder.

| Situation | Read commands¹ | Write commands² |
|---|---|---|
| No `spectra.config.json` | built-in defaults, no warning | exit 1, `No spectra.config.json found. Run 'spectra init' first.` (`CONFIG_MISSING`) |
| Not valid JSON, not an object, unreadable, or an exact duplicate key | exit 1, `spectra.config.json is invalid: …` (`CONFIG_INVALID`) | same |
| Two top-level keys that differ only in case (`coverage` and `Coverage`) | exit 1, `CONFIG_INVALID` naming both spellings | same |
| `source` or `tests` missing or `null`, and the command reads it | defaults + one `Warning: spectra.config.json has no '<section>' section; using the built-in defaults.` | same |
| `coverage` missing or `null` | defaults (`docs/criteria`, `docs/criteria/_criteria_index.yaml`), no warning | same |
| Any section the command doesn't read, missing or `null` | ignored | ignored |

¹ `ai compile-extraction-prompt`, `ai compile-critic-prompt`, `ai compile-repair-prompt`, `ai compile-repair-batch`,
`docs changed`. ² `ai ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`,
`ai analyze --import-criteria`.

Sections each command reads, which are the only ones that can warn:

| Command | Reads |
|---|---|
| `ingest-criteria`, `docs retire` | `tests` (for the referencing-test counts) |
| `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch` | `source`, `tests` |
| `docs changed` | `source` |
| `docs apply-move`, `docs migrate-criteria`, `--import-criteria`, `compile-extraction-prompt` | — |

A warning appears in the command's `warnings` (JSON), or as a `Warning:` line. It never changes the exit code.

In JSON mode the error code (`CONFIG_MISSING` or `CONFIG_INVALID`) is in the failure JSON:
- on stderr for the criteria write commands, `compile-extraction-prompt`, `compile-critic-prompt` and `docs changed`,
  in the shared shape `{ command, success, error_code, errors, warnings }`;
- on stdout for `--import-criteria`, as `ErrorResult.error_code`.

`compile-repair-prompt` and `compile-repair-batch` have no JSON mode, so they print the message as a stderr line.
`CONFIG_SECTION_MISSING` (2.6.0 pre-release) no longer exists.

Any future config rule is checked against the config `spectra init` writes and against a workspace created before the
rule. The test suite does this end to end (`InitTemplateWorkspaceTests`).

---

## Core Commands

### `spectra init`

Initialize a repository for SPECTRA.

```bash
spectra init
```

Creates `spectra.config.json` (including a `coverage` section with `criteria_dir: docs/criteria` and `criteria_file:
docs/criteria/_criteria_index.yaml`), `docs/`, `test-cases/`, `docs/criteria/_criteria_index.yaml`, `profiles/_default.yaml`, `.spectra/prompts/` (5 templates), `.claude/skills/` (16 bundled skills), `.claude/agents/` (6 subagents: `spectra-critic`; `spectra-updater`, `spectra-repairer`, `spectra-extractor` for the update and criteria skills — Spec 092; `spectra-analyst`, `spectra-generator` for the generate skill — Spec 093), `CUSTOMIZATION.md`, and `USAGE.md`.

**Options:**

| Option | Description |
|--------|-------------|
| `--force` | Overwrite existing configuration |
| `--interactive` / `-i` | Enable interactive setup |
| `--no-interactive` | Disable interactive setup |
| `--skip-skills` | Skip creation of SKILL and agent files |

### `spectra validate`

Validate all test case files against the schema.

```bash
spectra validate
spectra validate --path test-cases/checkout
```

Exit `1` (status `failed`, `<n> error(s) found.`) when a test file doesn't parse, the validator reports an error, or a
profile error is counted; the parse errors are listed in the JSON `errors` too (Spec 096 — before, a suite whose tests
didn't parse reported "Validation passed"). Warnings alone exit `0`.

### `spectra show <id>`

Shows one test. The file is found through its suite folder plus the `_index.json` entry's bare `file` (as every
command writes it since 073); the legacy `suite/file` form still works (Spec 096).

### `spectra list`

Lists the tests of every suite, or of one suite.

```bash
spectra list
spectra list --suite checkout
spectra list --suite checkout --all     # every test's details, not just the summary
```

### `spectra config`

Shows or sets `spectra.config.json` values by dotted key, and manages the automation code directories that coverage
analysis scans for `automated_by` links.

```bash
spectra config                          # the whole configuration
spectra config tests.dir                # one value
spectra config source.local_dir docs/   # set a value
spectra config --raw                    # raw JSON
spectra config add-automation-dir tests/e2e
spectra config remove-automation-dir tests/e2e
spectra config list-automation-dirs
```

### `spectra open <path>`

Opens a file or URL in the default application (used by the skills to open generated pages such as the dashboard).

```bash
spectra open site/index.html
```

### `spectra prompts list|show|validate|reset`

Manages the prompt templates in `.spectra/prompts/` (analysis, generation, criteria extraction, critic verification,
update).

```bash
spectra prompts list                       # every template and whether it is customised
spectra prompts show test-update [--raw]   # the template, placeholders resolved (or raw)
spectra prompts validate test-update       # syntax and unknown placeholders
spectra prompts reset test-update          # back to the built-in default (or --all)
```

### `spectra index`

Rebuild `_index.json` files for all suites. `--rebuild` reconstructs each suite's index from the `.md` files of record (parsing every `*.md` under `test-cases/{suite}/` and regenerating the index from the parsed set). Use it to recover from any state where the index has drifted from the on-disk files, for example when recovering tests created by `--from-description` runs from versions before v1.52.3, which wired that path into the index automatically.

```bash
spectra index
spectra index --rebuild
```

### `spectra auth` (removed)

There is no `spectra auth` command anymore, and nothing to authenticate separately. SPECTRA makes
no model calls of its own, since inference runs as ordinary turns in your interactive Claude Code
session, under whichever account is already signed in there. See
[Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md).

### `spectra update-skills`

Update bundled SKILL and agent files to the latest version.

```bash
spectra update-skills
```

Compares installed files against bundled versions using SHA-256 hashes recorded in `.spectra/skills-manifest.json`:
- **Unmodified files** → updated to latest version
- **User-modified files** → skipped with warning (preserves customizations): `Skipped (customized by user):`
- **Missing files** → recreated
- **No record** (Spec 096) → recorded when the file equals the bundled content; otherwise never overwritten:
  `<file>: skipped (no record — it may have been edited); run spectra prompts reset <id> or delete it to get the new version`
  (prompt templates) or `…; delete it to get the new version` (skills, agents, the profile, `CUSTOMIZATION.md`).

Spec 096: the manifest has one key form — the repo-relative path with `/`. An older manifest (relative keys from
`init`, absolute keys from `update-skills`) is normalised on load and saved in that form: two records for one file
both count as unedited, an absolute key from another location (a moved workspace) is re-rooted by its install path,
and a key matching no install path is dropped with one warning:
`<n> manifest entries from another location were dropped (was the workspace moved?)`.

Spec 092: this is how an existing workspace gets the update and criteria subagents (`spectra-updater`,
`spectra-repairer`, `spectra-extractor`) under `.claude/agents/`. Every bundled agent installs there — nothing goes to
`.github/agents/`. A customised update or criteria skill is skipped as usual and keeps its in-session flow until you
refresh it.

It also offers `spectra init`'s `.gitignore` patterns to an existing workspace (Spec 089; Spec 090 FR-D1–FR-D3;
Spec 096 adds `.spectra/verdicts/` and `.spectra/dropped-tests.json`):
- **Only patterns never offered before** are added. Spec 096: the offered set is recorded in `.gitignore` itself, as
  `# spectra-offered: p1, p2, …` lines (`init` writes one naming every pattern; each `update-skills` that offers or
  migrates anything appends one). The file is committed, so a pattern the team removed stays removed in every clone.
  Offered = the marker lines ∪ the old per-clone record (`gitignore_offered` in the skills manifest, migrated once and
  then no longer kept) ∪, only when neither exists, the 089 list without `.spectra/updates/` and `.spectra/repairs/`
  (a workspace from before 090). A negated pattern counts as offered and is marked too.
- **No `.gitignore`** → none is created; the output says `no .gitignore; add: <patterns>` and nothing is recorded.
- **Present** means a whole trimmed, non-comment line equal to the pattern or to the pattern without its trailing `/`
  (a longer line containing it doesn't count). A `!pattern` negation is your choice: the pattern isn't added.
- **Append** keeps every existing byte: one newline in the file's own style (LF or CRLF) only when the file doesn't end
  with one, `# SPECTRA` only when absent, then the patterns, then one marker line. The output names the patterns:
  `Updated: .gitignore (added: …)`.

Plain `spectra init` still refuses on an existing workspace, so this is how existing workspaces get new entries.

Spec 097 (FR-022, FR-022a): the critic agent (`.claude/agents/spectra-critic.agent.md`) is installed with
`model: <ai.critic.model>` (default `sonnet`). After changing the key, run `update-skills`: an unedited critic agent is
rewritten with the new model; an edited one is skipped as customized. `update-skills` reads the config only for this
value and never fails on it — no config → `sonnet`; a config that is invalid for any reason → one warning
`spectra.config.json is invalid (<error_code>: <first error>); the critic agent's model was not changed`, the critic keeps
its installed model line, everything else is refreshed, exit `0`. An agent installed before 097 (pinned
`claude-sonnet-4-6`, unedited) moves to `sonnet`.

Reports a summary of updated, unchanged, and skipped files.

---

## Documentation Commands

### `spectra docs index`

Build or incrementally update the v2 documentation index (manifest + per-suite files + checksum store under `docs/_index/`), then automatically extract acceptance criteria.

```bash
spectra docs index                # Incremental update
spectra docs index --force        # Full rebuild
spectra docs index --no-interaction --output-format json  # SKILL/CI mode
spectra docs index --suites checkout,payments  # Re-index only the named suites
spectra docs index --no-migrate  # Refuse to migrate a legacy _index.md (errors if found)
```

The index now lives at `docs/_index/`:

- `_manifest.yaml` is small (~2-5K tokens) and is always loaded into AI prompts.
- `groups/{suite}.index.md` holds per-suite index files, lazy-loaded only for the suite the user is working with.
- `_checksums.json` is a hash table for incremental detection, and it is never sent to the AI.

On first run after upgrading from a release that used the single-file `docs/_index.md`, the indexer automatically migrates: parses the legacy file, splits entries by suite, writes the new layout, and renames the legacy file to `docs/_index.md.bak` for safekeeping. No flag required.

`docs index` only indexes. Acceptance criteria are extracted by the `spectra-criteria` skill (`docs changed` →
`compile-extraction-prompt` → your session → `ingest-criteria`, below); the automatic extraction step after indexing was
removed in Spec 069, together with its `criteria_warning`. `--skip-criteria` and `--include-archived` are still accepted
and do nothing.

In SKILL/CI mode, the command writes `.spectra-result.json` (structured result with per-suite breakdown and migration metadata) and `.spectra-progress.html` (live progress page).

See [Document Index](document-index.md) and the [Document Index Migration Guide](migration-040.md) for full details.

---

### `spectra docs list-suites`

List every suite in the manifest with document count, token estimate, and analysis status.

```bash
spectra docs list-suites
spectra docs list-suites --output-format json
```

Useful when the pre-flight token-budget check on `spectra ai compile-analysis-prompt` fails, because the error message tells you to narrow with `--doc-suite <id>`, and `list-suites` shows the available IDs and their token costs.

---

### `spectra docs show-suite <suite-id>`

Print one suite's index file content to stdout.

```bash
spectra docs show-suite checkout
spectra docs show-suite SM_GSG_Topics
```

Errors with exit code 1 + the available-suites list if `<suite-id>` is unknown.

---

### `spectra docs changed`

Lists the documents whose acceptance criteria need attention, by comparing each document's SHA-256 with the
`doc_hash` recorded in `_criteria_index.yaml`. Read-only; calls no model.

```bash
spectra docs changed --output-format json
spectra docs changed --include-unchanged      # also list up-to-date documents
```

Each entry in `changed` has a `status`:

| Status | Meaning | What to do |
|---|---|---|
| `new` | No criteria extracted yet | Extract (`compile-extraction-prompt` → `ingest-criteria`) |
| `changed` | Content changed since the last extraction | Re-extract; existing criterion IDs are offered for reuse |
| `moved` | A new path whose content equals a deleted document's (`from`) | `spectra docs apply-move --from <from> --to <path>` |
| `deleted` | An indexed document that no longer exists (`criteria_count`) | Review, then `spectra docs retire --doc <path>` |

A move is reported only when exactly one new document and exactly one deleted document share the hash;
otherwise they are listed as `new` and `deleted`. Imported criteria are never reported as deleted. Paths are
compared normalised (repository-relative, `/`), so an index entry written as `docs\x.md` still matches.

The result also carries `duplicate_ids` (criterion IDs defined more than once, with their files; reported,
never renumbered), `migration_needed` (the criteria files still use the pre-2.6 layout) and `warnings`. The
exit code is 0. With no `spectra.config.json` it runs on the built-in defaults; a malformed or invalid config
exits 1 (see [Configuration](#configuration)).

---

### `spectra docs apply-move`

Applies a move reported by `docs changed`. The criteria and their IDs are kept; only `source_doc` and the
criteria file location follow the new path. No extraction runs.

```bash
spectra docs apply-move --from docs/refunds.md --to docs/billing/refunds.md [--dry-run]
```

Refuses (exit 4) unless `--from` is an indexed document that no longer exists (`NOT_INDEXED`,
`DOC_STILL_EXISTS`), `--to` exists (`TARGET_MISSING`) and has no criteria yet (`TARGET_INDEXED`), and its
content equals what was recorded for `--from` (`CONTENT_CHANGED`: extract it instead). Exit 1 on other errors.

If `--from` shares its criteria file with another document (a pre-2.6 collision), only the criteria whose
`source_doc` is `--from` move; they are taken out of the shared file, and the other document's criteria stay.

---

### `spectra docs retire`

Retires a deleted document: removes its entry from `_criteria_index.yaml` and deletes its criteria file.
Nothing is retired without this explicit call.

```bash
spectra docs retire --doc docs/old-feature.md --dry-run   # preview
spectra docs retire --doc docs/old-feature.md
```

The result's `changeset` lists every criterion as removed, each with `referencing_tests` and `test_ids` (the
tests whose `criteria:` still name it), so you can see which links break. In JSON mode the human summary goes to
stderr. Refuses (exit 4) a document that still exists (`DOC_STILL_EXISTS`) or is not indexed (`NOT_INDEXED`).
Criterion ID numbers are never freed for reuse.

If the document shares its criteria file with another document (a pre-2.6 collision), only the criteria whose
`source_doc` is that document are removed from the file; the file stays for the other document.

---

### `spectra docs migrate-criteria`

Moves criteria files from the pre-2.6 layout (`docs/criteria/{basename}.criteria.yaml`) to one file per
document, mirroring the document's repository path (`docs/a/login.md` → `docs/criteria/docs/a/login.criteria.yaml`).

```bash
spectra docs migrate-criteria --dry-run
spectra docs migrate-criteria
```

- Every criterion ID is preserved, `source_doc` is normalised, and the ID high-water marks are seeded.
- It is idempotent: a second run reports `already_migrated: true` and changes nothing.
- Index entries that already share one file (two docs with the same file name overwrote each other in the old
  layout) are reported under `collisions` and left as they are; re-extract both documents to split them.
- `ingest-criteria`, `retire`, `apply-move` and `ai analyze --import-criteria` run the migration automatically
  before they write. `docs changed` and `compile-extraction-prompt` never move files; they only warn.

### `spectra docs topology` (Spec 086)

Read-only diagnostic: which git repositories own the docs your tests reference. It explains why relinking or a history
bootstrap is, or isn't, available for a doc.

```bash
spectra docs topology
spectra docs topology --output-format json
```

```json
{"command":"docs-topology","success":true,
 "owners":[{"root":".","is_submodule":false,"head":"<40 hex>","shallow":false,"referenced_docs":12},
           {"root":"docs/requirements/gitbook-repo","is_submodule":true,"head":"<40 hex>","shallow":false,"referenced_docs":340}],
 "unowned":[{"doc":"/abs/path/shared/x.md","reason":"not in a git repository"}],"warnings":[]}
```

"Referenced docs" are the files the refs of every test in every suite resolve to. `root` and `doc` are repo-relative
when inside the repository, absolute otherwise; owners are ordered by root, unowned docs by path. A `shallow` owner
can't see renames older than its history (`(shallow history)` in the reasons). The human table is printed in every
mode (stderr in JSON mode). Exit `0`; a missing config means defaults.

---

## AI Generation Commands

### `spectra ai compile-prompt` (inverted handoff)

Deterministic, **model-free** prompt compiler. Assembles the grounded generation
prompt (doc criteria + profile schema) for a suite and writes it to **stdout**,
calling no model and spending no tokens.

```bash
spectra ai compile-prompt reporting --count 5 --focus "export to PDF"
spectra ai compile-prompt reporting --output-format json   # machine-readable refusal
```

- It is deterministic, meaning identical inputs produce byte-identical output.
- It refuses to emit when a required input (criteria context, positive count,
  user prompt) is missing, writing nothing to stdout and reporting the missing
  input to stderr (and a `missing_input` JSON field) while exiting **4**. It never
  emits a degraded prompt with a missing section.
- Writes nothing to disk.

Intended to be driven by a generation skill: the skill runs this to get the
prompt, the interactive agent generates the JSON, then the skill hands the
result to `ingest-tests`.

In count mode (`--count`, `--from-description`) the prompt tells the model to read the documentation with its file
tools; test ids are assigned when the tests are ingested. Since Spec 094 no generation prompt names the tools that
were removed in spec 069 (`ListDocumentationFiles`, `ReadTestIndex`, `CheckDuplicates`, `GetNextTestIds`).

#### `--batch <id>` (Spec 094)

```bash
spectra ai compile-prompt --suite storage --batch 2 [--count 7] [--focus "..."] --output-format json
```

Compiles the prompt for one batch of the suite's **generation plan** (written by `ingest-analysis`, below). The prompt
holds only that batch's behaviours (`- B3 [boundary · BVA] <title> (<ref>)`), the text of each of its doc sections
once (fenced under `### <ref>`; a whole-document ref absorbs its sections, a section absorbs its sub-sections), and
only the acceptance criteria of those sections — matched by `source_doc` and `source_section`; criteria without a
`source_section` (or naming a heading the document no longer has, with a warning) only for a whole-document ref. When
none match, all criteria of the batch's documents are used, with a warning. The prompt says the sections are the
complete source for the batch and that each test's `source_refs` are the section refs of the behaviours it covers. The
count is the batch's planned count unless `--count` is given. Identical inputs give byte-identical prompts.

The prompt goes to stdout. stderr carries the mode and any warnings — in JSON mode one line
`{"command":"compile-prompt","success":true,"mode":"batch","batch":"2","warnings":[…]}`, otherwise `Mode:` and
`Warning:` lines.

| Mode | When | Prompt |
|---|---|---|
| `batch` | a numbered batch | the batch prompt above |
| `count` | `--batch unsourced` (behaviours whose source didn't resolve) | count mode, focus = the unsourced titles |
| `count_fallback` | `.spectra/prompts/test-generation.md` predates 094 (no `{{source_sections}}`) and was customised, or has no skills-manifest record (e.g. a fresh clone) | count mode with your template, focus = the batch's titles and refs, plus a warning naming `spectra prompts reset test-generation` |

An unedited pre-094 copy (its hash equals the skills manifest's record) compiles in `batch` mode with the bundled
template and the warning "using the bundled test-generation template; run spectra update-skills to refresh your
copy". A template never causes a refusal.

Checks, in order — plan, batch, staleness, criteria, size (for `unsourced`: plan, batch, then count mode; for
`count_fallback`: no size check). Refusals exit `4` with the shared failure shape on stderr
(`{command, success, error_code, errors, warnings}`), and nothing is written:

| Code | When |
|---|---|
| `PLAN_MISSING` | no plan for the suite, or an unreadable one ("re-run the analysis") |
| `BATCH_NOT_FOUND` | no batch with that id (the message lists the plan's ids) |
| `PLAN_STALE` | a section of the batch changed since the analysis, or no longer resolves (one `errors` entry per section) — re-run the analysis. Batch mode only: Spec 098 (F8) decides the template mode first, and in `count_fallback` (the generator reads the current docs itself) a changed section is the warning "the documentation changed since the analysis; behaviour titles may be outdated (…)" instead |
| `MISSING_INPUT` | no acceptance criteria for the batch (`missing_input: "criteria_context"`) |
| `PROMPT_TOO_LARGE` | the estimate exceeds `analysis.max_prompt_tokens` (default 96,000) |

`--batch` with `--from-description` is a usage error (exit 1). Count mode without `--batch` keeps its options and its
refusal shape (`{refused, missing_input, message}`).

### `spectra ai ingest-tests` (fail-loud boundary)

Validates agent-generated test content and persists it **only when the whole
batch is valid**. Content is read from `--from <file>` or stdin.

```bash
spectra ai ingest-tests reporting --from ./generated.json
cat ./generated.json | spectra ai ingest-tests reporting
```

- It fails loud with zero persistence on failure, meaning malformed JSON, a truncated array
  (no salvage), empty content, zero valid tests, or any schema violation persists
  **nothing** and returns a machine-readable `error_code`
  (`EMPTY_CONTENT` / `MALFORMED_JSON` / `TRUNCATED` / `NO_TESTS` / `SCHEMA_INVALID`).
- It is batch-atomic, so one invalid test fails the whole batch.
- Valid content persists via the unchanged `TestPersistenceService` (writes
  `.md` files + regenerates `_index.json`).
- By convention, `_index.json` `file` fields are always the bare filename
  (e.g. `TC-100.md`), never suite-prefixed (`unit-converter/TC-100.md`); `ingest-tests` and
  `ingest-update` both enforce this on every round, including second-round re-ingests where
  existing test files are picked up from disk.
- Exit code is `0` on success, `5` on a content-class failure, and `6` when the schema is invalid.
- Spec 084: it takes `.spectra/baseline.lock` and loads the suite's `_source-baseline.json` **before** persisting
  (`LOCK_TIMEOUT` / `BASELINE_UNREADABLE`, exit 1, persist nothing), then records a baseline entry for every test it
  persisted; the success JSON gains `"baseline":{created,updated,unchanged,snapshots_written}`. If recording fails
  after the tests were persisted, it exits 1 with `BASELINE_WRITE_FAILED` (or the store's code) and `persisted:[ids]`,
  naming the recovery command. Baseline failures use the shared failure shape; the content and schema failures
  (exit 5/6) keep their `{success, error_code, errors}` shape.

### `spectra ai compile-extraction-prompt` (criteria re-homing)

Deterministic, **model-free** compiler for the **acceptance-criteria extraction**
prompt of a single document, serving as the criteria-extraction analogue of `compile-prompt`.
Writes the prompt to **stdout**; calls no model and spends no tokens.

```bash
spectra ai compile-extraction-prompt --doc docs/payment.md --component payment
```

- It is deterministic, so identical inputs produce byte-identical output.
- It refuses to emit when `--doc` is missing, writing nothing to stdout and reporting the missing input
  (`missing_input` JSON field) while exiting **4**. A document that does not exist or cannot be read exits
  **1** (`DOCUMENT_NOT_FOUND` / `DOCUMENT_UNREADABLE`).
- In JSON mode every failure is exactly one JSON document on stderr, in the shape shared by all criteria commands:
  `{ "command": "compile-extraction-prompt", "success": false, "error_code", "errors", "warnings" }`. A refusal has
  `error_code: "MISSING_INPUT"` and adds `missing_input`; `error_code` is `null` for a config error. On success,
  warnings are printed to stderr as text and the prompt is on stdout.
- An empty or whitespace document short-circuits as a genuine
  "nothing to extract" (`Extracted, []`): it emits **no** prompt, performs no
  model turn, and exits **0**.
- Writes nothing to disk.
- The prompt lists the document's **existing criteria** (`id`, `text`, `source_section`, as compact JSON)
  and asks the model to return an existing `id` when a criterion expresses the same requirement, so IDs stay
  stable across rewordings. They are found through the document's `_criteria_index.yaml` entry, in either
  storage layout. A customised `.spectra/prompts/criteria-extraction.md` without `{{existing_criteria}}` still
  compiles, with a warning to run `spectra prompts reset criteria-extraction`.
- When the document shares its criteria file with another document (a pre-2.6 collision), only the criteria whose
  `source_doc` is this document are offered, which are the only IDs `ingest-criteria` accepts for it.

### `spectra ai ingest-criteria` (fail-loud boundary)

Classifies agent-extracted criteria content and persists it **only** when the
outcome is a genuine extraction. Content is read from `--from <file>` or stdin.
The criteria-extraction analogue of `ingest-tests`.

```bash
spectra ai ingest-criteria --doc docs/payment.md --from ./criteria.json
cat ./criteria.json | spectra ai ingest-criteria --doc docs/payment.md
```

- It fails loud with zero persistence on failure: an empty response or unparseable
  content persists **nothing** and reports the typed `outcome`
  (`EmptyResponse` / `ParseFailure`). There is no cache poisoning, because the criteria index is
  left untouched so a later run re-attempts.
- A genuine `Extracted` result persists to the document's own `.criteria.yaml` file plus a criteria-index
  upsert (`outcome: extracted`). `source_doc` is stored repository-relative with `/`, however `--doc` was typed.
- **Stable IDs.** Each criterion may carry an optional `id`. It is kept only if it is one of this document's
  existing criteria and appears once; otherwise the ingest fails with exit **7** (`INVALID_CRITERION_ID`) and
  writes nothing. A criterion without an `id` keeps an existing ID when its normalised text (markup, whitespace
  and case ignored) is unchanged; then its stored text is kept verbatim.
- **Never-reused IDs.** New IDs come from `id_high_water_marks` in `_criteria_index.yaml` (committed, per prefix
  such as `AC-CHECKOUT`), so a removed criterion's number is never issued again, and two documents with the same
  component never collide. Import uses the same allocator.
- **Changeset.** The JSON result has `changeset` (`added`, `modified` with old/new text, `removed` with
  `referencing_tests`/`test_ids`, `metadata` changes by field), plus `written`, `duplicate_ids` and `migration`.
  A human summary of the changeset is always printed: on stderr in JSON mode (stdout is exactly the JSON) and on
  stdout otherwise. When nothing changed, nothing is written and the criteria file stays byte-identical.
- Writers take `.spectra/criteria.lock`, so concurrent ingests never lose an update or share an ID.
- `--dry-run` computes everything (IDs, changeset) without writing.
- Tags compare as a case-insensitive set: tags that differ only in order or case are not a change, and the
  stored text is kept.
- Failures in JSON mode are one document on stderr in the shape shared by all criteria commands:
  `{ "command": "ingest-criteria", "success": false, "error_code", "errors", "warnings" }`. A classified response
  that is refused, or fails to parse, adds `outcome` (`Rejected`, `EmptyResponse`, `ParseFailure`). Errors before
  any response is classified (config, `--from` file not found) carry no `outcome`.
- Comparing criterion text ignores markup: Markdown emphasis, links and code, HTML comments, and tags of known HTML
  elements in one of five HTML forms:
  1. a closing tag (`</Summary>`);
  2. a tag with at least one attribute (`<details open>`, `<col span=2>`);
  3. a self-closing bare tag (`<br/>`, `<hr />`);
  4. lowercase `<br>`, `<hr>`, `<wbr>`;
  5. an opening tag closed later with **exactly** the same spelling (`<p>x</p>`, `<P>x</P>`).

  Everything else in angle brackets is text, so a change inside it is reported. That includes `<source>`, `<input>`,
  `<summary>` with no matching close, `<Summary>`, `<Source>`, `<LIMIT>`, `<BR>`, comparisons such as `a<b and c>d`,
  and `<Summary>` when only `</summary>` follows. Known residual: a lowercase placeholder whose exact lowercase closing
  tag appears later in the same text is treated as markup.
- Exit code is `0` when extracted/persisted (or unchanged), `5` for `EmptyResponse`, `6` for `ParseFailure`,
  `7` for `INVALID_CRITERION_ID`, and `1` for other errors (`CRITERIA_FILE_UNREADABLE`,
  `CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH`, `LOCK_TIMEOUT`, config errors).

> This compile/ingest pair is the only criteria extraction. The in-process extraction that `spectra docs index` and
> `spectra ai analyze` used to run was removed in Spec 069; an empty or malformed response is reported per document
> (exit `5` / `6`) instead of aborting the corpus.

### `spectra ai compile-critic-prompt` (critic subagent)

Deterministic, model-free compilation of the **critic verification prompt** for one generated
test, serving as the verification analogue of `compile-prompt`. It writes only the fingerprint record (below) —
never the verdict — and calls no model. The
`spectra-critic` subagent runs the emitted prompt in a fresh, isolated context
(test artifact + the source text it cites only) and hands its verdict to `ingest-verdict`.

```bash
spectra ai compile-critic-prompt --suite checkout --test TC-900
spectra ai compile-critic-prompt --suite checkout --test TC-900 --full-docs
spectra ai compile-critic-prompt --test ./tc-900.json --docs ./docs/checkout
```

- **What the critic sees (Spec 101).** In suite mode without `--docs`, each `source_ref` gives the critic only what it
  cites: an anchor ref (`docs/a.md#login`) gives that section — its heading, body and subsections, found by the same
  matcher as `update-triage` — and a ref without an anchor gives the whole file. The cited sections of one document are
  one item, in first-reference order; a section inside another cited section, or cited twice, appears once. An anchor
  that no longer matches a heading gives the whole document, after the line
  `> Note: the anchor "#<anchor>" was not found in this document, so the whole document is given.` An anchor that
  matches two headings uses the first, with a `Warning:` line on stderr.
- **`--full-docs`** gives the pre-101 input instead: each referenced file whole. It is the escape hatch for a test the
  cited sections don't explain, and the baseline of the Spec 101 quality comparison. The `spectra-critic` agent passes
  it only when its caller asks for `full_docs: true`; the skills never do. With `--docs` or the legacy `--test <path>`
  form, the given documents are used as before and `--full-docs` changes nothing.
- Either way, each document item is cut at 8,000 characters and at most 5 documents are given (unchanged). The prompt
  holds no acceptance criteria (it never did; Spec 101 removed the unused criteria load).

- It is deterministic, so identical `(test, docs)` input produces a byte-identical prompt (no timestamps/GUIDs).
- It refuses to emit when the test artifact is missing (no id/title), exiting `4`; an empty `--docs` set is
  allowed (the prompt notes "no documentation provided") and is not treated as a refusal.
- Exit code is `0` when compiled/emitted, `4` when refused, and `1` on an environment error.
- Spec 090 (FR-A2): for each test it compiles (suite mode or `--test <path>`), it writes the fingerprint record
  `.spectra/verdicts/critic-input-<id>.json` = `{id, suite, file, fingerprint}` — the content the critic judges, not its
  grounding block. Nothing is written on a refusal. `ingest-verdict` / `ingest-grounding` refuse a verdict whose test
  changed since (`VERDICT_STALE`).
- Spec 091 (FR-001): the record also has `compiled_at` (UTC, ISO 8601), when it was written. A verdict file written
  more than 2 s before it is stale (`VERDICT_STALE`, "older than the last critic prompt"), so a critic run that compiled
  the prompt but wrote no verdict never revives an older one. Nothing is deleted or renamed.
- Compiling the whole suite (`--suite S` without `--test`) records `compiled_at` for every test, so every verdict
  written before it — including partial verdicts waiting for repair — becomes stale and needs a new critic run.
- Spec 097 (FR-013): the time a verdict is compared with is the **record file's own write time**, set by the same file
  system as the verdict file, so a CLI clock that differs from the file system's (a network share, a container mount)
  no longer refuses new verdicts. `compiled_at` still marks a 091+ record (a 090 record keeps the fingerprint-only rule)
  and is used only when the record's file time can't be read. Every compile rewrites the record, so its file time
  always marks the last compile.

### `spectra ai ingest-verdict` (fail-loud boundary)

Classifies an agent-produced critic JSON into a typed outcome and reports the verdict + gate
decision. Content is read from `--from <file>` or stdin.

```bash
echo '{"verdict":"hallucinated","score":0.1,"findings":[]}' | spectra ai ingest-verdict
spectra ai ingest-verdict --from ./verdict.json --output-format json
```

- The gate is advisory: only `hallucinated` reports `drop`, while `grounded`/`partial` report `pass`.
- It fails loud on damage, so a missing or unparseable `verdict` or `score` is a `ParseFailure` with a
  specific error, **never** a silent `partial`/`0.5` soft pass. Empty input is `EmptyResponse`.
- Exit code is `0` for a classified verdict, `5` for `EmptyResponse`, `6` for `ParseFailure`, and `1` for an environment error.
- Spec 090 (FR-A2): `--from .spectra/verdicts/critic-verdict-<id>.json` with a fingerprint record whose test has changed
  since → exit `4`, `VERDICT_STALE` (shared failure shape). `--suite <s> --all` doesn't classify stale verdicts:
  `skipped_stale` (count) and `stale` (ids). A verdict without a record (from before 090, stdin, or another path) is
  accepted as before.
- Spec 091 (FR-002): a verdict file last written more than 2 s (the file-system timestamp allowance) before its record's
  `compiled_at` is also stale — the message says it is older than the last critic prompt for that test. A record
  written by 090 (no `compiled_at`) keeps the fingerprint-only rule; the rule compares times, not contents, so a new
  verdict written after the compile is fresh even when byte-identical to an older one.
- Spec 093 (FR-016–FR-018): `--suite <s> --all` also returns `results` — one `{id, verdict, score, gate}` per classified
  test (`verdict` grounded / partial / hallucinated / manual; `gate` `drop` for hallucinated, else `pass`) — and
  `not_found` (`[]` without `--ids`). Stale, unreadable and unparseable verdicts have no `results` entry; their lists
  and counts are unchanged. The skills route each test by its `results` entry.

| Flag | Description |
|------|-------------|
| `--ids a,b` | Spec 093: with `--all`, classify only these tests (comma-separated; trimmed, de-duplicated, case-insensitive — the 091 rule). Ids not in the suite → `not_found`. Without `--all` → `--ids is only valid with --all.` (exit 1) |

```bash
spectra ai ingest-verdict --suite storage --all --ids TC-101,TC-102 --output-format json
```

The batch's human summary — the counts line, then `TC-101: grounded 0.92 (pass)` per result and `TC-999: not found in
suite 'storage'` per unknown id — goes to stdout in human mode and to stderr in JSON mode (Spec 093). The single
`--from` / stdin form is unchanged.

Spec 097:
- An `--ids` value that is empty after trimming (`""`, `" , "`) counts as not given: every verdict of the suite is
  classified (before, an empty list classified none).
- `--dry-run` is accepted in both forms. `ingest-verdict` only reads and classifies — it writes nothing either way — so
  the result is the same, plus `"dry_run": true`.

### `spectra ai ingest-grounding` (durable verdict write-back)

Writes a condensed grounding block into the test's `.md` frontmatter from a critic verdict JSON.
`grounding_written` reflects the **actual on-disk `.md` frontmatter**, so the writer and oracle always agree on `test-cases/{suite}/{id}.md` and the `grounding:` field.

**Per-test mode (used inside the repair loop):**
```bash
spectra ai ingest-grounding --suite reporting --test TC-113 \
  --from .spectra/verdicts/critic-verdict-TC-113.json
# After repair (upgraded to grounded):
spectra ai ingest-grounding --suite reporting --test TC-113 \
  --from .spectra/verdicts/critic-verdict-TC-113.json --repaired --repair-attempts 1
```

**Batch mode (replaces per-test shell loops entirely):**
```bash
# After 8a critic loop — write all grounded tests in one call (partial skipped: pre-repair filter)
spectra ai ingest-grounding --suite reporting --all --output-format json
# After 8b repair loop — write the re-critiqued tests (grounded + still-partial); --ids is required with --repaired
spectra ai ingest-grounding --suite reporting --all --repaired --repair-attempts 1 --ids TC-113,TC-114 --output-format json
```

`--all` scans `.spectra/verdicts/critic-verdict-*.json`, filters to the named suite via `_index.json`, and writes each eligible block in one pass. It is idempotent, since tests whose `.md` already has a `grounding:` block are skipped (`skipped_already_written`). Without `--repaired`, partial verdicts are skipped (`skipped_partial_pre_repair`) because they need one repair attempt first. With `--repaired`, the listed tests' `grounded` and `partial` re-verdicts are written.

| Flag | Description |
|------|-------------|
| `--ids a,b` | Spec 091: with `--all`, consider only these tests (comma-separated; trimmed, de-duplicated, case-insensitive). Required with `--all --repaired`. Ids not in the suite are listed under `not_found` (not a failure) |

Argument checks (Spec 091), in this order, before config, index or verdicts are read — usage errors are plain stderr
in both output modes, exit `1`, nothing written:
1. `--test` with `--all` → `Use either --test <id> or --all, not both.` (before 091, `--all` won);
2. `--ids` without `--all` → `--ids is only valid with --all.`;
3. neither `--test` nor `--all` → `Either --test <id> or --all must be specified.`;
4. `--all --repaired` with no ids → exit `4`, `IDS_REQUIRED` (shared failure shape): only the tests re-critiqued after
   a repair may be marked repaired — never a test whose repair failed to ingest, nor a partial verdict left over from an
   interrupted run.

Batch output (JSON): `{ command, mode:"batch", suite, written, skipped_already_written, skipped_partial_pre_repair, errors, skipped_manual, manual, skipped_stale, stale, skipped_hallucinated, hallucinated, not_found }`.
The human summary — the counts line, then `TC-140: hallucinated, waiting for review` per kept hallucinated test and
`TC-999: not found in suite 'reporting'` per unknown id — goes to stdout in human mode and to stderr in JSON mode.

Spec 090:
- **Manual verdicts are never overwritten (FR-A3).** `--test` → exit `4`, `MANUAL_VERDICT`; `--all` → `skipped_manual`
  (count) and `manual` (ids), checked before the already-written skip.
- **Stale verdicts are refused (FR-A2).** A verdict whose fingerprint record no longer matches the test → `--test`:
  exit `4`, `VERDICT_STALE`; `--all`: `skipped_stale` / `stale`. Nothing is written.
- An update edit drops the test's critic block and verdict (see `ingest-update`), so the existing skip-if-present rule
  lets the new verdict land — there is no replace option.

Spec 091:
- **Verdicts older than the last critic prompt are stale** (`compiled_at` rule, see `compile-critic-prompt`). `--test
  [--from <file>]` is checked by test id, against the last-write time of the file actually read.
- **Kept hallucinated tests (F3).** In `--all`, a hallucinated verdict for a test whose file still exists is counted in
  `skipped_hallucinated` and listed under `hallucinated` — kept for review (the update flow leaves that decision to the
  user), not written and not an error. Unreadable or unparseable verdict files and refused writes still count as
  `errors`; a test whose file is gone (dropped by the generation flow) is skipped as before.

Spec 097:
- **Empty `--ids` counts as not given** (FR-016): `--all --ids ""` processes every eligible test, as `--all` alone;
  `--all --repaired` with an empty list is still `IDS_REQUIRED`. Without `--all`, an empty `--ids` is no usage error.
- **`--all --dry-run`** (FR-017): the same JSON (plus `"dry_run": true`), human summary and exit code as a real run;
  no test file is written. `--test <id> --dry-run` → exit `1`, `--dry-run requires --all` (plain stderr).
- The skills pass `--ids` with exactly the tests they confirmed: the generate skill's critic step the kept-grounded ids,
  the update skill's re-verify step the group's grounded ids (skipped when there are none), so a test reported
  unverified never gets a block.

- **grounded** → condensed "verified clean" block (`verdict: grounded`, `score`, `verified_at`).
- **partial** → block with condensed findings + `flagged_for_review: true` (unless `--repaired`; the `spectra-review-flagged` skill then dispositions flagged tests).
- **hallucinated** → `--test` refuses (exit 4); use `record-drop` + `spectra delete` instead. `--all` lists it under `hallucinated` (Spec 091).
- Exit code is `0` when written, `4` when hallucinated is refused, `5` for an empty verdict, `6` for a parse failure, and `1` for an environment error.

### `spectra ai record-drop` (drop audit trail)

Appends a hallucinated-test entry to `.spectra/dropped-tests.json` before the three-phase clean
delete (`spectra delete`). The trail is append-only NDJSON, gitignored scratch.

```bash
spectra ai record-drop --suite reporting --test TC-138 \
  --from .spectra/verdicts/critic-verdict-TC-138.json
# Manual user decision during review:
spectra ai record-drop --suite reporting --test TC-138 --reason user_decided
```

- Fields written: `id`, `suite`, `title`, `drop_reason` (hallucinated|user_decided), `contradicting_claim` (from verdict), `doc_ref`, `critic_model`, `timestamp`, `source` (critic|review).
- Exit code is `0` when appended and `1` on error.

### `spectra ai compile-repair-prompt` (repair seam)

Compiles a plain-text repair prompt for a partial test, injecting the original test artifact,
the critic's non-grounded findings, and relevant source docs. Emits to stdout (mirrors
`compile-critic-prompt`). Called by the `spectra-generate` and `spectra-review-flagged` skills.

```bash
spectra ai compile-repair-prompt --suite reporting --test TC-113 \
  --from .spectra/verdicts/critic-verdict-TC-113.json
```

- Refuses (exit 4) if the verdict is `grounded` or if no non-grounded findings exist. Spec 090 (FR-A6): it accepts
  `partial` and `hallucinated` verdicts — the update flow repairs a hallucinated edit once instead of dropping a test
  that existed before (the generation flow still record-drops hallucinated tests before any repair).
- The agent patches in-session, then `ingest-update` + re-critic + `ingest-grounding` close the loop.
- Spec 097 (FR-010): a **stale** verdict (the 090/091 freshness check the ingest applies) is refused before any prompt
  is compiled: stderr `VERDICT_STALE: <the ingest's message>`, exit `4` — a stale verdict never spends the test's repair
  attempt. A verdict without a fingerprint record is accepted, as before. With a custom `--from`, only the fingerprint
  rule applies (the record describes the default verdict file).
- Exit code is `0` when emitted, `4` when refused (grounded / no findings / stale verdict), `5` when the verdict is empty, `6` on a parse failure, and `1` on error.

### `spectra ai review-flagged` (human review phase)

Lists tests flagged for review (still-partial-after-repair) and allows per-test disposition:
**accept** (clear `flagged_for_review`, keep partial block), **delete** (trail + clean delete).
Retry-repair requires the `spectra-review-flagged` skill (needs agent inference).

```bash
spectra ai review-flagged --suite reporting                     # interactive
spectra ai review-flagged --suite reporting --no-interaction --output-format json  # list only
```

- In interactive mode, the prompts are `[A]ccept`, `[D]elete`, `[S]kip`, `[Q]uit` per flagged test.
- In non-interactive mode (or with `--output-format json`), the command exits `2` if undisposed flagged tests remain.
- Exit code is `0` when all tests are dispositioned, `2` when undisposed flagged tests remain (non-interactive), and `1` on error.

### `spectra ai audit-grounding` (resume oracle + inspection)

Reports per-test grounding state for a suite. Serves as both the human inspection surface and the
**resume oracle** for `compile-repair-batch`: a test with `grounding_written: false` still needs repair.

```bash
spectra ai audit-grounding --suite smoke                        # human-readable table
spectra ai audit-grounding --suite smoke --output-format json  # machine-readable (resume oracle)
```

Output per test: `id`, `verdict`, `score`, `grounding_written` (bool), `flagged_for_review` (bool),
`action_needed` (`repair` / `review` / `none`). Plus a summary line: total / grounded / pending-repair / flagged.

- `grounding_written: true` means the durable grounding block is in the `.md` frontmatter.
- `action_needed: repair` means a partial verdict with no grounding yet, so feed it to `compile-repair-batch`.
- `action_needed: review` means partial with grounding written, but `flagged_for_review: true`; use `review-flagged`.
- `action_needed: none` means the test is grounded and clean.
- Exit code is `0` on success and `1` when the suite is not found.

Spec 090 (FR-A7): entries for tests that have a verdict file are unchanged. Every indexed test **without** a verdict file
is listed too, judged by its grounding block (verdict files are workspace-local, so a fresh clone can have blocks but no verdict
files):

| Test file | `verdict` | `grounding_written` | `action_needed` |
|---|---|---|---|
| critic grounding block | the block's verdict and score | `true` | `review` when flagged, else `none` |
| Manual block | `manual` | `true` | `none` |
| no block (e.g. just edited by `ingest-update`) | `none` | `false` | `critic` |

The summary gains `unverified` (tests with verdict `none`).

Spec 097 (FR-004–FR-009a):
- **One suite.** Only the suite's indexed tests are listed: verdict files of other suites' tests and stray verdicts (an
  id in no index) are ignored. (Before, every `critic-verdict-*.json` was listed, a stray one with `action_needed:
  repair`.)
- **Stale verdicts.** An entry whose verdict fails the freshness check the ingest applies gets `"stale": true` and
  `action_needed: critic` (shown as `critic (stale)`); `summary.stale` counts them. Fresh entries and entries without a
  record have no `stale` field.
- **Filters.** `--action critic|repair|review|none` (repeatable or comma-separated, case-insensitive; anything else →
  exit `1`, `--action must be one of critic, repair, review, none (got '<v>').`) and `--ids a,b` (the 091 parsing; empty
  = not given; ids not in the suite → `not_found` and `<id>: not found in suite '<s>'` — on stderr in JSON mode). They
  narrow `tests` only: the summary still covers the whole suite.
- The generate skill's resume path calls `audit-grounding --suite <s> --action repair`, so only the tests still to
  repair reach the session.

Spec 098 (F2) — **the action comes from the verdict.** A test with a fresh verdict and no grounding block needs:

| Verdict | `action_needed` | What to do |
|---|---|---|
| `grounded` | `ground` | Only the block is missing: `ingest-grounding --suite <s> --all --ids <ids>` (no repair). |
| `partial` | `repair` | One repair attempt (`spectra-repairer`), then the critic again. |
| `hallucinated` | `review` | Waiting for the user's decision; never repaired automatically. |
| anything else | `critic` | Run the critic again. |

Stale verdicts stay `critic`; tests with a block stay `review` (flagged) / `none`. `--action` accepts `ground`
(`--action must be one of critic, ground, repair, review, none`). The summary gains `pending_ground` and
`pending_review`; `partial_pending_repair` counts only `repair` entries. (Before 098, grounded-without-block and kept
hallucinated tests were both `repair`, so the resume path sent them to the repairer.) The generate skill's resume path
now calls `audit-grounding --suite <s> --action ground,repair,review` and routes each id by its action.

```bash
spectra ai audit-grounding --suite smoke --action ground,repair,review --output-format json
spectra ai audit-grounding --suite smoke --ids TC-101,TC-102
```

### `spectra ai compile-repair-batch` (deterministic batch repair manifest)

Reads all partial verdict JSONs for the suite, **filters to those without a grounding block** (the
resume checkpoint, so already-repaired tests are skipped automatically), compiles every repair prompt in
one pass via the same logic as `compile-repair-prompt`, and emits a JSON manifest to stdout.
Model-free and idempotent.

```bash
spectra ai compile-repair-batch --suite smoke                  # emits JSON manifest to stdout
```

Each manifest entry: `id`, `prompt` (full repair prompt text), `source_refs`, `file` (relative path to
the test `.md`). The agent processes the manifest: for each entry, patch the test, drive the
`spectra-critic` subagent, then `ingest-update --from repaired-{id}.json` + `ingest-grounding`.

Re-running `compile-repair-batch` after an interrupted session emits only the
remaining ungrounded tests. The grounding block in `.md` frontmatter is the done-marker, so a test is
skipped once its block is written. Never double-processes a completed test; never restarts from scratch.

- Spec 097 (FR-011): a partial test whose verdict is **stale** gets no prompt. Stdout stays the JSON array (without
  it); stderr gets one line `{"command":"compile-repair-batch","stale":[{"id":"TC-102","reason":"…"}]}` and
  `TC-102: stale verdict — run the critic again`, only when a test is stale.
- Exit code is `0` when the manifest is emitted (possibly empty) and `1` when the suite index is not found.

> There is no longer a `spectra ai generate` command; generation runs **in
> your interactive Claude Code session** via the `spectra-generate` skill, which drives the
> deterministic model-free seam below: the CLI compiles a grounded prompt, Claude generates
> in-session, and the CLI ingests/validates the result, with the `spectra-critic` subagent verifying
> every test. The retired critic keys (`ai.critic.provider`/`api_key_env`/`base_url`,
> `ai.fallback_strategy`) are ignored with a non-blocking `spectra validate` notice.
> Spec 097: `ai.critic.model` (default `sonnet`; an alias, a model id, or `inherit`) is the model the `spectra-critic`
> subagent runs on — `spectra init` / `spectra update-skills` write it into the installed agent's frontmatter.
> `ai.providers` and the GitHub Copilot SDK are gone entirely, and criteria extraction runs on this
> same seam too (`spectra docs changed` → `compile-extraction-prompt` → in-session turn →
> `ingest-criteria`), so there is no `spectra auth`/Copilot authentication step anymore.

### `spectra ai init-seam-progress`

Writes `.spectra/seam-progress.html`, a throwaway page that polls the seam's progress file so you can watch a long
skill-driven run (generation, criteria, update) in the browser. The skills call it; it is gitignored.

```bash
spectra ai init-seam-progress            # write the page and open it
spectra ai init-seam-progress --no-open  # CI / headless: write it only
```

### Generation (in-session via the `spectra-generate` skill)

Test generation is **skill-driven**, not a direct CLI command; ask Claude Code (e.g. *"generate test
cases for the checkout suite"*) and the `spectra-generate` skill orchestrates the seam. The underlying
model-free CLI commands (each deterministic, emitting a prompt to stdout or ingesting agent output, all
supporting `--output-format json`) are:

| Command | Role |
|---------|------|
| `spectra ai compile-analysis-prompt --suite <s> [--doc-suite <id>] [--focus <text>]` | Emit the behavior-analysis prompt (analyze-first step). |
| `spectra ai ingest-analysis --suite <s> [--from <file>] [--dry-run]` | Turn the agent's behavior JSON into a recommendation (already-covered = behaviours an existing test covers, recommended = the uncovered ones, category + ISTQB technique breakdowns) and save the suite's generation plan (Spec 094/095, below). |
| `spectra ai compile-prompt --suite <s> --batch <id> [--count <n>] [--focus <text>]` | Emit the prompt for one batch of the generation plan: its behaviours, doc sections and their criteria (Spec 094, above). |
| `spectra ai compile-prompt --suite <s> --count <n> [--focus <text>]` | Emit the bulk generation prompt for `<n>` tests (count mode: the model reads the docs). |
| `spectra ai compile-prompt --suite <s> --from-description "<text>" [--context <text>]` | Emit a single-test prompt from a plain-language description (count forced to 1; matching criteria injected). |
| `spectra ai ingest-tests <suite> [--from <file>]` | Validate and persist the agent-generated tests, failing loud with exit `5` for content-invalid and `6` for schema-invalid. |
| `spectra ai compile-critic-prompt` / `spectra ai ingest-verdict` | The mandatory `spectra-critic` verification step. |
| `spectra ai ingest-grounding --suite <s> --test <id> [--repaired] [--repair-attempts <n>]` | Write the durable condensed verdict block into the test `.md` frontmatter. |
| `spectra ai ingest-grounding --suite <s> --all [--ids a,b] [--repaired] [--repair-attempts <n>] [--dry-run]` | In batch form, writes grounding blocks for all eligible tests (or only `--ids`) in one pass; without `--repaired` only grounded tests are written, and with `--repaired --ids` the listed re-critiqued tests. Idempotent. `--dry-run` writes nothing (Spec 097). |
| `spectra ai ingest-verdict --suite <s> --all [--ids a,b] [--dry-run]` | In batch form, classifies all `.spectra/verdicts/critic-verdict-*.json` files for the suite in one call, emitting a `{grounded, partial, hallucinated, errors}` summary. Advisory-gate only, so no files are written; replaces per-test `--from` loops. |
| `spectra ai ingest-update <suite> --all` | In batch form, ingests all staged update files from `.spectra/updates/<suite>/updated-<id>.json` in one call, emitting `{written, unchanged, unchanged_pending, skipped_no_original, errors, results}`; consumed staging files are deleted, failed ones kept (Spec 088); exit 8 when a file failed, and `--all --keep-baseline` reads `.spectra/repairs/<suite>/repaired-<id>.json` instead (Spec 089). Per-entry `--test-id --from` mode unchanged. |
| `spectra ai compile-repair-prompt --suite <s> --test <id>` | Emit a repair prompt for a partial test (critic findings + source docs); plain text to stdout. |
| `spectra ai record-drop --suite <s> --test <id> [--reason user_decided]` | Append a drop-trail entry before deleting a hallucinated or user-decided-to-drop test. |
| `spectra ai review-flagged [--suite <s>]` | List and disposition flagged (still-partial) tests: accept, delete, or (via skill) retry repair. |
| `spectra ai compile-repair-batch --suite <s>` | Compile a batch repair manifest for all ungrounded partials (model-free, resume-aware). |
| `spectra ai audit-grounding --suite <s> [--action a,b] [--ids a,b] [--output-format json]` | Report per-test grounding state of the suite's indexed tests (id, verdict, grounding_written, flagged, action_needed, `stale`); serves as the resume oracle plus inspection view. |

**The generation plan (Spec 094).** On success `ingest-analysis` resolves each behaviour's `source` — the most specific
`doc#anchor` it comes from, as tests use in `source_refs`, or a bare document path for the whole document — with the
baseline's ref and section rules, and writes `.spectra/generation-plan/<suite>.json` (scratch, gitignored, replaced by
the next analysis of the suite). Behaviours get ids `B1…` in input order. Batches group resolved behaviours by section in
document order, at most `generation.batch_size` (default 8) per batch, never across documents; behaviours whose source
doesn't resolve go to one `unsourced` batch. Counts: every batch gets at least 1; when `recommended` ≥ the number of
batches the counts add up to `recommended` (1 each plus a share by behaviours — `count_rule: "proportional"`),
otherwise every batch gets 1 (`count_rule: "one_per_batch"`, "counts raised to one per batch"). The JSON adds `plan`,
`planned_total`, `count_rule`, `batches` (`id`, `count`, `source_refs`, `behaviors`), `unresolved` (`id`, `title`,
`source`, `reason`) and `notes`; `documents_analyzed` counts documents (sections of one document once). The human
summary — on stderr in JSON mode — adds one `batch <id> | count <k> | <refs> | <titles>` line per batch (a `|` in a
title shown as `/`) and one `unresolved: <id> <title> — <reason>` line each. A failed analysis (exit 5/6) writes no
plan; a plan that can't be written is exit 1 `PLAN_WRITE_FAILED`. The command loads `spectra.config.json` as a write
command reading `source` (missing → `CONFIG_MISSING`; `generation.batch_size` outside 1–50 → `CONFIG_INVALID`).
Spec 097: `--dry-run` reports the same JSON (plus `"dry_run": true`), summary and exit code — config refusals included —
and writes no plan file (a write failure can't be known without writing, so it isn't simulated).

**Covered behaviours (Spec 095).** One rule decides, per behaviour, whether the suite's existing tests already cover it:
an existing test whose title is at least `generation.coverage_threshold` (Spec 098; default 0.6) similar to the behaviour's title
(the duplicate detector's title similarity). The behaviour's `covered_by` is the most similar test (ties → the lowest id,
ordinal). `already_covered` = covered behaviours, `recommended` = uncovered behaviours — with or without acceptance
criteria (before 095, a suite with any test or criterion reported its test count as `already_covered`). The breakdowns
still count all behaviours. The plan keeps every behaviour with `covered_by` and records the threshold (`coverage_threshold`; a 095 plan's `duplicate_threshold` is read the same way), but
batches only the uncovered ones: each batch's count is one per uncovered behaviour, summing to `recommended`; all
covered → no batches. The JSON adds `covered` (`id`, `title`, `covered_by`); `unresolved` lists only uncovered
behaviours, so a covered one appears only under `covered`, whatever its source. The summary adds one
`covered: <title> (<covered_by>)` line each, between the batch lines and the `unresolved:` lines. A threshold outside
(0, 1] is used as given, with the stderr warning "generation.coverage_threshold = <v> is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered." (exit code unchanged).
**Spec 098 (F9):** coverage reads `generation.coverage_threshold` (0–1, `CONFIG_INVALID` outside); `duplicate_threshold`
is duplicate detection only. For one release, a config without `coverage_threshold` whose `duplicate_threshold` is set to
something other than 0.6 keeps using it for coverage, with the stderr notice "generation.coverage_threshold is not set;
using generation.duplicate_threshold (<v>) for coverage as before. Set generation.coverage_threshold — this fallback will
be removed in the next release." (the init template's 0.6 gives no notice).
`compile-prompt --batch` never names a covered behaviour; a plan written by 094 (no threshold) still
compiles, with the warning "the generation plan predates per-behaviour coverage; re-run the analysis for corrected
counts".

ISTQB techniques (EP, BVA, DT, ST, EG, UC) and coverage-aware analysis (existing
`_index.json` / criteria / doc sections inform the recommended gap count) are applied inside the
analysis prompt. `--include-archived` includes `skip_analysis` suites in the analyzer input.

Because `compile-analysis-prompt` inlines
documentation, pass `--doc-suite <id>` when the test-suite name doesn't match a doc-suite ID;
otherwise it loads the full corpus and may exit `4` (estimated prompt exceeds
`analysis.max_prompt_tokens`, default 96,000) with an actionable message naming candidate suites and
token costs. `spectra docs list-suites` shows available IDs and their token estimates. `compile-prompt`
(generation) is criteria-grounded and not subject to the inlined-doc budget.

Compile commands exit `0` when emitted and `4` when refused (missing input or budget exceeded), or `1`
on a config/IO error. Ingest commands exit `0` when persisted/classified, `5` for empty content, `6` for
schema/parse damage, or `1` on a config/IO error. The skill drives a bounded regenerate-and-retry on `5`/`6`.

Duplicate detection (>80% title similarity) continues to apply to the verification path. (The keys of the removed
in-process critic — `max_concurrent` and the others — are ignored; Spec 097.)

### `spectra ai update-triage` (Spec 084)

Reports, per test, what changed in the doc sections it references (`source_refs`) and the acceptance criteria it
links (`criteria`) since the test was written, edited or acknowledged. Deterministic and model-free. Git is only read
(Spec 086), and only when a referenced file is missing, to find where it was renamed (see [Git access](#git-access-spec-086)).
This is the replacement for `spectra ai update`'s word-overlap classification.

```bash
spectra ai update-triage --suite checkout --output-format json
spectra ai update-triage --all-suites --output-format json
spectra ai update-triage --suite checkout --no-auto-ack     # report COSMETIC changes instead of acknowledging them
spectra ai update-triage --suite checkout --dry-run         # write nothing at all
```

| Option | Description |
|--------|-------------|
| `--suite <s>` / `--all-suites` | The scope (exactly one is required) |
| `--no-auto-ack` | Report COSMETIC refs instead of refreshing their baseline |
| `--dry-run` | Write nothing: no auto-ack, no detail file, no lock file |

**The unit of change.** A ref `file#anchor` is a section: the first heading whose key matches the anchor's key,
through to the next heading of the same or a higher level (or the end of the file); the heading line is included. A
ref without an anchor (or `file#`) is the whole file. The file is resolved as for every other command (repo root,
then `source.local_dir`). The key is lowercase letters and digits only (percent-escapes decoded), so GitBook
(`#Create-customer-records`), GitHub (`#create-customer-records`) and generator-style anchors all match the heading
"Create customer records". Headings inside fenced code (```` ``` ```` / `~~~`) are ignored.

**Anchor matching (Spec 085).** An anchor resolves to a heading by the first rule that matches:

1. **Text key**: the key of the heading text with markup removed: HTML tags such as GitBook's
   `<a href="#x" id="x"></a>`, links (`[Payments](payments.md)` → "Payments") and a trailing attribute block
   (`{#custom-id}`, `{: #id}`, `{.cls}`). The first heading wins; when several share the key, `notes` says so.
2. **Explicit ID**: any `id="…"` / `id='…'` inside the heading, or the ID of a trailing `{#id}` block.
3. **GitHub duplicate suffix**: `#setup-1` is the second "Setup" heading, `#setup-2` the third, and so on (only
   when that many exist; a heading whose text really is "Step-2" is found by rule 1 first).

Extraction and the `no_tests` list use the same matcher, and `no_tests` shows the cleaned heading text. A heading's
attribute block is also left out of the comparison text, so adding an anchor tag, a link or a custom ID to an existing
heading is COSMETIC, not REMOVED. Refs to plain, emphasised and inline-code headings resolve exactly as in 084 (no
fingerprint changes). Residuals: an anchor written from a heading's raw HTML entity (`#qampa` for `Q&amp;A`) or from
HTML-like text inside inline code (`` `<br>` ``) no longer matches; no supported slug style produces either.

**States per ref**

| State | Rule |
|---|---|
| UNCHANGED | The section's fingerprint equals the baseline's |
| COSMETIC | It differs, but its normalised text equals the baseline snapshot: only markup, whitespace or case changed. Acknowledged automatically unless `--no-auto-ack` / `--dry-run` |
| LOAD | The normalised text changed and a number changed, a word that flips a rule's meaning changed (`must`, `should`, `may`, `required`, `no`, `not`, `never`, `none`, `nothing`, `cannot`, `can't`, `don't`, `doesn't`, `won't`, `isn't`, `aren't`, `shouldn't`, `mustn't`, `without`, `unless`, `except`, `only`, `always`, `all`, `any`, `every`; a curly apostrophe counts as a straight one), more than `update.light_threshold_words` words changed, or no diff could be computed (snapshot missing, or a side over 5,000 words) |
| LIGHT | Any other change of the normalised text |
| RELINK | Spec 086: the file is missing, and git history names the file it was renamed or moved to (`why`: `renamed to <path>`; the group's ref item carries `target`). Cleared by `relink-sources` |
| REMOVED | The file no longer resolves (and history names no rename), or the anchor matches no heading. A missing file's reason is `not found; <why>`: `not found; no git`, `not found; not in a git repository`, `not found; no rename found at 90% similarity` (`(shallow history)` appended for a shallow clone), `not found; ambiguous rename`, `not found; rename chain ends at a missing file`, `not found; renamed outside the allowed roots`, and (Spec 087) `not found; submodule not checked out: <submodule path> (run git submodule update --init)` — a setup problem, not a removal: the file is in a submodule this clone hasn't checked out, so no history is consulted and it is never RELINK |

Per linked criterion: CRITERIA_CHANGED (its normalised text changed) or CRITERIA_REMOVED (the ID is in no criteria
file). A test's **bucket** is its worst state: RELINK > REMOVED > CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED > LIGHT >
COSMETIC > UNCHANGED. A test with no baseline entry is **UNBASELINED** — reported apart, never as changed; one whose
entry lacks some of its current refs or criteria is **partial** (bucketed on the rest).

**RELINK without a baseline (Spec 086).** A ref broken before the first baseline is never baselined, so RELINK also
covers refs without one: an unbaselined ref of a baselined test is RELINK (not listed as unbaselined), and the refs of
a test with no entry are checked for RELINK only. Such a test is listed once, under RELINK, with `"unbaselined": true`
in the detail file (same record as any triaged test), and is left out of `unbaselined.full`, `counts.unbaselined` and
the human UNBASELINED line, so the counts add up. The RELINK bucket is shown in the human summary only when it is not
empty. Triage always uses a 90% rename similarity; `relink-sources --min-similarity` can go lower.

Fingerprints are `sha256:` over the section's raw text with line endings normalised to LF and a BOM removed (so
Windows and Linux checkouts agree); the comparison text is the `TextNormalizer` key. Documented residuals: a
case-only change (`Save` → `SAVE`) is COSMETIC; setext headings (`===` / `---` underlines) are not section
boundaries; anchors keep diacritics (`#über` matches "Über", `#uber` does not). A changed word is counted as the
larger of the words removed and the words added (a one-word replacement counts as 1).

**Groups.** Tests in the same suite and bucket with the same set of changed refs and criteria form a group, chunked at
`update.group_size` (default 10) in test-ID order and numbered `G1`, `G2`, … across the run (suite, then bucket worst
first). COSMETIC and UNCHANGED tests form no groups.

**Submodules that aren't checked out (Spec 087).** All of a test's refs into one such submodule fold into a single ref
item `{"ref": "<submodule path>", "state": "REMOVED", "submodule": true}`, so a suite's tests form **one** group per
submodule, never chunked by `update.group_size`; `removed` still lists every ref. `notes` adds one line per submodule,
counting every ref into it — baselined or not, including tests with no baseline entry: `N reference(s) from M test(s)
point into submodule <path>, which isn't checked out (run git submodule update --init)` (N counts (test, ref) pairs, M
distinct tests). The fix is `git submodule update --init` in the repository, then triage again; SPECTRA never runs it.

**Output.** stdout (JSON mode) is one compact line — `suites[].counts` (non-zero buckets only, plus `unbaselined` /
`partial`; a missing bucket is 0), `groups` (`id`, `suite`, `bucket`, `refs`, `criteria`, `tests`, `why` such as
`numbers: 10gb→50gb`, `modal: must→should`, `words: 15`, `no diff: snapshot missing`, `anchor not found`), `removed`,
`unbaselined` (counts per suite), `no_tests` (headings of a document with a changed section that no triaged test
references — generation hints; a whole-file ref covers its document, a section ref covers its sub-headings, and a
parent of a covered heading counts as covered), `notes`, `auto_acked` (or `would_auto_ack` on `--dry-run`),
`detail_file` and `warnings`. The full detail — every test's states, both fingerprints, the word diffs, the
unbaselined IDs and the group membership — goes to `.spectra/update-triage.json` (gitignored scratch, not written on
`--dry-run`). The human summary (counts, groups, hints) is printed in every mode: stderr in JSON mode, stdout
otherwise. Exit code `0` whatever triage finds.

### `spectra ai update-ack` (Spec 084)

"Reviewed, no edit needed": refreshes the named tests' baseline entries (refs and criteria) to current content. Never
writes a test file.

```bash
spectra ai update-ack --suite checkout --ids TC-104,TC-107
spectra ai update-ack --suite checkout --group G2          # a group from the last update-triage
```

- `--group` resolves through `.spectra/update-triage.json`, and is refused (`TRIAGE_STALE`) when any member's sections
  or criteria changed since that triage, so an ack never swallows a change triage didn't show.
- A test that still lists a REMOVED ref or criterion can't be acknowledged (`REF_REMOVED` / `CRITERION_REMOVED`):
  fix or drop the ref in its `source_refs` / `criteria` (then ack), or delete the test if the feature is gone.
- Spec 086: neither can a test with a RELINK ref (`REF_REMOVED`, the message names `relink-sources`) — with `--ids`
  (every missing file is checked, baselined or not, also for a test with no entry, which never gets an entry from an
  ack) and with `--group` (a `RELINK` group).
- An entry is recomputed in full, so refs and criteria the test no longer lists are dropped.
- A change that is never acknowledged (or edited) stays in the baseline as it was and is reported on every triage.

### `spectra ai source-baseline --init` (Spec 084)

Adopts current content as the baseline for tests that have none — the bootstrap for a workspace created before 084.

```bash
spectra ai source-baseline --init --all-suites
spectra ai source-baseline --init --suite checkout --force   # recompute every entry from current content
```

Without `--force` it creates missing entries and adds missing refs/criteria to partial entries, but never changes a
recorded fingerprint or drops a key. Refs that don't resolve and unknown criterion IDs are left out (they show as
unbaselined). Never writes a test file.

**History bootstrap (Spec 086).** Baselines can start in the past, so doc changes made since the tests were written
show up at the next triage:

```bash
spectra ai source-baseline --init --all-suites --from-date 2026-03-01           # each repo's last commit at or before the date
spectra ai source-baseline --init --all-suites --from-ref v2.4 --owner docs/requirements/gitbook-repo
spectra ai source-baseline --init --all-suites --from-date 2026-03-01 --min-similarity 60   # also follow heavier-edited renames back
```

- **The point.** `--from-date <ISO date>`: per owner repository, the last commit at or before the date (a date without
  a time means the end of that day, UTC). `--from-ref <rev> --owner <path>`: the repository containing `<path>` uses
  that commit; docs of other repositories use current content, with one warning naming them.
- **Per ref** without a baseline key: the file at the point, following the renames made since back to its old path
  (`--min-similarity <N>`, 50–100, default 90, the same option and validation as `relink-sources`; on Windows and macOS
  paths match ignoring case, Spec 087); the section by the 085 matcher; fingerprint and snapshot as usual. Criteria
  always use current content.
- **Fills missing keys only**: an existing fingerprint is never changed (a partial entry gets only its missing refs).
- **Left unbaselined, with a reason** (`suites[].unbaselined: [{test, ref, reason}]`): `file did not exist at <hash>`,
  `anchor did not exist at <hash>`, `no commit at or before <date>`, `git history unavailable: <git reason>` (the
  "renames since" read failed — never treated as "no renames", Spec 087), `submodule not checked out: <path> (run git
  submodule update --init)`, `ambiguous rename`, or the ref's current missing reason.
- **Submodules not checked out** (Spec 087): every mode of `--init` — plain, `--from-date`, `--from-ref` — adds one
  warning per such submodule (the same line as triage's note) and leaves those refs unbaselined.
- **No git / no repository**: current content, with a warning listing the docs. Never an error.
- **Refused** with `INVALID_ARGUMENTS`: `--force` with `--from-*`; `--from-ref` without `--owner`; `--owner` without
  `--from-ref`; `--from-date` or `--from-ref` without `--init`; both `--from-date` and `--from-ref`; an unparseable date;
  `--min-similarity` outside 50–100 (same message as `relink-sources`) or without `--from-*`.
  With `GIT_REF_UNAVAILABLE` (exit 4): an `--owner` that isn't a readable repository, or a `--from-ref` that doesn't
  name a commit in it. Nothing is written.
- The JSON gains `"from": {"date": "…", "min_similarity": N}` or `{"ref": "…", "owner": "…", "min_similarity": N}` (only
  with `--from-*`).

### `spectra ai relink-sources` (Spec 086)

Relinks refs whose doc file was renamed or moved, as found in git history. For every ref whose file is missing and has
a rename candidate — baselined or not:

1. rewrites **only the ref's path** in the test file (the `#anchor` is kept exactly; the value keeps its quoting
   style; a ref whose new value the test already lists is dropped instead of duplicated; the rest of the file is
   byte-identical);
2. updates the suite's `_index.json`;
3. moves the baseline key **with its old fingerprint** (never re-baselined, so content the rename commit also changed
   shows as LOAD/LIGHT at the next triage); an existing entry for the new key wins; a ref or test without a baseline
   gets no key — the new ref stays unbaselined until the next `--init`;
4. removes `status`, `orphaned_reason` and `orphaned_date` from an orphaned test when every ref then resolves (file
   and anchor).

```bash
spectra ai relink-sources --suite checkout --dry-run          # preview old → new; writes nothing, not even the lock
spectra ai relink-sources --all-suites
spectra ai relink-sources --suite checkout --min-similarity 60 --dry-run   # also heavier-edited renames
```

| Option | Description |
|--------|-------------|
| `--suite <s>` / `--all-suites` | The scope (exactly one is required) |
| `--min-similarity <N>` | Git's rename similarity threshold in percent, 50–100 (default 90) |
| `--dry-run` | Report the same plan, write nothing |

**Candidates.** One rename scan per owner repository (`git log -l0 -M<N>% --diff-filter=R`, so a large restructure
commit is still seen as renames). A path with two or more distinct rename targets anywhere in its history is
ambiguous and never a candidate; otherwise the chain (`a → b → c`) is followed to a file that exists now. The target
must be under the repo root or `source.local_dir`. A bare legacy ref (`checkout.md`) is checked at the same places the
resolver looks, in the same order. The new ref keeps the original's base when the target is under it
(source-dir-relative stays source-dir-relative), otherwise it is repo-root-relative. Heading renames inside a file
(`anchor not found`) are not relinked. Spec 087: a ref into a submodule that isn't checked out is never a candidate
(`submodule not checked out: …`); on Windows and macOS a path is matched against git's history ignoring case (one
match → git's casing in the new ref; two paths differing only in case → `ambiguous rename`).

**Output.** `{command, success, scope, dry_run, min_similarity, relinked: [{suite, test, refs: [{from, to}],
orphan_cleared}], not_relinked: [{suite, test, ref, reason}], counts: {tests_relinked, refs_relinked,
refs_not_relinked}, warnings}`; `reason` is the bare reason (`no git`, `no rename found at 90% similarity`,
`ambiguous rename`, …). The human list (old → new per test) is printed in every mode: stderr in JSON mode. Exit `0`
whatever it finds. If an in-place edit can't be verified by re-parsing, the canonical writer is used and a warning
names the file. Takes `.spectra/baseline.lock` (not on `--dry-run`).

**Storage.**

| Path | Committed | Content |
|---|---|---|
| `{tests_dir}/{suite}/_source-baseline.json` | yes | `{"version":1,"tests":{"<id>":{"refs":{"<ref as written>":"sha256:…"},"criteria":{"<id>":"sha256:…"}}}}` — keys sorted, LF, one test per block (a test's change is a local diff); moves with `suite rename`, goes with `suite delete`; never read as a test by any other command |
| `.spectra/source-snapshots/{hex}.txt` | yes | The normalised text of a section, named by its fingerprint; shared across suites; never pruned automatically (a missing one only means no diff) |
| `.spectra/update-triage.json` | no (gitignored) | The last triage's detail |
| `.spectra/baseline.lock` | no (gitignored) | The cross-process lock every baseline writer takes |

Writers: `update-ack`, `source-baseline --init`, triage's COSMETIC auto-ack, `ingest-tests` (new tests) and
`ingest-update` (the edited test). A write also drops entries for tests that no longer exist in the suite, **but only
when every test file in the suite parsed** (Spec 085): if any `*.md` test file fails to parse, nothing is pruned in that
suite and the command warns `<files> could not be parsed; no baseline entries were pruned in suite '<s>'.` (in the
JSON `warnings` and as a `Warning:` line on stderr). A full rebuild (`ingest-update`, `source-baseline --init --force`,
`update-ack`) keeps the previous fingerprint of a ref or criterion the test still lists but that is gone, so it keeps
reporting REMOVED / CRITERIA_REMOVED until the test drops or fixes it.

**Config** (optional, in the existing `update` section; missing or `null` → the defaults, silently; not in the init
template):

```json
"update": { "group_size": 10, "light_threshold_words": 12 }
```

`group_size` must be an integer ≥ 1 and `light_threshold_words` an integer ≥ 0; otherwise `CONFIG_INVALID` naming the
key.

**Errors** (all three commands are write commands under the config rule: no config → `CONFIG_MISSING`; they read
`source` and `tests`). Every failure is one JSON document on stderr — `{command, success, error_code, errors,
warnings}` — and human mode ends with `[CODE]`.

| Code | Exit | When |
|---|---|---|
| `CONFIG_MISSING` / `CONFIG_INVALID` | 1 | No config / a malformed or invalid one |
| `BASELINE_UNREADABLE` | 1 | A `_source-baseline.json` that is invalid JSON or the wrong shape (names the file; never treated as empty) |
| `LOCK_TIMEOUT` | 1 | `.spectra/baseline.lock` held for more than 10 s |
| `INVALID_ARGUMENTS` | 4 | Both or neither of `--suite`/`--all-suites` or `--ids`/`--group`; `source-baseline` without `--init`; `--min-similarity` outside 50–100; the `--from-*` combinations above |
| `GIT_REF_UNAVAILABLE` | 4 | `--owner` isn't a readable git repository, or `--from-ref` names no commit in it (Spec 086) |
| `SUITE_NOT_FOUND` | 4 | `--suite` names no suite folder |
| `TEST_NOT_FOUND` | 4 | An `--ids` entry is not a test in the suite |
| `TRIAGE_MISSING` / `GROUP_NOT_FOUND` / `TRIAGE_STALE` | 4 | `--group`: no triage for the suite / no such group / the group changed since |
| `REF_REMOVED` / `CRITERION_REMOVED` | 4 | A named test still lists a REMOVED (or RELINK) ref / a REMOVED criterion |

The four baseline commands (`update-triage`, `update-ack`, `source-baseline`, `relink-sources`) share these codes.

#### Git access (Spec 086)

`update-triage`, `update-ack`, `relink-sources`, `source-baseline --from-*` and `docs topology` read git; nothing else
does, and **no SPECTRA command ever writes to a git repository**.

- **Which git runs (Spec 087).** Git is started by the absolute path of the first `git.exe` (Windows) / `git` found in
  an **absolute** PATH entry — empty, `.`, relative and current-folder entries are skipped, and `.cmd`/`.bat` are never
  used — so a `git.exe` inside a reviewed repository never runs. Arguments are passed one by one, with no shell. The
  same lookup starts `git config user.name` for the execution identity. No git found → `no git`.
- **A clean environment (Spec 087).** Every git process gets the environment without `GIT_DIR`, `GIT_WORK_TREE`,
  `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`, `GIT_COMMON_DIR`, `GIT_NAMESPACE`,
  `GIT_CEILING_DIRECTORIES`, `GIT_DISCOVERY_ACROSS_FILESYSTEM`, `GIT_CONFIG`, `GIT_CONFIG_PARAMETERS`,
  `GIT_CONFIG_COUNT` and `GIT_CONFIG_KEY_*`/`GIT_CONFIG_VALUE_*`, so running SPECTRA from a git hook (or a shell with
  those set) still reads the repository each call names.
- **Read-only subcommands only**: `rev-parse`, `log`, `show`, `ls-files`, `cat-file`. Every call is
  `git -C <repo> -c core.quotepath=false …` with `GIT_OPTIONAL_LOCKS=0` (no `index.lock`, no contention with a
  concurrent `git`) and `GIT_TERMINAL_PROMPT=0`; output is NUL-separated where possible and read as UTF-8, so
  non-ASCII paths round-trip.
- **Owners.** A doc's repository is the nearest one containing it (from its folder, or the nearest existing parent
  when the folder is gone). Submodules and nested submodules are ordinary owners; docs may also live in
  `source.local_dir` outside the repo, or outside git entirely.
- **Never required.** When git is missing, a doc is in no repository, git refuses a repository ("dubious ownership"),
  or a call fails, git features are skipped for that path with a reason — `no git`, `not in a git repository`,
  `repository not readable (dubious ownership)`, `git failed: <message>` — and the command carries on. Without git
  every command behaves as under 085, except that a missing file's REMOVED reason says `not found; no git`.
- Triage runs git only when a referenced file is missing; each repository's history is scanned once per command, and
  its submodules (`ls-files -s -z`) read once.
- **Casing (Spec 087).** On Windows and macOS rename lookups ignore case, as the file system does; Linux matches
  exactly. Residual: a bootstrap ref whose casing differs from git's, for a file not renamed since the point, is still
  read under the ref's casing and reported as `file did not exist at <hash>`.

Option-parsing errors from the command-line framework itself (an unknown option, a missing value) keep the
framework's own output.

Redirected `spectra` output (stdout and stderr, each on its own) is UTF-8 without a BOM, also on Windows. An
interactive console is left as it is: the terminal's code page is never changed (Spec 085).

### `spectra ai update` (legacy)

> **Legacy (Spec 084).** `spectra ai update` and its word-overlap classifier are unchanged but no longer used by any
> skill; `spectra ai update-triage` replaces them. They will be removed in a later phase.

Update existing test cases when documentation changes. Classifies test cases as UP_TO_DATE, OUTDATED, ORPHANED, or REDUNDANT.

```bash
spectra ai update                                              # Interactive mode
spectra ai update checkout                                     # Direct mode: classify, review, apply
spectra ai update checkout --diff --dry-run                    # Classify and show proposed changes; write nothing
spectra ai update checkout --no-interaction                    # CI: classify and report only
spectra ai update checkout --no-interaction --no-review        # CI: classify and apply (mark orphans)
spectra ai update checkout --no-review --delete-orphaned       # Apply and delete orphaned test cases
```

| Option | Short | Description |
|--------|-------|-------------|
| `--diff` | `-d` | Show diff of proposed changes |
| `--delete-orphaned` | | Delete orphaned test cases when applying (instead of marking them `status: orphaned`) |
| `--no-interaction` | | Disable interactive prompts. On its own, classifies and reports only |
| `--no-review` | | Apply without interactive review |
| `--dry-run` | | Classify and report only; never writes, even with `--no-review` |

**What gets written**

| Flags | Behaviour | Writes test files / `_index.json` |
|---|---|---|
| `--dry-run` (with anything) | Classify, print tables, write result | No |
| `--no-interaction` without `--no-review` | Classify, print tables, write result | No |
| `--no-interaction --no-review` | Classify, then mark orphans (or delete with `--delete-orphaned`) and regenerate the index | Yes |
| `--no-review` | Same as above | Yes |
| neither | Interactive review, then apply what you approve | Yes, if approved |

Every run prints the classification tables and writes `.spectra-result.json`, including dry runs.
The result's `applied` field is `false` when nothing was allowed to change and `true` when the apply
step ran. The exit code is the same either way (0 on success). Marking a test orphaned changes only its
`status`, `orphaned_reason` and `orphaned_date`; everything else in the file is kept.

> **Changed in 2.5.0:** `--no-interaction` used to apply changes. CI that relied on that must add
> `--no-review`.

**How `source_refs` are resolved.** A ref such as `docs/checkout.md#Payment` is resolved by dropping the
`#fragment`, normalising `\` to `/`, and looking for the file relative to the repository root; bare
legacy refs such as `checkout.md` fall back to the configured `source.local_dir`. A ref resolves only to a
file under the repository or under `source.local_dir`, which may lie outside the repository (for example
`../product-docs/docs`); refs outside both never resolve. A test is classified ORPHANED for a missing source
only when none of its referenced files exist, and it is compared with the text of all of its referenced
documents. `compile-critic-prompt` and `compile-repair-prompt` resolve refs the same way and fail when one is
missing, saying why: `Source ref not found: …` or `Source ref is outside the repository and the configured
source directory: …`.

`spectra ai update` is the **selector**: it classifies tests but does not rewrite them. The doc-aware targeted edit of an OUTDATED test runs in the interactive session via the `spectra-update` skill, over the deterministic update seam below.

### `spectra ai compile-update-prompt`

Compile a deterministic, model-free **edit** prompt for one test (`--test-id`) or for every test of a triage group
(`--group`). Emits the prompt to stdout; writes nothing. Spec 088: the prompt carries the tests' doc sections and only
the criteria they link.

```bash
spectra ai compile-update-prompt --suite checkout --test-id TC-104
spectra ai compile-update-prompt --suite checkout --group G2
```

| Option | Short | Description |
|--------|-------|-------------|
| `--suite` | `-s` | Suite containing the tests (required) |
| `--test-id` | | One test to compile an edit prompt for |
| `--group` | | A group id from the last `update-triage` (`.spectra/update-triage.json`); any bucket |

Exactly one of `--test-id` and `--group` is required.

**Layout.** One prompt, in this order: the edit rules; `SOURCE SECTIONS` — each distinct section the tests reference,
once, labelled `S1`, `S2`, … in order of first appearance, naming the tests that reference it; `ACCEPTANCE CRITERIA`
— the union of the criteria the tests link, once, sorted by id; `TESTS TO REVIEW` — each test once, sorted by id,
under `### TEST <id>` with its section labels, its criteria and its current JSON; the output format (one JSON array,
one object per test). Identical inputs give byte-identical prompts.

A section is one block per distinct ref and baseline fingerprint (two tests on the same ref with different baselines
get two blocks). Its state:

| State | What the block shows |
|---|---|
| `changed (LOAD)` / `changed (LIGHT)` / `changed (COSMETIC)` | The bucket and triage's reasons; the current section (raw); the previous version (the baseline snapshot, normalised: lowercased, markup removed); `What changed:` the removed / added word runs. Without a snapshot: `Previous version: unavailable (no snapshot)`. |
| `unchanged` | The current section only. |
| `not baselined` | The current section; `previous version unavailable`. |
| `REMOVED` | Triage's reason (`anchor not found`, `not found; <git reason>`, `submodule not checked out: …`); no text. |
| `RELINK` | `renamed to <path>`; no text. |

The current section is the 085 heading matcher's section, or the whole file for a ref without an anchor. The bucket
uses `update.light_threshold_words`, as triage does. A criterion line is `- AC-1: text`, `- AC-1 (changed since
baseline): text`, `- AC-1 (not baselined — compare the test with the current text): text` (Spec 089: no baseline entry,
or the entry lacks the criterion) or `- AC-1 (removed)`; across a group the strongest label wins (removed, changed, not
baselined, unchanged). Only criteria the tests link appear — never a suite-wide or repo-wide set.

**Refusals (exit 4, one JSON document on stderr in JSON mode, the shared failure shape):**

| `error_code` | When |
|---|---|
| `INVALID_ARGUMENTS` | Neither or both of `--test-id` / `--group`, or no `--suite` |
| `SUITE_NOT_FOUND` / `TEST_NOT_FOUND` | Unknown suite or test |
| `TRIAGE_MISSING` / `GROUP_NOT_FOUND` / `TRIAGE_STALE` | As for `update-ack --group` |
| `NOTHING_TO_RECONCILE` | A test with no resolvable ref and no existing linked criterion (names the tests) |
| `PROMPT_TOO_LARGE` | The prompt's estimated tokens exceed `analysis.max_prompt_tokens` (default 96,000); names the size; compile the tests one by one with `--test-id`, or lower `update.group_size` and triage again |
| `TEMPLATE_OUTDATED` | `--group` (or more than one test) with a customised `.spectra/prompts/test-update.md` that lacks `{{tests}}` or `{{source_diff}}`; run `spectra update-skills` (refreshes a copy never edited) or `spectra prompts reset test-update` (replaces a customised one) |

With such an outdated custom template, `--test-id` still works: the template gets the test through `test_case` and the
sections through `current_source`, and a warning names `spectra prompts reset test-update`. `compile-update-prompt`
follows the 083 read rule: without `spectra.config.json` it runs on defaults. Exit `1` on a baseline or I/O error.

### `spectra ai ingest-update`

Validate the edited test and persist it through the single write+index path, without allocating a new id. It deterministically protects invariants: id from the original, pre-existing `Manual` verdict/notes re-asserted, and a drift guard that fails loud on out-of-scope field changes.

```bash
spectra ai ingest-update checkout --test-id TC-104 --from edited.json
spectra ai ingest-update checkout --all
```

| Option | Description |
|--------|-------------|
| `suite` (positional) | Suite the edited test belongs to (required) |
| `--test-id` | Id of the original test being edited (required without `--all`) |
| `--from` | File with the edited-test JSON (omit to read stdin) |
| `--all` | Ingest every staged `.spectra/updates/<suite>/updated-<id>.json`; with `--keep-baseline`, every `.spectra/repairs/<suite>/repaired-<id>.json` instead |
| `--keep-baseline` | Keep the source-change baseline (critic repairs; see below) |
| `--dry-run` | Spec 090: report the outcome each edit would get, with the same exit code; write nothing |

Exit codes:

| Exit | Form | Meaning |
|---|---|---|
| `0` | both | Done: written, unchanged or `unchanged_pending` (single form); no staged file failed (`--all`) |
| `5` | single | Content invalid, incl. `DRIFT_DETECTED` and `MULTIPLE_TESTS` (Spec 089); nothing persisted |
| `6` | single | `SCHEMA_INVALID`; nothing persisted |
| `8` | `--all` | Spec 089: the batch completed, but at least one staged file failed (even if every one did). Same JSON as exit 0; read `results` and retry the failed tests. Command-namespaced, like `suite delete`'s 8 |
| `1` | both | Config, lock or baseline error (`BASELINE_UNREADABLE`, `BASELINE_WRITE_FAILED`), or the original isn't found (single form) |

**One test per edit (Spec 089).** The content (a staged file, or `--from`) must hold exactly one test object. More than
one — counted before validation, so an extra invalid object counts — fails with `MULTIPLE_TESTS`, naming the count and
the ids (`(no id)` for one without an id); nothing is written. An empty array stays `NO_TESTS`.

**Unchanged edits (Spec 088).** An edit identical to the original in every editable field (title, preconditions,
steps, expected result, test data, scenario, `source_refs`, `criteria`) is not written. It counts as reviewed: the
test's baseline entry is rebuilt from its current content, as `update-ack` would (with `--keep-baseline`, nothing is
recorded). For the comparison only, CRLF / CR count as LF, trailing whitespace of each line and field is ignored and
null equals empty; whitespace inside a line counts. The drift guard runs first. Single form: exit 0,
`{"success":true,"outcome":"unchanged","persisted":0,"unchanged":true,"id":"TC-104",…}`, human `Test 'TC-104' needs no
change; its baseline was refreshed.` Every single-form success JSON carries `outcome` (`written`, `unchanged` or
`unchanged_pending`).

**Unchanged while a change is pending (Spec 089).** Without `--keep-baseline`, an unchanged edit of a test that has a
baseline entry is first classified exactly as `update-triage` would (same rules, `update.light_threshold_words`).
`UNCHANGED`, `COSMETIC` and `LIGHT` count as reviewed (above). `LOAD`, `CRITERIA_CHANGED`, `REMOVED`,
`CRITERIA_REMOVED` and `RELINK` need an edit, so the outcome is `unchanged_pending`: nothing is written or recorded, the
change stays in triage. Single form: exit 0, `{"success":true,"outcome":"unchanged_pending","persisted":0,
"unchanged":true,"id":"TC-104","bucket":"LOAD","why":["numbers: 30gb→45gb"],…}`, human `Test 'TC-104' returned
unchanged — LOAD change still pending (acknowledge with update-ack or edit again).` A test without a baseline entry is
recorded as before.

**`--all` (Spec 077, 088, 089).** The folder follows the mode: `--all` reads only `.spectra/updates/<suite>/` (update
edits, `spectra-update`); `--all --keep-baseline` reads only `.spectra/repairs/<suite>/repaired-<id>.json` (critic
repairs, `spectra-generate`). Neither ever reads the other's folder, so a kept file is never ingested in the wrong
baseline mode. Each staging file, in ordinal order, gets one outcome:

| Outcome | Written | Baseline | Staging file |
|---|---|---|---|
| `written` | the test + index | rebuilt (kept with `--keep-baseline`) | deleted |
| `unchanged` | nothing | rebuilt (none with `--keep-baseline`) | deleted |
| `unchanged_pending` | nothing | untouched | deleted |
| `failed` | nothing | — | kept |

`failed` codes: the ingest codes (`EMPTY_CONTENT`, `MALFORMED_JSON`, `TRUNCATED`, `NO_TESTS`, `SCHEMA_INVALID`,
`DRIFT_DETECTED`, `MULTIPLE_TESTS`), `NO_ORIGINAL` (no test with that id), `ID_MISMATCH` (the one staged test's `id`
differs from the file name), `UNREADABLE`, `PERSIST_FAILED`. Staging files are deleted only after the batch recorded its
baseline; on a baseline write failure none is deleted, so a re-run picks them up. The JSON keeps `written`,
`skipped_no_original` (`NO_ORIGINAL` outcomes) and `errors` (the other failures) and adds `unchanged`,
`unchanged_pending` and `results` (`[{test, file, outcome[, error_code, errors][, bucket, why]}]`). The per-test summary
is printed in every mode (stderr in JSON mode):

```
Batch update-ingest for suite 'storage': written=1 unchanged=1 unchanged_pending=1 skipped_no_original=0 errors=1
  TC-101: updated
  TC-102: unchanged (reviewed, no edit needed)
  TC-103: returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again)
  TC-104: failed [MULTIPLE_TESTS] The edit holds 3 test objects (…) (kept .spectra/updates/storage/updated-TC-104.json)
```

A completed batch exits 0 when no file failed and 8 when at least one did; an empty or missing folder exits 0.

**Spec 090.**
- **Verdicts (FR-A1).** A written edit (single or `--all`, with or without `--keep-baseline`) removes the test's critic
  grounding block in the same write and deletes `.spectra/verdicts/critic-verdict-<id>.json` and its fingerprint
  record, so `audit-grounding` shows the test unverified until the critic runs again. A Manual verdict is kept: the
  single-form JSON has `manual_verdict_kept: true`, the batch JSON lists the ids under `manual_verdict_kept`, and the
  human line reads `TC-101: updated (manual verdict kept)`. `unchanged` and `unchanged_pending` touch nothing.
- **Failed staging files (FR-B1).** A file that fails is renamed to `failed-<id>.json` in the same folder (replacing an
  older one), and `results[].file` gives that path. `--all` never reads `failed-*`, so a later batch doesn't fail on it
  again. A later `written`, `unchanged` or `unchanged_pending` outcome for the id deletes its `failed-<id>.json`. To
  retry, write a fresh `updated-<id>.json` / `repaired-<id>.json`. On a baseline write failure nothing is renamed.
- **`--dry-run` (FR-B2).** Single form and `--all`: the same outcomes (incl. `MULTIPLE_TESTS`, `ID_MISMATCH`,
  `DRIFT_DETECTED`, `unchanged_pending`) and the same exit code as the real run, with `dry_run: true` in the JSON and a
  `Dry run: nothing written.` line. No test, index, baseline, staging or verdict file changes, and no baseline lock is
  taken.

Spec 084: like `ingest-tests`, it locks and strictly loads the suite baseline before persisting and then replaces the
edited test's entry (the single form and `--all`, which takes one lock for the batch); both JSON outputs gain
`baseline` (with `mode`) and `warnings`.

**`--keep-baseline` (Spec 085)**, for both forms: the edited test's existing source-change fingerprints are not
changed; refs and criteria the edit added get entries. Spec 098 (F5): a test with **no** entry gets none — it stays
unbaselined (triage keeps listing it) and `baseline.left_unbaselined` counts it (only when > 0), so a repair never
baselines a test at the current docs (before 098 it created a full entry). Critic repairs
(`spectra-generate`'s repair batch, `spectra-review-flagged`) pass it, because a repair fixes grounding, not doc drift:
a doc change the test was never updated for stays in `update-triage`. `baseline.mode` is `"keep"` with the flag and
`"rebuild"` without it. Baseline failures use the shared failure shape; the content, drift and schema failures (exit 5/6) keep
their `{success, error_code, errors}` shape.

### `spectra ai analyze --coverage`

Run unified coverage analysis across three dimensions.

```bash
spectra ai analyze --coverage
spectra ai analyze --coverage --format json --output coverage.json
spectra ai analyze --coverage --format markdown --output coverage.md
spectra ai analyze --coverage --auto-link
spectra ai analyze --coverage --verbosity detailed
```

| Option | Description |
|--------|-------------|
| `--format` | Output format: `text` (default), `json`, `markdown` |
| `--output` | Write report to file |
| `--auto-link` | Scan automation code and update `automated_by` fields in test case files |

See [Coverage](coverage.md) for details on the three coverage types.

---

## Dashboard Commands

### `spectra dashboard`

Generate an interactive HTML dashboard.

```bash
spectra dashboard --output ./site
spectra dashboard --output ./site --title "My Project QA Dashboard"
spectra dashboard --output ./site --dry-run
spectra dashboard --output ./site --template ./my-template
```

The dashboard reads from `test-cases/*/_index.json`, `.execution/spectra.db`, and `reports/*.json`.

Features include a suite overview, execution history, three-section coverage analysis, a coverage map (D3.js), a test browser, and trend analysis.

See [Getting Started](getting-started.md) for serving locally.

---

## Profile Commands

### `spectra init-profile`

Create or edit a test generation profile.

```bash
spectra init-profile                                    # Interactive wizard
spectra init-profile --non-interactive --detail-level detailed --min-negative 3
spectra init-profile --suite test-cases/checkout             # Suite-level override
spectra init-profile --edit --min-negative 5            # Update existing
spectra init-profile --force                            # Re-run full wizard
```

### `spectra profile show`

View the effective profile.

```bash
spectra profile show
spectra profile show --json
spectra profile show --context
spectra profile show --suite test-cases/checkout
```

See [Generation Profiles](generation-profiles.md) for details.

---

## Testimize Commands (Optional)

### `spectra testimize check`

Reports the status of the optional Testimize integration: whether
it is enabled in config, whether the `Testimize.MCP.Server` global tool is
installed, whether the server passes a health probe, the configured mode and
strategy, and whether the optional `testimizeSettings.json` is present.

```bash
spectra testimize check
spectra testimize check --output-format json
```

When Testimize is disabled (the default), the command does NOT start the MCP
process; it just reports `enabled: false`. When enabled but not installed,
the output includes a one-line `dotnet tool install --global Testimize.MCP.Server`
instruction. JSON output always contains the fields `enabled`, `installed`,
`healthy`, plus `mode`, `strategy`, and (when not installed) `install_command`.

See [Testimize Integration](testimize-integration.md) for the full guide.

---

## Execution Commands

Execution runs entirely through the **`spectra run` CLI** over the deterministic engine and
`.execution/spectra.db`, with **no MCP server** (SPECTRA's MCP execution adapter was removed).
A short-lived `spectra run` process reconstructs run state losslessly from the database, so no live
session is required.

### `spectra run …`

The single `dotnet tool install -g Spectra` provides execution as well as generation.
Drive a full manual loop from the shell:

```bash
spectra run list-suites                         # what's runnable
spectra run list-active                         # runs in progress
spectra run start <suite> [--priorities high] [--tags …] [--components …]
spectra run start --test-ids TC-1 TC-2 --output-format json
spectra run start --selection smoke
spectra run status [<run-id>]                   # counts + next actionable test (active run if omitted)
spectra run show [<run-id>] [--test-id ID | --handle H]   # full steps/expected for the current test
spectra run advance [<handle>] --status pass|fail|blocked|skip [--notes "…"]
spectra run skip [<handle>] --reason "…" [--blocked]
spectra run note [<handle>] --note "…"
spectra run bulk-record [<run-id>] --status pass --remaining   # or --test-ids TC-1 TC-2
spectra run retest [<run-id>] --test-id TC-201
spectra run screenshot [<handle>] --file ./bug.png
spectra run screenshot-clipboard [<handle>]     # local CLI is the host
spectra run pause|resume|cancel [<run-id>] | spectra run cancel-all
spectra run finalize [<run-id>] [--force]       # generates JSON/MD/HTML reports under .execution/reports/
spectra run history [--suite X] | spectra run summary [<run-id>] | spectra run selections
spectra run console [--port N]                  # serve a local web console for the active run
spectra run console --stop                       # stop the running console
```

#### `spectra run console` (local web console)

A local, human-driven **execution console**: a small detached HTTP server on `127.0.0.1` serving an
ephemeral, gitignored page (in Spectra's report styling) where a QA engineer drives a run from the
browser, seeing the current test, clicking **PASS / FAIL / BLOCKED**, adding a comment, and dropping or
pasting a screenshot. It is a *second transport over the same engine* (sibling of `spectra run …`):
**SQLite is the single source of truth; the browser is a view + write-back caller, never a
store**, so a refresh or reopen loses nothing. Verdict guardrails are enforced server-side (a comment is
required for FAIL/BLOCKED/SKIP; a verdict is never inferred), identical to `spectra run advance`.

The server starts **detached**, so it survives the launching terminal/agent session ending, and stops only
on `spectra run console --stop` (a `.execution/console.json` marker records its pid/port/url). `--port`
is optional (a free port is auto-selected). This is a different deployment model from the dashboard; see
`deployment.md`.

Every subcommand honors the global `--output-format json|human` and `--verbosity`. Exit code is `0` on
success and non-zero on error; a reconstruction failure surfaces as a distinct `RECONSTRUCTION_FAILED`
outcome (never conflated with a benign "run not found"). The `spectra-execute` SKILL and the execution
agent **orchestrate** this run (select tests, `spectra run start`, launch `spectra run console`,
hand over the local URL, then stay on-call) rather than driving a per-test loop in chat. The tester records
each verdict in the browser console, whose write-back endpoint enforces the human-in-the-loop guardrails
(explicit verdict; comment required for fail/blocked/skip; no auto-advance; no inferred verdict). The
agent reads current state on-call from `spectra run status` (SQLite), never from the console page.

When `<handle>`/`<run-id>` is omitted, the active run for the current user and its
in-progress (or next-pending) test are auto-resolved, so the agent rarely needs to pass them explicitly.

### MCP execution server (removed)

SPECTRA no longer ships an MCP execution server (`spectra-mcp`). Execution is CLI-only via `spectra run`
above; `spectra init` no longer writes any `.vscode/mcp.json` or `mcp__spectra__*` allowlist. The former
MCP tools each map one-to-one to a `spectra run` subcommand, e.g. `start_execution_run` → `spectra run
start`, `advance_test_case` → `spectra run advance`, `bulk_record_results` → `spectra run bulk-record`,
`get_run_history` → `spectra run history`. The data tools' operations remain available as their own CLI
commands (`spectra validate`, `spectra index` / `docs index`, `spectra ai analyze --coverage`). The
SEPARATE BELLATRIX/Nova MCP that drives the system-under-test is unrelated and unaffected.

---

## Test lifecycle commands (v1.52.0)

### `spectra delete <test-id>...`

Delete one or more test cases atomically with automation and dependency safety checks.

```bash
spectra delete TC-142                                # single
spectra delete TC-142 TC-150 TC-151                  # bulk
spectra delete --suite legacy                        # alias for `spectra suite delete legacy`
spectra delete TC-142 --dry-run                      # preview, no filesystem changes
spectra delete TC-142 --force                        # skip confirmation + automation guard
spectra delete TC-142 --no-automation-check          # override automation guard but still confirm
spectra delete TC-142 --no-interaction --output-format json
```

| Flag | Default | Description |
|---|---|---|
| `--suite <name>` | — | Alias for `spectra suite delete <name>` |
| `--dry-run` | off | Preview only |
| `--force` | off | Skip confirmation; override automation guard |
| `--no-automation-check` | off | Override automation guard without forcing past confirmation |

Exit code is 0 on success, 1 for a generic error, 3 for missing arguments, 4 for `TEST_NOT_FOUND`, 5 for `AUTOMATION_LINKED`, and 130 for SIGINT.

Spec 090 (FR-C1–FR-C3):
- Ids are read from each file's parsed frontmatter, never from a pattern over the file.
- A dependent with a scalar `depends_on: <deleted id>` has the key dropped through the lossless writer: nothing else in
  its file changes, except a comment line directly above `depends_on:`, which goes with the key. Body lines and
  `criteria:` entries are never touched.
- A dependent whose `depends_on` is a block or flow list (such tests don't parse today) is left unchanged, with a
  warning: `TC-203: depends_on is a list (not supported) and names TC-101; not changed (…)`.
- The deleted tests' baseline entries are removed (under the baseline lock), with their `updated-` / `repaired-` /
  `failed-` staging files and their verdict file and fingerprint record. Snapshots are kept.
- The JSON adds `baseline_entries_removed` (`suite/id`), `staging_files_removed`, `verdict_files_removed` and
  `warnings`; `--dry-run` fills them with what would happen.
- Legacy `spectra ai update … --delete-orphaned` deletes through the same service.

### `spectra suite list|rename|delete`

Suite management.

```bash
spectra suite list                                          # list all with test/automated counts
spectra suite rename <old> <new>                            # atomic rename + reference cleanup
spectra suite rename <old> <new> --dry-run                  # preview
spectra suite rename <old> <new> --force                    # skip confirmation
spectra suite delete <name>                                 # recursive delete + cascade
spectra suite delete <name> --dry-run                       # preview
spectra suite delete <name> --force                         # skip automation/external-deps guards
```

For `rename`/`delete`, exit code is 0 on success, 4 for `SUITE_NOT_FOUND`, 5 for `AUTOMATION_LINKED`, 6 for `SUITE_ALREADY_EXISTS`, 7 for `INVALID_SUITE_NAME` (must match `^[a-z0-9][a-z0-9_-]*$`), 8 for `EXTERNAL_DEPENDENCIES`, and 130 for SIGINT.

`suite delete` (Spec 096) cleans dependencies the way `spectra delete` does:
- a single-value `depends_on` in another suite naming one of the suite's tests is cleared through the lossless writer
  (no other byte of that file changes);
- a list or unparseable `depends_on` is left alone and reported in `warnings` (lists aren't supported);
- any such reference refuses the delete without `--force` (`EXTERNAL_DEPENDENCIES`).

It also removes the suite's leftovers: `.spectra/updates/<suite>/`, `.spectra/repairs/<suite>/` (incl. `failed-*`),
`.spectra/generation-plan/<suite>.json`, and the verdict and fingerprint-record files of its tests. The suite folder
(with `_index.json` and `_source-baseline.json`) goes as before; no other baseline is touched and no baseline lock is
taken. `--dry-run` lists all of it and writes nothing. JSON adds `dependents_cleaned` (`id`, `suite`, `file`,
`removed_dep`), `warnings`, `leftovers_removed` (`leftovers` in a dry run: staging folders and the generation plan) and
(Spec 098) `verdict_files_removed` — each verdict and record file is listed there once, never under the leftovers; the
human summary goes to stdout, or to stderr in JSON mode.

### `spectra cancel [--force]`

Cancel an in-progress SPECTRA operation. Cooperative shutdown via `.spectra/.cancel` sentinel; force-kills `Process.Kill(entireProcessTree: true)` after a 5 s grace.

```bash
spectra cancel                                              # cooperative + escalate after 5s
spectra cancel --force                                      # immediate kill
spectra cancel --no-interaction --output-format json
```

Result: `target_pid`, `target_command`, `shutdown_path` (`cooperative` / `forced` / `none`), `elapsed_seconds`. Exit `0` whether the run was cancelled or there was nothing running (`no_active_run` is not an error).

### `spectra doctor ids [--fix]`

Audit and repair test ID uniqueness across suites.

```bash
spectra doctor ids                                          # read-only audit
spectra doctor ids --fix                                    # renumber later occurrences
spectra doctor ids --no-interaction --output-format json
```

Reports: `total_tests`, `unique_ids`, `duplicates[]` (with file paths and mtimes), `index_mismatches[]`, `high_water_mark`, `next_id`. Under `--fix`: also `renumbered[]` and `unfixable_references[]` (in-source `[TestCase("TC-NNN")]` literals that need manual review).

Exit code is 0 on success, 9 for `DUPLICATES_FOUND` (only when `--no-interaction` and duplicates are reported without `--fix`, a CI-friendly regression gate), and 130 for SIGINT.

### Cancellation behavior on long-running commands

`ai generate`, `ai update`, `ai analyze --coverage`, `dashboard`, and `docs index` register with the process-level `CancellationManager` at startup. A peer `spectra cancel` (or Ctrl+C) triggers cooperative shutdown at the next batch boundary; partial results already on disk are preserved. The result artifact reports `status: cancelled` and the progress page transitions to a terminal "Cancelled" phase. Exit code is **130** (standard SIGINT convention).
