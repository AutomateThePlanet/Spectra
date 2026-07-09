---
title: Customization
parent: User Guide
nav_order: 10
---

# SPECTRA Customization Guide

This guide covers every way you can customize SPECTRA's behavior, from test case
output format to AI reasoning strategy.

> **Looking for workflow how-tos instead of customization?** See [`USAGE.md`](USAGE.md) — the workflow guide for using SPECTRA from Claude Code.

---

## Quick Reference

| What to customize | File(s) | How |
|---|---|---|
| Test case format (fields, structure) | `profiles/_default.yaml` | Edit the `format` field directly |
| AI reasoning (analysis, generation, verification) | `.spectra/prompts/*.md` | Edit markdown prompt templates |
| Behavior categories | `spectra.config.json` → `analysis.categories` | Add/remove categories with descriptions |
| Coverage settings | `spectra.config.json` → `coverage` | Configure dirs, patterns, criteria paths |
| Generation pacing (batch size, timeouts) | `spectra.config.json` → `ai` | `generation_batch_size`, `generation_timeout_minutes`, `analysis_timeout_minutes` |
| Session model used for generation/analysis | Your Claude Code session | Pick the model for your session (`/model`) — generation runs as a turn in it, there's no separate config |
| Critic model | `.claude/agents/spectra-critic.agent.md` → `model:` frontmatter | Edit the subagent file directly (re-apply after `spectra update-skills` if customized) |
| Dashboard branding | `spectra.config.json` → `dashboard.branding` | Logo, colors, company name, theme |
| Claude Code integration | `.claude/skills/<name>/SKILL.md`, `.claude/agents/spectra-critic.agent.md` | Edit authoring SKILL files / critic subagent |

---

## 1. Test Case Format — `profiles/_default.yaml`

**What it controls**: The JSON schema the AI must follow when producing test
cases. This determines what fields appear in YAML frontmatter and how test case
steps/expected results are structured.

**File**: `profiles/_default.yaml`

**How to customize**:

1. Open `profiles/_default.yaml`.
2. Edit the `format` field — this is the JSON template the AI receives.
3. Re-run `spectra ai generate` — new test cases follow the new schema.

**Common modifications**:

- Add custom frontmatter fields (e.g., `regulatory_reference`, `test_data_class`)
- Change step format (numbered vs. action/expected pairs)
- Add environment or browser fields
- Enforce specific tag taxonomies

**Example — adding a `test_data` field**:

```yaml
format: |
  [
    {
      "id": "TC-XXX",
      "title": "...",
      "priority": "high|medium|low",
      "tags": ["tag1"],
      "component": "component-name",
      "test_data": {
        "users": ["admin@test.com"],
        "environment": "staging"
      },
      "steps": ["Step 1: ..."],
      "expected_result": "...",
      "criteria": ["AC-XXX-001"]
    }
  ]
```

**Resolution order**: `profiles/_default.yaml` on disk > built-in embedded
default. Missing or malformed files fall back gracefully to the embedded
default — generation never crashes due to a profile error.

---

## 2. AI Reasoning — Root Prompt Templates

**What it controls**: How the AI thinks — analysis strategy, test case writing
approach, verification rigor, criteria extraction depth.

**Files**: `.spectra/prompts/*.md`

| Template | Controls | Used by |
|----------|----------|---------|
| `behavior-analysis.md` | How docs are read, what counts as "testable" | `spectra ai generate` (analysis phase) |
| `test-generation.md` | How test cases are written from analyzed behaviors | `spectra ai generate` (generation phase) |
| `criteria-extraction.md` | How acceptance criteria are pulled from docs | `spectra ai analyze --extract-criteria` |
| `critic-verification.md` | How generated test cases are verified against sources | Grounding verification |
| `test-update.md` | How existing test cases are classified and rewritten | `spectra ai update` |

**How to customize**: Edit any `.md` file directly. The prompt body uses
`{{placeholders}}` for dynamic content injected at runtime. Available
placeholders are listed in each file's YAML frontmatter.

**Example — adding domain focus to behavior analysis**:

Open `.spectra/prompts/behavior-analysis.md` and add after the persona section:

```
## Domain Focus
When analyzing documentation, pay special attention to:
- PCI-DSS compliance implications in payment flows
- GDPR data handling in user registration
- Idempotency requirements on all write operations
- Rate limiting and abuse prevention on public APIs
```

**Coverage-aware analysis**: The `behavior-analysis.md` template includes a
`{{coverage_context}}` placeholder (spec 044). When an existing suite has
coverage data, this resolves to a markdown block listing covered/uncovered
criteria and doc sections, directing the AI to focus on gaps. If your custom
template omits this placeholder, coverage context is simply not injected.

**Safe updates**: `spectra update-skills` refreshes unmodified templates to
the latest version. Modified templates are preserved. Use
`spectra prompts reset {template}` to restore a template to default.

---

## 3. Behavior Categories

**What it controls**: The classification taxonomy for analyzed behaviors.
Categories appear in test case frontmatter as tags and drive coverage distribution.

**File**: `spectra.config.json` → `analysis.categories`

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

**Domain-specific examples**:

```json
// Fintech
{ "name": "compliance", "description": "PCI-DSS, AML, KYC regulatory requirements" }
{ "name": "reconciliation", "description": "Balance matching, settlement, ledger consistency" }
{ "name": "idempotency", "description": "Duplicate request handling, retry safety" }

// Healthcare
{ "name": "hipaa", "description": "PHI access controls, audit logging, encryption" }

// E-commerce
{ "name": "inventory", "description": "Stock tracking, oversell prevention, reservation" }
```

The `description` field is injected into the behavior analysis prompt to tell
the AI what to look for. Specific descriptions produce targeted behaviors.

---

## 4. Coverage Settings

**What it controls**: Which directories are scanned, what file patterns
match, and where acceptance criteria live.

**File**: `spectra.config.json` → `coverage`

```json
{
  "coverage": {
    "criteria_file": "docs/criteria/_criteria_index.yaml",
    "criteria_dir": "docs/criteria",
    "automation_dirs": ["tests/", "e2e/"],
    "scan_patterns": ["*.cs", "*.ts", "*.py"],
    "file_extensions": [".cs", ".ts", ".py"]
  }
}
```

**Key settings**:

- `automation_dirs` — directories scanned for `automated_by` links. Add more
  with `spectra config add-automation-dir ../integration-tests`.
- `scan_patterns` — glob patterns for automation code files.
- `criteria_dir` — where per-document `.criteria.yaml` files are stored.
- `criteria_file` — path to the master criteria index.
- `requirements_file` — path to the requirements YAML index (default:
  `"docs/requirements/_requirements.yaml"`). Used by requirements coverage
  analysis.
- `report_orphans` / `report_broken_links` / `report_mismatches` — boolean
  flags (all default `true`) controlling which coverage report sections are
  generated. Set to `false` to suppress noisy sections in large repos.

**Criteria import tuning** (`coverage.criteria_import`):

```json
{
  "coverage": {
    "criteria_import": {
      "default_source_type": "manual",
      "auto_split": true,
      "normalize_rfc2119": true,
      "id_prefix": "AC"
    }
  }
}
```

- `auto_split` — when importing compound acceptance criteria from CSV/YAML,
  automatically split them into atomic criteria.
- `normalize_rfc2119` — normalize RFC 2119 keywords (MUST, SHALL, SHOULD) to
  a consistent form during import.
- `id_prefix` — prefix for auto-generated acceptance criteria IDs (default: `"AC"`).

---

## 5. AI Model & Batch Tuning

**What it controls**: How generation/analysis turns are shaped and paced, and which model
verifies them.

**File**: `spectra.config.json` → `ai`

> **Spec 069 (v2):** there is no provider or model-routing config here anymore. The GitHub Copilot
> SDK, `ai.providers`, and `ai.critic` (as a config block) were removed outright — SPECTRA makes no
> model calls of its own. Generation and analysis run as ordinary turns in your own Claude Code
> session; the model is whatever your session is using (`/model`), not something
> `spectra.config.json` selects. See
> [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) and the
> [AI Models & Cost Guide](ai-models-cost-guide.md) for the full picture. A pre-v2 config with
> `ai.providers`/`ai.critic` still loads — the keys are simply ignored.

```json
{
  "ai": {
    "generation_batch_size": 30,
    "generation_timeout_minutes": 5,
    "analysis_timeout_minutes": 2
  }
}
```

- `generation_batch_size` — tests requested per generation turn (default 30). Fewer, larger
  batches mean fewer `compile-prompt`/`ingest-tests` round-trips.
- `generation_timeout_minutes` / `analysis_timeout_minutes` — bound the deterministic CLI side of
  the seam (parsing, validation, index writes), not the model turn itself.

**Critic model**: the `spectra-critic` subagent's model is a static `model:` field in
`.claude/agents/spectra-critic.agent.md` (shipped as `claude-sonnet-4-6`), not a
`spectra.config.json` setting. Edit that file directly to change it, and re-apply your edit after
`spectra update-skills` if you've customized it.

---

## 6. Dashboard Branding

**What it controls**: Visual appearance of the generated coverage dashboard.

**File**: `spectra.config.json` → `dashboard.branding`

```json
{
  "dashboard": {
    "branding": {
      "company_name": "Acme Corp",
      "logo": "assets/logo.svg",
      "favicon": "assets/favicon.ico",
      "theme": "dark",
      "colors": {
        "primary": "#2E75B6",
        "success": "#27AE60",
        "warning": "#F39C12",
        "danger": "#E74C3C"
      },
      "custom_css": "assets/dashboard-overrides.css"
    }
  }
}
```

**Preview**: `spectra dashboard --preview` shows the dashboard with sample
data and your branding applied, without needing real test data.

---

## 7. Claude Code Integration

**What it controls**: How Claude Code interacts with SPECTRA CLI.

**Files**:
- `.claude/skills/<name>/SKILL.md` — authoring SKILL files (one per command group)
- `.claude/agents/spectra-critic.agent.md` — critic subagent (`context: fork`), run as a mandatory step by the generation SKILL
- `.vscode/mcp.json` — MCP server connection for the execution agent

**SKILLs** (bundled, authoring set): `spectra-generate`, `spectra-update`, `spectra-coverage`,
`spectra-dashboard`, `spectra-validate`, `spectra-list`, `spectra-init-profile`,
`spectra-help`, `spectra-criteria`, `spectra-docs`, `spectra-prompts`, `spectra-quickstart`.

**Critic subagent** (bundled): `spectra-critic` — independent verification of generated tests.

> The test **execution** agent is now a Claude Code main-session skill at `.claude/skills/spectra-execution/SKILL.md` (ported from its legacy GitHub Copilot agent).

**How to customize**: Edit any `SKILL.md` or the critic subagent file to change how Claude Code
invokes CLI commands, presents results, or handles multi-step workflows.

**Safe updates**: Same hash-tracking as prompt templates. `spectra update-skills`
refreshes unmodified files, preserves your edits.

---

## Customization Precedence Summary

```
profiles/_default.yaml (on disk)
  ↓ fallback (missing or malformed)
Built-in embedded default

.spectra/prompts/{template}.md (on disk)
  ↓ fallback
Built-in embedded prompt template

spectra.config.json (user values)
  ↓ fallback
Built-in defaults (categories, coverage paths, etc.)
```

All user files are created by `spectra init` and tracked by
`spectra update-skills`. You can always restore prompt-template defaults with
`spectra prompts reset --all`.

---

## Common Workflows

### "I want better test case quality for my domain"

1. Edit `.spectra/prompts/behavior-analysis.md` — add domain-specific focus.
2. Add domain categories to `spectra.config.json` → `analysis.categories`.
3. Run `spectra ai generate --suite {name} --count 5` and evaluate.

### "I want to change the test case output format"

1. Edit `profiles/_default.yaml` → `format` field directly.
2. Re-run generation. New test cases follow the new schema.

### "I want stricter verification"

1. Edit `.spectra/prompts/critic-verification.md` — tighten verdict rules.
2. Consider using a stronger model for critic in config → `ai.critic`.

### "I want the AI to focus on specific areas"

Use `--focus` at runtime:
`spectra ai generate --suite checkout --focus "payment validation, error handling"`

Or permanently: edit the behavior analysis prompt template to always
prioritize those areas.
