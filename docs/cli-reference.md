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

Rebuild `_index.json` files for all suites.

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

Build or incrementally update the documentation index (`docs/_index.md`), then automatically extract acceptance criteria.

```bash
spectra docs index                # Incremental update + auto-extract acceptance criteria
spectra docs index --force        # Full rebuild + auto-extract acceptance criteria
spectra docs index --skip-criteria  # Index only, skip criteria extraction
spectra docs index --no-interaction --output-format json  # SKILL/CI mode
```

The index contains per-document metadata (title, sections with summaries, key entities, word/token counts, content hashes). The AI agent reads this lightweight index (~1-2K tokens) instead of scanning all files.

Content hashes enable incremental updates — only changed files are re-indexed. The index is also auto-refreshed before `spectra ai generate` runs.

After indexing, acceptance criteria are automatically extracted from the documentation using the configured AI provider and merged into `_criteria_index.yaml`. If no provider is configured, the extraction step is skipped. Use `--skip-criteria` to skip extraction entirely.

In SKILL/CI mode, the command writes `.spectra-result.json` (structured result) and `.spectra-progress.html` (live progress page with auto-refresh).

See [Document Index](document-index.md) for full details.

---

## AI Generation Commands

### `spectra ai generate`

Generate test cases from documentation using systematic ISTQB test design
techniques (Equivalence Partitioning, Boundary Value Analysis, Decision
Table, State Transition, Error Guessing, Use Case). The analysis output
includes both a category breakdown (`happy_path`, `boundary`, `negative`, …)
and a technique breakdown (`BVA`, `EP`, `DT`, `ST`, `EG`, `UC`) — see spec
037. The analysis step is **coverage-aware** (spec 044): for existing suites,
it considers test coverage from `_index.json`, acceptance criteria, and
doc sections to recommend only gap tests. Supports multiple modes.

**Interactive Session** — four-phase guided session:

```bash
spectra ai generate
```

Launches a generation session that flows through:
1. **Phase 1 — Analysis**: Counts testable behaviors in documentation, shows breakdown by category
2. **Phase 2 — Generation**: Creates test cases with AI verification (grounded/partial/hallucinated)
3. **Phase 3 — Suggestions**: Proposes additional test cases for uncovered areas
4. **Phase 4 — User-Described**: Create test cases from your own descriptions (skips critic, marked `verdict: manual`)
5. Phases 3-4 loop until you choose "Done", then displays session summary

**Direct Mode** — specify suite and options upfront:

```bash
spectra ai generate checkout --count 10
spectra ai generate checkout --focus "error handling"
spectra ai generate checkout --skip-critic
spectra ai generate checkout --count 5 --dry-run
```

**Session Commands** — work with previous session state:

```bash
spectra ai generate checkout --from-suggestions                     # Generate from last session's suggestions
spectra ai generate checkout --from-suggestions 1,3                 # Generate specific suggestions by index
spectra ai generate checkout --from-description "IBAN validation"   # Create test case from description
spectra ai generate checkout --from-description "IBAN validation" --context "checkout page"
spectra ai generate checkout --auto-complete --output-format json   # CI: all phases, no prompts
```

**SKILL/CI Mode** — structured JSON output:

```bash
spectra ai generate checkout --output-format json --verbosity quiet   # JSON for Copilot SKILL parsing
spectra ai generate checkout --no-interaction --output-format json    # CI pipeline with exit codes
```

| Option | Short | Description |
|--------|-------|-------------|
| `--count` | `-n` | Number of test cases to generate (default: AI-recommended count) |
| `--focus` | `-f` | Focus area description for targeted generation |
| `--skip-critic` | | Skip grounding verification (faster) |
| `--from-suggestions` | | Generate from previous session's suggestions (optionally pass indices like `1,3`) |
| `--from-description` | | Create a test case from a plain-language behavior description |
| `--context` | | Additional context for `--from-description` |
| `--auto-complete` | | Run all phases without prompts (analyze → generate → suggestions → finalize) |
| `--dry-run` | | Preview without writing files |

Session state is stored in `.spectra/session.json` and expires after 1 hour.

**Live progress bars** (spec 041) — at default verbosity, long runs render two
sequential Spectre.Console progress bars in the terminal: a `Generating test cases`
bar that advances per batch, then a `Verifying test cases` bar that advances per
critic call (showing the most recent test ID and verdict). The same data is
mirrored to `.spectra-progress.html` via an in-flight `progress` object inside
`.spectra-result.json`. Progress bars are automatically suppressed under
`--output-format json`, `--verbosity quiet`, or non-interactive stdout (piped
or redirected) so SKILL/CI output remains clean.

User-described test cases are marked with `grounding.verdict: manual` and `source: user-described`.

When a project has documentation in `docs/` and acceptance criteria in `docs/criteria/`, `--from-description` runs in **doc-aware mode**: it best-effort loads matching docs (capped at 3 docs × 8000 chars) and matching `.criteria.yaml` entries as formatting context, then populates the new test case's `source_refs` (with the doc paths used) and `criteria` fields (with any IDs the AI matches to your description). The grounding verdict stays `manual` — doc context is used for terminology and navigation alignment only, never for verification. If no docs or criteria exist, the flow is identical to the no-context behavior.

Duplicate detection warns when a new test case has >80% title similarity to an existing test case.

**Exit codes:** `0` = success, `1` = error, `3` = missing required args with `--no-interaction`.

**Speeding up the critic phase** (Spec 043, v1.48.0+): on a large generate run, the critic verifies test cases one at a time and is typically the dominant cost (~6s/call × N tests). Set `ai.critic.max_concurrent: 5` (or higher, max 20) in `spectra.config.json` to run multiple critic calls in parallel — this typically cuts critic phase time to ~1/N of sequential without changing any output. The Run Summary panel shows the active `Critic concurrency` along with `Errors` and `Rate limits` counts. If you see `Rate limits > 0`, drop `max_concurrent` and re-run.

**Troubleshooting failed runs** (Spec 043, v1.48.0+): when `debug.enabled: true`, every failed AI call (timeout, HTTP 429, parse error, MCP failure, generic exception) writes a full entry to `.spectra-errors.log` — exception type, message, response body (truncated to 500 chars), `Retry-After` header, and stack trace. The file is created lazily on first error and never touched on healthy runs, so a single `cat .spectra-errors.log` answers "did anything go wrong?". Each error gets a `see=.spectra-errors.log` cross-reference in `.spectra-debug.log` so you can jump from the timeline to the full context.

**Tuning for slow models** (v1.41.0+): generation runs in batches of `ai.generation_batch_size` test cases (default 30), each subject to `ai.generation_timeout_minutes` (default 5). Slower / reasoning-class models (DeepSeek-V3, large Azure deployments, GPT-4 Turbo with long contexts) typically need `generation_timeout_minutes: 15–20` and `generation_batch_size: 6–10`. When `ai.debug_log_enabled` is true (the default), each batch writes a timestamped `BATCH START` / `BATCH OK` / `BATCH TIMEOUT` line to `.spectra-debug.log` in the project root with the model, provider, requested count, and elapsed seconds — inspect this file to dial the knobs precisely. See [Configuration → ai.generation_timeout_minutes](configuration.md) for the full reference, example configs, and the timeout error message format.

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

### `spectra-mcp` (separate tool)

The MCP execution server is a separate global tool packaged as `Spectra.MCP`. Install it once and let your MCP client launch it via stdio:

```bash
dotnet tool install -g Spectra.MCP
spectra-mcp                # Started by your MCP client over stdio (JSON-RPC 2.0)
```

For VS Code Copilot Chat, `spectra init` writes a working `.vscode/mcp.json` that points at `spectra-mcp`. The MCP tools available are grouped below.

#### Run Management Tools

| Tool | Description |
|------|-------------|
| `start_execution_run` | Start a new run by suite, test IDs, or saved selection |
| `get_execution_status` | Get current run status and next test |
| `pause_execution_run` | Pause execution |
| `resume_execution_run` | Resume paused execution |
| `cancel_execution_run` | Cancel execution |
| `finalize_execution_run` | Complete run and generate reports |
| `list_available_suites` | List test suites |

#### Test Execution Tools

| Tool | Description |
|------|-------------|
| `get_test_case_details` | Get test steps, expected result, preconditions |
| `advance_test_case` | Record PASSED/FAILED/BLOCKED result |
| `skip_test_case` | Skip test with reason |
| `bulk_record_results` | Bulk record results for multiple tests |
| `add_test_note` | Add notes to a test |
| `retest_test_case` | Requeue a test for another attempt |
| `save_screenshot` | Save screenshot attachment |

#### Smart Selection Tools

| Tool | Description |
|------|-------------|
| `find_test_cases` | Cross-suite search and filter by query, priority, tags, component, automation status |
| `get_test_execution_history` | Per-test execution statistics (pass rate, last status, run count) |
| `list_saved_selections` | List named selections from config with estimated test case counts |

The `find_test_cases` tool supports free-text search (OR across keywords, matching title + description + tags), metadata filters (AND between types, OR within arrays), and returns results ranked by relevance with total estimated duration.

The `start_execution_run` tool supports three mutually exclusive modes:
- `suite` — run all tests in a suite (with optional filters)
- `test_ids` — run specific tests from any suites
- `selection` — run a named saved selection from config

#### Data & Reporting Tools

| Tool | Description |
|------|-------------|
| `validate_tests` | Validate test case files |
| `rebuild_indexes` | Rebuild `_index.json` files |
| `analyze_coverage_gaps` | Analyze test case coverage |
| `get_run_history` | Get execution history |
| `get_execution_summary` | Get summary statistics |
