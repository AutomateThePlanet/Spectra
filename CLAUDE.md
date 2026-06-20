# Spectra Development Guidelines

Last updated: 2026-06-20 | Version history in `CHANGELOG.md`

## Tech Stack
- C# 12, .NET 8+, Claude Code (AI runtime — authoring orchestration ships as `.claude/skills/` + `.claude/agents/`), System.CommandLine, Spectre.Console, System.Text.Json. Spec 058 retired the in-process **critic** provider chain (the critic is the `spectra-critic` subagent; `ai.critic.model` is its only selector). **Spec 059** inverted **generation** onto the compile→in-session-generate→ingest seam and removed the in-process generator (`CopilotGenerationAgent`, `BehaviorAnalyzer`, `UserDescribedGenerator`, `ProviderChain`, `AgentFactory.CreateAgentAsync`, the `spectra ai generate` command) — generation now runs in the interactive session via the `spectra-generate` skill (`spectra ai compile-prompt` / `compile-analysis-prompt` → ingest). **Spec 069** completed the migration: it inverted **criteria extraction** onto the same compile→in-session→ingest seam (`spectra docs changed` → `compile-extraction-prompt` → in-session turn → `ingest-criteria`, driven by the `spectra-criteria` skill), made `docs index` index-only (retiring `RequirementsExtractor`/`_requirements.yaml`), dropped import compound-splitting, and **deleted the GitHub Copilot SDK entirely** — `CopilotService`, `ProviderMapping`, `CriteriaExtractor`, `Agent/Copilot/`, `AgentFactory`, `spectra auth`, and the `ai.providers`/`ai.critic` config model are gone (model-free analysis helpers were rescued to `Spectra.CLI.Analysis`; `Microsoft.Extensions.AI.Abstractions` is now a direct dependency). All inference is the user's Claude Code session.
- Microsoft.Data.Sqlite, SQLite (`.execution/spectra.db`) — execution is CLI-only (`spectra run`); SPECTRA's MCP execution adapter was removed in Spec 070
- YamlDotNet (manifest serialization), Microsoft.Extensions.FileSystemGlobbing (exclusion patterns)
- CsvHelper (CSV import), dual-model verification (Generator + Critic) — the critic runs as a `context: fork` subagent (`.claude/agents/spectra-critic`)
- File-based: test-cases/, docs/, spectra.config.json, _index.json, profiles, .spectra/prompts/
- **Doc index v2 (Spec 040)**: `docs/_index/_manifest.yaml` + `docs/_index/groups/{suite}.index.md` + `docs/_index/_checksums.json` (manifest always loaded; per-suite files lazy-loaded; checksums never AI-visible)
- Criteria index: `docs/criteria/_criteria_index.yaml` + per-doc `.criteria.yaml`

## Project Structure

```
src/
  Spectra.CLI/          # CLI app
    Commands/           # Analyze, Dashboard, Docs, Generate, Update, Run (Spec 065 execution surface)
    Agent/              # Critic/ (subagent prompt builders), Tools/, Testimize/ — Copilot SDK removed (Spec 069)
    Analysis/           # Model-free analysis helpers (AnalyzerInputBuilder, DocumentTools, …)
    Extraction/         # Model-free criteria seam (ExtractionPromptCompiler, CriteriaIngestor, CriteriaResponseClassifier)
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
  Spectra.Execution/    # Transport-neutral engine (Spec 065); sole execution surface drives it via `spectra run` (Spec 070). Namespaces still Spectra.MCP.* (cosmetic, rename out of scope)
    Execution/          # ExecutionEngine, TestQueue, DependencyResolver, StateMachine, QueueReconstructionException (ns Spectra.MCP.Execution)
    Storage/            # ExecutionDb (WAL+busy_timeout), RunRepository, ResultRepository, QueueSnapshotRepository (ns Spectra.MCP.Storage)
    Reports/            # ReportGenerator, ReportWriter (ns Spectra.MCP.Reports), ScreenshotService (Spec 065)
    Identity/           # UserIdentityResolver (ns Spectra.MCP.Identity)
    Infrastructure/     # McpConfig (ns Spectra.MCP.Infrastructure)
  Spectra.GitHub/       # GitHub integration (future)

dashboard-site/         # Static template: index.html, styles/, scripts/(app.js, coverage-map.js), functions/(auth)
tests/
  Spectra.Core.Tests/   # Unit tests (~557)
  Spectra.CLI.Tests/    # Integration tests (~1150)
  Spectra.Execution.Tests/  # Engine + relocated/ported execution tests (Spec 070, ~228)
  Spectra.Integration.Tests/ # Cross-spec generation→persistence→execution (engine-direct)
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

# Execution (Spec 065/070 — `spectra run` is the SOLE execution surface; no MCP server)
spectra run list-suites|list-active|selections|history [--suite]
spectra run start <suite> [--priorities --tags --components --test-ids --selection --environment]
spectra run status|summary [<run-id>]
spectra run show [<run-id>] [--test-id|--handle]
spectra run advance [<handle>] --status pass|fail|blocked|skip [--notes]
spectra run skip [<handle>] --reason "..." [--blocked]
spectra run note [<handle>] --note "..."
spectra run bulk-record [<run-id>] --status <s> [--remaining|--test-ids a,b] [--reason]
spectra run retest [<run-id>] --test-id <id>
spectra run screenshot [<handle>] --file <path> | spectra run screenshot-clipboard [<handle>]
spectra run pause|resume|cancel [<run-id>] | spectra run cancel-all
spectra run finalize [<run-id>] [--force]

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

## Execution (CLI-only since Spec 070)

SPECTRA has no MCP server. All execution runs through the `spectra run` command group (see Commands
above), driving the `Spectra.Execution` engine over `.execution/spectra.db`. Bulk-record accepts
`--remaining` or `--test-ids a,b` with `--status` and an optional `--reason`. Reports are generated in
JSON, Markdown, and HTML (test titles from `_index.json`, human-readable durations, UTC timestamps,
expandable non-passing tests, status enums as strings). The SEPARATE BELLATRIX/Nova MCP that drives the
system-under-test is unrelated to SPECTRA's removed execution adapter.

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

## Active Technologies
- C# 12, .NET 8+ + GitHub Copilot SDK, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet, Microsoft.Extensions.FileSystemGlobbing (045-doc-index-restructure)
- File-based (Markdown/YAML/JSON in `test-cases/`, `docs/_index/`, `docs/criteria/`, `_index.json`, `_manifest.yaml`, `_checksums.json`, `_criteria_index.yaml`) (045-doc-index-restructure)

## Recent Changes
- **073-fix-grounding-oracle-batch**: Amendment to Spec 072 fixing 5 confirmed defects from a live run. **FR-A1** (oracle bug): `audit-grounding`'s `grounding_written` always reported `false` even when the block existed on disk — root cause: `AuditGroundingHandler.cs:91` read `testsPath/entry.File` (e.g. `test-cases/TC-100.md`) instead of `suitePath/entry.File` (`test-cases/smoke/TC-100.md`); same bug in `CompileRepairBatchCommand.cs:95`. Both fixed to `suitePath`. Test fixtures updated from `"file":"suite/id.md"` → `"file":"id.md"` to match `GeneratedTestIngestor.ParseTestCase` production format. **FR-A2** (repair-batch): confirmed independent same-root-cause bug; both fixes land together. **FR-A3**: `ingest-grounding --suite <s> --all` batch form — scans `.spectra/verdicts/`, filters by suite index, writes all eligible grounding blocks in one pass. Without `--repaired`: pre-repair filter skips partial verdicts. With `--repaired --repair-attempts N`: writes all ungrounded tests (both grounded and partial re-verdicts). Idempotent (skips already-written). Reuses `GroundingWriteBackService.WriteAsync` — no code duplication. **FR-A4**: `spectra-generate.md` Steps 8–9 restructured: per-test `ingest-grounding` removed from 8a.2 and 8b.5 loops; replaced with two post-loop batch calls (`--all` after 8a, `--all --repaired` after 8b). Added shell-improvisation prohibition block (no find/grep/cat/ls — use audit-grounding, ingest-grounding --all, spectra config --raw, Read tool). Added `ingest-grounding --suite --all` to CLI flags table. Fixed pre-existing `GenerationSkill_DrivesFailLoudBoundedLoop` test failure (added "retry limit" text to Step 7). **FR-A5**: docs — `cli-reference.md` updated with batch form docs; `usage.md` Repair Batch section rewritten with two-call batch pattern. Core 568 / CLI 1262 / Execution 228.
- **071-verdict-disposition**: Makes critic verdicts durable and visible. **grounded** → condensed `grounding:` block in `.md` frontmatter (`verdict`, `score`, `verified_at`) + full JSON in `.spectra/verdicts/critic-verdict-{id}.json`. **partial** → block with condensed findings + `flagged_for_review: true` + 1 bounded repair attempt (compile-repair-prompt → agent patches → ingest-update → re-critic → ingest-grounding); batch stays non-blocking. **hallucinated** → drop trail entry in `.spectra/dropped-tests.json` then existing clean three-phase delete. New CLI verbs: `ingest-grounding`, `record-drop`, `compile-repair-prompt`, `review-flagged`. New skill `spectra-review-flagged`. `GroundingMetadata` gains `FlaggedForReview`, `RepairAttempts`, `Repaired`, `CondensedFindings`. New services: `GroundingWriteBackService`, `DroppedTestsTrail`, `RepairPromptCompiler`. `.spectra/verdicts/` and `.spectra/dropped-tests.json` gitignored. Core 568 / CLI 1216 / Execution 228.
- **070-remove-mcp-adapter (v2)**: Removed SPECTRA's own MCP execution adapter — `spectra run` is now the **sole** execution surface (reverses 065's "keep MCP as a thin adapter"). Deleted the `Spectra.MCP` project (Server/, Tools/, the 25 tool schemas, `spectra-mcp` dotnet-tool packaging) and `Spectra.MCP.Tests`; removed both from `Spectra.slnx`. `spectra init` no longer emits `.vscode/mcp.json` or the `mcp__spectra__*` allowlist (deleted `VsCodeMcpConfigInstaller`/`ClaudeSettingsInstaller`); a peer `.vscode/mcp.json` is left untouched. The execution skill/agent are CLI-only (removed the optional "drive over the SPECTRA MCP server" fallback; the SEPARATE BELLATRIX/Nova SUT-driver MCP and Azure DevOps bug-log MCP references stay). **Engine untouched** (`Spectra.Execution`, incl. cosmetic `Spectra.MCP.*` namespaces — rename out of scope). **No coverage lost**: 16 engine test files relocated verbatim to `Spectra.Execution.Tests`; the adapter tests' engine substance ported engine-direct there (closing bulk-record/cancel-all/list-active/run-history gaps; Execution.Tests 6→228); transport-only tests (JSON-RPC strictness, ToolRegistry error-code mapping, tool param validation) retired with named survivors; `Spectra.Integration.Tests` re-pointed off the MCP tools to the engine. Latent fix: `spectra run start --selection` now applies the saved selection's filters (was discarding them and running the whole corpus). Canary (Core 557 / CLI / pre-existing Execution.Tests) unmodified and green. `docs/architecture/ARCHITECTURE-v2.md` (+`docs/specs/` copy) updated to "one surface: the CLI". Stale constitution clauses II/III (MCP/provider wording) left for a separate amendment.
- **065-execution-surface-consolidation (v2)**: Makes the deterministic execution engine a **first-class CLI surface** (`spectra run …`) while keeping `Spectra.MCP` as a thin adapter over the same engine — one `dotnet tool install -g Spectra` for both generation and execution, the 25 MCP tool schemas out of the model context, and zero per-client MCP config for the CLI path. The engine + storage were extracted from the `Spectra.MCP` executable into a new transport-neutral **`Spectra.Execution`** class library referenced by both CLI and MCP. **Decisive design (research R1):** the moved types **keep their existing namespaces** (`Spectra.MCP.Execution/.Storage/.Identity/.Infrastructure/.Reports`), because every protected tool/integration test references them — so the extraction is *file move + project-reference rewiring only*, **zero `using` edits**, and the ~14 transport tests + the whole MCP corpus compile and pass **byte-unchanged** (402/402), which is the behavior-preservation proof (SC-003/SC-004). Moved: `ExecutionEngine`, `TestQueue`, `DependencyResolver`, `StateMachine`, `QueueReconstructionException`, `ExecutionDb`, `RunRepository`, `ResultRepository`, `QueueSnapshotRepository`, `UserIdentityResolver`, `McpConfig` (engine ctor dep, carried), and `ReportGenerator`/`ReportWriter` (the CLI's `finalize` needs them). New `spectra run` group (`Commands/Run/`: `RunCommand`/`RunServices`/`RunHandler`/`RunResult`) is thin adapters over the SAME `ExecutionEngine` over the SAME `.execution/spectra.db` — `start`/`status`/`show`/`advance`/`skip`/`note`/`bulk-record`/`retest`/`screenshot[-clipboard]`/`pause`/`resume`/`cancel`/`cancel-all`/`finalize`/`list-suites`/`list-active`/`history`/`summary`/`selections`; short-lived CLI processes reconstruct the queue losslessly (Spec 064) so behaviour == MCP (FR-007). `QueueReconstructionException` surfaces as a distinct CLI outcome (`error_code: RECONSTRUCTION_FAILED`), never conflated with `RUN_NOT_FOUND` (FR-008). **`ExecutionDb` now sets `PRAGMA journal_mode=WAL; busy_timeout=5000` at open** so concurrent short-lived writers don't hit `SQLITE_BUSY` (FR-004). New `ScreenshotService` (shared encode + OS clipboard capture); MCP screenshot tools left unchanged (behavior-preserving — delegation deferred to protect the green report tests). New `spectra-execute` SKILL + the execution agent re-pointed at `spectra run` (guardrails preserved: present→wait-for-verdict→advance, never fabricate, never auto-advance; MCP kept as optional networked path). +1 project (`Spectra.Execution`), +1 test project (`Spectra.Execution.Tests`); ~20 new tests (run-loop, parity, guardrails, WAL concurrency, skill); `Spectra.Core`/`TestPersistenceService`/MCP transport nets untouched and green.
- **064-lossless-queue-reconstruction (v2)**: Fixes the single shared root cause that blocked a CLI-driven (short-lived-process) execution surface: the in-memory `TestQueue` was reconstructed from SQLite **lossily** (`TestQueue.AddFromResult` hard-coded `Title=TestId`/`Priority=Medium`/`DependsOn=null`; `ReconstructQueue` re-ordered alphabetically), so any process not holding the original queue silently lost dependency-blocking and ordering. Now a durable **orchestration snapshot** is persisted at run-build into a new `queue_snapshot` table (`run_id,test_id,title,priority,depends_on,order_index`; new `QueueSnapshotEntry` + `QueueSnapshotRepository`), and reconstruction rebuilds the queue **DB-complete** from it — never re-reading the mutable on-disk index, so it is drift-immune. `AddFromResult` was removed in favour of `TestQueue.AddReconstructed`; `ReconstructQueue`/`GetQueueAsync` validate and **fail loud** via new `QueueReconstructionException` (snapshot missing/incomplete/inconsistent or dangling `depends_on`), surfaced centrally in `ToolRegistry` as `RECONSTRUCTION_FAILED` — distinct from the benign null "run not found". `GetStatusAsync`/`StartTestAsync`/`AdvanceTestAsync`/`BulkRecordResultsAsync`/`RetestAsync`/`FinalizeRunAsync` now route through the reconstruct-aware `GetQueueAsync` (warm `_queues` path unchanged), so long-lived == short-lived behaviour, the B-column of process-lifetime tools collapses to 0, and the cross-process `retest` `RUN_NOT_FOUND` bug is fixed. Correctness prerequisite for the follow-on execution-surface consolidation (CLI `spectra run`, `Spectra.Execution` extraction, WAL/busy_timeout — all out of scope here). +3 source files (`QueueSnapshotEntry`, `QueueSnapshotRepository`, `QueueReconstructionException`) + engine/schema/DI edits; +5 test files (~21 tests); `TestQueueReconstructionTests` migrated off the removed lossy primitive. `Spectra.Core` + `TestPersistenceService` test nets untouched and green.
- **063-targeted-test-updates (v2)**: Adds the missing **update** counterpart of the inverted compile→in-session→ingest seam. Before this, `spectra ai update` only classified tests and never rewrote OUTDATED ones against changed docs (the `spectra-update` skill's "rewrites affected test cases" claim was false). New CLI pair: `spectra ai compile-update-prompt --suite <s> --test-id <id>` (deterministic, model-free **edit** prompt — existing test + changed source/criteria + "edit, don't regenerate; preserve id/structure/manual fields", emits to stdout) and `spectra ai ingest-update <suite> --test-id <id> [--from <file>]` (fail-loud validate+persist through the single `TestPersistenceService` write+index path). `ingest-update` protects invariants deterministically: id **from the original** (no new id allocated; edit not create), pre-existing **`Manual` verdict/grounding** + non-round-tripped fields re-asserted from the original, and a **drift guard** that fails loud (`DRIFT_DETECTED`, exit 5) on a protected-field change not implicated by the doc change (priority/component/tags). New `UpdatePromptCompiler`, `UpdatedTestIngestor` (+`DriftReport`/`DriftEntry`); reuses `GeneratedTestIngestor.ParseAndValidate` verbatim. `test-update.md` template rewritten from classify-and-propose to edit-and-return-whole-test (output = JSON array of one edited test, generation schema). `spectra-update` skill rewritten to drive the loop with bounded fail-loud retry; `TestClassifier` (selector) + `TestPersistenceService` (persist) reused unchanged. SkillsManifest per-line-flag checks exclude `spectra-update` (seam commands take no `--no-interaction`/`--output-format`/`--verbosity`), mirroring `spectra-generate`. +4 source, +3 test files (~28 tests); `usage.md`/`cli-reference.md` corrected.
- **049-from-description-index-parity (v1.52.3)**: Fixes the bug where tests created via `spectra ai generate --suite X --from-description "..."` materialized on disk but were never registered in `test-cases/{suite}/_index.json` — making them invisible to every MCP discovery and execution tool (`find_test_cases`, `start_execution_run`, `list_available_suites`, saved-selection counts). Introduces `Spectra.CLI.IO.TestPersistenceService` as the single write-file + regenerate-index entry point for generation flows. `GenerateHandler.ExecuteFromDescriptionAsync` was rewired to call `PersistAsync`; `ExecuteDirectModeAsync` (batch) and `ExecuteInteractiveModeAsync` (gap-driven) were refactored to use the same service so no generation path writes a test file without also updating the index. The high-priority-filter symptom (`smoke` saved selection not matching from-description high tests) resolves as a downstream consequence — filter code was already correct, it was reading from an index the from-description path never populated. `spectra index --rebuild` is the backfill path for pre-fix workspaces (verified to parse `.md` files of record and regenerate every suite's index). Out of scope: `BatchWriteTestsTool` (AI-discretion tool, separate path) and `UpdateHandler.ApplyChangesAsync` (update, not generation). +1 service file, +5 test files (~20 tests), zero new CLI flags, zero MCP surface changes, no index data-model change.
- **048-criteria-coverage-guards (v1.52.2)**: Three non-blocking guards so users are never silently left without acceptance criteria. (1) `CriteriaSource` gains an additive `outcome` field (default `"extracted"`); legacy entries without the key deserialize as `"extracted"` via the property default — no migration. (2) `spectra docs index` emits a prominent non-blocking warning and writes a `criteria_warning` JSON field when it indexed at least one document but extracted 0 criteria across the whole corpus, naming the recovery command. (3) `spectra ai generate` (both batch and from-description flows) attaches a non-blocking `notes` entry to its result when zero criteria match the target suite by component/source-doc/file-name. `LoadCriteriaContextAsync` was refactored to return a `CriteriaContextResult` record exposing the suite-match count separately from the prompt context. All guards are output-only (no prompts, no exit-code changes). Pure-function helpers `ComputeCriteriaWarning` and `BuildNoCriteriaNote` extracted for testability.
- **047-resilient-criteria-extraction (v1.52.1)**: Two confirmed defects in criteria extraction fixed. (1) Cache-poisoning: new typed `CriteriaExtractionResult` (`Extracted` | `EmptyResponse` | `ParseFailure`) gates the per-document hash write on `IsCacheable`, so a parse-class or empty-response failure no longer poisons the cache; non-cacheable outcomes are retried once with a 1.5 s backoff (2 attempts total) via the new `IExtractionDelayProvider`. (2) `docs index` replaced its single 60 s corpus-level deadline with a 2 min per-document deadline matching the `ai analyze --extract-criteria` path, so a single slow doc no longer aborts the whole corpus. Failed/timed-out documents are surfaced via the existing `documents_failed` count / `failed_documents` list and exit code. No new commands or flags; `--force` cache-bypass behavior unchanged. Two extractor implementations (`RequirementsExtractor` for `docs index`, `CriteriaExtractor` for `ai analyze`) intentionally not merged — deferred to a future spec.
- **046-test-lifecycle-control (Spec 040 lifecycle, v1.52.0)**: Persistent `PersistentTestIdAllocator` (in `Spectra.Core/IdAllocation/`) wraps existing in-memory `TestIdAllocator` with cross-process file lock + `.spectra/id-allocator.json` HWM + filesystem-frontmatter scan — guarantees globally unique IDs across concurrent generation runs. New commands: `spectra delete <ids…>`, `spectra suite list|rename|delete`, `spectra cancel`, `spectra doctor ids [--fix]`. New `CancellationManager` (singleton in `Spectra.CLI/Cancellation/`) owns process CTS + `.spectra/.cancel` sentinel + `.spectra/.pid`; six long-running handlers gain cooperative cancellation at batch boundaries with `Cancelled` terminal phase on the progress page. Two new SKILLs (`spectra-delete`, `spectra-suite`); six existing long-running SKILLs gain a Cancel recipe. Status enum gains `cancelled` + `no_active_run`. Hard delete with Git-as-undo; `--dry-run` previews everywhere.
- **045-doc-index-restructure (v1.51.0)**: Replaced single-file `docs/_index.md` with v2 layout under `docs/_index/` (manifest + per-suite + checksums). Pre-flight token-budget check at `ai.analysis.max_prompt_tokens` (default 96K) fails fast with exit code 4 instead of model-side 400 overflow. New flags: `--suite` (doc-suite filter on `ai generate`), `--include-archived`, `--no-migrate`, `--suites`. New commands: `spectra docs list-suites`, `spectra docs show-suite`. Auto-migration on first run preserves legacy file as `.bak`.
- 044-coverage-aware-analysis: Added C# 12, .NET 8+ + GitHub Copilot SDK, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet
