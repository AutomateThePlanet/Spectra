# Spectra Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-10

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

## Recent Changes
- 034-github-pages-docs: ✅ COMPLETE - GitHub Pages documentation site. Deployed existing `docs/` markdown files as a branded documentation site using Just the Docs Jekyll theme at `automatetheplanet.github.io/Spectra/`. Custom `spectra` color scheme matching ATP brand identity (dark navy sidebar #16213e, teal accents #00bcd4, white content area). Sidebar navigation with hierarchical grouping (User Guide, Architecture, Execution Agents, Deployment sections). Built-in client-side search across all pages. Landing page with quick install and feature overview. New `.github/workflows/docs.yml` (jekyll-build-pages → deploy-pages) auto-deploys on push to `main` when `docs/**` changes; one-time manual setting required (Settings → Pages → Source: GitHub Actions). "Edit this page" links on every page via `gh_edit_*` config. Excluded machine-generated files (`_index.md`, `_index.yaml`, `_index.json`, `criteria/`, `**/*.criteria.yaml`, `_criteria_index.yaml`) from site build. Added YAML frontmatter (`title`, `nav_order`, `parent`) to all 20 existing user-facing doc files in their actual subfolder locations — no files moved. Created 8 new files: `docs/_config.yml`, `docs/Gemfile`, `docs/_sass/color_schemes/spectra.scss`, `docs/index.md`, `docs/user-guide.md`, `docs/architecture.md`, `docs/execution-agents.md`, `docs/deployment.md`. Updated `README.md` with docs site badge and pointer. Documentation-only change — no C# code, no test changes.
- 033-from-description-chat-flow: ✅ COMPLETE - From-description chat flow & doc-aware manual tests. Updated `spectra-generate` SKILL with dedicated "When the user wants to create a specific test case" section (numbered 5-step sequence) and intent-routing table mapping topic-vs-scenario signals to `--focus`, `--from-description`, or `--from-suggestions`. Updated `spectra-generation` agent prompt with new "Test Creation Intent Routing" section (Intent 1: explore area → `--focus`; Intent 2: specific test → `--from-description`; Intent 3: from suggestions → `--from-suggestions`) and explicit "do NOT ask about count or scope" rule. Enhanced `UserDescribedGenerator` with new public static `BuildPrompt()` method (testable prompt construction) and optional `documentContext` / `criteriaContext` / `sourceRefPaths` parameters on `GenerateAsync()`. `GenerateHandler.ExecuteFromDescriptionAsync` now best-effort loads matching documentation (capped at 3 docs × 8000 chars via `SourceDocumentLoader`) and acceptance criteria (via existing `LoadCriteriaContextAsync`) before calling the generator — failures are swallowed (best-effort, non-blocking). Resulting tests get populated `source_refs` (from loaded doc paths) and `criteria` (from AI-matched IDs) when context is available; `grounding.verdict` remains `manual` regardless. New `FilterDocsForSuite` and `FormatDocContext` private helpers in `GenerateHandler`. New tests: `UserDescribedGeneratorTests` (9 prompt-builder tests) and `GenerateSkillContentTests` (10 SKILL/agent content tests). `GenerationAgent_LineCount` limit raised 100→140 to fit the new routing section. 19 new tests. 1453 total tests passing.
- 032-quickstart-skill-usage-guide: ✅ COMPLETE - Quickstart SKILL & USAGE.md offline guide. New `spectra-quickstart` SKILL (12th bundled SKILL) — workflow-oriented onboarding that responds to "help me get started", "tutorial", "walk me through" with 12 workflow walkthroughs and example conversations. Teaching-only (no CLI execution); delegates actual workflow execution to the corresponding workflow SKILLs. New `USAGE.md` bundled doc written to project root by `spectra init` (offline mirror of the quickstart SKILL, free of in-chat tool references). Both artifacts hash-tracked by the existing `update-skills` system. New `ProfileFormatLoader.LoadEmbeddedUsageGuide()` method. New `InitHandler.CreateUsageGuideAsync` (gated by `--skip-skills`). Generation and execution agent prompts gain a `**QUICKSTART**` delegation line directing onboarding intents to the new SKILL. Updated SKILL count test (11→12). 7 new tests (quickstart SKILL content, USAGE.md content + offline-clean assertions, init creates both files, --skip-skills skips both files, both agents reference quickstart). 1434 total tests passing.
- 030-prompt-templates: ✅ COMPLETE - Customizable root prompt templates. Introduced `.spectra/prompts/` directory with 5 markdown templates (behavior-analysis, test-generation, criteria-extraction, critic-verification, test-update) controlling all AI operations. Templates use `{{placeholder}}`, `{{#if}}`, `{{#each}}` syntax with built-in defaults as embedded resources. New `PlaceholderResolver`, `PromptTemplateParser`, `PromptTemplateLoader`, `BuiltInTemplates` in `Spectra.CLI/Prompts/`. Replaced hardcoded prompts in `BehaviorAnalyzer`, `CopilotGenerationAgent`, `CriteriaExtractor`, `CriticPromptBuilder` with template-driven approach (legacy fallback preserved). New `analysis.categories` config section with 6 default categories (happy_path, negative, edge_case, boundary, error_handling, security). New `spectra prompts list/show/reset/validate` CLI commands with JSON output. New `spectra-prompts` SKILL (11th bundled SKILL). Init creates `.spectra/prompts/` with defaults. `update-skills` tracks template hashes for safe updates. 65+ new tests. 1417 total tests passing.
- 029-spectra-update-skill: ✅ COMPLETE - Added spectra-update SKILL (10th bundled SKILL) for test update workflow via Copilot Chat. SKILL wraps `spectra ai update` with progress page, result file, classification breakdown (UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT). Agent delegation tables updated (both generation and execution agents delegate update requests to SKILL). Extended `UpdateResult` with `success`, `totalTests`, `testsFlagged`, `flaggedTests`, `duration` fields. Generation agent inline update section replaced with delegation row. 6 new tests (SKILL content, step format, do-NOTHING instruction, tools list, agent delegation). Documentation updated (SKILL count 9→10). Version 1.35.0.
- 028-coverage-criteria-fix: ✅ COMPLETE - Coverage semantics fix & criteria-generation pipeline. Fixed `TestCaseParser` to propagate `Criteria` field from frontmatter to `TestCase` (was missing). Wired criteria loading into `GenerateHandler`: loads per-doc `.criteria.yaml` files matching suite name and component, formats as context string, passes to `CopilotGenerationAgent.BuildFullPrompt()` via new `criteriaContext` parameter on `GenerateTestsAsync()`. `TestFileWriter` now always writes `criteria: []` field (even when empty) for visibility. Audit confirmed coverage analyzers already correct: `DocumentationCoverageAnalyzer` checks test existence via `source_refs`, `AcceptanceCriteriaCoverageAnalyzer` reads both `criteria` and legacy `requirements` fields. 4 new regression tests. 1354 total tests passing.
- 027-skill-agent-dedup: ✅ COMPLETE - SKILL/Agent prompt deduplication. Refactored both agents to delegate CLI tasks to authoritative SKILL files instead of duplicating instructions. Execution agent reduced from ~400 to 120 lines, generation agent from ~219 to 81 lines. Agents now contain delegation tables pointing to `spectra-*` SKILLs for dashboard, coverage, criteria, validate, list, and docs index tasks. Fixed SKILL inconsistencies: replaced "Tool call N" with "Step N" format in spectra-list, spectra-init-profile, spectra-validate; added `--no-interaction --output-format json --verbosity quiet` flags to spectra-list and spectra-init-profile; added "do NOTHING between runInTerminal and awaitTerminal" instruction to spectra-coverage and spectra-dashboard; added incremental vs force note to spectra-docs. Extended spectra-help SKILL with acceptance criteria and documentation index sections. Removed redundant "NEVER" warnings from execution agent (kept askQuestion and fabrication warnings). 6 new tests (agent line count limits, no CLI block duplication, step format consistency, no terminalLastCommand, help completeness). 1350 total tests passing.
- 026-criteria-folder-coverage-fix: ✅ COMPLETE - Criteria folder rename, index exclusion & coverage fix. Renamed default criteria directory from `docs/requirements/` to `docs/criteria/` across all config defaults, init handler, update handler fallbacks, and documentation. Auto-migration logic in `AnalyzeHandler.MigrateCriteriaFolderAsync()` renames `docs/requirements/` → `docs/criteria/` on first run, updates `spectra.config.json` paths, and deletes stale `_index.criteria.yaml`. Added `ShouldSkipDocument()` filter to exclude `_index.md`, `_index.yaml`, `_index.json`, `.criteria.yaml`, and `_criteria_index.yaml` files from criteria extraction (prevents duplicate criteria from metadata files). Fixed dashboard acceptance criteria coverage: `DataCollector` now uses `AcceptanceCriteriaCoverageAnalyzer` with per-document `.criteria.yaml` file enumeration instead of legacy `AcceptanceCriteriaParser`, and reads both `Requirements` and `Criteria` fields from test index entries. Fixed `app.js` unit label from `acceptance_criteria` to `criteria` for proper grammar. Updated empty-state message to guide users to `spectra ai analyze --extract-criteria`. Updated all documentation (CLAUDE.md, PROJECT-KNOWLEDGE.md, cli-reference.md, coverage.md, getting-started.md, configuration.md). Version 1.32.0. 29 new tests (10 document skip tests, 7 coverage analyzer tests, 5 migration tests, 3 config default tests, 4 terminology audit updates). 1344 total tests passing.
- 025-universal-skill-progress: ✅ COMPLETE - Universal progress/result for all SKILL-wrapped commands. New shared `ProgressManager` service (`src/Spectra.CLI/Progress/ProgressManager.cs`) extracts duplicated progress/result file logic from GenerateHandler and DocsIndexHandler. New `ProgressPhases` static class defines phase sequences for 6 command types. All 9 SKILL-wrapped commands now write `.spectra-result.json` on completion. 6 long-running commands (generate, update, docs-index, coverage, extract-criteria, dashboard) write `.spectra-progress.html` with auto-refreshing phase stepper. `ProgressPageWriter` extended with phase configs for update (classifying/updating/verifying), coverage (scanning-tests/analyzing-docs/analyzing-criteria/analyzing-automation), extract-criteria (scanning-docs/extracting/building-index), and dashboard (collecting-data/generating-html). Dynamic title support in progress page header ("SPECTRA — Coverage Analysis", etc.). Summary card renderers for coverage percentages, update counts, criteria counts, dashboard stats. Renamed `AnalyzeCoverageResult.Requirements` → `AcceptanceCriteria` (JSON: `acceptanceCriteria`). New `UpdateResult` model with classification counts. Updated all SKILL files (coverage, criteria, dashboard, validate) with universal 5-step progress flow. Updated generation agent prompt with progress/result instructions for all commands. 36 new tests (19 ProgressManager unit tests, 9 ProgressPageWriter phase/title tests, 5 SKILL flag verification tests, 3 terminology audit tests). 1315 total tests passing.
- 024-docs-skill-coverage-fix: ✅ COMPLETE - Docs index SKILL integration, progress page, coverage fix & terminology rename. Added `.spectra-result.json` and `.spectra-progress.html` to `DocsIndexHandler` (same pattern as generate). New `--skip-criteria` flag skips auto-triggered acceptance criteria extraction. `--no-interaction` passthrough to criteria extraction (runs with defaults). Extended `DocsIndexResult` with new fields (skipped, new, total, criteria). Extended `ProgressPageWriter` for docs-index phases (scanning, indexing, extracting-criteria). New `spectra-docs` SKILL (9th bundled SKILL) with structured tool-call-sequence. Completed "requirements" → "acceptance criteria" rename in all remaining user-facing strings. `CriteriaIndexReader` auto-renames legacy `_requirements.yaml` to `.bak`. Dashboard coverage null-crash fix with zero-state defaults. Updated generation agent prompt for docs index progress-aware flow. Version 1.30.0. 1279 total tests passing.
- 023-criteria-extraction-overhaul: ✅ COMPLETE - Acceptance criteria import & extraction overhaul. Renamed "requirements" to "acceptance criteria" across codebase (CLI, dashboard, reports, SKILLs, agents). Replaced single-prompt AI extraction (truncation-prone) with per-document iterative extraction producing individual `.criteria.yaml` files and `_criteria_index.yaml` master index. SHA-256 incremental extraction skips unchanged docs. Import from YAML/CSV/JSON with auto-column-detection for Jira/ADO exports, AI splitting of compound criteria, RFC 2119 normalization. `--merge` (default) and `--replace` modes. `--list-criteria` with `--source-type`, `--component`, `--priority` filters. Generation auto-loads related criteria as prompt context, produces `criteria` field in test frontmatter. Update flow detects criteria changes (OUTDATED/ORPHANED classification). Dashboard shows per-source-type coverage breakdown. New `spectra-criteria` SKILL (8th). `--extract-requirements` kept as hidden alias. New models: `AcceptanceCriterion`, `CriteriaIndex`, `CriteriaSource`, `AcceptanceCriteriaCoverage`. New services: `CriteriaExtractor`, `CriteriaIndexReader/Writer`, `CriteriaFileReader/Writer`, `CsvCriteriaImporter`, `JsonCriteriaImporter`, `CriteriaMerger`. Live progress HTML page (`.spectra-progress.html`) with auto-refresh for generation status. SKILL/agent names standardized to lowercase-hyphenated format. `browser/openBrowserPage` added to all tool lists. BehaviorAnalyzer retry on failure. MCP `finalize_execution_run` returns instruction to open HTML report. `--no-interaction` on all SKILL CLI commands. `--focus` flag for filtered generation. 38+ new tests, 1280+ total passing.
- 023-copilot-chat-integration: ✅ COMPLETE - Full VS Code Copilot Chat integration for test generation and all CLI features. Added `--suite` option (flag alternative to positional arg) for LLM-friendly syntax. Added `--analyze-only` flag for two-step analyze→approve→generate flow. Automatic batch generation for large counts (batches of 30, writes per batch, index updates per batch). Live progress via `.spectra-result.json` with status lifecycle: analyzing→analyzed→generating→completed/failed. BehaviorAnalyzer timeout increased 30s→2min with proper error handling (was silently failing). 7 bundled SKILLs (generate, coverage, dashboard, validate, list, init-profile, help) with explicit tool-call-sequence format. Both agents (Generation, Execution) handle all SPECTRA commands with help reference. Progress messages from AI tool calls (scanning docs, reading docs, allocating IDs, verifying tests). Per-test verification progress. Critic verification per batch. Smart fallback count accounting for existing tests. Result file with `FileStream.Flush(true)` for Windows NTFS reliability. 1241 total passing.
- 022-bundled-skills: ✅ COMPLETE - Bundled SKILL files and agent prompts for Copilot Chat integration. 6 SKILL files (generate, coverage, dashboard, validate, list, init-profile) and 2 agent prompts (execution, generation) created by `spectra init` in `.github/skills/` and `.github/agents/`. Each SKILL contains CLI commands with `--output-format json --verbosity quiet`, JSON parsing instructions, and example user requests. New `SkillContent`, `AgentContent` static classes with bundled content. New `SkillsManifest` with SHA-256 hash tracking for safe updates. New `spectra update-skills` command (updates unmodified files, skips user-customized). New `--skip-skills` flag on init. New `FileHasher` utility. 10 new tests, 1241 total passing.
- 021-generation-session: ✅ COMPLETE - Four-phase generation session flow (Analysis → Generation → Suggestions → User-Described). Session state persisted in `.spectra/session.json` (1-hour TTL). New `Session/` directory: `GenerationSessionState`, `SessionStore`, `SuggestionBuilder`, `SessionSummary`. New `UserDescribedGenerator` creates tests from plain-language descriptions with `grounding.verdict: manual`. New `DuplicateDetector` with Levenshtein similarity (80% threshold). New `SuggestionPresenter` for interactive suggestions menu. New CLI flags: `--from-suggestions [indices]`, `--from-description`, `--context`, `--auto-complete`. Interactive mode loops Phases 3-4 until user exits with session summary. 20 new tests, 1231 total passing.
- 020-cli-non-interactive: ✅ COMPLETE - CLI non-interactive mode and structured JSON output for SKILL/CI workflows. New global options: `--output-format` (human/json) and `--no-interaction` on all commands. New `OutputFormat` enum, `ExitCodes.MissingArguments` (exit code 3). New `Results/` directory with typed result models per command (`GenerateResult`, `AnalyzeCoverageResult`, `ValidateResult`, `DashboardResult`, `ListResult`, `ShowResult`, `InitResult`, `DocsIndexResult`, `ErrorResult`). New `JsonResultWriter` serializes any result to stdout as JSON (camelCase, enums as strings, null omission). All presenters (`ProgressReporter`, `ResultPresenter`, `VerificationPresenter`, `AnalysisPresenter`, `NextStepHints`) suppress output when `OutputFormat.Json`. All 12 command handlers updated. Standardized exit codes: 0 (success), 1 (error), 2 (validation), 3 (missing args). 7 new tests, 1211 total passing.
- 017-mcp-tool-resilience: ✅ COMPLETE - MCP tool resilience for weaker models (GPT-4.1, GPT-4o). All tools accepting `run_id` (13 tools) and `test_handle` (6 tools) now auto-resolve when parameters are omitted: single active run or single in-progress test is used automatically; 0 or 2+ returns descriptive error with listings. New shared `ActiveRunResolver` helper. New tools: `list_active_runs` (returns all non-terminal runs with progress summaries), `cancel_all_active_runs` (bulk cancel with per-run status reporting). Enhanced `get_run_history` with `status` filter and per-run pass/fail/skip summary counts. New `GetActiveRunsAsync` and `GetInProgressTestsAsync` repository methods. 26 new tests, 1148 total passing.
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
