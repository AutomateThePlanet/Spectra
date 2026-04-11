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
- v1.48.0 (Spec 043): parallel critic verification + dedicated error log. New `ai.critic.max_concurrent` (default 1, max 20) parallelizes the critic loop in `GenerateHandler.VerifyTestsAsync` via `SemaphoreSlim` + `Task.WhenAll` with results collected into a pre-sized array indexed by original position (sequential path is preserved when `max_concurrent <= 1`, byte-identical to pre-spec behavior). `max_concurrent: 5` cuts critic phase to ~1/5 of sequential time on a 200-test suite. New `.spectra-errors.log` file (path via `debug.error_log_file`, default `.spectra-errors.log`) captures full exception type/message/response body (truncated 500 chars)/`Retry-After`/stack trace at every catch site (analyze/generate/critic/criteria/update); created lazily on first error so clean runs leave no file. New `ErrorLogger` static class at `src/Spectra.CLI/Infrastructure/ErrorLogger.cs` (mirrors `DebugLogger`, thread-safe via single lock, graceful stderr-warning fallback on file failure with `IsRateLimit` classifier for HTTP 429 / message-substring fallbacks). New `RunErrorTracker` instance class at `src/Spectra.CLI/Services/RunErrorTracker.cs` (thread-safe `Errors` + `RateLimits` counters via `Interlocked`). Run Summary panel gains `Critic concurrency`, `Errors`, `Rate limits` rows (with `(consider reducing ai.critic.max_concurrent)` hint when rate-limited). `RUN TOTAL` debug log line always ends with ` rate_limits=<n> errors=<n>`. `CopilotCritic.VerifyTestAsync` classifies rate limits separately (`CRITIC RATE_LIMITED` vs. `CRITIC ERROR`). `CriticFactory.TryCreate`/`AgentFactory.CreateAgentAsync`/`BehaviorAnalyzer`/`CopilotGenerationAgent` all gained an optional `RunErrorTracker` parameter. Values >10 emit a one-line stderr warning at run start about rate-limit risk; >20 clamp to 20 with a warning. +14 tests (`CriticConfigClampTests`, `ErrorLoggerTests`, `RunErrorTrackerTests`, expanded `RunSummaryDebugFormatterTests`).
- v1.46.1: removed the bogus 3-second TESTIMIZE UNHEALTHY grace window. The Copilot CLI's MCP client takes ~60s to attempt + time out an initialize handshake against a non-spec-compliant server, so the 3s wait always fired a false UNHEALTHY before the real SessionMcpServersLoadedEvent came through. Fix: trust the SDK event entirely — when it fires (success or failure), log the real status with the real error message. Companion fix in `Testimize.MCP.Server` (separate repo): the server was emitting JSON-RPC responses with both `"result":{...}` AND `"error":null`, which is a JSON-RPC 2.0 spec violation — the Copilot CLI's MCP client correctly rejected these as malformed and timed out with `MCP error -32001: Request timed out`. Updated `Testimize.MCP.Server/Program.cs` to serialize only the applicable field per spec. Requires rebuild + reinstall of testimize-mcp.
- v1.46.0: Testimize integration migrated to the Copilot SDK's native `SessionConfig.McpServers` API. The SDK owns spawn, initialize handshake, framing, tool discovery, and lifecycle — deleted the custom `TestimizeMcpClient` (which had a broken Content-Length framing assumption that deadlocked against testimize-mcp's newline-delimited protocol) and `TestimizeTools.CreateGenerateTestDataTool` wrapper. New `McpConfigBuilder.BuildTestimizeServer` helper translates `TestimizeConfig` → `McpLocalServerConfig`. Generation agent subscribes to `SessionMcpServersLoadedEvent` / `SessionMcpServerStatusChangedEvent` and logs `TESTIMIZE CONFIGURED / LOADED / STATUS_CHANGED / DISPOSED` lines with the SDK's status enum (`Connected / Failed / NeedsAuth / Pending / Disabled / NotConfigured`). 3-second defensive grace window before falling back. `{{testimize_strategy}}` placeholder added to `test-generation.md` so the AI knows to prefer `testimize/generate_hybrid_test_cases` (default) or `testimize/generate_pairwise_test_cases`. `AnalyzeFieldSpec` moved to new `FieldSpecAnalysisTools.cs` (no behavior change). User-facing `testimize.*` config schema is unchanged.
- v1.45.2: run header block at the start of every `.spectra-debug.log` run (`=== SPECTRA v<version> | <command line> | <UTC timestamp> ===` preceded by a 62-char horizontal separator). New `debug.mode` config (`append` default / `overwrite`) controls whether the file is truncated or prepended at run start. New `DebugLogger.BeginRun()` wired from `GenerateHandler` and `UpdateHandler`. Version pulled from `AssemblyInformationalVersionAttribute`, command line from `Environment.GetCommandLineArgs()`. **Deleted** `.spectra-debug-analysis.txt` and `.spectra-debug-response.txt` writers — all debug output now lives in `.spectra-debug.log` only. `SaveDebugResponse` method removed from `GenerationAgent`. Testimize lifecycle lines survive the overwrite-mode truncate (regression-tested).
- v1.45.1 (Spec 040 follow-up): real token counts at every AI call site via `AssistantUsageEvent` subscription through new `CopilotUsageObserver`. Falls back to `text.Length / 4` via new `TokenEstimator` when the usage event misses the 200ms grace window. Fallback values are flagged with `~` prefix in debug log (`~tokens_in=` / `~tokens_out=`) and `"estimated": true` on `token_usage` in JSON. New `RUN TOTAL` summary line emitted to `.spectra-debug.log` at the end of every generate/update run via `RunSummaryDebugFormatter` — format `RUN TOTAL command=… suite=… calls=… tokens_in=… tokens_out=… elapsed=… phases=analysis:1/18.5s,generation:3/52.3s,critic:20/1m34s`. Discovered during implementation: `AssistantUsageData.InputTokens`/`OutputTokens` are actually `double?` in the SDK assembly (XML doc said `int`) — cast via `(int)(v ?? 0)`.
- v1.45.0 (Spec 041): default models updated to `gpt-4.1` generator + `gpt-5-mini` critic (both 0× on any paid Copilot plan; cross-architecture for independent verification). New **AI Model Preset** step in `spectra init -i` offers four curated pairings (GPT-4.1+GPT-5 mini / Sonnet 4.5+GPT-4.1 / GPT-4.1+Haiku 4.5 / Custom) that rewrite both `ai.providers[0]` and `ai.critic` in a single step. `Templates/spectra.config.json` is now an EmbeddedResource. `CriticConfig.GetEffectiveModel` / `CopilotService.GetCriticModel` / `CopilotCritic.GetEffectiveModel` / `SpectraConfig.Default` all updated. Existing configs keep working — change only affects fresh `spectra init` runs.
- v1.44.0 (Spec 040): token usage tracking + Run Summary panel after every `generate`/`update` run (per-phase table, model/provider, elapsed, estimated USD cost for BYOK providers; `github-models` → "Included in Copilot plan"). `run_summary` + `token_usage` fields added to JSON output and `.spectra-result.json`; progress page gains token card grid. New `TokenUsageTracker` + `CostEstimator` services. Debug log line format: every AI line now ends with `model=<name> provider=<name> tokens_in=<n|?> tokens_out=<n|?>`. `.spectra-debug.log` is now **opt-in** via new top-level `debug.enabled` config (default `false`); `--verbosity diagnostic` force-enables for one run. Removed obsolete `ai.debug_log_enabled`. `TokenUsage` record moved to `Spectra.Core.Models` with `PromptTokens`/`CompletionTokens`.
- v1.43.0: tolerant analysis JSON parser (recovers from truncated reasoning-model output); shared `DebugLogger` writes `CRITIC START/OK/TIMEOUT/ERROR` and `TESTIMIZE` lifecycle lines to `.spectra-debug.log`; critic now honors `critic.timeout_seconds` (default bumped 30→120); progress page polls terminal pages every 5s for fresh-run detection; better `analysis_failed` error message.
- v1.42.0: configurable `ai.analysis_timeout_minutes` (default 2). Analyze-only path returns `status: "analysis_failed"` with explanatory message instead of fake `analyzed`/15-recommended. `[analyze]` lines added to `.spectra-debug.log`. `spectra-generate` SKILL handles `analysis_failed` status.
- v1.41.0: configurable per-batch generation timeout/batch size (`ai.generation_timeout_minutes` default 5, `ai.generation_batch_size` default 30, `ai.debug_log_enabled` default true). Per-batch `BATCH START/OK/TIMEOUT` lines in `.spectra-debug.log`. Improved timeout error with remediation snippets.
- 039-unify-critic-providers: critic provider list aligned with generator (canonical 5: github-models, azure-openai, azure-anthropic, openai, anthropic). `github`→`github-models` legacy alias with stderr warning; `google` is now hard error. New `DefaultProvider` constant; case-insensitive resolution.
- 038-testimize-integration: optional Testimize.MCP.Server integration for algorithmic test data optimization (BVA/EP/pairwise/ABC). Disabled by default. New `testimize` config section, `TestimizeMcpClient`, two AI tools, `spectra testimize check` command. Graceful degradation everywhere.
- 037-istqb-test-techniques: ISTQB black-box techniques (EP, BVA, DT, ST, EG, UC) added to all 5 prompt templates. New `IdentifiedBehavior.Technique` field, `BehaviorAnalysisResult.TechniqueBreakdown` map, `AcceptanceCriterion.TechniqueHint`. Technique breakdown rendered in analysis presenter and progress page.
- 034-github-pages-docs: GitHub Pages docs site (Just the Docs theme) at `automatetheplanet.github.io/Spectra/`. Auto-deploys on push to main via `.github/workflows/docs.yml`.
- 033-from-description-chat-flow: doc-aware `--from-description` flow. `UserDescribedGenerator.BuildPrompt()` testable; best-effort loads matching docs (cap 3×8000 chars) + criteria; resulting tests get `source_refs`/`criteria` (verdict stays `manual`). Intent routing in SKILL/agent.
- 032-quickstart-skill-usage-guide: new `spectra-quickstart` SKILL (12th) and `USAGE.md` offline guide (written by `spectra init`). Both hash-tracked.
- 030-prompt-templates: customizable `.spectra/prompts/` with 5 markdown templates (`{{placeholder}}`, `{{#if}}`, `{{#each}}`). New `PlaceholderResolver`/`PromptTemplateLoader`/`BuiltInTemplates`. New `analysis.categories` config (6 defaults). New `spectra prompts list/show/reset/validate` commands and `spectra-prompts` SKILL.
- 029-spectra-update-skill: new `spectra-update` SKILL (10th) wrapping `spectra ai update` with progress page + classification breakdown.
- 028-coverage-criteria-fix: `TestCaseParser` propagates `Criteria` field; `GenerateHandler` loads per-doc `.criteria.yaml` and passes to generator as context; `TestFileWriter` always writes `criteria: []`.
- 027-skill-agent-dedup: agents delegate CLI tasks to SKILL files (execution agent ~400→120 lines, generation agent ~219→81). Standardized "Step N" format and `--no-interaction --output-format json --verbosity quiet` flags.
- 026-criteria-folder-coverage-fix: renamed `docs/requirements/` → `docs/criteria/` with auto-migration in `AnalyzeHandler`. Skip `_index.*`/`.criteria.yaml`/`_criteria_index.yaml` from criteria extraction. Dashboard uses `AcceptanceCriteriaCoverageAnalyzer`.
- 025-universal-skill-progress: shared `ProgressManager` + `ProgressPhases`. All 9 SKILL-wrapped commands write `.spectra-result.json`; 6 long-running ones write `.spectra-progress.html` with phase stepper. Renamed `Requirements`→`AcceptanceCriteria`.
- 024-docs-skill-coverage-fix: docs-index SKILL with progress page; `--skip-criteria` flag; auto-rename of legacy `_requirements.yaml`; dashboard zero-state defaults.
- 023-criteria-extraction-overhaul: "requirements"→"acceptance criteria" rename. Per-document iterative extraction → individual `.criteria.yaml` + `_criteria_index.yaml`. SHA-256 incremental. Import from YAML/CSV/JSON with Jira/ADO column auto-detection, AI splitting, RFC 2119 normalization. `--merge`/`--replace`/`--list-criteria` modes. `spectra-criteria` SKILL.
- 023-copilot-chat-integration: full VS Code Copilot Chat integration. `--suite` flag, `--analyze-only` two-step flow, automatic batch generation (groups of 30), `.spectra-result.json` lifecycle, BehaviorAnalyzer timeout 30s→2min, 7 bundled SKILLs.
- 022-bundled-skills: bundled SKILL files + 2 agent prompts created by `spectra init` in `.github/skills/` and `.github/agents/`. SHA-256 hash tracking via `SkillsManifest`. New `spectra update-skills` command, `--skip-skills` flag.
- 021-generation-session: four-phase generation flow (Analysis → Generation → Suggestions → User-Described). `.spectra/session.json` (1h TTL). New `UserDescribedGenerator`, `DuplicateDetector` (Levenshtein), `SuggestionPresenter`. CLI: `--from-suggestions`, `--from-description`, `--context`, `--auto-complete`.
- 020-cli-non-interactive: global `--output-format human|json` and `--no-interaction` on all commands. Typed `Results/` models per command, `JsonResultWriter`. Standardized exit codes (0/1/2/3).
- 017-mcp-tool-resilience: MCP `run_id`/`test_handle` auto-resolve when only one active run/test exists. New `list_active_runs`, `cancel_all_active_runs` tools. Enhanced `get_run_history` with status filter + summary counts.
- 015-auto-requirements-extraction: AI extraction of testable requirements via Copilot SDK. `RequirementsWriter` (YAML merge, duplicate detection, sequential `REQ-NNN`). CLI: `spectra ai analyze --extract-requirements`.
- 014-open-source-ready: README redesign, CI pipeline, NuGet publish pipeline, GitHub issue/PR templates, Dependabot.
- 013-cli-ux-improvements: `NextStepHints` after every command. Init prompts for automation dirs + critic model. New `spectra config add/remove/list-automation-dir` subcommands. Interactive generation continuation menu.
- 012-dashboard-branding: dashboard branding (company name, logo, favicon, light/dark theme, CSS variable overrides). `BrandingInjector` + `SampleDataFactory`. `dashboard --preview` mode.
- 010-smart-test-selection: cross-suite test search via `find_test_cases` MCP tool. `start_execution_run` accepts `test_ids` and `selection` modes. New `get_test_execution_history`, `list_saved_selections` tools.
- 011-coverage-overhaul: unified three-type coverage (Documentation, Requirements, Automation). New analyzers/services/models. CLI `--auto-link` writes `automated_by` back into test files.
- 010-document-index: persistent `docs/_index.md` with per-doc metadata + SHA-256 incremental updates. `spectra docs index [--force]`. Auto-refresh before `ai generate`.
- 009-copilot-sdk-consolidation: unified all AI under GitHub Copilot SDK. Removed legacy per-provider agent/critic implementations.
- 008-grounding-verification: dual-model critic flow (grounded/partial/hallucinated verdicts), grounding metadata in YAML frontmatter, `--skip-critic` flag.
- 006-conversational-generation: two-mode generation (Direct/Interactive), test updates with classification (UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT), Spectre.Console UX.
- 004-test-generation-profile: profile support for generation settings.
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
