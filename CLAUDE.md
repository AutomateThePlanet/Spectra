# Spectra Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-20

## Active Technologies
- C# 12, .NET 8+ + GitHub Copilot SDK (sole AI runtime for generation and verification)
- System.CommandLine (CLI), Spectre.Console (terminal UX), System.Text.Json (serialization)
- ASP.NET Core (MCP server), Microsoft.Data.Sqlite (state storage)
- SQLite database (`.execution/spectra.db`) for execution state; file system for reports
- File-based (tests/, docs/, spectra.config.json, _index.json, _index.md, profiles)
- Dual-model verification: Generator (any provider) + Critic (any provider) via Copilot SDK
- Document index (`docs/_index.md`) for pre-built documentation metadata with incremental updates

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
│   ├── Validation/           # Test validation, dedup
│   ├── Review/               # Interactive terminal UI
│   ├── Interactive/          # Interactive mode components (selectors, session)
│   ├── Output/               # Progress reporters, result presenters, NextStepHints
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
├── Spectra.Core.Tests/       # Unit tests (349 tests)
│   ├── Coverage/             # AutomationScanner, LinkReconciler, Calculator, DocCoverageAnalyzer, ReqCoverageAnalyzer, AutoLinkService tests
│   ├── Index/                # DocumentIndexReader, DocumentIndexWriter tests
│   └── Parsing/              # DocumentIndexExtractor, RequirementsParser, FrontmatterUpdater tests
├── Spectra.CLI.Tests/        # Integration tests (329 tests)
│   ├── Commands/             # DocsIndexCommand tests
│   ├── Dashboard/            # DataCollector, Generator tests
│   ├── Source/               # DocumentIndexService tests
│   └── Coverage/             # CoverageReportWriter (unified), CoverageAnalysis tests
├── Spectra.MCP.Tests/        # MCP server tests (306 tests)
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

# Test Generation (006-conversational-generation)
spectra ai generate                              # Interactive mode (guided prompts)
spectra ai generate checkout                     # Direct mode (specific suite)
spectra ai generate checkout --focus "negative"  # Direct mode with focus
spectra ai generate checkout --no-interaction    # CI mode (no prompts, exit codes)
spectra ai generate --dry-run                    # Preview without writing
spectra ai generate checkout --skip-critic       # Skip grounding verification (008)

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

# Documentation Index (010-document-index)
spectra docs index                               # Incremental update (only changed files)
spectra docs index --force                       # Full rebuild

# Coverage Analysis (003 + unified coverage overhaul)
spectra ai analyze --coverage                                    # Unified three-section report (doc, req, auto)
spectra ai analyze --coverage --format json --output coverage.json
spectra ai analyze --coverage --format markdown --output coverage.md
spectra ai analyze --coverage --auto-link                        # Write automated_by back into test files
spectra ai analyze --coverage --verbosity detailed
spectra ai analyze --extract-requirements            # Extract requirements from docs
spectra ai analyze --extract-requirements --dry-run  # Preview without writing

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

## Recent Changes
- 015-auto-requirements-extraction: ✅ COMPLETE - AI-powered extraction of testable requirements from documentation. New models: `ExtractionResult`, `DuplicateMatch`. New service: `RequirementsWriter` handles YAML merge, duplicate detection (normalized title + substring matching), sequential ID allocation (REQ-NNN, never reuse gaps), atomic writes. New `RequirementsExtractor` uses Copilot SDK to extract requirements with RFC 2119 priority inference. CLI: `spectra ai analyze --extract-requirements [--dry-run]`. 11 new RequirementsWriter tests.
- 014-open-source-ready: ✅ COMPLETE - Open source readiness. README redesign with banner placeholder, shields.io badges, value props, feature showcase, quickstart. CI pipeline (`.github/workflows/ci.yml`) — build+test on push/PR. NuGet publish pipeline (`.github/workflows/publish.yml`) — tag-triggered pack+push for Spectra.CLI and Spectra.MCP. All 1071 tests passing (fixed parallel test isolation with `[Collection("WorkingDirectory")]`). GitHub issue templates (bug report, feature request), PR template, Dependabot config. All README doc links verified.
- 013-cli-ux-improvements: ✅ COMPLETE - CLI UX improvements for discoverability and workflow. New `NextStepHints` helper prints context-aware next-step suggestions after every command (init, generate, analyze, dashboard, validate, docs index, index) in dimmed text, suppressed by `--quiet` or piped output. Init flow: new interactive prompts for automation directory setup (`coverage.automation_dirs`) and critic model configuration (`ai.critic`). New config subcommands: `spectra config add-automation-dir`, `remove-automation-dir`, `list-automation-dirs`. Interactive generation mode: continuation menu after suite completion (generate more, switch suite, create suite, exit) with session summary. 18 new tests.
- 012-dashboard-branding: ✅ COMPLETE - Dashboard branding and theming customization. New models: `BrandingConfig`, `ColorPaletteConfig` in `DashboardConfig`. New service: `BrandingInjector` handles company name, logo, favicon, CSS variable overrides, dark theme, and custom CSS injection via template placeholders. `SampleDataFactory` provides mock data for `--preview` mode. Light/dark theme presets via CSS custom properties. Config: `dashboard.branding` section in spectra.config.json with `company_name`, `logo`, `favicon`, `theme`, `colors`, `custom_css`. CLI: `spectra dashboard --preview` for branding verification. 51 new tests (8 config + 21 injector + 9 sample data + 13 existing generator).
- 010-smart-test-selection: ✅ COMPLETE - Cross-suite test search and filtering via `find_test_cases` MCP tool (free-text query, priority/tag/component/automation filters, AND between types, OR within arrays). Extended `start_execution_run` with `test_ids` (custom test ID list) and `selection` (saved selection by name) modes alongside existing `suite` mode. New `get_test_execution_history` tool for per-test execution statistics (pass rate, last status, total runs). New `list_saved_selections` tool reads named selections from `spectra.config.json`. Added `description` field to TestCaseFrontmatter/TestCase/TestIndexEntry. Index writer now populates description, estimated_duration, automated_by, requirements fields. Default config includes "smoke" saved selection. Agent prompt updated with smart selection workflow and risk-based recommendations.
- 009-coverage-dashboard-viz: ✅ COMPLETE - Enhanced dashboard coverage visualizations. Typed detail models replacing `IReadOnlyList<object>` (`DocumentationSectionData`, `RequirementsSectionData`, `AutomationSectionData` with per-item detail classes). `DataCollector` populates detail lists for all three sections. Dashboard: expandable progress bar drill-down with per-item breakdown, donut chart (SVG, automated/manual/unlinked distribution), D3.js treemap (suites sized by test count, colored by automation %), empty state guidance with setup instructions. CSS transitions for expand/collapse animations.
- 011-coverage-overhaul: ✅ COMPLETE - Unified three-type coverage system (Documentation, Requirements, Automation) in one report. Added `automated_by` and `requirements` fields to TestCase/TestCaseFrontmatter/TestIndexEntry. New models: `UnifiedCoverageReport`, `DocumentationCoverage`, `RequirementsCoverage`, `AutomationCoverage`, `RequirementDefinition`. New services: `DocumentationCoverageAnalyzer`, `RequirementsCoverageAnalyzer`, `UnifiedCoverageBuilder`, `AutoLinkService`, `RequirementsParser`, `FrontmatterUpdater`. New config: `scan_patterns`, `file_extensions`, `requirements_file` in CoverageConfig. CLI: `--auto-link` flag writes `automated_by` back into test files. Dashboard: three stacked coverage sections with progress bars. `spectra init` creates `docs/requirements/_requirements.yaml` template.
- 010-document-index: ✅ COMPLETE - Persistent `docs/_index.md` with per-document metadata (sections, entities, tokens, content hashes). Incremental updates via SHA-256 hashing. `spectra docs index [--force]` CLI command. Auto-refresh before `ai generate`. `GetDocumentMapTool` prefers index when available. Models: `DocumentIndex`, `DocumentIndexEntry`, `SectionSummary`. Services: `DocumentIndexExtractor`, `DocumentIndexWriter`, `DocumentIndexReader`, `DocumentIndexService`.
- 009-copilot-sdk-consolidation: ✅ COMPLETE - Unified all AI operations under GitHub Copilot SDK as the sole runtime. Removed legacy agent implementations (OpenAiAgent, AnthropicAgent, GitHubModelsAgent, MockAgent) and critic implementations (GoogleCritic, OpenAiCritic, AnthropicCritic, GitHubCritic). Provider selection now via spectra.config.json with CopilotGenerationAgent and CopilotCritic handling all providers.
- 008-grounding-verification: ✅ COMPLETE - Dual-model critic flow (generator + verifier), three verdicts (grounded/partial/hallucinated), grounding metadata in YAML frontmatter, configurable critic provider, --skip-critic flag
- 007-execution-agent-mcp-tools: Added MCP tools for execution agents
- 006-conversational-generation: ✅ COMPLETE - Two-mode test generation (Direct/Interactive), test updates with classification (UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT), rich terminal UX with Spectre.Console
- 004-test-generation-profile: Added profile support for test generation settings

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
- `save_screenshot` - Save screenshot attachment

**Data Tools:**
- `validate_tests` - Validate test files
- `rebuild_indexes` - Rebuild _index.json files
- `analyze_coverage_gaps` - Analyze test coverage
- `find_test_cases` - Cross-suite search and filter by query, priority, tags, component, automation
- `get_test_execution_history` - Per-test execution statistics (pass rate, last status, run count)
- `list_saved_selections` - List named selections from config with estimated test counts

**Reporting:**
- `get_run_history` - Get execution history
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
