# Data Model: Generation-skill inversion + completion (Spec 059)

No persistent on-disk schema changes. Tests, criteria, indexes, and config files keep their shapes. The only *removed* persisted field is the optional `ai.providers` config key (retired, ignore-with-notice). The new entities are in-flight artifacts on the analyze seam.

## New / relocated entities

### AnalysisRecommendation (new, in-memory result)

The deterministic recommendation `ingest-analysis` emits and the skill renders. Relocated from the existing `BehaviorAnalysisResult` (the model-call output type) so it can be produced from agent-supplied behaviors plus deterministic accounting.

| Field | Type | Notes |
|-------|------|-------|
| `TotalBehaviors` | int | Count of behaviors the agent identified. |
| `AlreadyCovered` | int | Behaviors matched to existing tests (deterministic dedup via `CoverageSnapshot`, else title-similarity). |
| `RecommendedCount` | int | Computed: `max(0, TotalBehaviors − AlreadyCovered)`. |
| `Breakdown` | `IReadOnlyDictionary<string,int>` | Per-category counts (happy_path, boundary, negative, edge_case, security, error_handling, …). |
| `TechniqueBreakdown` | `IReadOnlyDictionary<string,int>` | ISTQB technique counts (BVA, EP, DT, ST, EG, UC). |
| `DocumentsAnalyzed` | int | Carried for the result summary. |

**Validation / outcome** (mirrors the 054/055 ingest outcome enums):
- `Recommendation` — well-formed behavior JSON parsed and accounted → exit 0.
- `EmptyResponse` — no behaviors / empty content → exit 5 (fail-loud, no recommendation).
- `ParseFailure` — missing/unparseable required fields → exit 6 (damage, fail-loud).

### AnalysisPromptCompileResult (new, mirrors `PromptCompileResult`)

| Field | Type | Notes |
|-------|------|-------|
| `IsSuccess` | bool | |
| `Prompt` | string? | Non-null on success (emitted to stdout). |
| `MissingInput` | string? | Named missing input on refuse-to-emit (exit 4). |
| `Message` | string? | Human-readable refusal. |

### Compiled from-description prompt (no new type)

Reuses `PromptCompileResult`. `compile-prompt --from-description` populates `userPrompt` from description+context and sets `requestedCount = 1`; the criteria-injection and Testimize sections are produced by the unchanged `PromptCompiler`.

## Modified entities

### AiConfig.Providers (retired)

| Aspect | Before (058) | After (059) |
|--------|--------------|-------------|
| `Providers` | `required IReadOnlyList<ProviderConfig>` | optional `IReadOnlyList<ProviderConfig>` (default `[]`), no longer read by any generation code |
| `ConfigLoader` validation | `MISSING_PROVIDERS` error when absent | removed — absence is valid |
| `DeprecatedKeyPaths` | `ai.fallback_strategy`, `ai.critic.provider`, `ai.critic.api_key_env`, `ai.critic.base_url` (NOT `ai.providers`) | **adds `ai.providers`** → flagged by `DetectDeprecatedKeys` |

Legacy config carrying `ai.providers` → still validates, surfaces a non-blocking notice. Config without it → validates.

## Removed entities (code, not persisted data)

| Type / member | File | Reason |
|---|---|---|
| `CopilotGenerationAgent` | `Agent/Copilot/GenerationAgent.cs` | in-process generator — replaced by the seam |
| `CopilotService` | `Agent/Copilot/CopilotService.cs` | session factory for the dead generator/analyzer |
| `ProviderMapping` | `Agent/Copilot/ProviderMapping.cs` | provider→model/SDK mapping, now unused |
| `ProviderChain` | `Agent/ProviderChain.cs` | provider fallback, now unused |
| `AgentFactory.CreateAgentAsync` + hardcoded fallback | `Agent/AgentFactory.cs` | sole constructor of the dead agent |
| `BehaviorAnalyzer.AnalyzeAsync` (model call) | `Agent/Copilot/BehaviorAnalyzer.cs` | split: prompt build → `AnalysisPromptCompiler`; accounting → `AnalysisRecommendationBuilder` |
| `UserDescribedGenerator.GenerateAsync` (model call) | `…/UserDescribedGenerator.cs` | split: prompt shaping → `compile-prompt --from-description` |
| `GenerateHandler` model-calling methods | `Commands/Generate/GenerateHandler.cs` | in-process generate/analyze loops; static deterministic helpers retained |
| `GitHub.Copilot.SDK` package reference | `Spectra.CLI.csproj` | no remaining SDK consumer |

## Reused verbatim (do not modify)

`PromptCompiler`, `GeneratedTestIngestor`/`IngestResult` (`ingest-tests`), the `spectra-critic` subagent + `CriticPromptCompiler`/`VerdictIngestor`, `TestPersistenceService`, `GenerateHandler.LoadCriteriaContextAsync`, `ProfileFormatLoader`, `PromptTemplateLoader`, and all of `Spectra.Core`.
