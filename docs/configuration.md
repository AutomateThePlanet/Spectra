---
title: Configuration
parent: User Guide
nav_order: 2
---

# Configuration Reference

Complete reference for `spectra.config.json`.

Related: [Getting Started](getting-started.md) | [CLI Reference](cli-reference.md)

---

SPECTRA is configured via `spectra.config.json` at the repository root. Run `spectra init` to create one with defaults.

## Full Schema

```json
{
  "source": { ... },
  "tests": { ... },
  "ai": { ... },
  "generation": { ... },
  "update": { ... },
  "suites": { ... },
  "coverage": { ... },
  "execution": { ... },
  "validation": { ... },
  "git": { ... },
  "dashboard": { ... },
  "selections": { ... },
  "profile": { ... }
}
```

---

## `source` — Documentation Source

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `mode` | string | `"local"` | Source mode (`local` or `spaces`) |
| `local_dir` | string | `"docs/"` | Path to documentation directory |
| `space_name` | string | — | Copilot Space name (when `mode: "spaces"`) |
| `doc_index_dir` | string | `"docs/_index"` | **Spec 040** Directory containing the v2 index layout (`_manifest.yaml`, `_checksums.json`, `groups/{suite}.index.md`). |
| `doc_index` | string | — | Legacy single-file path. Used only by the auto-migrator on first run after upgrading. New writes always use `doc_index_dir`. |
| `group_overrides` | object | `{}` | **Spec 040** Per-document suite overrides: `{ "docs/path.md": "suite-id" }`. Consulted after frontmatter overrides and before the directory-default rule. |
| `max_file_size_kb` | int | `50` | Maximum file size to process |
| `include_patterns` | string[] | `["**/*.md"]` | Glob patterns for files to include |
| `exclude_patterns` | string[] | `["**/CHANGELOG.md"]` | Glob patterns for files to exclude (does NOT touch the analyzer-input filter — see `coverage.analysis_exclude_patterns` for that) |

## `tests` — Test Case Output

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `dir` | string | `"test-cases/"` | Test case output directory |
| `id_prefix` | string | `"TC"` | Prefix for test IDs |
| `id_start` | int | `100` | Starting number for first generated ID |

## `ai` — Generation Pacing (no provider/model routing anymore)

> **Spec 069 (v2):** `ai.providers` and `ai.critic` (as a config block) were removed entirely along
> with the GitHub Copilot SDK. SPECTRA makes no model calls of its own — generation and analysis
> run as ordinary turns in your interactive Claude Code session, and the critic runs as the
> `spectra-critic` subagent. There is nothing left here to route to a provider or pick a generator
> model; a legacy config with `ai.providers`/`ai.critic` still loads (the keys are simply ignored).
> See [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) and the
> [AI Models & Cost Guide](ai-models-cost-guide.md).

```json
{
  "ai": {
    "generation_batch_size": 30,
    "generation_timeout_minutes": 5,
    "analysis_timeout_minutes": 2
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `generation_batch_size` | int | `30` | Number of tests requested per generation turn (`compile-prompt` → turn → `ingest-tests`). Larger batches mean fewer round-trips, at the cost of a bigger single response. Values ≤ 0 fall back to the default. |
| `generation_timeout_minutes` | int | `5` | Bounds the deterministic CLI side of the generation seam (parsing, validation, index writes) — not the model turn itself, which runs in your own session. |
| `analysis_timeout_minutes` | int | `2` | Same, for the behavior-analysis seam. |

**Choosing a model**: pick it the same way you'd pick a model for any other Claude Code work
(`/model`, or your plan's default) — whatever your session is running when you drive
`spectra-generate`/`spectra-criteria`/`spectra-update` **is** the model doing that turn. See the
[AI Models & Cost Guide](ai-models-cost-guide.md#choosing-a-session-model) for guidance on
Haiku/Sonnet/Opus trade-offs.

**Critic model**: fixed by the `model:` frontmatter field in
`.claude/agents/spectra-critic.agent.md` (shipped as `claude-sonnet-4-6`), not by
`spectra.config.json`. Edit that file directly to change it.

#### `.spectra-debug.log` — no longer tracks per-call AI timing

Pre-v2, `.spectra-debug.log` recorded one line per SDK call (`ANALYSIS START/OK`, `BATCH
START/OK`, `CRITIC START/OK`) with model, provider, and elapsed time, because SPECTRA's own
process made those calls and could observe them. **As of Spec 069, SPECTRA doesn't make those
calls anymore** — generation/analysis run as turns in your own Claude Code session, invisible to
the CLI process. There is nothing left for SPECTRA to time or log for those phases; `debug.enabled`
still exists (see [the `debug` section](#debug)) but the AI-call timing lines it used to produce
no longer apply. Track actual usage/cost through Claude Code's own session/usage indicators — see
the [AI Models & Cost Guide](ai-models-cost-guide.md#tracking-what-youve-spent).

**Testimize lifecycle lines** are unaffected — Testimize is an in-process NuGet library (not an
AI call), so `TESTIMIZE DISABLED/HEALTHY/DISPOSED` etc. still log normally when `debug.enabled` is
set.

### `ai.critic` — removed (Spec 058/069)

There is no `ai.critic` config block anymore. Grounding verification runs as the `spectra-critic`
subagent (`.claude/agents/spectra-critic.agent.md`), whose `model:` frontmatter field is the only
model selector, and whose `timeout`/concurrency come from how Claude Code itself schedules subagent
calls — not from `spectra.config.json`. See [Grounding Verification](grounding-verification.md)
and [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md). A legacy config
that still carries `ai.critic.*` loads unchanged; the keys are ignored.

## `generation` — Test Case Generation Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `default_count` | int | `15` | Default number of test cases to generate |
| `require_review` | bool | `true` | Require interactive review before writing |
| `duplicate_threshold` | double | `0.6` | Similarity threshold for duplicate detection |
| `categories` | string[] | `["happy_path", "negative", "boundary", "integration"]` | Test case categories to generate |

## `analysis` — Behavior Analysis Categories

Controls how the AI categorizes testable behaviors during the analysis phase
(`spectra ai generate` runs this automatically before generation). See
[Customization > Behavior Categories](customization.md#3-behavior-categories)
for domain-specific examples.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `categories` | object[] | (6 built-in) | Behavior classification taxonomy injected into the analysis prompt. |
| `categories[].name` | string | — | Category identifier (used in tags and breakdown). |
| `categories[].description` | string | — | One-line description injected into the AI prompt to guide classification. |
| `max_prompt_tokens` | int | `96000` | **Spec 040** Pre-flight budget for the behavior-analysis prompt. When the estimated prompt exceeds this, the command exits cleanly with code `4` and an actionable error naming every candidate suite + token cost. Set to 0 to disable the check. Default leaves a 32K margin under the 128K context window for response + prompt template + ancillary content. |

Default categories:

```json
{
  "analysis": {
    "categories": [
      { "name": "happy_path", "description": "Core happy-path functionality" },
      { "name": "negative", "description": "Invalid input, unauthorized access, bad state" },
      { "name": "edge_case", "description": "Unusual but valid scenarios" },
      { "name": "boundary", "description": "Min/max values, exact limits, off-by-one" },
      { "name": "error_handling", "description": "System failures, timeouts, downstream errors" },
      { "name": "security", "description": "Auth checks, input sanitization, data exposure" }
    ]
  }
}
```

## `update` — Test Case Update Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `chunk_size` | int | `30` | Number of test cases to process per AI call |
| `require_review` | bool | `true` | Require interactive review before applying |

## `suites` — Per-Suite Configuration

```json
{
  "suites": {
    "checkout": {
      "component": "checkout",
      "relevant_docs": ["docs/features/checkout.md"],
      "default_tags": ["checkout"],
      "default_priority": "high"
    }
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `component` | string | — | Default component for test cases in this suite |
| `relevant_docs` | string[] | `[]` | Documentation files relevant to this suite |
| `default_tags` | string[] | `[]` | Default tags for generated test cases |
| `default_priority` | string | `"medium"` | Default priority for generated test cases |

## `coverage` — Coverage Analysis

See [Coverage](coverage.md) for how these settings are used.

```json
{
  "coverage": {
    "automation_dirs": ["tests", "test", "spec", "e2e"],
    "scan_patterns": [
      "[TestCase(\"{id}\")]",
      "@pytest.mark.manual_test(\"{id}\")"
    ],
    "file_extensions": [".cs", ".java", ".py", ".ts"],
    "criteria_file": "docs/criteria/_criteria_index.yaml",
    "file_patterns": ["*.cs", "*.ts", "*.js", "*.py", "*.java"],
    "attribute_patterns": ["..."],
    "test_id_pattern": "TC-\\d{3,}",
    "report_orphans": true,
    "report_broken_links": true,
    "report_mismatches": true
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `automation_dirs` | string[] | `["tests", "test", "spec", "specs", "e2e"]` | Directories to scan for automation code |
| `scan_patterns` | string[] | (framework defaults) | Templates where `{id}` is replaced with test ID regex |
| `file_extensions` | string[] | `[".cs", ".java", ".py", ".ts"]` | File types to scan for test references |
| `criteria_file` | string | `"docs/criteria/_criteria_index.yaml"` | Path to acceptance criteria index |
| `criteria_dir` | string | `"docs/criteria"` | Directory containing per-document `.criteria.yaml` files |
| `file_patterns` | string[] | `["*.cs", "*.ts", "*.js", "*.py", "*.java"]` | Glob patterns for automation files (legacy) |
| `attribute_patterns` | string[] | (framework defaults) | Regex patterns for test attributes (legacy, use `scan_patterns` instead) |
| `test_id_pattern` | string | `"TC-\\d{3,}"` | Regex for matching test IDs |
| `requirements_file` | string | `"docs/requirements/_requirements.yaml"` | Path to requirements YAML index for requirements coverage analysis |
| `report_orphans` | bool | `true` | Report automation files not linked to any test case |
| `report_broken_links` | bool | `true` | Report tests referencing non-existent files |
| `report_mismatches` | bool | `true` | Report inconsistencies between index and files |
| `criteria_import.default_source_type` | string | `"manual"` | Default source type for imported acceptance criteria |
| `criteria_import.auto_split` | bool | `true` | Automatically split compound criteria into atomic ones during import |
| `criteria_import.normalize_rfc2119` | bool | `true` | Normalize RFC 2119 keywords (MUST, SHALL, SHOULD) in imported criteria |
| `criteria_import.id_prefix` | string | `"AC"` | Prefix for auto-generated acceptance criteria IDs |
| `analysis_exclude_patterns` | string[] | see below | **Spec 040** Glob patterns whose matched documents are still indexed and counted in coverage but whose suites are flagged `skip_analysis: true` and excluded from AI analyzer prompts by default. Pass `--include-archived` on `spectra ai generate` / `spectra ai analyze` / `spectra docs index` to override. The list **replaces** rather than merges with defaults — set to `[]` to disable all default exclusions. |
| `coverage_exclude_patterns` | string[] | `[]` (empty) | **Spec 060** Glob patterns whose matched documents are dropped from the **documentation-coverage denominator only**. Excluded docs remain fully present in the document map for generation, analysis, and indexing, and are reported with a distinct `excluded` status. Defaults to empty (no implicit patterns) — unconfigured workspaces see unchanged coverage output. See [the three exclusion mechanisms](#the-three-exclusion-mechanisms) below. |
| `max_suite_tokens` | int | `80000` | **Spec 040** Spillover threshold. When a single suite's `tokens_estimated` exceeds this, the indexer additionally writes per-doc files at `docs/_index/docs/{sanitized}.index.md` so finer-grained loading is possible (Spec 041 prep). |

### Default `analysis_exclude_patterns`

```json
{
  "coverage": {
    "analysis_exclude_patterns": [
      "**/Old/**",
      "**/old/**",
      "**/legacy/**",
      "**/archive/**",
      "**/release-notes/**",
      "**/CHANGELOG*",
      "**/SUMMARY.md"
    ]
  }
}
```

Documents matching these patterns are still counted in coverage reports — the exclusion only removes them from analyzer-facing prompts. To opt a single document back in despite a pattern match, add `analyze: true` to its frontmatter (planned for a future release; see Spec 040 §3.6).

### Example: `coverage_exclude_patterns`

```json
{
  "coverage": {
    "coverage_exclude_patterns": [
      "docs/release-notes/**",
      "**/SUMMARY.md"
    ]
  }
}
```

Documents matching these patterns are dropped from the documentation-coverage **denominator** and reported with a distinct `excluded` status. They remain in the document map for generation, analysis, and indexing — only the coverage percentage ignores them. The default is empty (`[]`): with no patterns configured, coverage output is identical to before Spec 060.

### The three exclusion mechanisms

Spectra has **three** independent document-exclusion mechanisms. They are evaluated separately — matching a document under one has no effect on the others. Choose by the scope you want to affect:

| Config key | Scope of effect | What it does | Default |
|------------|-----------------|--------------|---------|
| `source.exclude_patterns` | **Everything** | Total removal at discovery — the document never enters the document map. Invisible to generation, analysis, indexing, **and** coverage. | `["**/CHANGELOG.md"]` |
| `coverage.analysis_exclude_patterns` | **Index / AI analysis** | The document is still indexed and **still counts in coverage**, but its suite is flagged `skip_analysis: true` and it is dropped from AI analyzer prompts. | `["**/Old/**", "**/old/**", "**/legacy/**", "**/archive/**", "**/release-notes/**", "**/CHANGELOG*", "**/SUMMARY.md"]` |
| `coverage.coverage_exclude_patterns` | **Coverage % only** | The document is dropped from the documentation-coverage denominator and reported `excluded`. It remains in the map for generation, analysis, and indexing. | `[]` (empty) |

> A document matched by `analysis_exclude_patterns` but **not** by `coverage_exclude_patterns` **still counts** against the coverage percentage. To stop a release-notes folder from dragging the coverage number down without removing it from generation/analysis, use `coverage_exclude_patterns`.

## `selections` — Saved Test Selections

Named test case selections for quick execution. Each selection defines filters that are evaluated across all suites at runtime.

```json
{
  "selections": {
    "smoke": {
      "description": "Quick smoke test — high priority test cases only",
      "priorities": ["high"]
    },
    "regression": {
      "description": "Full regression suite",
      "tags": ["regression"]
    },
    "auth-manual": {
      "description": "Manual auth tests without automation",
      "components": ["auth"],
      "has_automation": false
    }
  }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `description` | string | Human-readable description shown when listing selections |
| `tags` | string[] | Filter by tags (OR within array) |
| `priorities` | string[] | Filter by priority (OR within array) |
| `components` | string[] | Filter by component (OR within array) |
| `has_automation` | bool | `true` = only automated test cases, `false` = only manual test cases |

Filters are combined with AND logic between types. Use the `list_saved_selections` MCP tool to preview how many test cases each selection matches, or start a run with `start_execution_run` using the `selection` parameter.

The default configuration includes a `smoke` selection that matches all high-priority test cases.

---

## `execution` — Execution Settings

This section currently has no configurable fields. During a run, the execution agent answers tester questions about a test step or expected result by reading that test case's `source_refs` documentation files directly — no configuration is required.

> The former `copilot_space` / `copilot_space_owner` fields were removed and no longer have any effect. Existing config files that still carry these keys keep working — the keys are simply ignored.

## `validation` — Test Validation Rules

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `required_fields` | string[] | `["id", "priority"]` | Fields required in test case frontmatter |
| `allowed_priorities` | string[] | `["high", "medium", "low"]` | Valid priority values |
| `max_steps` | int | `20` | Maximum test case steps allowed |
| `id_pattern` | string | `"^TC-\\d{3,}$"` | Regex pattern for valid test IDs |

## `git` — Git Operations

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `auto_branch` | bool | `true` | Create feature branches for generated tests |
| `branch_prefix` | string | `"spectra/"` | Branch name prefix |
| `auto_commit` | bool | `true` | Automatically commit generated test cases |
| `auto_pr` | bool | `false` | Automatically create pull requests |

## `dashboard` — Dashboard Generation

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `output_dir` | string | `"./site"` | Default output directory |
| `title` | string | — | Dashboard title (defaults to repository name) |
| `template_dir` | string | — | Custom template directory |
| `include_coverage` | bool | `true` | Include coverage analysis tab |
| `include_runs` | bool | `true` | Include execution history |
| `max_trend_points` | int | `30` | Maximum data points for trend charts |

## `profile` — Profile Loading

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `repository_file` | string | `"spectra.profile.md"` | Repository-level profile filename |
| `suite_file` | string | `"_profile.md"` | Suite-level profile filename |
| `auto_load` | bool | `true` | Automatically load profiles during generation |
| `validate_on_load` | bool | `true` | Validate profile structure on load |
| `include_in_validation` | bool | `true` | Include profile validation in `spectra validate` |

## `testimize` — Testimize Integration (Optional)

Disabled by default. When enabled, SPECTRA runs the Testimize engine
in-process during test generation to produce algorithmically precise
boundary values, equivalence partitions, and multi-field combinations.
See [Testimize Integration](testimize-integration.md) for the full setup
guide, how-it-works flow, and ABC tuning reference.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | bool | `false` | Master switch. |
| `mode` | string | `"exploratory"` | Maps to Testimize's `TestCaseCategory`: `"exploratory"` → All, `"valid"` → Valid only, `"validation"` → Invalid only. |
| `strategy` | string | `"HybridArtificialBeeColony"` | Algorithm for multi-field suites: `Pairwise`, `OptimizedPairwise`, `Combinatorial`, `OptimizedCombinatorial`, `HybridArtificialBeeColony`. |
| `settings_file` | string? | `null` | Path to a `testimizeSettings.json` with per-type equivalence classes and ABC tuning. |
| `abc_settings` | object? | `null` | Optional ABC algorithm tuning. See [Testimize Integration > abc_settings](testimize-integration.md#abc_settings-optional-tuning). |

## `debug` — Debug Logging (Spec 040)

Controls the append-only `.spectra-debug.log` diagnostic file. **Disabled by
default.** When off, no file is created and no disk I/O occurs for debug
logging, eliminating stale-file accumulation in CI environments.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | bool | `false` | Historically, every AI call (analyze, generate, critic, criteria extraction) wrote a timestamped line to the debug log. **As of Spec 069, SPECTRA makes none of those calls itself** — generation/analysis/criteria/critic all run as turns/subagent calls in your own Claude Code session, invisible to this log. Testimize lifecycle lines (a non-AI, in-process library) still write normally. Best-effort; never blocks the calling code. |
| `log_file` | string | `".spectra-debug.log"` | Path to the log file. Relative paths resolve against the repo root. |
| `error_log_file` | string | `".spectra-errors.log"` | **Spec 043:** path to the dedicated error log. Written only when `enabled` is `true` AND at least one error occurs during the run. On a clean run the file is not created or modified. Captures full exception type, message, response body (truncated to 500 chars), `Retry-After` header, and stack trace per failure. Follows the same `mode` semantics as `log_file`. |
| `mode` | string | `"append"` | Controls how the file is opened at run start. `"append"` prepends a separator + header block before each new run (useful for comparing multiple runs). `"overwrite"` truncates the file and writes just the header (keeps only the latest run). Any other value falls back to `"append"`. |

### What still gets logged (v2)

The AI-call line format (`model=… provider=… tokens_in=… tokens_out=…`), the per-run Run Summary
cost panel, and the dedicated error log (`.spectra-errors.log`) all described SPECTRA observing
its *own* SDK calls — pre-Spec-069 behavior. None of it applies anymore: SPECTRA doesn't make those
calls, so it has nothing to time, count, or attribute cost to. What's left:

- **Testimize lifecycle lines** (`DISABLED`/`START`/`NOT_INSTALLED`/`UNHEALTHY`/`HEALTHY`/
  `DISPOSED`) still write normally — Testimize is an in-process library call, not a model call.
- **`--verbosity diagnostic`** still force-enables `debug.enabled` for a single invocation.

To see what a generation/analysis/criteria/update run actually cost, use Claude Code's own usage
surfaces (your plan's usage indicator, or the API/Console token dashboard) — see
[Tracking what you've spent](ai-models-cost-guide.md#tracking-what-youve-spent).
