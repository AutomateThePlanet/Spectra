# SPECTRA Project Knowledge

## What is SPECTRA?

SPECTRA is an AI-native test case generation and execution framework. It reads product documentation, extracts testable behaviors, generates structured manual test cases (as Markdown files with YAML frontmatter), executes them through an MCP server with AI agent orchestration, and produces coverage reports and dashboards.

## Architecture Overview

```
User → CLI (spectra) → AI Provider (Copilot SDK) → Test Files (Markdown/YAML)
User → VS Code Copilot Chat → SKILL/Agent → CLI commands → Results
User → VS Code Copilot Chat → MCP Server → Test Execution → Reports
```

### Three Main Components

| Component | Purpose | Package |
|-----------|---------|---------|
| **Spectra.CLI** | CLI tool for generation, coverage, dashboard, validation | `Spectra.CLI` NuGet (global tool: `spectra`) |
| **Spectra.MCP** | MCP execution server for AI-driven test execution | `Spectra.MCP` NuGet (global tool: `spectra-mcp`) |
| **Spectra.Core** | Shared models, parsers, coverage analyzers | Referenced by CLI and MCP |

### Supporting Components

| Component | Purpose |
|-----------|---------|
| **dashboard-site/** | Static HTML/JS dashboard template with D3.js visualizations |
| **SKILLs** (12 files) | VS Code Copilot Chat integration — each SKILL wraps CLI commands or guides users |
| **Agents** (2 files) | Copilot Chat agent prompts for generation and execution workflows |

## Technology Stack

- **Language**: C# 12, .NET 8+
- **AI Runtime**: GitHub Copilot SDK (sole runtime — supports github-models, azure-openai, azure-anthropic, openai, anthropic providers)
- **CLI Framework**: System.CommandLine
- **Terminal UX**: Spectre.Console
- **Serialization**: System.Text.Json (JSON), YamlDotNet (YAML)
- **CSV Parsing**: CsvHelper
- **MCP Server**: ASP.NET Core
- **State Storage**: Microsoft.Data.Sqlite (execution state in `.execution/spectra.db`)
- **Testing**: xUnit
- **Dashboard**: Vanilla JS + D3.js (treemap/donut charts)

## Project Structure

```
src/
├── Spectra.CLI/
│   ├── Commands/           # CLI command handlers
│   │   ├── Analyze/        # Coverage analysis + criteria extraction/import/list
│   │   ├── Dashboard/      # Dashboard generation
│   │   ├── Docs/           # Documentation index management
│   │   ├── Generate/       # Test generation (direct + interactive modes)
│   │   ├── Init/           # Project initialization
│   │   └── Update/         # Test update with classification
│   ├── Agent/Copilot/      # AI provider integration
│   │   ├── CopilotGenerationAgent  # Test generation via Copilot SDK
│   │   ├── CopilotCritic          # Grounding verification
│   │   ├── CriteriaExtractor      # Per-document criteria extraction
│   │   └── BehaviorAnalyzer       # Documentation behavior analysis
│   ├── Skills/Content/     # Bundled SKILL and agent markdown files
│   ├── Progress/           # Live progress HTML page writer
│   ├── Results/            # Typed JSON result models per command
│   ├── Coverage/           # Coverage report writer
│   ├── Dashboard/          # Dashboard data collection + generation
│   └── IO/                 # File writers for test cases
├── Spectra.Core/
│   ├── Models/             # All data models
│   │   ├── Coverage/       # AcceptanceCriterion, CriteriaIndex, UnifiedCoverageReport
│   │   ├── Dashboard/      # DashboardData, CoverageSummaryData
│   │   ├── Execution/      # Run, TestResult, McpToolResponse
│   │   └── Config/         # SpectraConfig, CoverageConfig
│   ├── Parsing/            # YAML/Markdown parsers, criteria readers/writers, importers
│   ├── Coverage/           # Coverage analyzers (documentation, criteria, automation)
│   └── Update/             # TestClassifier for test updates
├── Spectra.MCP/
│   ├── Tools/              # MCP tool implementations (20+ tools)
│   ├── Execution/          # ExecutionEngine, TestQueue, StateMachine
│   ├── Reports/            # HTML/Markdown/JSON report generation
│   └── Storage/            # SQLite repositories
└── Spectra.GitHub/         # GitHub integration (future)

dashboard-site/             # Static dashboard template
tests/                      # xUnit test projects (1280+ tests)
```

## CLI Commands

### Test Generation
```bash
spectra ai generate --suite {name}              # Interactive session
spectra ai generate --suite {name} --count 15   # Direct mode with count
spectra ai generate --suite {name} --focus "negative, high priority"  # Filtered
spectra ai generate --suite {name} --analyze-only  # Analysis only
spectra ai generate --suite {name} --skip-critic   # Skip verification
spectra ai generate --suite {name} --no-interaction --output-format json  # CI mode
```

### Acceptance Criteria
```bash
spectra ai analyze --extract-criteria            # Incremental extraction from docs
spectra ai analyze --extract-criteria --force    # Full re-extraction
spectra ai analyze --import-criteria ./file.csv  # Import from CSV/YAML/JSON
spectra ai analyze --list-criteria               # List all criteria
spectra ai analyze --list-criteria --component checkout --priority high  # Filtered
```

### Coverage & Dashboard
```bash
spectra ai analyze --coverage --auto-link        # Unified coverage report
spectra dashboard --output ./site                # Generate dashboard
```

### Other Commands
```bash
spectra ai update --suite {name}                 # Update tests (classify changes)
spectra validate                                 # Validate test files
spectra docs index                               # Rebuild documentation index
spectra list / spectra show {id}                 # Browse tests
spectra init                                     # Initialize project
spectra update-skills                            # Sync bundled SKILL files
```

## Test Case Format

Test cases are Markdown files with YAML frontmatter in `test-cases/{suite}/`:

```yaml
---
id: TC-101
title: "Verify login with valid credentials"
priority: high
tags: [authentication, login]
component: auth
criteria: [AC-AUTH-001, AC-AUTH-002]     # Linked acceptance criteria
automated_by: [LoginTests.cs]
requirements: [REQ-001]                  # Legacy field (deprecated)
grounding:
  verdict: grounded
  source: docs/auth.md
---

## Steps
1. Navigate to login page
2. Enter valid email and password
3. Click Sign In

## Expected Result
User is redirected to dashboard with welcome message
```

## Acceptance Criteria System

### Architecture
```
docs/
├── feature1.md                          # Source documentation
├── feature2.md
└── criteria/
    ├── _criteria_index.yaml             # Master index (auto-generated)
    ├── feature1.criteria.yaml           # Per-document criteria
    ├── feature2.criteria.yaml
    └── imported/
        └── jira-sprint-42.criteria.yaml # Imported from external sources
```

### Criterion Format
```yaml
criteria:
  - id: AC-CHECKOUT-001
    text: "System MUST validate IBAN format before submitting payment"
    rfc2119: MUST
    source_doc: docs/checkout.md
    source_section: "Payment Validation"
    component: checkout
    priority: high
    tags: [payment, validation]
```

### Key Features
- **Per-document extraction**: Each doc processed independently (no truncation)
- **SHA-256 incremental**: Only re-extracts changed documents
- **Import**: CSV (Jira/ADO auto-column-detection), YAML, JSON
- **AI splitting**: Compound criteria split into individual entries
- **RFC 2119 normalization**: Informal language → MUST/SHOULD/MAY
- **Merge/Replace**: `--merge` (default) matches by ID/source, `--replace` overwrites target file only

## VS Code Copilot Chat Integration

### 12 Bundled SKILLs
| SKILL | Purpose |
|-------|---------|
| `spectra-generate` | Test generation (analyze → approve → generate flow) |
| `spectra-update` | Test updates (classify → review → apply changes) |
| `spectra-coverage` | Coverage analysis |
| `spectra-dashboard` | Dashboard generation and opening |
| `spectra-validate` | Test validation |
| `spectra-list` | Test listing and browsing |
| `spectra-init-profile` | Generation profile setup |
| `spectra-help` | Command reference (terse, flag-oriented) |
| `spectra-criteria` | Criteria extraction, import, listing |
| `spectra-docs` | Documentation indexing with progress page |
| `spectra-prompts` | Prompt template management (list/show/reset/validate) |
| `spectra-quickstart` | Workflow-oriented onboarding & walkthroughs (12 workflows with example conversations) |

### Bundled Project-Root Docs
- `CUSTOMIZATION.md` — configuration & customization reference (profiles, prompts, branding).
- `USAGE.md` — workflow guide for Copilot Chat usage (offline mirror of `spectra-quickstart`).

Both are written by `spectra init` and tracked by the SKILL manifest hash system so `spectra update-skills` refreshes unmodified copies and preserves user edits.

### 2 Agent Prompts (Delegation Model)
| Agent | Purpose |
|-------|---------|
| `spectra-generation` (~81 lines) | Primary: test generation and update. Delegates all other CLI tasks (update, dashboard, coverage, criteria, validate, list, docs index) to corresponding SKILLs via delegation table. |
| `spectra-execution` (~120 lines) | Test execution via MCP tools. Delegates all CLI tasks (update, dashboard, coverage, criteria, validate, list, docs index) to corresponding SKILLs via delegation table. |

Agents are **routers**, not executors. Each CLI command has exactly one source of truth — its SKILL file. Agents reference SKILLs by name (e.g., "Follow the `spectra-dashboard` SKILL") instead of duplicating CLI instructions.

### SKILL Format Conventions
- **Name**: lowercase-hyphenated (e.g., `spectra-generate`)
- **Tools**: Include `browser/openBrowserPage` for preview capability
- **CLI flags**: Always include `--no-interaction --output-format json --verbosity quiet`
- **Result reading**: Use `readFile .spectra-result.json` (not `terminalLastCommand`)
- **Progress**: Long-running commands use `.spectra-progress.html` for live status
- **Steps**: Use `**Step N**` format (not `### Tool call N:`)
- **Wait instruction**: Include "Between runInTerminal and awaitTerminal, do NOTHING" for long-running commands
- **Preview**: Use `show preview {file}` to open files in VS Code (not `start`)

### Generation Flow (SKILL/Agent)
1. `show preview .spectra-progress.html`
2. `runInTerminal`: `spectra ai generate --suite {suite} --analyze-only --no-interaction --output-format json`
3. `awaitTerminal` (wait for completion)
4. `readFile .spectra-result.json` → show breakdown, ask to proceed
5. `runInTerminal`: `spectra ai generate --suite {suite} --count {n} --no-interaction --output-format json`
6. `awaitTerminal`
7. `readFile .spectra-result.json` → show results

## MCP Execution Server

### Key MCP Tools
- `start_execution_run`, `get_execution_status`, `pause/resume/cancel/finalize_execution_run`
- `get_test_case_details`, `advance_test_case`, `skip_test_case`, `bulk_record_results`
- `save_screenshot`, `save_clipboard_screenshot`, `add_test_note`
- `list_active_runs`, `cancel_all_active_runs`
- `find_test_cases`, `get_test_execution_history`, `list_saved_selections`

### Execution Flow
1. `list_active_runs` → check for active runs
2. `start_execution_run` → begin with suite/selection/test_ids
3. `get_test_case_details` → present test to user (MUST show full details)
4. `advance_test_case` → record PASSED/FAILED/BLOCKED
5. `finalize_execution_run` → generate reports, instruction to open HTML report

### Reports
- Generated in `.execution/reports/` as JSON, Markdown, and HTML
- HTML report has professional styling (navy theme, pass rate circle, expandable test cards)
- `finalize_execution_run` returns instruction: "show preview .execution/reports/{filename}"

## Progress Page System

The CLI generates a self-contained `.spectra-progress.html` during test generation:

- **Auto-refresh**: `<meta http-equiv="refresh" content="2">` (removed on completion)
- **Embedded JSON**: Status data is inline in HTML (no fetch needed)
- **Phase stepper**: Analyzing → Analyzed → Generating → Completed
- **Live status**: Spinner, current message, timestamp
- **Summary cards**: Behaviors found, already covered, recommended, tests written
- **File links**: Generated test files as `vscode://file/` clickable links
- **Reset**: Both `.spectra-result.json` and `.spectra-progress.html` are deleted at command start

## Coverage System

Three-section unified coverage with distinct semantics:

1. **Documentation Coverage**: Which docs have test cases (via `source_refs` or suite name match). "Covered" = at least 1 test references the document. Automation status is irrelevant.
2. **Acceptance Criteria Coverage**: Which criteria are tested (via `criteria: []` or legacy `requirements: []` in test frontmatter, with per-source-type breakdown). Generation pipeline loads criteria and passes to AI prompt so generated tests include `criteria: [AC-XXX]`.
3. **Automation Coverage**: Which tests have `automated_by: []` links resolving to existing files in `automation_dirs`.

## Config Schema (`spectra.config.json`)

```json
{
  "source": { "mode": "local", "local_dir": "docs/" },
  "tests": { "dir": "tests/", "id_prefix": "TC", "id_start": 100 },
  "ai": {
    "providers": [{ "name": "azure-anthropic", "model": "claude-sonnet-4-5", "enabled": true }],
    "critic": { "provider": "azure-anthropic", "model": "claude-sonnet-4-5" },
    "analysis_timeout_minutes": 2,
    "generation_timeout_minutes": 5,
    "generation_batch_size": 30,
    "debug_log_enabled": true
  },
  "coverage": {
    "criteria_file": "docs/criteria/_criteria_index.yaml",
    "criteria_dir": "docs/criteria",
    "automation_dirs": ["tests"]
  },
  "testimize": {
    "enabled": false,
    "mode": "exploratory",
    "strategy": "HybridArtificialBeeColony"
  }
}
```

> **Generation & analysis tuning (v1.42.0)**:
> - `ai.analysis_timeout_minutes` (default 2) — behavior analysis timeout
> - `ai.generation_timeout_minutes` (default 5) — per-batch generation timeout
> - `ai.generation_batch_size` (default 30) — tests per AI call
> - `debug.enabled` (default false) — writes per-call timing diagnostics
>   to `.spectra-debug.log` in the project root. Use `--verbosity diagnostic`
>   for one-shot override without changing config.
>
> Slower / reasoning models (DeepSeek-V3, large Azure deployments) typically
> need: analysis 5–10 min, generation 15–20 min, batches of 6–10.
>
> When behavior analysis fails (timeout, parse error, empty response), the
> `.spectra-result.json` from `--analyze-only` now sets `status: "analysis_failed"`
> and a `message` field explaining the cause + remediation. The `spectra-generate`
> SKILL recognizes this and refuses to present the fallback default (15) as if
> it were a real recommendation.

> **Spec 039**: critic providers now use the same five names as the generator
> (`github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`).
> Legacy `github` is a soft alias; legacy `google` is rejected.
>
> **Spec 038 / v1.48.3**: optional `testimize` section enables in-process
> Testimize integration for algorithmic test data optimization (BVA, EP,
> pairwise, ABC). The library runs in-process as a bundled NuGet dependency
> — no MCP child process, no separate tool installation. When enabled,
> `TestimizeRunner` calls `TestimizeEngine.Configure(...).Generate()` once
> per suite and embeds the result in the generation prompt as authoritative
> facts. Off by default.

## Code Conventions

- **C# 12, .NET 8+**
- **Naming**: PascalCase types/methods, camelCase locals
- **Async**: All I/O operations async with `Async` suffix
- **Nullable**: Reference types enabled
- **Tests**: xUnit, structured results (never throw on validation errors)
- **Serialization**: `[JsonPropertyName("snake_case")]` for JSON, `[YamlMember(Alias = "snake_case")]` for YAML
- **Atomic writes**: Temp file + rename pattern for all file writes
- **Error handling**: Non-critical operations (progress files, browser open) wrapped in try/catch

## Completed Feature Specs

| # | Feature | Key Changes |
|---|---------|-------------|
| 039 | Unified Critic Provider List | Critic provider validation now matches the generator provider list (`github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`). Legacy `github` is a soft alias with deprecation warning; legacy `google` is hard-rejected. New `ResolveProvider` helper in `CriticFactory`. Enables Azure-only billing setups. |
| 038 | Testimize Integration | Optional MCP integration with the external `Testimize.MCP.Server` global tool for algorithmic test data optimization (BVA, EP, pairwise, ABC). New `testimize` config section (disabled by default), `TestimizeMcpClient` (process lifecycle, JSON-RPC, 30s/5s timeouts, idempotent disposal), two new conditionally-registered AI tools (`GenerateTestData`, `AnalyzeFieldSpec`), `{{#if testimize_enabled}}` blocks in behavior-analysis and test-generation templates, new `spectra testimize check` CLI command, full graceful degradation. No NuGet dependency. |
| 037 | ISTQB Test Design Techniques | All five built-in prompt templates rewritten to teach the AI six ISTQB techniques (EP, BVA, DT, ST, EG, UC). New `IdentifiedBehavior.Technique` field, `BehaviorAnalysisResult.TechniqueBreakdown` map, `AcceptanceCriterion.TechniqueHint` field. Analysis output includes `technique_breakdown` alongside `breakdown`. Terminal and progress page render a Technique Breakdown section in fixed display order. Distribution guideline caps any single category at 40%. Existing user-edited templates preserved; opt in via `spectra prompts reset --all`. |
| 033 | From-Description Chat Flow | Dedicated `--from-description` SKILL section, agent intent routing (focus vs from-description vs from-suggestions), doc-aware manual tests with populated `source_refs` and `criteria` (verdict stays manual) |
| 029 | spectra-update SKILL (10th) | Agent delegation, documentation sync, version 1.35.0 |
| 028 | Coverage & Criteria Pipeline | Fixed criteria propagation in parser, wired criteria into generation pipeline, always write criteria: [] |
| 027 | SKILL/Agent Deduplication | Agents delegate to SKILLs, execution ~120 lines, generation ~81 lines, SKILL consistency fixes |
| 024 | Docs Index SKILL & Coverage Fix | 9th SKILL (spectra-docs), result/progress files, --skip-criteria, terminology fix |
| 023 | Criteria Extraction Overhaul | Per-doc extraction, import, rename requirements→criteria, progress page |
| 023 | Copilot Chat Integration | SKILLs, agents, result file polling, batch generation |
| 022 | Bundled Skills | 8 SKILLs + 2 agents embedded as resources, hash-tracked updates |
| 021 | Generation Session | 4-phase flow, suggestions, user-described tests, duplicate detection |
| 020 | CLI Non-Interactive | `--output-format json`, `--no-interaction`, exit codes |
| 017 | MCP Tool Resilience | Auto-resolve run_id/test_handle, list_active_runs |
| 015 | Requirements Extraction | AI-powered extraction (now superseded by 023) |
| 014 | Open Source Ready | CI/CD, README, publish workflows |
| 013 | CLI UX Improvements | NextStepHints, interactive prompts, config subcommands |
| 012 | Dashboard Branding | Company logo, colors, dark theme, `--preview` |
| 011 | Coverage Overhaul | Unified 3-type coverage, auto-link, requirements parser |
| 010 | Document Index | `docs/_index.md`, SHA-256 incremental, sections/entities |
| 010 | Smart Test Selection | find_test_cases, saved selections, execution history |
| 009 | Copilot SDK Consolidation | Single AI runtime, removed legacy agents |
| 008 | Grounding Verification | Dual-model critic, grounded/partial/hallucinated verdicts |
| 006 | Conversational Generation | Direct/interactive modes, test updates, classification |

## Test Counts (as of 2026-04-11)

| Project | Tests |
|---------|-------|
| Spectra.Core.Tests | 491 |
| Spectra.CLI.Tests | 709 |
| Spectra.MCP.Tests | 351 |
| **Total** | **1551** |
| **Total** | **1279** |
