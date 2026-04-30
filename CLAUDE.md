# Spectra Development Guidelines

Last updated: 2026-04-30 | Version history in `CHANGELOG.md`

## Tech Stack
- C# 12, .NET 8+, GitHub Copilot SDK (sole AI runtime), System.CommandLine, Spectre.Console, System.Text.Json
- ASP.NET Core (MCP server), Microsoft.Data.Sqlite, SQLite (`.execution/spectra.db`)
- YamlDotNet (manifest serialization), Microsoft.Extensions.FileSystemGlobbing (exclusion patterns)
- CsvHelper (CSV import), dual-model verification (Generator + Critic) via Copilot SDK
- File-based: test-cases/, docs/, spectra.config.json, _index.json, profiles, .spectra/prompts/
- **Doc index v2 (Spec 040)**: `docs/_index/_manifest.yaml` + `docs/_index/groups/{suite}.index.md` + `docs/_index/_checksums.json` (manifest always loaded; per-suite files lazy-loaded; checksums never AI-visible)
- Criteria index: `docs/criteria/_criteria_index.yaml` + per-doc `.criteria.yaml`

## Project Structure

```
src/
  Spectra.CLI/          # CLI app
    Commands/           # Analyze, Dashboard, Docs, Generate, Update
    Agent/              # Copilot SDK integration (Copilot/, Critic/)
    Source/             # Document map, index service
    Index/              # _index.json ops
    Validation/         # Dedup, DuplicateDetector
    Review/             # Interactive terminal UI
    Interactive/        # Selectors, session, UserDescriptionPrompt
    Prompts/            # Template engine (PlaceholderResolver, TemplateLoader, BuiltIns)
    Session/            # SessionStore, SuggestionBuilder
    Skills/             # Bundled SKILL content, AgentContent, SkillsManifest
    Results/            # Typed JSON result models (CommandResult, GenerateResult, etc.)
    Output/             # Progress reporters, presenters, NextStepHints, JsonResultWriter
    Classification/     # Test classification (update flow)
    Coverage/           # Gap analysis, coverage reporting
    Profile/            # Generation profile loading
    Config/             # Config loader, automation dir subcommands
    Dashboard/          # Data collection, generation, BrandingInjector, SampleDataFactory
    IO/                 # File writers
  Spectra.Core/         # Shared library
    Models/             # TestCase, Suite, Config + Dashboard/, Coverage/, Execution/, Grounding/
    Coverage/           # AutomationScanner, LinkReconciler, Calculator, DocCovAnalyzer, ReqCovAnalyzer, UnifiedCovBuilder, AutoLinkService
    Storage/            # ExecutionDbReader
    Parsing/            # Markdown+YAML parser, DocIndexExtractor, RequirementsParser, FrontmatterUpdater
    Validation/         # Schema validation
    Update/             # TestClassifier
    Index/              # DocumentIndexReader/Writer
  Spectra.MCP/          # MCP execution server
    Execution/          # ExecutionEngine, TestQueue, StateMachine
    Storage/            # RunRepository, ResultRepository, ExecutionDb
    Reports/            # ReportGenerator, ReportWriter (JSON/MD/HTML)
    Tools/              # RunManagement/, TestExecution/, Reporting/, Data/
    Server/             # McpServer, ToolRegistry, McpProtocol
    Identity/           # UserIdentityResolver
    Infrastructure/     # McpConfig, McpLogging
  Spectra.GitHub/       # GitHub integration (future)

dashboard-site/         # Static template: index.html, styles/, scripts/(app.js, coverage-map.js), functions/(auth)
tests/
  Spectra.Core.Tests/   # Unit tests (~462)
  Spectra.CLI.Tests/    # Integration tests (~466)
  Spectra.MCP.Tests/    # MCP server tests (~351)
  TestFixtures/         # Sample data
```

## Commands

```bash
dotnet build                                        # Build
dotnet test                                         # Test
dotnet run --project src/Spectra.CLI -- <command>   # Run CLI

# Global: --output-format json|human  --no-interaction  --verbosity quiet

# Generate
spectra ai generate [suite] [--focus "..."] [--no-interaction] [--dry-run] [--skip-critic]
spectra ai generate --suite X --analyze-only          # Analysis only (SKILL two-step)
spectra ai generate --suite X --count 80              # Batch (auto-groups of 30)
spectra ai generate --suite X --include-archived      # Include skip_analysis suites (Spec 040)
spectra ai generate X --auto-complete --output-format json  # CI: all phases, no prompts
spectra ai generate X --from-suggestions [1,3]        # From previous suggestions
spectra ai generate X --from-description "..." --context "..."  # User-described test

# Update
spectra ai update [suite] [--no-interaction] [--diff]

# Dashboard
spectra dashboard --output ./site [--title "..."] [--dry-run] [--preview]

# Docs Index
spectra docs index [--force] [--skip-criteria] [--no-migrate] [--include-archived] [--suites a,b]
spectra docs list-suites [--output-format json]       # Spec 040: list manifest suites
spectra docs show-suite <id>                          # Spec 040: print one suite's index file

# Coverage & Criteria
spectra ai analyze --coverage [--format json|markdown --output FILE] [--auto-link]
spectra ai analyze --extract-criteria [--force] [--dry-run]
spectra ai analyze --import-criteria FILE [--replace] [--skip-splitting] [--dry-run]
spectra ai analyze --list-criteria [--source-type X] [--component X] [--priority X]

# Prompts
spectra prompts list|show|validate|reset [template] [--raw] [--all]

# Other
spectra validate [--output-format json]
spectra update-skills
spectra init [--skip-skills]
spectra config add-automation-dir|remove-automation-dir|list-automation-dirs PATH
```

## Code Style
- PascalCase types/methods, camelCase locals
- All I/O async with `Async` suffix
- Nullable reference types enabled
- xUnit tests with structured results (never throw on validation errors)

## MCP Tools

**Run Management:** `start_execution_run`, `get_execution_status`, `pause_execution_run`, `resume_execution_run`, `cancel_execution_run`, `finalize_execution_run`, `list_available_suites`

**Test Execution:** `get_test_case_details`, `advance_test_case`, `skip_test_case`, `bulk_record_results`, `add_test_note`, `retest_test_case`, `save_screenshot`, `save_clipboard_screenshot`

**Discovery:** `list_active_runs`, `cancel_all_active_runs`

**Data:** `validate_tests`, `rebuild_indexes`, `analyze_coverage_gaps`, `find_test_cases`, `get_test_execution_history`, `list_saved_selections`

**Reporting:** `get_run_history`, `get_execution_summary`

### Bulk Operations
```json
{"status": "SKIPPED", "remaining": true, "reason": "Environment unavailable"}
{"status": "PASSED", "remaining": true}
{"status": "FAILED", "test_ids": ["TC-001", "TC-002"], "reason": "API down"}
```

### Reports
Generated in JSON, Markdown, HTML. Features: test titles from `_index.json`, human-readable durations, UTC timestamps, expandable non-passing tests, status enums as strings.

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

## Active Technologies
- C# 12, .NET 8+ + GitHub Copilot SDK, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet, Microsoft.Extensions.FileSystemGlobbing (045-doc-index-restructure)
- File-based (Markdown/YAML/JSON in `test-cases/`, `docs/_index/`, `docs/criteria/`, `_index.json`, `_manifest.yaml`, `_checksums.json`, `_criteria_index.yaml`) (045-doc-index-restructure)

## Recent Changes
- **046-test-lifecycle-control (Spec 040 lifecycle, v1.52.0)**: Persistent `PersistentTestIdAllocator` (in `Spectra.Core/IdAllocation/`) wraps existing in-memory `TestIdAllocator` with cross-process file lock + `.spectra/id-allocator.json` HWM + filesystem-frontmatter scan — guarantees globally unique IDs across concurrent generation runs. New commands: `spectra delete <ids…>`, `spectra suite list|rename|delete`, `spectra cancel`, `spectra doctor ids [--fix]`. New `CancellationManager` (singleton in `Spectra.CLI/Cancellation/`) owns process CTS + `.spectra/.cancel` sentinel + `.spectra/.pid`; six long-running handlers gain cooperative cancellation at batch boundaries with `Cancelled` terminal phase on the progress page. Two new SKILLs (`spectra-delete`, `spectra-suite`); six existing long-running SKILLs gain a Cancel recipe. Status enum gains `cancelled` + `no_active_run`. Hard delete with Git-as-undo; `--dry-run` previews everywhere.
- **045-doc-index-restructure (v1.51.0)**: Replaced single-file `docs/_index.md` with v2 layout under `docs/_index/` (manifest + per-suite + checksums). Pre-flight token-budget check at `ai.analysis.max_prompt_tokens` (default 96K) fails fast with exit code 4 instead of model-side 400 overflow. New flags: `--suite` (doc-suite filter on `ai generate`), `--include-archived`, `--no-migrate`, `--suites`. New commands: `spectra docs list-suites`, `spectra docs show-suite`. Auto-migration on first run preserves legacy file as `.bak`.
- 044-coverage-aware-analysis: Added C# 12, .NET 8+ + GitHub Copilot SDK, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet
