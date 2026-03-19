# Spectra Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-19

## Active Technologies
- C# 12, .NET 8+ + ASP.NET Core (MCP server), Microsoft.Data.Sqlite (state storage), System.Text.Json (serialization) (002-mcp-execution-server)
- SQLite database (`.execution/spectra.db`) for execution state; file system for reports (002-mcp-execution-server)
- C# 12, .NET 8+ (CLI and coverage analysis); HTML/CSS/JS (dashboard output) + Spectra.Core (parsing, indexes), Spectra.CLI (command integration), System.Text.Json, Microsoft.Data.Sqlite (reading .execution DB) (003-dashboard-coverage-analysis)
- Reads from `tests/*/_index.json`, `reports/*.json`, `.execution/spectra.db`; Writes to output directory (static files) (003-dashboard-coverage-analysis)
- C# 12, .NET 8+ + Spectra.CLI (command integration), Spectra.Core (config, parsing), System.CommandLine (interactive prompts) (004-test-generation-profile)
- File-based (spectra.profile.md at repo root, _profile.md in suites) (004-test-generation-profile)
- C# 12, .NET 8+ + System.CommandLine (CLI), Spectra.Core (parsing, indexes), Spectra.CLI.Review (terminal UI), Microsoft.Extensions.AI (AI agents) (006-interactive-generation)
- File-based (tests/, docs/, spectra.config.json, _index.json) (006-interactive-generation)
- C# 12, .NET 8+ + System.CommandLine (CLI), Spectre.Console (terminal UX), Microsoft.Extensions.AI (AI tools) (006-conversational-generation)
- File system (tests/{suite}/*.md), JSON indexes (_index.json) (006-conversational-generation)
- C# 12, .NET 8+ + Spectra.Core (parsing, validation, indexing), Spectra.MCP (tool registry, protocol), System.Text.Json, System.CommandLine (007-execution-agent-mcp-tools)
- File system (Markdown test files, JSON indexes), embedded resources for bundled agent prompts (007-execution-agent-mcp-tools)
- C# 12, .NET 8+ + ICriticRuntime (multi-provider), Microsoft.Extensions.AI, System.Text.Json (008-grounding-verification)
- Dual-model verification: Generator (Claude/GPT) + Critic (Gemini Flash/GPT-4o-mini) (008-grounding-verification)

- C# 12, .NET 8+ + System.CommandLine (CLI), Microsoft.Extensions.AI (AI tools), Markdig (Markdown parsing), YamlDotNet (frontmatter), GitHub Copilot SDK (AI runtime) (001-ai-test-generation-cli)

## Project Structure

```text
src/
├── Spectra.CLI/              # .NET CLI application
│   ├── Commands/             # Command handlers
│   │   ├── Analyze/          # Coverage analysis command
│   │   ├── Dashboard/        # Dashboard generation command
│   │   ├── Generate/         # Test generation (direct + interactive modes)
│   │   └── Update/           # Test update (direct + interactive modes)
│   ├── Agent/                # AI provider integration (GitHub Models, OpenAI, Anthropic)
│   │   └── Critic/           # Grounding verification (ICriticRuntime implementations)
│   ├── Source/               # Document map builder
│   ├── Index/                # _index.json operations
│   ├── Validation/           # Test validation, dedup
│   ├── Review/               # Interactive terminal UI
│   ├── Interactive/          # Interactive mode components (selectors, session)
│   ├── Output/               # Progress reporters, result presenters
│   ├── Classification/       # Test classification (update flow)
│   ├── Coverage/             # Gap analysis and coverage reporting
│   ├── Profile/              # Generation profile loading
│   ├── Config/               # Configuration loader
│   ├── Dashboard/            # Dashboard data collection and generation
│   └── IO/                   # File writers
├── Spectra.Core/             # Shared library
│   ├── Models/               # TestCase, Suite, Config models
│   │   ├── Dashboard/        # DashboardData, SuiteStats, TestEntry, etc.
│   │   ├── Coverage/         # CoverageReport, CoverageLink, etc.
│   │   ├── Execution/        # Run, TestResult, ExecutionReport, McpToolResponse
│   │   └── Grounding/        # GroundingMetadata, VerificationVerdict, VerificationResult
│   ├── Coverage/             # AutomationScanner, LinkReconciler, CoverageCalculator
│   ├── Storage/              # ExecutionDbReader
│   ├── Parsing/              # Markdown + YAML parser
│   ├── Validation/           # Schema validation
│   ├── Update/               # TestClassifier for test updates
│   └── Index/                # Index read/write
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
│   └── coverage-map.js       # D3.js coverage visualization
├── functions/                # Cloudflare Pages functions (auth)
│   ├── _middleware.js        # OAuth middleware
│   └── auth/callback.js      # OAuth callback handler
└── access-denied.html        # Auth error page

tests/
├── Spectra.Core.Tests/       # Unit tests (284 tests)
│   └── Coverage/             # AutomationScanner, LinkReconciler, Calculator tests
├── Spectra.CLI.Tests/        # Integration tests (327 tests)
│   ├── Dashboard/            # DataCollector, Generator tests
│   └── Coverage/             # CoverageReportWriter tests
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

# Dashboard Generation (003)
spectra dashboard --output ./site
spectra dashboard --output ./site --title "My Dashboard"
spectra dashboard --dry-run  # Preview without generating

# Coverage Analysis (003)
spectra ai analyze --coverage
spectra ai analyze --coverage --format json --output coverage.json
spectra ai analyze --coverage --format markdown --output coverage.md
spectra ai analyze --coverage --verbosity detailed
```

## Code Style

- **Language:** C# 12, .NET 8+
- **Naming:** PascalCase for types/methods, camelCase for locals
- **Async:** All I/O operations are async with `Async` suffix
- **Nullability:** Nullable reference types enabled
- **Tests:** xUnit with structured results (never throw on validation errors)

## Recent Changes
- 008-grounding-verification: ✅ COMPLETE - Dual-model critic flow (generator + verifier), three verdicts (grounded/partial/hallucinated), grounding metadata in YAML frontmatter, configurable critic provider (Google/OpenAI/Anthropic/GitHub), --skip-critic flag
- 007-execution-agent-mcp-tools: Added C# 12, .NET 8+ + Spectra.Core (parsing, validation, indexing), Spectra.MCP (tool registry, protocol), System.Text.Json, System.CommandLine
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
