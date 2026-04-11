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
| `doc_index` | string | — | Path to document index file (defaults to `{local_dir}/_index.md`) |
| `max_file_size_kb` | int | `50` | Maximum file size to process |
| `include_patterns` | string[] | `["**/*.md"]` | Glob patterns for files to include |
| `exclude_patterns` | string[] | `["**/CHANGELOG.md"]` | Glob patterns for files to exclude |

## `tests` — Test Output

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `dir` | string | `"tests/"` | Test output directory |
| `id_prefix` | string | `"TC"` | Prefix for test IDs |
| `id_start` | int | `100` | Starting number for first generated ID |

## `ai` — AI Provider Configuration

```json
{
  "ai": {
    "providers": [
      {
        "name": "github-models",
        "model": "gpt-4.1",
        "enabled": true,
        "priority": 1,
        "api_key_env": null,
        "base_url": null
      }
    ],
    "fallback_strategy": "auto",
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "gpt-5-mini",
      "api_key_env": null,
      "base_url": null,
      "timeout_seconds": 120
    }
  }
}
```

> **Spec 041 defaults:** `spectra init` writes `gpt-4.1` + `gpt-5-mini` — both 0× multiplier on any paid Copilot plan and from different model architectures for independent critic verification. `spectra init -i` offers a choice of four presets (GPT-4.1 + GPT-5 mini free, Claude Sonnet 4.5 + GPT-4.1 critic premium, GPT-4.1 + Claude Haiku 4.5 cross-family, or Custom). Existing configs with `gpt-4o` / `gpt-4o-mini` continue to work unchanged.

### `ai.providers[]`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` | string | — | Provider name (see table below) |
| `model` | string | — | Model identifier |
| `enabled` | bool | `true` | Whether this provider is active |
| `priority` | int | `1` | Selection priority (lower = preferred) |
| `api_key_env` | string | — | Environment variable for API key (BYOK providers) |
| `base_url` | string | — | Custom API endpoint |

### Supported Providers

| Provider | Description | Default API Key Env |
|----------|-------------|---------------------|
| `github-models` | GitHub Models API (default) | `GITHUB_TOKEN` or `gh auth token` |
| `azure-openai` | Azure OpenAI Service | — |
| `azure-anthropic` | Azure AI with Anthropic models | — |
| `openai` | OpenAI API (BYOK) | `OPENAI_API_KEY` |
| `anthropic` | Anthropic API (BYOK) | `ANTHROPIC_API_KEY` |

All providers are accessed through the GitHub Copilot SDK.

### `ai.generation_timeout_minutes`, `ai.generation_batch_size`

Per-batch tuning for `spectra ai generate`. These knobs are needed for slower
/ reasoning-class models (DeepSeek-V3, large Azure Anthropic deployments,
GPT-4 Turbo with long contexts) where the default 5-minute / 30-tests-per-batch
budget is too small.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `analysis_timeout_minutes` | int | `2` | Timeout for the **behavior analysis** SDK call (the analyze step that runs before generation). Slower models routinely overshoot 2 minutes when scanning a multi-document suite — bump to 5–10 minutes for those. The same timer applies to the retry attempt. |
| `generation_timeout_minutes` | int | `5` | Per-batch **generation** SDK call timeout. The timer measures the entire batch round-trip including all tool calls the AI makes. Slower models may need 10–20+ minutes. Minimum effective value is 1. |
| `generation_batch_size` | int | `30` | Number of tests requested per AI call. Smaller batches reduce per-batch latency on slow models at the cost of more total round-trips. Pair with `generation_timeout_minutes`. Values ≤ 0 fall back to the default. |

> **Spec 040 note:** the former `ai.debug_log_enabled` setting has moved to a
> new top-level [`debug`](#debug) section and defaults to **off**. See below.

**Critic timeout**: `ai.critic.timeout_seconds` (default 120, was 30 prior to v1.43.0) controls the per-test verification timeout. Pre-v1.43.0 the runtime ignored the config and used a hardcoded 2 minutes. v1.43.0 honors the field; the default was bumped to 120 to preserve existing behavior. Slow critic models (Claude Sonnet, GPT-4 Turbo) on long tests may need 180–300 seconds.

**Why a separate analysis timeout?** Behavior analysis runs once before generation and tends to be a single big call (no tool-calling loop). With slow / reasoning models on multi-doc suites it often takes 3–7 minutes — well over the 2-minute default — and fails silently. The symptom is `behaviors_found: 0` with a `recommended` count that looks plausible but is actually a hardcoded fallback default. v1.42.0+ surfaces this clearly: when analysis fails, the result file's `status` is set to `"analysis_failed"` (not `"analyzed"`) and the `message` field explains the cause and remediation. The bundled `spectra-generate` SKILL recognizes the new status and stops the agent from confidently presenting fallback numbers as a real recommendation.

#### `.spectra-debug.log` format

When `debug_log_enabled` is true, every batch writes one or more timestamped
lines to `.spectra-debug.log`. Example session:

```text
2026-04-11T01:30:00.012Z [generate] TESTIMIZE DISABLED (testimize.enabled=false in config)
2026-04-11T01:30:01.045Z [analyze ] ANALYSIS START documents=12 model=DeepSeek-V3.2 provider=azure-openai timeout=10min
2026-04-11T01:33:47.219Z [analyze ] ANALYSIS OK behaviors=75 response_chars=18402 elapsed=226.2s
2026-04-11T01:33:48.221Z [generate] BATCH START requested=8 model=DeepSeek-V3.2 provider=azure-openai timeout=20min
2026-04-11T01:38:25.778Z [generate] BATCH OK   requested=8 elapsed=277.6s
2026-04-11T01:38:26.014Z [critic ] CRITIC START test_id=TC-101 model=gpt-4.1-mini timeout=120s
2026-04-11T01:38:32.881Z [critic ] CRITIC OK    test_id=TC-101 verdict=Grounded score=0.95 elapsed=6.9s
2026-04-11T01:38:33.014Z [critic ] CRITIC START test_id=TC-102 model=gpt-4.1-mini timeout=120s
2026-04-11T01:38:41.220Z [critic ] CRITIC OK    test_id=TC-102 verdict=Partial score=0.62 elapsed=8.2s
2026-04-11T01:42:45.330Z [generate] BATCH TIMEOUT requested=8 model=DeepSeek-V3.2 configured_timeout=20min
```

**Component prefixes** (v1.43.0+):
- `[analyze ]` — behavior analysis (one START + OK/TIMEOUT/PARSE_FAIL/EMPTY/ERROR per run)
- `[generate]` — test generation (one BATCH START + BATCH OK/TIMEOUT per batch, plus TESTIMIZE lifecycle lines)
- `[critic ]` — grounding verification (one CRITIC START + OK/TIMEOUT/ERROR per test verified)

**Testimize lifecycle lines** (always emitted, even when disabled, so you can verify what actually ran):

| Line | Meaning |
|------|---------|
| `TESTIMIZE DISABLED (testimize.enabled=false in config)` | The optional integration is off. No MCP process spawned. |
| `TESTIMIZE START command=X args=[...]` | Attempting to start the MCP server. |
| `TESTIMIZE NOT_INSTALLED command=X` | The tool is not on PATH. SPECTRA falls back to AI-generated values. |
| `TESTIMIZE UNHEALTHY command=X` | The process started but the health probe failed. Fallback. |
| `TESTIMIZE HEALTHY command=X tools_added=2 strategy=X mode=Y` | Tools registered with the AI session. |
| `TESTIMIZE DISPOSED` | Child process killed at the end of the run. |

**Cost attribution**: every line is one billable API call to the corresponding
provider. To estimate cost for a run, count the lines:
- `ANALYSIS START` lines × analyzer model input cost
- `BATCH START` lines × generator model input cost
- `CRITIC START` lines × critic model input cost (typically dominant — one per generated test)
- `TESTIMIZE *` lines have zero AI cost (local MCP child process, not an API call)

Lines starting with `BATCH START` mark the beginning of an AI call (model,
provider, batch size, configured timeout). `BATCH OK` marks success and
records the elapsed wall time. `BATCH TIMEOUT` marks a timeout failure with
the configured budget for cross-reference. Use this to dial
`generation_timeout_minutes` and `generation_batch_size` precisely to your
model's actual throughput.

The file's behavior at run start is controlled by `debug.mode` (see the
[`debug` section](#debug) below): `"append"` (default) prepends a separator
and header before each new run; `"overwrite"` truncates the file at run
start so only the latest run is kept.

> **v1.45.2**: the legacy `.spectra-debug-analysis.txt` and
> `.spectra-debug-response.txt` files are no longer written. All debug output
> — including raw AI responses and testimize lifecycle — goes to
> `.spectra-debug.log` only.

#### Example: configuring for a slow / reasoning model

```json
{
  "ai": {
    "providers": [
      { "name": "azure-openai", "model": "DeepSeek-V3.2", "enabled": true }
    ],
    "analysis_timeout_minutes": 10,
    "generation_timeout_minutes": 20,
    "generation_batch_size": 8,
    "debug_log_enabled": true
  }
}
```

With this config, behavior analysis gets a 10-minute budget (DeepSeek typically
finishes a multi-doc scan in 3–7 minutes), and `spectra ai generate --count 100`
splits into 13 batches of 8 tests each, every batch with a 20-minute budget.
Every analyze and generate call's actual elapsed time is logged so you can
re-tune later.

#### Example: silencing the debug log

As of Spec 040 the debug log is **disabled by default** (no `.spectra-debug.log`
file is written). No action needed to silence it.

If you previously set `ai.debug_log_enabled: false` it can be removed; the field
is obsolete. If you previously set it to `true` to force logging, move that
preference to the new top-level `debug` section:

```json
{
  "debug": {
    "enabled": true
  }
}
```

See [the `debug` section](#debug) below for the full contract.

#### Timeout error message

When a batch hits the configured budget, the generation result includes a
multi-line error message that names the model, the batch size, the configured
timeout, and three remediation paths (bump the timeout, shrink the batch, or
reduce `--count`). The same message points at `.spectra-debug.log` for
per-batch timing context.

### `ai.critic`

Grounding verification configuration. See [Grounding Verification](grounding-verification.md).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | bool | `false` | Enable dual-model verification |
| `provider` | string | `github-models` | Critic provider — same set as the generator: `github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`. The legacy value `github` is still recognized as an alias for `github-models` (deprecation warning). |
| `model` | string | — | Critic model identifier |
| `api_key_env` | string | — | Environment variable for critic API key |
| `base_url` | string | — | Custom API endpoint |
| `timeout_seconds` | int | `30` | Critic request timeout |
| `max_concurrent` | int | `1` | **Spec 043:** number of concurrent critic verification calls. `1` (default) preserves the original sequential behavior. Higher values run multiple critic calls in parallel via a `SemaphoreSlim` throttle and dramatically reduce critic phase time on large suites. Clamped to `[1, 20]`. Values >10 emit a rate-limit-risk warning at run start. |

#### Critic concurrency tuning (Spec 043)

The critic phase is typically the dominant cost on large generate runs (one
sequential API call per test). Setting `max_concurrent` higher parallelizes
those calls without changing any output. Test files, verdicts, and indexes
are written in the original input order regardless of completion order.

| `max_concurrent` | 200 tests × ~6s | Approx. critic phase |
|------------------|-----------------|----------------------|
| `1` (default)    | sequential      | ~20 min              |
| `3`              | 3 parallel      | ~7 min               |
| `5`              | 5 parallel      | ~4 min               |
| `10`             | 10 parallel     | ~2 min               |

If you start hitting provider rate limits, the Run Summary panel surfaces a
`Rate limits` count with a hint pointing back at this knob. Drop the value
and re-run.

#### Example: Azure-only billing (generator + critic on the same account)

```json
{
  "ai": {
    "providers": [
      { "name": "azure-openai", "model": "gpt-4.1-mini", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "provider": "azure-openai",
      "model": "gpt-4o"
    }
  }
}
```

#### Example: Cross-provider critic for independent verification

```json
{
  "ai": {
    "providers": [
      { "name": "azure-openai", "model": "gpt-4.1-mini", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "provider": "anthropic",
      "model": "claude-3-5-haiku-latest",
      "api_key_env": "ANTHROPIC_API_KEY"
    }
  }
}
```

## `generation` — Test Generation Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `default_count` | int | `15` | Default number of tests to generate |
| `require_review` | bool | `true` | Require interactive review before writing |
| `duplicate_threshold` | double | `0.6` | Similarity threshold for duplicate detection |
| `categories` | string[] | `["happy_path", "negative", "boundary", "integration"]` | Test categories to generate |

## `update` — Test Update Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `chunk_size` | int | `30` | Number of tests to process per AI call |
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
| `component` | string | — | Default component for tests in this suite |
| `relevant_docs` | string[] | `[]` | Documentation files relevant to this suite |
| `default_tags` | string[] | `[]` | Default tags for generated tests |
| `default_priority` | string | `"medium"` | Default priority for generated tests |

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
| `report_orphans` | bool | `true` | Report automation files not linked to any test |
| `report_broken_links` | bool | `true` | Report tests referencing non-existent files |
| `report_mismatches` | bool | `true` | Report inconsistencies between index and files |

## `selections` — Saved Test Selections

Named test selections for quick execution. Each selection defines filters that are evaluated across all suites at runtime.

```json
{
  "selections": {
    "smoke": {
      "description": "Quick smoke test — high priority tests only",
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
| `has_automation` | bool | `true` = only automated tests, `false` = only manual tests |

Filters are combined with AND logic between types. Use the `list_saved_selections` MCP tool to preview how many tests each selection matches, or start a run with `start_execution_run` using the `selection` parameter.

The default configuration includes a `smoke` selection that matches all high-priority tests.

---

## `execution` — Execution Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `copilot_space` | string | — | Copilot Space name for inline documentation lookup during execution |
| `copilot_space_owner` | string | — | GitHub user or organization that owns the Copilot Space |

When configured, the execution agent uses the specified Copilot Space to answer tester questions about test steps and expected behavior. See [Copilot Spaces Setup](copilot-spaces-setup.md).

## `validation` — Test Validation Rules

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `required_fields` | string[] | `["id", "priority"]` | Fields required in test frontmatter |
| `allowed_priorities` | string[] | `["high", "medium", "low"]` | Valid priority values |
| `max_steps` | int | `20` | Maximum test steps allowed |
| `id_pattern` | string | `"^TC-\\d{3,}$"` | Regex pattern for valid test IDs |

## `git` — Git Operations

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `auto_branch` | bool | `true` | Create feature branches for generated tests |
| `branch_prefix` | string | `"spectra/"` | Branch name prefix |
| `auto_commit` | bool | `true` | Automatically commit generated tests |
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

## `debug` — Debug Logging (Spec 040)

Controls the append-only `.spectra-debug.log` diagnostic file. **Disabled by
default.** When off, no file is created and no disk I/O occurs for debug
logging, eliminating stale-file accumulation in CI environments.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | bool | `false` | When true, every AI call (analyze, generate, critic, criteria extraction) writes a timestamped line to the debug log. Non-AI lifecycle lines (testimize) are also written. Best-effort; never blocks the calling code. |
| `log_file` | string | `".spectra-debug.log"` | Path to the log file. Relative paths resolve against the repo root. |
| `error_log_file` | string | `".spectra-errors.log"` | **Spec 043:** path to the dedicated error log. Written only when `enabled` is `true` AND at least one error occurs during the run. On a clean run the file is not created or modified. Captures full exception type, message, response body (truncated to 500 chars), `Retry-After` header, and stack trace per failure. Follows the same `mode` semantics as `log_file`. |
| `mode` | string | `"append"` | Controls how the file is opened at run start. `"append"` prepends a separator + header block before each new run (useful for comparing multiple runs). `"overwrite"` truncates the file and writes just the header (keeps only the latest run). Any other value falls back to `"append"`. |

### Error log (Spec 043)

The error log is the companion to the debug log. Where the debug log is
high-volume (one line per AI call, hundreds per run), the error log is
zero-volume on a healthy run and only fills up when something actually
fails. A single `cat .spectra-errors.log` answers "did anything go wrong?"
without grepping through hundreds of OK lines.

Each entry includes:

```text
2026-04-11T19:05:12.345Z [critic ] ERROR test_id=TC-150
  Type: System.Net.Http.HttpRequestException
  Message: Response status code does not indicate success: 429 (Too Many Requests).
  Response: {"error":{"code":"rate_limit_exceeded","message":"Rate limit reached for gpt-4.1"}}
  Retry-After: 2
  Stack: at Spectra.CLI.Agent.Copilot.CopilotCritic.VerifyTestAsync(...)
```

When an error fires, the corresponding debug-log line gains a
`see=<error_log_file>` suffix so you can jump from the timeline view to
the full context with one search.

Errors are captured from every phase that talks to the AI runtime:
analyze, generate, critic, criteria extraction, and update.

### Run header (v1.45.2)

At the start of every `generate` / `update` run, `spectra` writes a header
identifying the version, command line, and UTC timestamp:

```text
──────────────────────────────────────────────────────────────
=== SPECTRA v1.45.2 | spectra ai generate checkout --count 20 | 2026-04-11T14:30:00Z ===
```

In `"append"` mode, the separator line is prepended before each new run so
multiple runs are visually distinct. In `"overwrite"` mode, the separator
still appears but it's written fresh (file was just truncated).

### Testimize lifecycle lines (always written)

Testimize `DISABLED` / `START` / `NOT_INSTALLED` / `UNHEALTHY` / `HEALTHY` /
`DISPOSED` lines are written to `.spectra-debug.log` whenever debug logging
is enabled. They appear after the header, mixed in with the AI call lines
in timestamp order, and always survive a mode=overwrite truncate (since
truncate happens at run start, before any testimize activity).

### `--verbosity diagnostic` one-shot override

You can force-enable debug logging for a single run without editing config:

```bash
spectra ai generate checkout --count 20 --verbosity diagnostic
```

The override takes effect for that invocation only and does not touch config.

### AI line format (Spec 040)

Every AI-call line ends with `model=<name> provider=<name> tokens_in=<n> tokens_out=<n>`:

```text
2026-04-11T12:34:56.789Z [analyze ] ANALYSIS OK behaviors=42 elapsed=18.5s model=gpt-4.1 provider=github-models tokens_in=? tokens_out=?
2026-04-11T12:35:09.123Z [generate] BATCH OK   requested=8 elapsed=12.3s model=gpt-4.1 provider=github-models tokens_in=? tokens_out=?
2026-04-11T12:35:11.456Z [critic  ] CRITIC OK  test_id=TC-201 verdict=grounded elapsed=2.1s model=gpt-4o-mini provider=github-models tokens_in=? tokens_out=?
```

When the provider response does not include a `usage` block, `tokens_in` and
`tokens_out` are written as the literal `?` so the line stays grep-able. When
the SDK surfaces token counts (future runtime versions), numeric values appear
instead.

### Cost and token summary

After every `spectra ai generate` and `spectra ai update` run the terminal
prints a Run Summary panel containing per-phase call counts, pure-AI elapsed
time, and an estimated USD cost (BYOK providers) or *Included in Copilot plan*
(for `github-models`). The same data is written to `.spectra-result.json`
under `run_summary` and `token_usage` so SKILL / CI consumers can read it.
