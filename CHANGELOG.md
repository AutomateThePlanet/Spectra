# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.46.1] - 2026-04-11

### Fixed
- **False `TESTIMIZE UNHEALTHY no_load_event_within_3s` line in debug log.** The 3-second grace window in `GenerationAgent` was based on a wrong assumption about how fast the Copilot CLI's MCP client handshakes with a server — in reality it takes ~60s to attempt + time out an initialize against a non-spec-compliant server. Removed the grace window entirely. The SDK's `SessionMcpServersLoadedEvent` is the single source of truth; when it fires (success or failure), the real status is logged with the real error message. Generation proceeds immediately because the SDK does not block session creation on MCP loading.
- **Session-companion fix in `Testimize.MCP.Server`** (separate repo): the server previously emitted JSON-RPC responses with both `"result"` and `"error":null`, which is a [JSON-RPC 2.0 spec violation](https://www.jsonrpc.org/specification#response_object) — the spec says a response MUST contain `result` OR `error`, not both. The Copilot CLI's MCP client (built on `@modelcontextprotocol/sdk`) correctly rejects malformed responses and times out at ~60s with `MCP error -32001: Request timed out`. Fix: serialize only the field that applies. See `Testimize.MCP.Server/Program.cs` change; requires rebuilding testimize-mcp 1.1.10 from source and reinstalling.

### Notes
- **Zero code-path changes beyond the 3s grace removal** — token tracking, debug log formatting, run summary, `RUN TOTAL` line, and the native `SessionConfig.McpServers` wiring from v1.46.0 are all untouched.
- The SDK's behavior (blocking vs non-blocking session creation on MCP load) was empirically verified by observing the timing in `.spectra-debug.log`: session creation completed immediately, generation ran in parallel with the MCP handshake, and the load event arrived ~68s later with `status=Failed`.

## [1.46.0] - 2026-04-11

### Added
- **Native MCP integration for Testimize** — `spectra ai generate` now passes Testimize to the Copilot SDK via its native `SessionConfig.McpServers` API. The SDK owns the full lifecycle: process spawn, `initialize` handshake, framing variant (Content-Length OR newline-delimited), tool discovery, cost attribution (`AssistantUsageData.Initiator = "mcp-sampling"`), permission prompts, and OAuth. No more custom JSON-RPC protocol client.
- **`McpConfigBuilder.BuildTestimizeServer(TestimizeConfig)`** — small static helper that translates the user-facing `testimize` config block into an `McpLocalServerConfig` for the SDK. Unit-tested in isolation.
- **`CopilotService.CreateGenerationSessionAsync(..., mcpServers)`** — new optional parameter on the session factory that forwards an MCP server map into `SessionConfig.McpServers`. Existing callers unaffected.
- **SDK-driven testimize lifecycle in `.spectra-debug.log`** — the agent subscribes to `SessionMcpServersLoadedEvent` and `SessionMcpServerStatusChangedEvent` and writes `TESTIMIZE CONFIGURED / LOADED / STATUS_CHANGED / DISPOSED` lines driven by the SDK's own status enum (`Connected / Failed / NeedsAuth / Pending / Disabled / NotConfigured`). No more 5-second probe hangs.
- **`testimize_strategy` prompt placeholder** — the `test-generation.md` template now embeds the preferred testimize tool name (derived from `testimize.strategy`) so the AI knows whether to prefer `testimize/generate_hybrid_test_cases` (HybridArtificialBeeColony, default) or `testimize/generate_pairwise_test_cases` (fast Pairwise).
- **Defensive 3-second grace window** — if testimize is configured but the SDK hasn't reported a load status by the time the handler is ready to send the prompt, the agent logs `TESTIMIZE UNHEALTHY reason=no_load_event_within_3s` and continues. The AI will still succeed, just without testimize's optimized test data.
- **+17 tests** — `McpConfigBuilderTests` (7 cases) and `TestimizeStrategyResolverTests` (4 theory cases + 1 fact + 1 edge), plus updates to `TestimizeConditionalBlockTests` and `AnalyzeFieldSpecTests` to reflect the new tool names.

### Fixed
- **Testimize health probe 5-second hang.** Root cause: our custom `TestimizeMcpClient` used `Content-Length: N\r\n\r\n<body>` MCP-spec-v1 framing, but `Testimize.MCP.Server` uses newline-delimited JSON (`ReadLineAsync`/`WriteLineAsync`). Worse, Spectra wrote the JSON body with no trailing `\n`, so testimize's `ReadLineAsync` blocked forever waiting for one — while Spectra's own `ReadLineAsync` blocked waiting for a response. Exactly 5 seconds later, the probe timeout fired. Deleting the client and delegating to the SDK's native MCP implementation fixes this class of bug permanently (the SDK handles both framing variants correctly).

### Removed
- **`src/Spectra.CLI/Agent/Testimize/TestimizeMcpClient.cs`** — the 240-line custom JSON-RPC-over-stdio client with the Content-Length framing bug. Gone.
- **`src/Spectra.CLI/Agent/Testimize/TestimizeTools.cs`** — the `CreateGenerateTestDataTool` `AIFunction` wrapper that routed to the broken client. Gone. The AI now calls the real testimize MCP tools directly.
- **Tests for the deleted classes** — `TestimizeMcpClientTests`, `TestimizeMcpClientGracefulTests`, `TestimizeToolsTests`. Also deleted one obsolete test from `TestimizeCheckHandlerTests` (`Check_EnabledButBogusCommand_...`) that asserted old-client behavior.
- `spectra testimize check`'s "healthy" field now just mirrors `installed` — the real runtime health check moved to `spectra ai generate` where the debug log's `TESTIMIZE LOADED server=testimize status=Connected` line is the authoritative signal.

### Changed
- **`FieldSpecAnalysisTools.cs`** (new file, replaces the surviving half of `TestimizeTools.cs`) — contains `CreateAnalyzeFieldSpecTool` + the local regex-based `ExtractFields` helper. No behavior change; same signatures, new class name scoped to what the code actually does.
- **`test-generation.md` prompt template** — the `{{#if testimize_enabled}}` block now names both real testimize MCP tools explicitly, references the new `{{testimize_strategy}}` placeholder, and instructs the AI to call `AnalyzeFieldSpec` first for prose-described fields.
- **`behavior-analysis.md` prompt template** — same update: references `testimize/generate_hybrid_test_cases` / `testimize/generate_pairwise_test_cases` instead of the deleted `GenerateTestData` wrapper name.

### Notes
- The user-facing config schema (`testimize.enabled`, `testimize.mcp.command`, `testimize.mcp.args`, `testimize.strategy`, `testimize.mode`) is **unchanged**. Existing `spectra.config.json` files continue to work with no edits.
- `TestimizeDetector.IsInstalledAsync()` is unchanged — still checks `dotnet tool list -g` for `testimize.mcp.server`.

## [1.45.2] - 2026-04-11

### Added
- **Run header in `.spectra-debug.log`** — every `generate` / `update` run now writes a header block at the start containing the SPECTRA version, the full user command line, and an ISO-8601 UTC timestamp:
  ```text
  ──────────────────────────────────────────────────────────────
  === SPECTRA v1.45.2 | spectra ai generate checkout --count 20 | 2026-04-11T14:30:00Z ===
  ```
  Makes each run self-identifying when grepping or comparing logs across days.
- **`debug.mode` config option** with two values:
  - `"append"` (**default**): keeps existing log content and prepends a horizontal separator + header before each new run. Good for comparing multiple runs post-hoc.
  - `"overwrite"`: truncates `.spectra-debug.log` at run start so only the latest run is preserved. Good for focused troubleshooting.
  Unknown values fall back to `"append"`. Wired from `GenerateHandler` and `UpdateHandler` at run start via new `DebugLogger.BeginRun()`.
- New `DebugLogger.BeginRun()` static method that materializes the header and honors the mode. Best-effort — never throws, no-op when debug logging is disabled.
- Version is read via `AssemblyInformationalVersionAttribute` so the header reflects the actual installed build.
- Command line is reconstructed from `Environment.GetCommandLineArgs()` (skipping the dotnet tool shim path) so it looks like what the user typed.
- +11 tests: `BeginRun_Disabled_NoFileWritten`, `BeginRun_Overwrite_TruncatesAndWritesHeader`, `BeginRun_Append_ExistingFile_PrependsSeparatorAndHeader`, `BeginRun_Append_MissingFile_CreatesWithHeader`, `BeginRun_UnknownMode_TreatedAsAppend`, `BeginRun_HeaderIncludesIsoTimestamp`, `BeginRun_ThenAppendAi_LogContainsHeaderAndAiLine`, `BeginRun_ThenAppendTestimize_LinesSurvive` (regression test for the mode=overwrite truncate), plus `DebugConfigTests.Default_Mode_IsAppend` and two deserialization round-trip tests.

### Removed
- **`.spectra-debug-analysis.txt`** — no longer written by `BehaviorAnalyzer` on `ANALYSIS PARSE_FAIL`. The parse-fail reason is still logged to `.spectra-debug.log`.
- **`.spectra-debug-response.txt`** — no longer written by `GenerationAgent` after each batch. The `SaveDebugResponse` method and its call site have been deleted. The stale "Check .spectra-debug-response.txt for the raw AI output" error message now points at `.spectra-debug.log`.
- All debug output now flows through a single file: `.spectra-debug.log`.

### Verified
- **Testimize lifecycle lines** (`TESTIMIZE DISABLED` / `START` / `NOT_INSTALLED` / `UNHEALTHY` / `HEALTHY` / `DISPOSED`) are written via `DebugLogger.Append("generate", ...)` after `BeginRun`, so they survive the `mode=overwrite` truncate. Covered by `BeginRun_ThenAppendTestimize_LinesSurvive`.

## [1.45.1] - 2026-04-11

### Added
- **Real token counts from `AssistantUsageEvent`** (spec 040 follow-up) — the Copilot SDK surfaces provider-reported `InputTokens` / `OutputTokens` on a separate `AssistantUsageEvent` (not on `AssistantMessageEvent` as Spectra previously assumed). Every AI call site (BehaviorAnalyzer, GenerationAgent batch loop, GroundingAgent critic, CriteriaExtractor — both methods) now subscribes to this event via a new `CopilotUsageObserver` helper, waits a 200ms grace after the request for ordering-safe capture, and records the real counts into `TokenUsageTracker`. No more `?` placeholders in the normal case.
- **`text.Length / 4` fallback** when the usage event fails to arrive within the grace window — captured by the new `TokenEstimator` service. Fallback values are flagged end-to-end: `~tokens_in=…` / `~tokens_out=…` in the debug log and `"estimated": true` on both the `token_usage` report and each affected phase DTO in JSON.
- **`RUN TOTAL` summary line** written to `.spectra-debug.log` at the end of every `generate` / `update` run, via the new `RunSummaryDebugFormatter`. Format: `RUN TOTAL command=generate suite=checkout calls=24 tokens_in=64480 tokens_out=24240 elapsed=2m45s phases=analysis:1/18.5s,generation:3/52.3s,critic:20/1m34s`. Makes the log self-summarizing — one grep and you have the run-level numbers. When any phase used the estimate fallback, the totals gain `~` prefixes.
- New services: `TokenEstimator`, `CopilotUsageObserver`, `RunSummaryDebugFormatter`.
- +36 tests (TokenEstimatorTests, CopilotUsageObserverTests, RunSummaryDebugFormatterTests, TokenUsageReportEstimatedTests, plus extensions to TokenUsageTrackerTests, DebugLoggerTests, GenerateResultTokenUsageTests).

### Changed
- `PhaseUsage`, `TokenUsageTracker.Record`, `TokenUsageReport`, `PhaseUsageDto`, and `DebugLogger.AppendAi` all carry a new `bool estimated` field / parameter. The flag propagates via logical OR during aggregation — any estimated call in a phase flags the whole phase (and the run total) as estimated. Backwards compatible: the parameter defaults to `false`.
- `UpdateHandler` emits the `RUN TOTAL` line with `calls=0 phases=` as a run-boundary marker (no AI calls today — still useful when grepping the log).

### Notes
- Event ordering between `AssistantUsageEvent` and `SessionIdleEvent` is undocumented in the SDK. The 200ms grace via `TaskCompletionSource` handles both orderings: returns immediately if usage arrived before idle, waits briefly if it arrives after. On timeout, the length-based estimate kicks in.
- `AssistantUsageData.InputTokens` / `OutputTokens` are `double?` in the SDK assembly (not `int` as the XML doc implies) — we cast with `(int)(value ?? 0)`.

## [1.45.0] - 2026-04-11

### Added
- **Interactive model preset menu** (spec 041) — `spectra init -i` gains a new "AI Model Preset" step that offers four curated generator + critic pairings: (1) GPT-4.1 + GPT-5 mini (free, unlimited), (2) Claude Sonnet 4.5 + GPT-4.1 critic (premium, high quality), (3) GPT-4.1 + Claude Haiku 4.5 critic (free gen + cross-family critic), (4) Custom — writes preset 1 defaults so you can edit `spectra.config.json` by hand. Non-custom presets rewrite both `ai.providers[0]` and `ai.critic` in a single step and skip the separate granular critic wizard.
- **Embedded `spectra.config.json` template** — the init config template is now an `<EmbeddedResource>` so `spectra init` produces the same file whether run from published (single-file) builds or from dev source. Previously the fallback path used `ConfigLoader.GenerateDefaultConfig()` which omitted the critic block entirely.

### Changed
- **New default models** (spec 041) — `spectra init` now writes `gpt-4.1` (generator) + `gpt-5-mini` (critic) instead of `gpt-4o` / `gpt-4o-mini`. Both are 0× multiplier on any paid Copilot plan, and they're from different model architectures so the dual-model critic (spec 008) provides genuine cross-verification. `SpectraConfig.Default`, `CriticConfig.GetEffectiveModel()`, `CopilotCritic.GetEffectiveModel()`, and `CopilotService.GetCriticModel()` all reflect the new defaults. Per-provider defaults: `github-models` / `openai` / `azure-openai` → `gpt-5-mini`; `anthropic` / `azure-anthropic` → `claude-haiku-4-5`.
- `spectra init -i` granular critic step provider menu updated: dropped `google` (hard-error since spec 039), added `github-models`, updated `anthropic` / `openai` / `azure-openai` / `azure-anthropic` defaults to current model strings.

### Backwards compatibility
- Existing `spectra.config.json` files with `gpt-4o` / `gpt-4o-mini` (or any other valid model) continue to work unchanged. The Copilot SDK still routes those models. No migration is required — the new defaults only affect fresh `spectra init` runs.

## [1.44.0] - 2026-04-11

### Added
- **Token usage tracking & Run Summary** (spec 040) — every `spectra ai generate` and `spectra ai update` run now prints a Run Summary panel (documents, behaviors, tests, verdict breakdown, duration) plus a per-phase Token Usage table grouped by `(phase, model, provider)` with an estimated USD cost for BYOK providers. `github-models` renders *"Included in Copilot plan (rate limits apply)"*. New `TokenUsageTracker` (thread-safe, one instance per run) records every AI call across analysis, generation, critic, and criteria phases. Hardcoded rate table in `CostEstimator` covers gpt-4o, gpt-4o-mini, gpt-4.1 family, claude-sonnet-4, claude-3-5-haiku, deepseek-v3.2.
- **`run_summary` and `token_usage` in JSON output and `.spectra-result.json`** — SKILLs and the live progress page read the same fields, so `spectra-generate` reports token totals + cost in its final summary message. The `.spectra-progress.html` page gains an AI Calls / Tokens In / Tokens Out / Total / AI Time card grid that updates as batches complete.
- **Model + provider in every AI debug log line** — debug log format gains `model=<name> provider=<name> tokens_in=<n|?> tokens_out=<n|?>` suffix on all AI lines (ANALYSIS, BATCH, CRITIC, UPDATE, CRITERIA). Non-AI lines (testimize lifecycle, file I/O) are unchanged.
- **`debug` config section** (spec 040) — new top-level `debug` block in `spectra.config.json` with `enabled` (bool, **default false**) and `log_file` (string, default `.spectra-debug.log`). `spectra init` writes the section disabled by default. `--verbosity diagnostic` force-enables debug logging for a single run without editing config.

### Changed
- **`.spectra-debug.log` is now opt-in.** Default Spectra runs produce zero debug log files, eliminating stale-file accumulation in CI environments. Existing configs without a `debug` section continue to load (no breaking change).
- Unified `TokenUsage` record now lives in `Spectra.Core.Models` with `PromptTokens` / `CompletionTokens` fields (replaces the unused CLI-scoped `InputTokens` / `OutputTokens` record).

### Removed
- **`ai.debug_log_enabled`** has been removed from `AiConfig`. Use the new top-level `debug.enabled` field instead. Existing configs that still set `ai.debug_log_enabled` are silently ignored (System.Text.Json default behavior for unknown fields).

### Notes
- The Copilot SDK does not currently surface `usage.prompt_tokens` / `completion_tokens` on its response object, so token counts are recorded as `null` (rendered as `?` in the debug log and `0` in the Run Summary table). All other Run Summary fields — call count, per-phase elapsed time, model, provider — are captured for every AI call. When the SDK begins exposing usage, a single read point in each agent picks it up with no further wiring.

## [1.36.0] - 2026-04-10

### Added
- **Quickstart SKILL & USAGE.md** (spec 032) — `spectra-quickstart` is the 12th bundled SKILL: workflow-oriented onboarding for Copilot Chat triggered by phrases like "help me get started", "tutorial", "walk me through". Presents 12 SPECTRA workflows with example conversations. Companion `USAGE.md` written to project root by `spectra init` as an offline workflow reference. Both hash-tracked by `update-skills`. Generation and execution agent prompts defer onboarding requests to the new SKILL.
- **Visible default profile format & customization guide** (spec 031) — `profiles/_default.yaml` and `CUSTOMIZATION.md` are now created by `spectra init` and bundled as embedded resources. Profile format is visible/editable instead of hardcoded. New `ProfileFormatLoader.LoadEmbeddedDefaultYaml()` and `LoadEmbeddedCustomizationGuide()` methods.
- **Customizable root prompt templates** (spec 030) — `.spectra/prompts/` directory with 5 markdown templates (behavior-analysis, test-generation, criteria-extraction, critic-verification, test-update) controlling all AI operations. Templates use `{{placeholder}}`, `{{#if}}`, `{{#each}}` syntax. New `analysis.categories` config section with 6 default categories. New `spectra prompts list/show/reset/validate` CLI commands. New `spectra-prompts` SKILL (11th bundled SKILL).
- `ProfileFormatLoader.LoadEmbeddedUsageGuide()` for resolving the bundled `USAGE.md` content.
- Bumped Spectre.Console 0.54.0 → 0.55.0, GitHub.Copilot.SDK 0.2.0 → 0.2.1, Markdig 1.1.1 → 1.1.2 (Dependabot PRs #10, #11, #12).

## [1.35.0] - 2026-04-10

### Added
- `spectra-update` SKILL (10th bundled SKILL) for test update workflow via Copilot Chat
- Agent delegation tables updated for update command routing
- `UpdateResult` extended with `totalTests`, `testsFlagged`, `flaggedTests`, `duration`, `success` fields

## [1.34.6] - 2026-04-09

### Fixed
- Dashboard test file path resolution & version bump

## [1.34.0] - 2026-04-08

### Fixed
- Criteria coverage, index generation, dashboard SKILL & CI pragma fixes

## [1.33.0] - 2026-04-07

### Fixed
- Criteria generation, polling, progress caching & CI build fixes

## [1.32.0] - 2026-04-06

### Changed
- Refined SPECTRA agents/skills CLI workflows

## [1.31.0] - 2026-04-05

### Added
- Coverage semantics fix & criteria-generation pipeline (spec 028)
- `TestCaseParser` now propagates `Criteria` field from frontmatter to `TestCase`
- Criteria loading wired into `GenerateHandler` for per-doc `.criteria.yaml` context

## [1.30.0] - 2026-04-04

### Added
- Docs index SKILL integration, progress page, coverage fix & terminology rename (spec 024)
- `spectra-docs` SKILL (9th bundled SKILL) with structured tool-call-sequence
- `--skip-criteria` flag for docs index command

### Fixed
- Dashboard coverage null-crash fix with zero-state defaults
