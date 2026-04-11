# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
