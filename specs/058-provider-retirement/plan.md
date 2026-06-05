# Implementation Plan: Provider retirement + config cleanup + demo repos

**Branch**: `058-provider-retirement` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/058-provider-retirement/spec.md`

## Summary

Convergence spec of the 052–058 migration. Specs 053–057 shipped additively, each building a Claude
Code replacement (the `compile-prompt`/`ingest-tests` generation seam, the `ingest-criteria`
extraction seam, the `spectra-critic` subagent, the authoring/execution skills) next to the legacy
in-process GitHub Copilot SDK path. This spec removes the legacy path: it **deletes the closed
Copilot SDK cluster and the `GitHub.Copilot.SDK` package**, **rewires the four command handlers**
(`GenerateHandler`, `UserDescribedGenerator`, `AnalyzeHandler`, `DocsIndexHandler`) onto the
already-landed deterministic seams, **gutts the `AgentFactory`/`CriticFactory` keystones** to drop
SDK construction, and **collapses the config** to the Claude-Code-only shape where `ai.critic.model`
is the only model selector. Existing configs that still carry the dead keys are accepted with a
**non-blocking, key-naming notice** (no silent drop); the two demo configs are hand-migrated. See
[research.md](./research.md) for the grounded call-site map and decisions.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine, System.Text.Json, Spectre.Console, YamlDotNet,
Microsoft.Data.Sqlite (MCP). **Removing**: `GitHub.Copilot.SDK` 0.2.1 (`Spectra.CLI.csproj:13`, the
sole reference).
**Storage**: File-based (`spectra.config.json`, `test-cases/`, `docs/`), SQLite (`.execution/spectra.db`, MCP — untouched).
**Testing**: xUnit — Core ≈ 496, CLI ≈ 1 050, MCP ≈ 318 (≈ 1 864 total).
**Target Platform**: .NET 8 CLI (Windows-primary, cross-platform) + ASP.NET Core MCP server.
**Project Type**: Multi-project solution (CLI + Core + MCP). This spec touches **CLI + Core config models only**; MCP untouched.
**Performance Goals**: N/A (removal). Gate: build stays green; surviving-config + MCP test corpus unchanged and green.
**Constraints**: No `Spectra.MCP` change (FR-008). Never edit Core/persistence tests (memory constraint). No Co-Authored-By line on commits. Keep CLAUDE.md < 40K.
**Scale/Scope**: ~20 source files edited/deleted, 1 NuGet package removed, ~12–19 tests rewritten (Bucket A), ~4 net-new test files (Bucket C), 2 demo configs migrated, ~5 doc pages updated.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Note |
|---|---|---|
| I. GitHub as Source of Truth | ✅ Pass | No change to where tests/docs/config live. |
| II. Deterministic Execution (MCP) | ✅ Pass / improved | MCP untouched (FR-008). Removing the in-process model call makes the CLI strictly more deterministic. |
| III. Orchestrator-Agnostic Design — "Provider chain MUST support BYOK" | ⚠️ Justified supersession | This spec removes the CLI provider chain. See Complexity Tracking. The MCP API (the principle's core) stays orchestrator-agnostic; BYOK moves to the Claude Code client layer per ARCHITECTURE-v2. |
| IV. CLI-First Interface | ✅ Pass | Commands remain named, deterministic, CI-friendly; the AI never writes files directly (ingest/persist still go through validated handlers). |
| V. Simplicity (YAGNI) | ✅ Pass / endorsed | "No backwards-compatibility shims when code can be changed directly" directly endorses deleting the dead chain, config, and SDK. |

**Gate result**: PASS with one documented supersession (Principle III provider-chain clause),
recorded in Complexity Tracking. A follow-up constitution amendment is recommended but out of scope.

Post-design re-check: unchanged — the design adds no new project, dependency, or MCP tool; it only
removes. Gate remains PASS.

## Project Structure

### Documentation (this feature)

```text
specs/058-provider-retirement/
├── spec.md              # Feature specification (/speckit.specify)
├── plan.md              # This file (/speckit.plan)
├── research.md          # Phase 0 — grounded call-site map + decisions
├── data-model.md        # Phase 1 — config shape before/after
├── quickstart.md        # Phase 1 — verification walkthrough
├── contracts/           # Phase 1 — cleaned schema + command/notice contracts
│   ├── config-schema.md
│   └── command-behavior.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (complete)
└── tasks.md             # Phase 2 (/speckit.tasks — not created here)
```

### Source Code (repository root) — files this plan touches

```text
src/Spectra.CLI/
├── Agent/
│   ├── AgentFactory.cs                 # GUT: remove CreateAgentAsync + SDK construction
│   ├── Copilot/                        # DELETE the SDK cluster; KEEP language-neutral helpers
│   │   ├── CopilotService.cs           # DELETE
│   │   ├── ProviderMapping.cs          # DELETE
│   │   ├── GenerationAgent.cs          # DELETE (CopilotGenerationAgent)
│   │   ├── BehaviorAnalyzer.cs         # DELETE
│   │   ├── CriteriaExtractor.cs        # DELETE
│   │   ├── RequirementsExtractor.cs    # DELETE
│   │   ├── GroundingAgent.cs           # DELETE (CopilotCritic)
│   │   ├── DocumentTools.cs            # KEEP   (no SDK)
│   │   ├── TestIndexTools.cs           # KEEP
│   │   ├── AnalyzerInputBuilder.cs     # KEEP
│   │   ├── DocSuiteSelector.cs         # KEEP
│   │   ├── CriteriaExtractionResult.cs # KEEP
│   │   ├── RequirementsExtractionResult.cs # KEEP
│   │   └── IExtractionDelayProvider.cs # KEEP
│   └── Critic/
│       ├── CriticFactory.cs            # GUT: remove CopilotCritic construction; keep TryCreate contract
│       └── CriticModelResolver.cs      # KEEP unchanged (single ai.critic.model source)
├── Provider/
│   └── ProviderChain.cs                # DELETE (thin wrapper over AgentFactory.CreateAgentAsync)
├── Commands/
│   ├── Generate/GenerateHandler.cs     # REWIRE: drop in-process generate/analyze/critic branches
│   ├── Generate/UserDescribedGenerator.cs # REWIRE: drop in-process call
│   ├── Analyze/AnalyzeHandler.cs       # REWIRE: drop in-process CriteriaExtractor
│   ├── Docs/DocsIndexHandler.cs        # REWIRE: drop in-process RequirementsExtractor
│   ├── Auth/AuthHandler.cs             # REDUCE to informational; drop SDK probe
│   ├── Init/InitHandler.cs             # drop GetAuthStatusAsync call
│   └── Config/ConfigHandler.cs         # drop providers/fallback display
├── Templates/spectra.config.json       # remove dead keys from the seed
└── Spectra.CLI.csproj                  # remove GitHub.Copilot.SDK PackageReference

src/Spectra.Core/
├── Models/Config/AiConfig.cs           # remove Providers (required) + FallbackStrategy
├── Models/Config/CriticConfig.cs       # remove Provider/ApiKeyEnv/BaseUrl + per-provider switches; relax IsValid
├── Models/Config/ProviderConfig.cs     # DELETE (orphaned)
└── Config/ConfigLoader.cs              # relax MISSING_PROVIDERS; add DetectDeprecatedKeys

tests/  (Bucket A rewrite, Bucket C add — see research.md §D-5)

# External (FR-009) — hand-migrated, not in this repo:
#   C:/SourceCode/Spectra_Demo/test_app_documentation/spectra.config.json
#   C:/SourceCode/AutomateThePlanet_SystemTests/spectra.config.json
```

**Structure Decision**: Single-solution CLI + Core edit. No new projects, no new dependencies — net
removal. MCP project is provably untouched (`git status src/Spectra.MCP` must stay empty).

## Implementation Phasing (build stays green at each phase)

1. **Config models + loader** (Core) — remove dead fields, relax validation, add `DetectDeprecatedKeys`; rewrite Bucket A Core-config tests; add Bucket C cleaned-schema + ignore-with-notice tests. *Build Core green.*
2. **Handler rewire** (CLI) — drop the in-process branches in `GenerateHandler`/`UserDescribedGenerator`/`AnalyzeHandler`/`DocsIndexHandler`, leaning on the landed seams; reduce `AuthHandler`; drop `InitHandler` auth call; fix `ConfigHandler` display. *Build CLI green against the still-present cluster.*
3. **Keystone gut + cluster delete** (CLI) — gut `AgentFactory`/`CriticFactory`, delete the Copilot SDK cluster files + `ProviderChain` + `ProviderConfig`, remove the `GitHub.Copilot.SDK` package. Remove/rewrite Bucket A CLI/integration tests that drove the deleted classes. *Build whole solution green; SDK gone.*
4. **Demo migration + docs** — hand-migrate the two demo configs and verify init/run; update docs (`configuration`, `cli-reference`, `copilot-cli`/`copilot-chat`, `overview`, getting-started snippets); refresh CLAUDE.md off "GitHub Copilot SDK (sole AI runtime)".
5. **Full verification** — `dotnet build` + `dotnet test`; confirm SC-001..SC-007; `git status src/Spectra.MCP` empty.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Supersede Constitution Principle III's "Provider chain MUST support BYOK (OpenAI/Azure/Anthropic)" | The entire 052–058 migration inverts the runtime: Claude Code (the orchestrator) owns the model and BYOK at the client layer; embedding a provider runtime in the CLI is the v1 mechanism being retired. Keeping the chain would defeat the migration's purpose and re-introduce the `GitHub.Copilot.SDK` coupling this spec removes. | Keeping a vestigial provider chain "for BYOK" was rejected: it would leave dead, untested code referencing a deleted SDK, violate Principle V (no shims when code can change), and contradict ARCHITECTURE-v2. The orchestrator-agnostic *contract* (the MCP API) is preserved; only the in-CLI provider mechanism is removed. Recommend a constitution amendment to re-scope Principle III to the MCP layer (out of scope here; logged as debt per the constitution's Supersession clause). |
