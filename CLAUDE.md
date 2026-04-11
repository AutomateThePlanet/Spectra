# Spectra Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-12

> Version history lives in `CHANGELOG.md`.

## Active Technologies
- C# 12, .NET 8+ + GitHub Copilot SDK (sole AI runtime for generation and verification)
- System.CommandLine (CLI), Spectre.Console (terminal UX), System.Text.Json (serialization)
- ASP.NET Core (MCP server), Microsoft.Data.Sqlite (state storage)
- SQLite database (`.execution/spectra.db`) for execution state; file system for reports
- File-based (tests/, docs/, spectra.config.json, _index.json, _index.md, profiles, .spectra/prompts/)
- CsvHelper (CSV import for acceptance criteria)
- Dual-model verification: Generator (any provider) + Critic (any provider) via Copilot SDK
- Document index (`docs/_index.md`) for pre-built documentation metadata with incremental updates
- Acceptance criteria index (`docs/criteria/_criteria_index.yaml`) with per-document `.criteria.yaml` files

**AI Runtime**: All AI operations use the GitHub Copilot SDK as the single runtime. Multiple providers (github-models, azure-openai, azure-anthropic, openai, anthropic) are supported through the SDK's provider configuration - no separate agent implementations.

## Project Structure

```text
src/
├── Spectra.CLI/              # .NET CLI application
│   ├── Commands/             # Command handlers
│   │   ├── Analyze/          # Coverage analysis command
│   │   ├── Dashboard/        # Dashboard generation command
│   │   ├── Docs/             # Documentation management (docs index)
│   │   ├── Generate/         # Test generation (direct + interactive modes)
│   │   └── Update/           # Test update (direct + interactive modes)
│   ├── Agent/                # AI provider integration (Copilot SDK)
│   │   ├── Copilot/          # CopilotGenerationAgent, CopilotCritic, tools
│   │   └── Critic/           # ICriticRuntime, CriticFactory, prompt builder
│   ├── Source/               # Document map builder, document index service
│   ├── Index/                # _index.json operations
│   ├── Validation/           # Test validation, dedup, DuplicateDetector
│   ├── Review/               # Interactive terminal UI
│   ├── Interactive/          # Interactive mode components (selectors, session, UserDescriptionPrompt)
│   ├── Prompts/              # Prompt template engine (PlaceholderResolver, PromptTemplateLoader, BuiltInTemplates)
│   ├── Session/              # Generation session state, SessionStore, SuggestionBuilder
│   ├── Skills/               # Bundled SKILL content, AgentContent, SkillsManifest
│   ├── Results/              # Typed JSON result models per command (CommandResult, GenerateResult, etc.)
│   ├── Output/               # Progress reporters, result presenters, NextStepHints, JsonResultWriter
│   ├── Classification/       # Test classification (update flow)
│   ├── Coverage/             # Gap analysis and coverage reporting
│   ├── Profile/              # Generation profile loading
│   ├── Config/               # Configuration loader, automation dir subcommands
│   ├── Dashboard/            # Dashboard data collection, generation, BrandingInjector, SampleDataFactory
│   └── IO/                   # File writers
├── Spectra.Core/             # Shared library
│   ├── Models/               # TestCase, Suite, Config models
│   │   ├── Dashboard/        # DashboardData, SuiteStats, TestEntry, etc.
│   │   ├── Coverage/         # UnifiedCoverageReport, CoverageReport, CoverageLink, etc.
│   │   ├── Execution/        # Run, TestResult, ExecutionReport, McpToolResponse
│   │   └── Grounding/        # GroundingMetadata, VerificationVerdict, VerificationResult
│   ├── Coverage/             # AutomationScanner, LinkReconciler, CoverageCalculator, DocumentationCoverageAnalyzer, RequirementsCoverageAnalyzer, UnifiedCoverageBuilder, AutoLinkService
│   ├── Storage/              # ExecutionDbReader
│   ├── Parsing/              # Markdown + YAML parser, DocumentIndexExtractor, RequirementsParser, FrontmatterUpdater
│   ├── Validation/           # Schema validation
│   ├── Update/               # TestClassifier for test updates
│   └── Index/                # Index read/write (DocumentIndexReader, DocumentIndexWriter)
├── Spectra.MCP/              # MCP execution server
│   ├── Execution/            # ExecutionEngine, TestQueue, StateMachine
│   ├── Storage/              # RunRepository, ResultRepository, ExecutionDb
│   ├── Reports/              # ReportGenerator, ReportWriter (JSON/MD/HTML)
│   ├── Tools/                # MCP tool implementations
│   │   ├── RunManagement/    # Start, pause, resume, finalize tools
│   │   ├── TestExecution/    # Advance, skip, bulk record, screenshot tools
│   │   ├── Reporting/        # History, summary tools
│   │   └── Data/             # Validate, rebuild, coverage tools
│   ├── Server/               # McpServer, ToolRegistry, McpProtocol
│   ├── Identity/             # UserIdentityResolver
│   └── Infrastructure/       # McpConfig, McpLogging
└── Spectra.GitHub/           # GitHub integration (future)

dashboard-site/               # Static dashboard template
├── index.html                # Main template with {{DASHBOARD_DATA}} placeholder
├── styles/main.css           # Dashboard styles
├── scripts/
│   ├── app.js                # Main dashboard JavaScript
│   └── coverage-map.js       # D3.js coverage visualization + treemap
├── functions/                # Cloudflare Pages functions (auth)
│   ├── _middleware.js        # OAuth middleware
│   └── auth/callback.js      # OAuth callback handler
└── access-denied.html        # Auth error page

tests/
├── Spectra.Core.Tests/       # Unit tests (462 tests)
│   ├── Coverage/             # AutomationScanner, LinkReconciler, Calculator, DocCoverageAnalyzer, ReqCoverageAnalyzer, AutoLinkService tests
│   ├── Index/                # DocumentIndexReader, DocumentIndexWriter tests
│   └── Parsing/              # DocumentIndexExtractor, RequirementsParser, FrontmatterUpdater tests
├── Spectra.CLI.Tests/        # Integration tests (466 tests)
│   ├── Commands/             # DocsIndexCommand tests
│   ├── Dashboard/            # DataCollector, Generator tests
│   ├── Source/               # DocumentIndexService tests
│   └── Coverage/             # CoverageReportWriter (unified), CoverageAnalysis tests
├── Spectra.MCP.Tests/        # MCP server tests (351 tests)
│   ├── Tools/                # Individual tool tests
│   ├── Integration/          # Full execution flow tests
│   └── Reports/              # Report generation tests
└── TestFixtures/             # Sample data
```

## Commands

```bash
# Build
dotnet build

# Test
dotnet test

# Run CLI
dotnet run --project src/Spectra.CLI -- <command>

# Global Options (020-cli-non-interactive)
# --output-format json          Structured JSON on stdout (for SKILL/CI)
# --output-format human         Default human-readable output
# --no-interaction              Fail with exit 3 if required args missing
# --verbosity quiet             Minimal output (only final result)

# Test Generation (006 + 021-generation-session + 023-copilot-chat)
spectra ai generate                              # Interactive session (analyze → generate → suggest → loop)
spectra ai generate --suite checkout             # Direct mode (--suite flag or positional arg)
spectra ai generate checkout --focus "negative"  # Direct mode with focus
spectra ai generate checkout --no-interaction    # CI mode (no prompts, exit codes)
spectra ai generate --dry-run                    # Preview without writing
spectra ai generate checkout --skip-critic       # Skip grounding verification (008)
spectra ai generate --suite checkout --analyze-only  # Analysis only (no generation) — for SKILL two-step flow
spectra ai generate --suite checkout --count 80  # Batch generation (auto-batches in groups of 30)

# Generation Session (021-generation-session)
spectra ai generate checkout --auto-complete --output-format json   # All phases, no prompts (CI)
spectra ai generate checkout --from-suggestions --output-format json  # Generate from previous session suggestions
spectra ai generate checkout --from-suggestions 1,3                 # Specific suggestions by index
spectra ai generate checkout --from-description "IBAN validation error" --context "checkout page"  # User-described test
spectra ai generate checkout --output-format json --verbosity quiet   # SKILL-friendly output

# Test Update (006-conversational-generation)
spectra ai update                                # Interactive mode (guided prompts)
spectra ai update checkout                       # Direct mode (specific suite)
spectra ai update checkout --no-interaction      # CI mode (no prompts, exit codes)
spectra ai update --diff                         # Show changes before applying

# Dashboard Generation (003 + 012-dashboard-branding)
spectra dashboard --output ./site
spectra dashboard --output ./site --title "My Dashboard"
spectra dashboard --dry-run                        # Preview without generating
spectra dashboard --preview                        # Sample data + branding verification
spectra dashboard --output ./site --output-format json  # JSON output for SKILL

# Documentation Index (010-document-index + 024-docs-skill-coverage-fix)
spectra docs index                               # Incremental update + auto-extract acceptance criteria
spectra docs index --force                       # Full rebuild + auto-extract acceptance criteria
spectra docs index --skip-criteria               # Index only, skip criteria extraction
spectra docs index --no-interaction --output-format json  # SKILL/CI mode (writes .spectra-result.json + .spectra-progress.html)

# Coverage Analysis (003 + unified coverage overhaul)
spectra ai analyze --coverage                                    # Unified three-section report (doc, req, auto)
spectra ai analyze --coverage --format json --output coverage.json
spectra ai analyze --coverage --format markdown --output coverage.md
spectra ai analyze --coverage --auto-link                        # Write automated_by back into test files
spectra ai analyze --coverage --output-format json               # Structured JSON to stdout (SKILL)
spectra ai analyze --extract-criteria                 # Extract acceptance criteria from docs (per-document, incremental)
spectra ai analyze --extract-criteria --force         # Force full re-extraction (ignore hashes)
spectra ai analyze --extract-criteria --dry-run       # Preview without writing
spectra ai analyze --extract-criteria --output-format json  # JSON output for SKILL/CI
spectra ai analyze --extract-requirements             # Hidden alias for --extract-criteria

# Acceptance Criteria Import (023-criteria-extraction-overhaul)
spectra ai analyze --import-criteria ./jira-export.csv        # Import from CSV/YAML/JSON
spectra ai analyze --import-criteria ./criteria.yaml --replace # Replace target file
spectra ai analyze --import-criteria ./export.csv --skip-splitting  # Skip AI splitting
spectra ai analyze --import-criteria ./criteria.json --dry-run     # Preview without writing

# Acceptance Criteria List (023-criteria-extraction-overhaul)
spectra ai analyze --list-criteria                               # List all criteria
spectra ai analyze --list-criteria --source-type jira            # Filter by source type
spectra ai analyze --list-criteria --component checkout          # Filter by component
spectra ai analyze --list-criteria --priority high               # Filter by priority
spectra ai analyze --list-criteria --output-format json          # JSON output

# Prompt Template Management (030-prompt-templates)
spectra prompts list                                 # List all templates with status
spectra prompts list --output-format json            # JSON output for SKILL/CI
spectra prompts show behavior-analysis               # Show template content
spectra prompts show behavior-analysis --raw         # Show with unresolved placeholders
spectra prompts validate behavior-analysis           # Check syntax and placeholders
spectra prompts reset behavior-analysis              # Reset one template to default
spectra prompts reset --all                          # Reset all templates

# Validation with JSON output
spectra validate --output-format json              # JSON errors for SKILL/CI

# SKILL Management (022-bundled-skills)
spectra update-skills                             # Update bundled SKILL files to latest version
spectra init --skip-skills                        # Init without creating SKILL/agent files

# Automation Directory Management (013-cli-ux-improvements)
spectra config add-automation-dir ../new-tests     # Add automation dir for coverage
spectra config remove-automation-dir ../old-tests   # Remove automation dir
spectra config list-automation-dirs                 # List dirs with existence status
```

## Code Style

- **Language:** C# 12, .NET 8+
- **Naming:** PascalCase for types/methods, camelCase for locals
- **Async:** All I/O operations are async with `Async` suffix
- **Nullability:** Nullable reference types enabled
- **Tests:** xUnit with structured results (never throw on validation errors)

## MCP Execution Server

The MCP server (`Spectra.MCP`) provides test execution tools for AI agents.

### Available MCP Tools

**Run Management:**
- `start_execution_run` - Start a new test execution run
- `get_execution_status` - Get current run status and next test
- `pause_execution_run` - Pause execution
- `resume_execution_run` - Resume paused execution
- `cancel_execution_run` - Cancel execution
- `finalize_execution_run` - Complete run and generate reports
- `list_available_suites` - List test suites

**Test Execution:**
- `get_test_case_details` - Get test steps, expected result, preconditions
- `advance_test_case` - Record PASSED/FAILED result
- `skip_test_case` - Skip test with reason (supports --blocked flag)
- `bulk_record_results` - Bulk record results for multiple tests at once
- `add_test_note` - Add notes to a test
- `retest_test_case` - Requeue a test for another attempt
- `save_screenshot` - Save screenshot attachment (base64 or file_path)
- `save_clipboard_screenshot` - Read image from system clipboard and save as attachment (cross-platform)

**Run Discovery & Cleanup:**
- `list_active_runs` - List all non-terminal runs with progress summaries
- `cancel_all_active_runs` - Cancel all active runs at once (bulk cleanup)

**Data Tools:**
- `validate_tests` - Validate test files
- `rebuild_indexes` - Rebuild _index.json files
- `analyze_coverage_gaps` - Analyze test coverage
- `find_test_cases` - Cross-suite search and filter by query, priority, tags, component, automation
- `get_test_execution_history` - Per-test execution statistics (pass rate, last status, run count)
- `list_saved_selections` - List named selections from config with estimated test counts

**Reporting:**
- `get_run_history` - Get execution history with optional status/suite/limit filters and per-run summary counts
- `get_execution_summary` - Get summary statistics

### Bulk Operations

The `bulk_record_results` tool allows processing multiple tests at once:

```json
// Skip all remaining tests
{"status": "SKIPPED", "remaining": true, "reason": "Environment unavailable"}

// Pass all remaining tests
{"status": "PASSED", "remaining": true}

// Process specific test IDs
{"status": "FAILED", "test_ids": ["TC-001", "TC-002"], "reason": "API down"}
```

### Report Generation

Reports are generated in three formats:
- **JSON** - Machine-readable with all data
- **Markdown** - Human-readable summary
- **HTML** - Professional styled report with expandable test details

Report features:
- Test titles from `_index.json` (not just IDs)
- Human-readable durations ("1h 23m 45s")
- UTC-normalized timestamps (no negative durations)
- Expandable non-passing tests with failure reasons
- Status enums serialized as strings

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
