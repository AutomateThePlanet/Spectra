# Spectra Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-18

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
│   │   └── Coverage/         # CoverageReport, CoverageLink, etc.
│   ├── Coverage/             # AutomationScanner, LinkReconciler, CoverageCalculator
│   ├── Storage/              # ExecutionDbReader
│   ├── Parsing/              # Markdown + YAML parser
│   ├── Validation/           # Schema validation
│   ├── Update/               # TestClassifier for test updates
│   └── Index/                # Index read/write
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
├── Spectra.Core.Tests/       # Unit tests
│   └── Coverage/             # AutomationScanner, LinkReconciler, Calculator tests
├── Spectra.CLI.Tests/        # Integration tests
│   ├── Dashboard/            # DataCollector, Generator tests
│   └── Coverage/             # CoverageReportWriter tests
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
- 007-execution-agent-mcp-tools: Added C# 12, .NET 8+ + Spectra.Core (parsing, validation, indexing), Spectra.MCP (tool registry, protocol), System.Text.Json, System.CommandLine
- 006-conversational-generation: ✅ COMPLETE - Two-mode test generation (Direct/Interactive), test updates with classification (UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT), rich terminal UX with Spectre.Console
- 004-test-generation-profile: Added profile support for test generation settings


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
