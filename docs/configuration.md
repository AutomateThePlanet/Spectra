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

### `ai.critic`

Grounding verification configuration. See [Grounding Verification](grounding-verification.md).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | bool | `false` | Enable dual-model verification |
| `provider` | string | — | Critic provider (`google`, `openai`, `anthropic`, `github`) |
| `model` | string | — | Critic model identifier |
| `api_key_env` | string | — | Environment variable for critic API key |
| `base_url` | string | — | Custom API endpoint |
| `timeout_seconds` | int | `30` | Critic request timeout |

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
