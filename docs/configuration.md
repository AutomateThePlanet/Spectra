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
        "model": "gpt-4o",
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
      "model": "gpt-4o-mini",
      "api_key_env": null,
      "base_url": null,
      "timeout_seconds": 30
    }
  }
}
```

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

### `ai.generation_timeout_minutes`, `ai.generation_batch_size`, `ai.debug_log_enabled`

Per-batch tuning for `spectra ai generate` and an append-only diagnostic log.
These knobs are needed for slower / reasoning-class models (DeepSeek-V3, large
Azure Anthropic deployments, GPT-4 Turbo with long contexts) where the default
5-minute / 30-tests-per-batch budget is too small.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `generation_timeout_minutes` | int | `5` | Per-batch SDK call timeout. The timer measures the entire batch round-trip including all tool calls the AI makes. Slower models may need 10–20+ minutes. Minimum effective value is 1. |
| `generation_batch_size` | int | `30` | Number of tests requested per AI call. Smaller batches reduce per-batch latency on slow models at the cost of more total round-trips. Pair with `generation_timeout_minutes`. Values ≤ 0 fall back to the default. |
| `debug_log_enabled` | bool | `true` | When true, appends per-batch diagnostics to `.spectra-debug.log` in the project root. Best-effort writes; never blocks generation. Set to `false` to silence. |

#### `.spectra-debug.log` format

When `debug_log_enabled` is true, every batch writes one or more timestamped
lines to `.spectra-debug.log`. Example session:

```text
2026-04-11T01:30:14.221Z [generate] BATCH START requested=8 model=DeepSeek-V3.2 provider=azure-openai timeout=20min
2026-04-11T01:34:51.778Z [generate] BATCH OK   requested=8 elapsed=277.6s
2026-04-11T01:34:53.014Z [generate] BATCH START requested=8 model=DeepSeek-V3.2 provider=azure-openai timeout=20min
2026-04-11T01:39:10.004Z [generate] BATCH OK   requested=8 elapsed=257.0s
2026-04-11T01:39:11.221Z [generate] BATCH START requested=8 model=DeepSeek-V3.2 provider=azure-openai timeout=20min
2026-04-11T01:59:11.330Z [generate] BATCH TIMEOUT requested=8 model=DeepSeek-V3.2 configured_timeout=20min
```

Lines starting with `BATCH START` mark the beginning of an AI call (model,
provider, batch size, configured timeout). `BATCH OK` marks success and
records the elapsed wall time. `BATCH TIMEOUT` marks a timeout failure with
the configured budget for cross-reference. Use this to dial
`generation_timeout_minutes` and `generation_batch_size` precisely to your
model's actual throughput.

The file is **append-only** — it grows over time. Delete it when stale.

In addition, the existing `.spectra-debug-response.txt` file (always written,
not gated by this flag) contains the raw text of the most recent AI response
for the current batch. Useful when the AI returns a malformed JSON array.

#### Example: configuring for a slow / reasoning model

```json
{
  "ai": {
    "providers": [
      { "name": "azure-openai", "model": "DeepSeek-V3.2", "enabled": true }
    ],
    "generation_timeout_minutes": 20,
    "generation_batch_size": 8,
    "debug_log_enabled": true
  }
}
```

With this config, `spectra ai generate --count 100` splits into 13 batches
of 8 tests each, every batch has a 20-minute budget, and each batch's actual
elapsed time is logged so you can re-tune later.

#### Example: silencing the debug log on a fast model

```json
{
  "ai": {
    "providers": [{ "name": "github-models", "model": "gpt-4o", "enabled": true }],
    "debug_log_enabled": false
  }
}
```

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
