# SPECTRA Project Knowledge

**Generated**: 2026-04-04  
**Version**: 1.18.0+  
**Tests**: 1,241 passing (435 Core + 455 CLI + 351 MCP)  
**Source files**: 339 C# files, 141 test files

---

## 1. Architecture Overview

SPECTRA is an AI-native test generation and execution framework built on .NET 8. It reads product documentation, generates structured test suites, and executes them through a deterministic AI-orchestrated protocol.

### System Flow

```
docs/                        Source documentation (Markdown)
  |
docs/_index.md               Pre-built document index (incremental, SHA-256)
  |
AI Test Generation CLI       GitHub Copilot SDK (sole AI runtime)
  |                            Supports: github-models, azure-openai,
tests/                         azure-anthropic, openai, anthropic
  |
MCP Execution Engine         Deterministic state machine (SQLite)
  |
LLM Orchestrator             Copilot Chat, Claude, any MCP client
  |
External Integrations        Azure DevOps, Jira, Teams, Slack via MCP
```

### Three Independent Subsystems

| Subsystem | Project | Purpose |
|-----------|---------|---------|
| **AI CLI** | `Spectra.CLI` | Generate, update, analyze tests from documentation |
| **MCP Engine** | `Spectra.MCP` | Execute tests through deterministic AI-orchestrated protocol |
| **Core Library** | `Spectra.Core` | Shared models, parsing, validation, coverage |

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Language | C# 12, .NET 8+ |
| AI Runtime | GitHub Copilot SDK (sole runtime, multi-provider) |
| CLI Framework | System.CommandLine |
| Terminal UX | Spectre.Console |
| Serialization | System.Text.Json |
| MCP Server | ASP.NET Core |
| State Storage | Microsoft.Data.Sqlite |
| Testing | xUnit |
| Dashboard | Static HTML/CSS/JS with D3.js |

### Key Design Principles

1. **GitHub as Source of Truth** - Test cases as Markdown in git, no external databases
2. **Deterministic Execution** - Same inputs produce same execution queue, validated state machine
3. **Orchestrator-Agnostic** - MCP API works with any LLM (Copilot, Claude, custom)
4. **CLI-First** - All functionality exposed via CLI before any UI
5. **YAGNI** - Simplest solution that works, no premature abstractions

---

## 2. Implemented Features

All features are complete and passing tests.

| # | Feature | Description |
|---|---------|-------------|
| 001 | AI Test Generation CLI | Core CLI with `spectra ai generate` command |
| 002 | MCP Execution Server | Deterministic test execution via MCP tools |
| 003 | Dashboard & Coverage | Static HTML dashboard with coverage analysis |
| 004 | Generation Profiles | Customize AI output style (detail level, domain rules) |
| 005 | AI Provider Completion | Provider chain with BYOK support |
| 006 | Conversational Generation | Two-mode generation (Direct/Interactive) + test updates |
| 007 | Execution Agent MCP Tools | Full MCP tool suite for execution agents |
| 008 | Grounding Verification | Dual-model critic (generator + verifier), three verdicts |
| 009 | Copilot SDK Consolidation | Unified all AI under GitHub Copilot SDK |
| 009 | Coverage Dashboard Viz | Expandable drill-down, donut chart, D3.js treemap |
| 010 | Document Index | Persistent `docs/_index.md` with incremental updates |
| 010 | Smart Test Selection | Cross-suite search, custom selections, execution history |
| 011 | Coverage Overhaul | Unified three-type coverage (Doc, Req, Automation) |
| 012 | Dashboard Branding | Company name, logo, favicon, themes, custom CSS |
| 013 | CLI UX Improvements | NextStepHints, config subcommands, interactive prompts |
| 014 | Open Source Ready | README, CI/CD, NuGet publish, issue templates |
| 015 | Requirements Extraction | AI-powered extraction from docs with RFC 2119 inference |
| 016 | Bug Logging Templates | Structured bug report templates for execution agent |
| 017 | MCP Tool Resilience | Auto-resolve run_id/test_handle for weaker models |
| 018 | Undocumented Tests | Manual verdict for tester-described tests |
| 019 | Smart Test Count | AI-powered behavior analysis for recommended count |
| 020 | CLI Non-Interactive | `--output-format json`, `--no-interaction`, exit codes |
| 021 | Generation Session | Four-phase session (analyze, generate, suggest, describe) |
| 022 | Bundled SKILLs | 6 SKILL files + 2 agent prompts via `spectra init` |

---

## 3. CLI Commands

### Global Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--verbosity` | `-v` | `normal` | Output level: quiet, minimal, normal, detailed, diagnostic |
| `--dry-run` | | `false` | Preview changes without executing |
| `--no-review` | | `false` | Skip interactive review |
| `--output-format` | | `human` | Output format: `human` or `json` |
| `--no-interaction` | | `false` | Fail with exit code 3 if required args missing |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Command failed (runtime error, missing config) |
| 2 | Validation errors found |
| 3 | Missing required arguments in non-interactive mode |
| 130 | Cancelled by user (SIGINT) |

### Commands Reference

#### `spectra init`

Initialize a repository for SPECTRA.

```bash
spectra init                    # Full init with SKILL files
spectra init --force            # Overwrite existing config
spectra init --skip-skills      # Skip SKILL/agent file creation
spectra init --no-interactive   # Non-interactive mode
```

Creates: `spectra.config.json`, `tests/`, `docs/`, `docs/requirements/_criteria_index.yaml`, `templates/bug-report.md`, `.github/skills/` (9 SKILLs), `.github/agents/` (2 agents), `.vscode/mcp.json`, `.github/workflows/deploy-dashboard.yml`

#### `spectra ai generate`

Generate test cases from documentation. Supports interactive sessions and direct mode.

```bash
# Interactive session (four phases: analyze, generate, suggest, describe)
spectra ai generate

# Direct mode
spectra ai generate checkout
spectra ai generate checkout --count 10 --focus "error handling"
spectra ai generate checkout --skip-critic

# Session commands
spectra ai generate checkout --from-suggestions          # From previous session
spectra ai generate checkout --from-suggestions 1,3      # Specific indices
spectra ai generate checkout --from-description "IBAN validation" --context "checkout page"
spectra ai generate checkout --auto-complete             # All phases, no prompts

# CI/SKILL mode
spectra ai generate checkout --output-format json --verbosity quiet
spectra ai generate checkout --no-interaction --output-format json
```

| Option | Description |
|--------|-------------|
| `<suite>` | Target suite name (optional for interactive mode) |
| `--count` / `-n` | Number of tests (default: AI-recommended) |
| `--focus` / `-f` | Focus area description |
| `--skip-critic` | Skip grounding verification |
| `--from-suggestions` | Generate from previous session suggestions |
| `--from-description` | Create test from plain-language description |
| `--context` | Additional context for `--from-description` |
| `--auto-complete` | Run all phases without prompts |

#### `spectra ai update`

Update existing tests when documentation changes.

```bash
spectra ai update                             # Interactive mode
spectra ai update checkout                    # Direct mode
spectra ai update checkout --diff             # Show proposed changes
spectra ai update checkout --delete-orphaned  # Auto-delete orphaned tests
```

Classifies tests as: UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT.

#### `spectra ai analyze`

Analyze test coverage and extract acceptance criteria.

```bash
spectra ai analyze --coverage                                        # Three-section report
spectra ai analyze --coverage --auto-link                            # Write automated_by fields
spectra ai analyze --coverage --format json --output coverage.json   # File output
spectra ai analyze --coverage --output-format json                   # Stdout JSON
spectra ai analyze --extract-criteria                                # Extract from docs
spectra ai analyze --extract-criteria --dry-run                      # Preview only
spectra ai analyze --import-criteria ./criteria.csv                  # Import from external file
spectra ai analyze --list-criteria                                   # List all criteria
```

#### `spectra validate`

Validate test files against schema rules.

```bash
spectra validate                           # All suites
spectra validate --suite checkout          # Specific suite
spectra validate --output-format json      # JSON output
```

#### `spectra list` / `spectra show`

Browse tests.

```bash
spectra list                               # All suites
spectra list --suite checkout              # Specific suite
spectra show TC-101                        # Test details
spectra show TC-101 --output-format json   # JSON output
```

#### `spectra index`

Build or update test metadata indexes.

```bash
spectra index                    # Incremental
spectra index --rebuild          # Full rebuild
spectra index --suite checkout   # Specific suite
```

#### `spectra docs index`

Build or refresh the documentation index, then auto-extract acceptance criteria.

```bash
spectra docs index                # Incremental update + auto-extract acceptance criteria
spectra docs index --force        # Full rebuild + auto-extract acceptance criteria
spectra docs index --skip-criteria  # Index only, skip criteria extraction
spectra docs index --no-interaction --output-format json  # SKILL/CI mode
```

#### `spectra dashboard`

Generate a static HTML dashboard.

```bash
spectra dashboard --output ./site
spectra dashboard --output ./site --title "My Dashboard"
spectra dashboard --preview            # Sample data + branding
spectra dashboard --output-format json # JSON output
```

#### `spectra config`

View or modify configuration.

```bash
spectra config source.dir                   # Show value
spectra config source.dir "documentation"   # Set value
spectra config add-automation-dir ../tests  # Add automation dir
spectra config remove-automation-dir ../old # Remove automation dir
spectra config list-automation-dirs         # List dirs
```

#### `spectra update-skills`

Update bundled SKILL and agent files.

```bash
spectra update-skills    # Updates unmodified, skips user-customized
```

#### `spectra auth`

Check authentication status.

```bash
spectra auth                    # All providers
spectra auth -p github-models   # Specific provider
```

#### `spectra init-profile`

Create or update generation profiles.

```bash
spectra init-profile
```

---

## 4. MCP Tools

The MCP server (`Spectra.MCP`) provides test execution tools for AI agents. All tools accepting `run_id` or `test_handle` auto-resolve when parameters are omitted (single active run/test used automatically).

### Run Management

| Tool | Parameters | Description |
|------|-----------|-------------|
| `start_execution_run` | `suite` (or `test_ids`, `selection`), `tester?`, `environment?` | Start a new test execution run |
| `get_execution_status` | `run_id?` | Get current run status and next test |
| `pause_execution_run` | `run_id?` | Pause execution |
| `resume_execution_run` | `run_id?` | Resume paused execution |
| `cancel_execution_run` | `run_id?`, `reason?` | Cancel execution |
| `finalize_execution_run` | `run_id?` | Complete run and generate reports (JSON/MD/HTML) |
| `list_available_suites` | (none) | List test suites with test counts |

### Test Execution

| Tool | Parameters | Description |
|------|-----------|-------------|
| `get_test_case_details` | `run_id?`, `test_handle?` | Get test steps, expected result, preconditions |
| `advance_test_case` | `run_id?`, `test_handle?`, `status`, `comment?` | Record PASSED/FAILED result |
| `skip_test_case` | `run_id?`, `test_handle?`, `reason`, `blocked?` | Skip test with reason |
| `bulk_record_results` | `run_id?`, `status`, `remaining?`, `test_ids?`, `reason?` | Bulk record results |
| `add_test_note` | `run_id?`, `test_handle?`, `note` | Add notes to a test |
| `retest_test_case` | `run_id?`, `test_handle?`, `reason?` | Requeue a test for retry |
| `save_screenshot` | `run_id?`, `test_handle?`, `base64?`, `file_path?`, `label?` | Save screenshot attachment |
| `save_clipboard_screenshot` | `run_id?`, `test_handle?`, `label?` | Capture from system clipboard |

### Run Discovery & Cleanup

| Tool | Parameters | Description |
|------|-----------|-------------|
| `list_active_runs` | (none) | List all non-terminal runs with progress summaries |
| `cancel_all_active_runs` | `reason?` | Cancel all active runs at once |

### Data Tools

| Tool | Parameters | Description |
|------|-----------|-------------|
| `validate_tests` | `suite?` | Validate test files against schema |
| `rebuild_indexes` | `suite?` | Rebuild `_index.json` files |
| `analyze_coverage_gaps` | (none) | Analyze test coverage gaps |
| `find_test_cases` | `query?`, `priority?`, `tags?`, `component?`, `automated?` | Cross-suite search and filter |
| `get_test_execution_history` | `test_id` | Per-test execution statistics |
| `list_saved_selections` | (none) | List named selections from config |

### Reporting

| Tool | Parameters | Description |
|------|-----------|-------------|
| `get_run_history` | `limit?`, `status?`, `suite?` | Execution history with summary counts |
| `get_execution_summary` | (none) | Overall summary statistics |

### Bulk Operations

```json
{"status": "SKIPPED", "remaining": true, "reason": "Environment unavailable"}
{"status": "PASSED", "remaining": true}
{"status": "FAILED", "test_ids": ["TC-001", "TC-002"], "reason": "API down"}
```

### Reports

Generated in three formats: JSON, Markdown, HTML. Features: test titles from `_index.json`, human-readable durations, UTC-normalized timestamps, expandable non-passing tests.

---

## 5. Configuration Schema (spectra.config.json)

```json
{
  "source": {
    "dir": "docs",
    "include_patterns": ["**/*.md"],
    "exclude_patterns": ["**/node_modules/**"]
  },
  "tests": {
    "dir": "tests"
  },
  "ai": {
    "providers": [
      {
        "name": "copilot",
        "model": "gpt-4o",
        "enabled": true,
        "priority": 1,
        "api_key_env": null
      }
    ],
    "critic": {
      "enabled": false,
      "provider": "copilot",
      "model": "gpt-4o"
    }
  },
  "generation": {
    "default_count": 5,
    "max_count": 100
  },
  "update": {},
  "suites": {},
  "git": {},
  "validation": {},
  "dashboard": {
    "branding": {
      "company_name": null,
      "logo": null,
      "favicon": null,
      "theme": "light",
      "colors": {
        "primary": null,
        "secondary": null,
        "accent": null
      },
      "custom_css": null
    }
  },
  "coverage": {
    "automation_dirs": [],
    "scan_patterns": ["**/*.cs", "**/*.ts", "**/*.js", "**/*.py"],
    "file_extensions": [".cs", ".ts", ".js", ".py"],
    "criteria_file": "docs/requirements/_criteria_index.yaml"
  },
  "execution": {
    "db_path": ".execution/spectra.db",
    "reports_dir": ".execution/reports"
  },
  "bug_tracking": {},
  "profile": {},
  "selections": {
    "smoke": {
      "description": "Quick smoke test - high priority tests only",
      "priorities": ["high"]
    }
  }
}
```

---

## 6. Agent Prompts

### spectra-execution.agent.md

```yaml
---
name: SPECTRA Execution
description: Executes manual test cases through SPECTRA with optional documentation lookup.
tools:
  - "spectra/*"
  - "github/get_copilot_space"
  - "github/list_copilot_spaces"
  - "read"
  - "edit"
  - "search"
  - "terminal"
  - "browser"
model: GPT-4o
disable-model-invocation: true
---
```

Workflow: Start run -> Get test details -> Guide user through steps -> Record results -> Finalize with reports. Supports inline doc lookup via Copilot Spaces and structured bug reporting on failures.

### spectra-generation.agent.md

```yaml
---
name: SPECTRA Generation
description: Generates test cases from documentation with AI verification and gap analysis.
tools:
  - "terminal"
  - "read"
  - "search"
  - "github/get_copilot_space"
  - "github/list_copilot_spaces"
model: GPT-4o
disable-model-invocation: true
---
```

Uses terminal to invoke CLI commands with `--output-format json --verbosity quiet`. Supports the full generation session flow (analyze, generate, suggestions, user-described).

### Bundled SKILL Files

| SKILL | Path | Wraps |
|-------|------|-------|
| SPECTRA Generate | `.github/skills/spectra-generate/SKILL.md` | `spectra ai generate` (all flags) |
| SPECTRA Coverage | `.github/skills/spectra-coverage/SKILL.md` | `spectra ai analyze --coverage` |
| SPECTRA Dashboard | `.github/skills/spectra-dashboard/SKILL.md` | `spectra dashboard` |
| SPECTRA Validate | `.github/skills/spectra-validate/SKILL.md` | `spectra validate` |
| SPECTRA List | `.github/skills/spectra-list/SKILL.md` | `spectra list` / `spectra show` |
| SPECTRA Profile | `.github/skills/spectra-init-profile/SKILL.md` | `spectra init-profile` |

All SKILLs use `--output-format json --verbosity quiet` for machine-readable output.

---

## 7. Project Structure

```
src/
├── Spectra.CLI/                    # .NET CLI application (NuGet: Spectra.CLI)
│   ├── Commands/                   # Command handlers (14 command directories)
│   │   ├── Ai/                     # Parent for generate, analyze, update
│   │   ├── Generate/               # GenerateCommand, GenerateHandler, UserDescribedGenerator
│   │   ├── Analyze/                # AnalyzeCommand, AnalyzeHandler
│   │   ├── Update/                 # UpdateCommand, UpdateHandler
│   │   ├── Init/                   # InitCommand, InitHandler
│   │   ├── Validate/               # ValidateCommand, ValidateHandler
│   │   ├── Dashboard/              # DashboardCommand, DashboardHandler
│   │   ├── Docs/                   # DocsCommand, DocsIndexCommand/Handler
│   │   ├── List/                   # ListCommand, ListHandler
│   │   ├── Show/                   # ShowCommand, ShowHandler
│   │   ├── Index/                  # IndexCommand, IndexHandler
│   │   ├── Config/                 # ConfigCommand, ConfigHandler
│   │   ├── Auth/                   # AuthCommand, AuthHandler
│   │   └── UpdateSkills/           # UpdateSkillsCommand, UpdateSkillsHandler
│   ├── Agent/                      # AI provider integration (Copilot SDK)
│   │   ├── Copilot/                # CopilotGenerationAgent, CopilotCritic, tools
│   │   ├── Critic/                 # ICriticRuntime, CriticFactory, prompt builder
│   │   └── Analysis/              # BehaviorAnalyzer, BehaviorAnalysisResult
│   ├── Session/                    # Generation session state management
│   │   ├── GenerationSession.cs    # Session state model
│   │   ├── SessionStore.cs         # Read/write .spectra/session.json
│   │   ├── SuggestionBuilder.cs    # Derive suggestions from analysis
│   │   └── SessionSummary.cs       # Exit summary display
│   ├── Skills/                     # Bundled SKILL file content
│   │   ├── SkillContent.cs         # 6 SKILL file strings
│   │   ├── AgentContent.cs         # 2 agent prompt strings
│   │   └── SkillsManifest.cs       # Hash tracking for updates
│   ├── Results/                    # Typed JSON result models
│   │   ├── CommandResult.cs        # Base result + ErrorResult
│   │   ├── GenerateResult.cs       # + DuplicateWarning, SessionCounts
│   │   ├── AnalyzeCoverageResult.cs
│   │   ├── ValidateResult.cs
│   │   ├── DashboardResult.cs
│   │   ├── ListResult.cs
│   │   ├── ShowResult.cs
│   │   ├── InitResult.cs
│   │   └── DocsIndexResult.cs
│   ├── Output/                     # Terminal output infrastructure
│   │   ├── ProgressReporter.cs     # Spinners, status (suppressed in JSON mode)
│   │   ├── ResultPresenter.cs      # Tables (suppressed in JSON mode)
│   │   ├── JsonResultWriter.cs     # Serialize results to stdout as JSON
│   │   ├── VerificationPresenter.cs
│   │   ├── AnalysisPresenter.cs
│   │   ├── SuggestionPresenter.cs  # Suggestions menu for session flow
│   │   ├── NextStepHints.cs        # Context-aware next-step suggestions
│   │   └── OutputSymbols.cs        # Unicode symbols and markup
│   ├── Infrastructure/             # Cross-cutting infrastructure
│   │   ├── ExitCodes.cs            # 0, 1, 2, 3, 130
│   │   ├── OutputFormat.cs         # Human, Json enum
│   │   ├── VerbosityLevel.cs       # Quiet, Minimal, Normal, Detailed, Diagnostic
│   │   └── FileHasher.cs           # SHA-256 for SKILL updates
│   ├── Options/                    # GlobalOptions (verbosity, dry-run, output-format, etc.)
│   ├── Interactive/                # Selectors, UserDescriptionPrompt
│   ├── Validation/                 # Test validation, DuplicateDetector
│   ├── Source/                     # Document map builder, document index service
│   ├── Index/                      # _index.json operations
│   ├── Coverage/                   # Gap analysis, coverage reporting
│   ├── Classification/             # Test classification (update flow)
│   ├── Dashboard/                  # Data collection, generation, BrandingInjector
│   ├── Profile/                    # Generation profile loading
│   ├── IO/                         # File writers
│   └── Review/                     # Interactive terminal UI
│
├── Spectra.Core/                   # Shared library (NuGet: Spectra.Core)
│   ├── Models/
│   │   ├── Config/                 # SpectraConfig and all sub-configs
│   │   ├── Dashboard/              # DashboardData, SuiteStats, TestEntry
│   │   ├── Coverage/               # UnifiedCoverageReport, CoverageLink
│   │   ├── Execution/              # Run, TestResult, ExecutionReport
│   │   ├── Grounding/              # GroundingMetadata, VerificationResult
│   │   ├── Index/                  # DocumentIndex, DocumentIndexEntry
│   │   └── Profile/                # GenerationProfile, EffectiveProfile
│   ├── Coverage/                   # AutomationScanner, LinkReconciler, Analyzers
│   ├── Parsing/                    # Markdown parser, RequirementsParser
│   ├── Validation/                 # Schema validation
│   ├── Index/                      # DocumentIndexReader/Writer
│   ├── Storage/                    # ExecutionDbReader
│   └── Update/                     # TestClassifier
│
├── Spectra.MCP/                    # MCP execution server (NuGet: Spectra.MCP)
│   ├── Tools/
│   │   ├── RunManagement/          # Start, pause, resume, finalize, list suites
│   │   ├── TestExecution/          # Advance, skip, bulk, screenshot, notes, retest
│   │   ├── Reporting/              # History, summary
│   │   └── Data/                   # Validate, rebuild, coverage, search, selections
│   ├── Execution/                  # ExecutionEngine, TestQueue, StateMachine
│   ├── Storage/                    # RunRepository, ResultRepository, ExecutionDb (SQLite)
│   ├── Reports/                    # ReportGenerator, ReportWriter (JSON/MD/HTML)
│   ├── Server/                     # McpServer, ToolRegistry, McpProtocol
│   └── Infrastructure/             # McpConfig, McpLogging
│
└── Spectra.GitHub/                 # GitHub integration (future)

dashboard-site/                     # Static dashboard template
├── index.html                      # Main template ({{DASHBOARD_DATA}} placeholder)
├── styles/main.css                 # Dashboard styles + dark theme
├── scripts/
│   ├── app.js                      # Main dashboard JavaScript
│   └── coverage-map.js             # D3.js treemap visualization
├── functions/                      # Cloudflare Pages functions
│   ├── _middleware.js              # OAuth middleware
│   └── auth/callback.js           # OAuth callback
└── access-denied.html

tests/
├── Spectra.Core.Tests/             # 435 tests
├── Spectra.CLI.Tests/              # 455 tests
├── Spectra.MCP.Tests/              # 351 tests
└── TestFixtures/                   # Sample data

specs/                              # Feature specifications (001-022)
spec-kit/                           # Feature spec drafts
```

---

## 8. Test Suite Status

| Project | Tests | Status |
|---------|-------|--------|
| Spectra.Core.Tests | 435 | All passing |
| Spectra.CLI.Tests | 455 | All passing |
| Spectra.MCP.Tests | 351 | All passing |
| **Total** | **1,241** | **All passing** |

Test areas covered:
- **Core**: Parsing, validation, index operations, coverage analyzers, requirements parser
- **CLI**: Command handlers, output presenters, dashboard generation, JSON result serialization, duplicate detection, session store, suggestion builder
- **MCP**: Individual tool tests, full execution flow integration tests, report generation, state machine transitions, tool resilience

---

## 9. Spec Files Status

### Implemented (22 specs)

| # | Spec | Status |
|---|------|--------|
| 001 | AI Test Generation CLI | Implemented |
| 002 | MCP Execution Server | Implemented |
| 003 | Dashboard & Coverage Analysis | Implemented |
| 004 | Test Generation Profile | Implemented |
| 005 | AI Provider Completion | Implemented |
| 006 | Conversational Generation | Implemented |
| 007 | Execution Agent MCP Tools | Implemented |
| 008 | Grounding Verification | Implemented |
| 009 | Coverage Dashboard Viz | Implemented |
| 009 | Copilot SDK Consolidation | Implemented |
| 010 | Document Index | Implemented |
| 010 | Smart Test Selection | Implemented |
| 011 | Coverage Overhaul | Implemented |
| 011 | Dashboard Fixes & Deploy | Implemented |
| 012 | Dashboard Branding | Implemented |
| 013 | CLI UX Improvements | Implemented |
| 014 | Open Source Ready | Implemented |
| 015 | Auto Requirements Extraction | Implemented |
| 016 | Bug Logging Templates | Implemented |
| 017 | MCP Tool Resilience | Implemented |
| 018 | Undocumented Tests | Implemented |
| 019 | Smart Test Count | Implemented |
| 020 | CLI Non-Interactive | Implemented |
| 021 | Generation Session | Implemented |
| 022 | Bundled SKILLs | Implemented |
| 023 | Criteria Extraction Overhaul | Implemented |
| 023 | Copilot Chat Integration | Implemented |
| 024 | Docs Index SKILL & Coverage Fix | Implemented |

### Spec-Kit Drafts

| File | Status |
|------|--------|
| `spec-kit/architecture.md` | Reference document |
| `spec-kit/feature-cli-non-interactive.md` | Implemented as 020 |
| `spec-kit/feature-generation-session.md` | Implemented as 021 |
| `spec-kit/feature-bundled-skills.md` | Implemented as 022 |

---

## 10. Dashboard Features

### Visualizations
- **Suite Browser** - Navigate all test suites with test counts
- **Test Viewer** - View individual test cases with steps and metadata
- **Run History** - Timeline of execution runs with pass/fail/skip stats
- **Coverage Progress Bars** - Three-section coverage (Doc, Req, Automation)
- **Expandable Drill-Down** - Click progress bars for per-item breakdown
- **Donut Chart** - SVG automated/manual/unlinked distribution
- **D3.js Treemap** - Suites sized by test count, colored by automation %
- **Empty State Guidance** - Setup instructions when no data exists

### Branding Options (via `dashboard.branding` config)
- Company name
- Logo URL
- Favicon URL
- Theme: `light` or `dark`
- Custom color palette (primary, secondary, accent)
- Custom CSS injection

### Deployment
- **Cloudflare Pages** - GitHub OAuth authentication via `_middleware.js`
- Auto-deploy workflow: `.github/workflows/deploy-dashboard.yml`
- Push to main triggers dashboard rebuild and deploy
- `access-denied.html` for auth errors

### CLI Options
```bash
spectra dashboard --output ./site                    # Generate
spectra dashboard --output ./site --title "Custom"   # Custom title
spectra dashboard --preview                          # Sample data for branding preview
spectra dashboard --dry-run                          # Preview without writing
spectra dashboard --output-format json               # JSON output for SKILLs
```

### Report Formats
Reports generated by `finalize_execution_run`:
- **JSON** - Machine-readable, all data
- **Markdown** - Human-readable summary
- **HTML** - Styled report with expandable test details, screenshots
