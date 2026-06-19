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

---

## Core Commands

### `spectra init`

Initialize a repository for SPECTRA.

```bash
spectra init
```

Creates `spectra.config.json`, `docs/`, `test-cases/`, `docs/criteria/_criteria_index.yaml`, `profiles/_default.yaml`, `.spectra/prompts/` (5 templates), `.github/skills/` (12 bundled SKILLs), `.github/agents/` (2 agent prompts), `CUSTOMIZATION.md`, and `USAGE.md`.

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

### `spectra index`

Rebuild `_index.json` files for all suites. `--rebuild` reconstructs each suite's index from the `.md` files of record (parsing every `*.md` under `test-cases/{suite}/` and regenerating the index from the parsed set). Use it to recover from any state where the index has drifted from the on-disk files — for example, recovering tests created by `--from-description` runs from versions before Spec 049 (v1.52.3) wired that path into the index automatically.

```bash
spectra index
spectra index --rebuild
```

### `spectra auth`

Check authentication status for all configured providers.

```bash
spectra auth
spectra auth -p github-models
spectra auth -p openai
```

### `spectra update-skills`

Update bundled SKILL and agent files to the latest version.

```bash
spectra update-skills
```

Compares installed files against bundled versions using SHA-256 hashes:
- **Unmodified files** → updated to latest version
- **User-modified files** → skipped with warning (preserves customizations)
- **Missing files** → recreated

Reports a summary of updated, unchanged, and skipped files.

---

## Documentation Commands

### `spectra docs index`

Build or incrementally update the v2 documentation index (manifest + per-suite files + checksum store under `docs/_index/`), then automatically extract acceptance criteria.

```bash
spectra docs index                # Incremental update + auto-extract acceptance criteria
spectra docs index --force        # Full rebuild + auto-extract acceptance criteria
spectra docs index --skip-criteria  # Index only, skip criteria extraction
spectra docs index --no-interaction --output-format json  # SKILL/CI mode
spectra docs index --suites checkout,payments  # Re-index only the named suites
spectra docs index --no-migrate  # Refuse to migrate a legacy _index.md (errors if found)
spectra docs index --include-archived  # Include skip_analysis suites in criteria extraction
```

The index now lives at `docs/_index/`:

- **`_manifest.yaml`** — small (~2-5K tokens), always loaded into AI prompts.
- **`groups/{suite}.index.md`** — per-suite index files, lazy-loaded only for the suite the user is working with.
- **`_checksums.json`** — hash table for incremental detection. NEVER sent to the AI.

On first run after upgrading from a release that used the single-file `docs/_index.md`, the indexer automatically migrates: parses the legacy file, splits entries by suite, writes the new layout, and renames the legacy file to `docs/_index.md.bak` for safekeeping. No flag required.

After indexing, acceptance criteria are automatically extracted from the analyzable documents (skip-analysis suites are excluded by default; pass `--include-archived` to override). Merged into `_criteria_index.yaml`. Use `--skip-criteria` to skip extraction entirely.

**Per-document timeouts (Spec 047)**: extraction iterates documents one at a time with a 2-minute per-document deadline. A single slow document no longer aborts the whole corpus. On a per-document timeout the command emits a scoped warning naming the document and suggesting `spectra ai analyze --extract-criteria` as a retry path for that document specifically. Previously, a 60-second corpus-wide deadline silently truncated extraction on large projects.

**Zero-criteria warning (Spec 048)**: when the run indexes at least one document but extracts zero acceptance criteria across the whole corpus, the command emits a prominent non-blocking warning naming the recovery command and writes a matching `criteria_warning` field into the JSON result. The exit code remains success. Suppressed when `--skip-criteria` is passed.

In SKILL/CI mode, the command writes `.spectra-result.json` (structured result with per-suite breakdown and migration metadata) and `.spectra-progress.html` (live progress page).

See [Document Index](document-index.md) and [Migration Spec 040](migration-040.md) for full details.

---

### `spectra docs list-suites`

List every suite in the manifest with document count, token estimate, and analysis status.

```bash
spectra docs list-suites
spectra docs list-suites --output-format json
```

Useful when the pre-flight token-budget check on `spectra ai compile-analysis-prompt` fails — the error message tells you to narrow with `--doc-suite <id>`, and `list-suites` shows the available IDs and their token costs.

---

### `spectra docs show-suite <suite-id>`

Print one suite's index file content to stdout.

```bash
spectra docs show-suite checkout
spectra docs show-suite SM_GSG_Topics
```

Errors with exit code 1 + the available-suites list if `<suite-id>` is unknown.

---

## AI Generation Commands

### `spectra ai compile-prompt` (Spec 053 — inverted handoff)

Deterministic, **model-free** prompt compiler. Assembles the grounded generation
prompt (doc criteria + profile schema) for a suite and writes it to **stdout** —
it calls no model and spends no tokens.

```bash
spectra ai compile-prompt reporting --count 5 --focus "export to PDF"
spectra ai compile-prompt reporting --output-format json   # machine-readable refusal
```

- **Deterministic**: identical inputs produce byte-identical output.
- **Refuse-to-emit**: when a required input (criteria context, positive count,
  user prompt) is missing it writes nothing to stdout and reports the missing
  input to stderr (and a `missing_input` JSON field), exiting **4**. It never
  emits a degraded prompt with a missing section.
- Writes nothing to disk.

Intended to be driven by a generation skill: the skill runs this to get the
prompt, the interactive agent generates the JSON, then the skill hands the
result to `ingest-tests`.

### `spectra ai ingest-tests` (Spec 053 — fail-loud boundary)

Validates agent-generated test content and persists it **only when the whole
batch is valid**. Content is read from `--from <file>` or stdin.

```bash
spectra ai ingest-tests reporting --from ./generated.json
cat ./generated.json | spectra ai ingest-tests reporting
```

- **Fail-loud, zero persistence on failure**: malformed JSON, a truncated array
  (no salvage), empty content, zero valid tests, or any schema violation persists
  **nothing** and returns a machine-readable `error_code`
  (`EMPTY_CONTENT` / `MALFORMED_JSON` / `TRUNCATED` / `NO_TESTS` / `SCHEMA_INVALID`).
- **Batch-atomic**: one invalid test fails the whole batch.
- Valid content persists via the unchanged `TestPersistenceService` (writes
  `.md` files + regenerates `_index.json`).
- Exit codes: `0` success, `5` content-class failure, `6` schema-invalid.

### `spectra ai compile-extraction-prompt` (Spec 054 — criteria re-homing)

Deterministic, **model-free** compiler for the **acceptance-criteria extraction**
prompt of a single document — the criteria-extraction analogue of `compile-prompt`.
Writes the prompt to **stdout**; calls no model and spends no tokens.

```bash
spectra ai compile-extraction-prompt --doc docs/payment.md --component payment
```

- **Deterministic**: identical inputs produce byte-identical output.
- **Refuse-to-emit**: a missing `--doc` (or unreadable content) writes nothing to
  stdout and reports the missing input (`missing_input` JSON field), exiting **4**.
- **Empty-source short-circuit**: an empty/whitespace document is a genuine
  "nothing to extract" (`Extracted, []`) — it emits **no** prompt, performs no
  model turn, and exits **0**.
- Writes nothing to disk.

### `spectra ai ingest-criteria` (Spec 054 — fail-loud boundary)

Classifies agent-extracted criteria content and persists it **only** when the
outcome is a genuine extraction. Content is read from `--from <file>` or stdin.
The criteria-extraction analogue of `ingest-tests`.

```bash
spectra ai ingest-criteria --doc docs/payment.md --from ./criteria.json
cat ./criteria.json | spectra ai ingest-criteria --doc docs/payment.md
```

- **Fail-loud, zero persistence on failure**: an empty response or unparseable
  content persists **nothing** and reports the typed `outcome`
  (`EmptyResponse` / `ParseFailure`). No cache poisoning — the criteria index is
  left untouched so a later run re-attempts.
- A genuine `Extracted` result persists via the unchanged `.criteria.yaml` writer
  + criteria-index upsert (`outcome: extracted`).
- `--dry-run` classifies and reports without writing.
- Exit codes: `0` extracted/persisted, `5` `EmptyResponse`, `6` `ParseFailure`.

> **Note (Spec 054):** `spectra docs index` and `spectra ai analyze
> --extract-criteria` continue to run their extraction in-process today; this
> spec adds the model-free compile/ingest surface alongside them (matching how
> Spec 053 shipped the generation surface) and unifies the legacy extractor's
> failure semantics so a single empty/slow/malformed document is now reported as
> an inconclusive document rather than throwing and aborting the corpus.

### `spectra ai compile-critic-prompt` (Spec 055 — critic subagent)

Deterministic, model-free compilation of the **critic verification prompt** for one generated
test — the verification analogue of `compile-prompt`. Writes nothing; calls no model. The
`spectra-critic` `context: fork` subagent runs the emitted prompt in a fresh, isolated context
(test artifact + selected source docs only) and hands its verdict to `ingest-verdict`.

```bash
spectra ai compile-critic-prompt --test ./tc-900.json --docs ./docs/checkout
```

- **Deterministic**: identical `(test, docs)` → byte-identical prompt (no timestamps/GUIDs).
- **Refuse-to-emit**: a missing test artifact (no id/title) exits `4`; an empty `--docs` set is
  allowed (the prompt notes "no documentation provided"), not a refusal.
- Exit codes: `0` compiled/emitted, `4` refused, `1` environment error.

### `spectra ai ingest-verdict` (Spec 055 — fail-loud boundary)

Classifies an agent-produced critic JSON into a typed outcome and reports the verdict + gate
decision. Content is read from `--from <file>` or stdin.

```bash
echo '{"verdict":"hallucinated","score":0.1,"findings":[]}' | spectra ai ingest-verdict
spectra ai ingest-verdict --from ./verdict.json --output-format json
```

- **Advisory gate**: only `hallucinated` reports `drop`; `grounded`/`partial` report `pass`.
- **Fail loud on damage**: a missing/unparseable `verdict` or `score` is a `ParseFailure` with a
  specific error — **never** a silent `partial`/`0.5` soft pass. Empty input is `EmptyResponse`.
- Exit codes: `0` verdict classified, `5` `EmptyResponse`, `6` `ParseFailure`, `1` env error.

### `spectra ai ingest-grounding` (Spec 071 — durable verdict write-back)

Writes a condensed grounding block into the test's `.md` frontmatter from a critic verdict JSON.
Called by the `spectra-generate` skill after `ingest-verdict`, once per test.

```bash
spectra ai ingest-grounding --suite reporting --test TC-113 \
  --from .spectra/verdicts/critic-verdict-TC-113.json
# After repair (upgraded to grounded):
spectra ai ingest-grounding --suite reporting --test TC-113 \
  --from .spectra/verdicts/critic-verdict-TC-113.json --repaired --repair-attempts 1
```

- **grounded** → condensed "verified clean" block (`verdict: grounded`, `score`, `verified_at`).
- **partial** → block with condensed findings + `flagged_for_review: true` (the `spectra-review-flagged` skill then dispositions it).
- **hallucinated** → refuses (exit 4); use `record-drop` + `spectra delete` instead.
- Exit codes: `0` written, `4` hallucinated refused, `5` empty verdict, `6` parse failure, `1` env error.

### `spectra ai record-drop` (Spec 071 — drop audit trail)

Appends a hallucinated-test entry to `.spectra/dropped-tests.json` before the three-phase clean
delete (`spectra delete`). The trail is append-only NDJSON, gitignored scratch.

```bash
spectra ai record-drop --suite reporting --test TC-138 \
  --from .spectra/verdicts/critic-verdict-TC-138.json
# Manual user decision during review:
spectra ai record-drop --suite reporting --test TC-138 --reason user_decided
```

- Fields written: `id`, `suite`, `title`, `drop_reason` (hallucinated|user_decided), `contradicting_claim` (from verdict), `doc_ref`, `critic_model`, `timestamp`, `source` (critic|review).
- Exit codes: `0` appended, `1` error.

### `spectra ai compile-repair-prompt` (Spec 071 — repair seam)

Compiles a plain-text repair prompt for a partial test, injecting the original test artifact,
the critic's non-grounded findings, and relevant source docs. Emits to stdout (mirrors
`compile-critic-prompt`). Called by the `spectra-generate` and `spectra-review-flagged` skills.

```bash
spectra ai compile-repair-prompt --suite reporting --test TC-113 \
  --from .spectra/verdicts/critic-verdict-TC-113.json
```

- Refuses (exit 4) if verdict is not `partial` or if no non-grounded findings exist.
- The agent patches in-session, then `ingest-update` + re-critic + `ingest-grounding` close the loop.
- Exit codes: `0` emitted, `4` refused (not partial / no findings), `5` verdict empty, `6` parse failure, `1` error.

### `spectra ai review-flagged` (Spec 071 — human review phase)

Lists tests flagged for review (still-partial-after-repair) and allows per-test disposition:
**accept** (clear `flagged_for_review`, keep partial block), **delete** (trail + clean delete).
Retry-repair requires the `spectra-review-flagged` skill (needs agent inference).

```bash
spectra ai review-flagged --suite reporting                     # interactive
spectra ai review-flagged --suite reporting --no-interaction --output-format json  # list only
```

- Interactive mode: `[A]ccept`, `[D]elete`, `[S]kip`, `[Q]uit` per flagged test.
- Non-interactive / `--output-format json`: exits `2` if undisposed flagged tests remain.
- Exit codes: `0` all dispositioned, `2` undisposed remain (non-interactive), `1` error.

> **Note (Spec 059):** there is no longer a `spectra ai generate` command. Generation runs **in your
> interactive Claude Code session** via the `spectra-generate` skill, which drives the deterministic
> model-free seam below: the CLI compiles a grounded prompt, Claude generates in-session, and the CLI
> ingests/validates the result, with the `spectra-critic` subagent verifying every test. The retired
> critic keys (`ai.critic.provider`/`api_key_env`/`base_url`, `ai.fallback_strategy`) are ignored with
> a non-blocking `spectra validate` notice; `ai.critic.model` (default `claude-sonnet-4-6`) is the only
> critic selector. `ai.providers` and the GitHub Copilot SDK are still used by the in-process
> criteria-extraction path and Copilot auth (their retirement is a future spec).

### Generation (in-session via the `spectra-generate` skill)

Test generation is **skill-driven**, not a direct CLI command — ask Claude Code (e.g. *"generate test
cases for the checkout suite"*) and the `spectra-generate` skill orchestrates the seam. The underlying
model-free CLI commands (each deterministic, emitting a prompt to stdout or ingesting agent output, all
supporting `--output-format json`) are:

| Command | Role |
|---------|------|
| `spectra ai compile-analysis-prompt --suite <s> [--doc-suite <id>] [--focus <text>]` | Emit the behavior-analysis prompt (analyze-first step). |
| `spectra ai ingest-analysis --suite <s> [--from <file>]` | Turn the agent's behavior JSON into a recommendation (already-covered, recommended count, category + ISTQB technique breakdowns). |
| `spectra ai compile-prompt --suite <s> --count <n> [--focus <text>]` | Emit the bulk generation prompt for `<n>` tests. |
| `spectra ai compile-prompt --suite <s> --from-description "<text>" [--context <text>]` | Emit a single-test prompt from a plain-language description (count forced to 1; Spec 050 criteria injected). |
| `spectra ai ingest-tests <suite> [--from <file>]` | Validate + persist the agent-generated tests (fail-loud: exit 5 content-invalid, 6 schema-invalid). |
| `spectra ai compile-critic-prompt` / `spectra ai ingest-verdict` | The mandatory `spectra-critic` verification step (Spec 055). |
| `spectra ai ingest-grounding --suite <s> --test <id> [--repaired] [--repair-attempts <n>]` | Write the durable condensed verdict block into the test `.md` frontmatter (Spec 071). |
| `spectra ai compile-repair-prompt --suite <s> --test <id>` | Emit a repair prompt for a partial test (critic findings + source docs); plain text to stdout (Spec 071). |
| `spectra ai record-drop --suite <s> --test <id> [--reason user_decided]` | Append a drop-trail entry before deleting a hallucinated or user-decided-to-drop test (Spec 071). |
| `spectra ai review-flagged [--suite <s>]` | List and disposition flagged (still-partial) tests: accept, delete, or (via skill) retry repair (Spec 071). |

ISTQB techniques (EP, BVA, DT, ST, EG, UC) and coverage-aware analysis (Spec 044 — existing
`_index.json` / criteria / doc sections inform the recommended gap count) are applied inside the
analysis prompt. `--include-archived` includes `skip_analysis` suites in the analyzer input.

**Doc-suite filtering & pre-flight budget check (Spec 040)** — `compile-analysis-prompt` inlines
documentation, so pass `--doc-suite <id>` when the test-suite name doesn't match a doc-suite ID;
otherwise it loads the full corpus and may exit `4` (estimated prompt exceeds
`ai.analysis.max_prompt_tokens`, default 96,000) with an actionable message naming candidate suites +
token costs. `spectra docs list-suites` shows available IDs and their token estimates. `compile-prompt`
(generation) is criteria-grounded and not subject to the inlined-doc budget.

**Exit codes** — compile commands: `0` emitted, `4` refused (missing input or budget exceeded), `1`
config/IO error. Ingest commands: `0` persisted/classified, `5` empty content, `6` schema/parse damage,
`1` config/IO error. The skill drives a bounded regenerate-and-retry on `5`/`6`.

**Troubleshooting** — when `debug.enabled: true`, failures during the still-in-process criteria
extraction (`ai analyze --extract-criteria` / `docs index`) write full entries to `.spectra-errors.log`.
Duplicate detection (>80% title similarity) and the `ai.critic.max_concurrent` critic-throughput lever
continue to apply to the verification path.

### `spectra ai update`

Update existing test cases when documentation changes. Classifies test cases as UP_TO_DATE, OUTDATED, ORPHANED, or REDUNDANT.

```bash
spectra ai update                             # Interactive mode
spectra ai update checkout                    # Direct mode
spectra ai update checkout --diff             # Show proposed changes
spectra ai update checkout --delete-orphaned  # Auto-delete orphaned test cases
spectra ai update checkout --no-interaction   # CI mode
```

| Option | Short | Description |
|--------|-------|-------------|
| `--diff` | `-d` | Show diff of proposed changes |
| `--delete-orphaned` | | Automatically delete orphaned test cases |
| `--no-interaction` | | Disable interactive prompts |
| `--no-review` | | Skip interactive review |
| `--dry-run` | | Preview without applying changes |

`spectra ai update` is the **selector**: it classifies tests but does not rewrite them. The doc-aware targeted edit of an OUTDATED test runs in the interactive session via the `spectra-update` skill, over the deterministic update seam below.

### `spectra ai compile-update-prompt`

Compile a deterministic, model-free **edit** prompt for one OUTDATED test (the existing test + the changed source/criteria + "edit, don't regenerate; preserve id/structure/manual fields"). Emits to stdout; writes nothing.

```bash
spectra ai compile-update-prompt --suite checkout --test-id TC-104
```

| Option | Short | Description |
|--------|-------|-------------|
| `--suite` | `-s` | Suite containing the test to update (required) |
| `--test-id` | | Id of the OUTDATED test to compile an edit prompt for (required) |

Exit codes: `0` prompt emitted · `4` refused (missing suite/test-id, test not found, or no changed source/criteria) · `1` config/I/O error.

### `spectra ai ingest-update`

Validate the edited test and persist it through the single write+index path — **without allocating a new id**. Deterministically protects invariants: id from the original, pre-existing `Manual` verdict/notes re-asserted, and a drift guard that fails loud on out-of-scope field changes.

```bash
spectra ai ingest-update checkout --test-id TC-104 --from .spectra/updated.json
```

| Option | Description |
|--------|-------------|
| `suite` (positional) | Suite the edited test belongs to (required) |
| `--test-id` | Id of the original test being edited (required) |
| `--from` | File with the edited-test JSON (omit to read stdin) |

Exit codes: `0` persisted (id unchanged) · `5` content invalid or `DRIFT_DETECTED` · `6` schema invalid · `1` config/I/O error or original not found. On any non-zero exit nothing is persisted.

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

Features: Suite overview, execution history, three-section coverage analysis, coverage map (D3.js), test browser, trend analysis.

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

Reports the status of the optional Testimize integration (spec 038): whether
it is enabled in config, whether the `Testimize.MCP.Server` global tool is
installed, whether the server passes a health probe, the configured mode and
strategy, and whether the optional `testimizeSettings.json` is present.

```bash
spectra testimize check
spectra testimize check --output-format json
```

When Testimize is disabled (the default), the command does NOT start the MCP
process — it just reports `enabled: false`. When enabled but not installed,
the output includes a one-line `dotnet tool install --global Testimize.MCP.Server`
instruction. JSON output always contains the fields `enabled`, `installed`,
`healthy`, plus `mode`, `strategy`, and (when not installed) `install_command`.

See [Testimize Integration](testimize-integration.md) for the full guide.

---

## Execution Commands

Execution runs entirely through the **`spectra run` CLI** over the deterministic engine and
`.execution/spectra.db` — **no MCP server** (SPECTRA's MCP execution adapter was removed in Spec 070).
A short-lived `spectra run` process reconstructs run state losslessly from the database, so no live
session is required.

### `spectra run …`

The single `dotnet tool install -g Spectra` provides execution as well as generation.
Drive a full manual loop from the shell:

```bash
spectra run list-suites                         # what's runnable
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

#### `spectra run console` — local web console (Spec 066)

A local, human-driven **execution console**: a small detached HTTP server on `127.0.0.1` serving an
ephemeral, gitignored page (in Spectra's report styling) where a QA engineer drives a run from the
browser — sees the current test, clicks **PASS / FAIL / BLOCKED**, adds a comment, drops/pastes a
screenshot. It is a *second transport over the same engine* (sibling of `spectra run …`):
**SQLite is the single source of truth; the browser is a view + write-back caller, never a
store**, so a refresh or reopen loses nothing. Verdict guardrails are enforced server-side (a comment is
required for FAIL/BLOCKED/SKIP; a verdict is never inferred), identical to `spectra run advance`.

The server starts **detached** — it survives the launching terminal/agent session ending and stops only
on `spectra run console --stop` (a `.execution/console.json` marker records its pid/port/url). `--port`
is optional (a free port is auto-selected). This is a different deployment model from the dashboard — see
`deployment.md`.

Every subcommand honors the global `--output-format json|human` and `--verbosity`. Exit code is `0` on
success and non-zero on error; a reconstruction failure surfaces as a distinct `RECONSTRUCTION_FAILED`
outcome (never conflated with a benign "run not found"). The `spectra-execute` SKILL and the execution
agent **orchestrate** this run — select tests → `spectra run start` → launch `spectra run console` →
hand over the local URL → stay on-call — rather than driving a per-test loop in chat. The tester records
each verdict in the browser console, whose write-back endpoint enforces the human-in-the-loop guardrails
(explicit verdict; comment required for fail/blocked/skip; no auto-advance; no inferred verdict). The
agent reads current state on-call from `spectra run status` (SQLite), never from the console page.

Handle resolution: when `<handle>`/`<run-id>` is omitted, the active run for the current user and its
in-progress (or next-pending) test are auto-resolved — so the agent rarely needs to pass them explicitly.

### MCP execution server — removed (Spec 070)

SPECTRA no longer ships an MCP execution server (`spectra-mcp`). Execution is CLI-only via `spectra run`
above; `spectra init` no longer writes any `.vscode/mcp.json` or `mcp__spectra__*` allowlist. The former
MCP tools each map one-to-one to a `spectra run` subcommand — e.g. `start_execution_run` → `spectra run
start`, `advance_test_case` → `spectra run advance`, `bulk_record_results` → `spectra run bulk-record`,
`get_run_history` → `spectra run history`. The data tools' operations remain available as their own CLI
commands (`spectra validate`, `spectra index` / `docs index`, `spectra ai analyze --coverage`). The
SEPARATE BELLATRIX/Nova MCP that drives the system-under-test is unrelated and unaffected.

---

## Spec 040 lifecycle commands (v1.52.0)

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

**Exit codes**: 0 success, 1 generic, 3 missing args, 4 `TEST_NOT_FOUND`, 5 `AUTOMATION_LINKED`, 130 SIGINT.

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

**Exit codes** for `rename`/`delete`: 0 success, 4 `SUITE_NOT_FOUND`, 5 `AUTOMATION_LINKED`, 6 `SUITE_ALREADY_EXISTS`, 7 `INVALID_SUITE_NAME` (must match `^[a-z0-9][a-z0-9_-]*$`), 8 `EXTERNAL_DEPENDENCIES`, 130 SIGINT.

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

**Exit codes**: 0 success, 9 `DUPLICATES_FOUND` (only when `--no-interaction` and duplicates reported without `--fix` — CI-friendly regression gate), 130 SIGINT.

### Cancellation behavior on long-running commands

`ai generate`, `ai update`, `ai analyze --coverage`, `dashboard`, and `docs index` register with the process-level `CancellationManager` at startup. A peer `spectra cancel` (or Ctrl+C) triggers cooperative shutdown at the next batch boundary; partial results already on disk are preserved. The result artifact reports `status: cancelled` and the progress page transitions to a terminal "Cancelled" phase. Exit code is **130** (standard SIGINT convention).
