# Implementation Plan: Generation-skill inversion + completion

**Branch**: `059-generation-skill-inversion` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/059-generation-skill-inversion/spec.md`

## Summary

Move all three generation flows (bulk, from-description, behavior-analysis) off the in-process `spectra ai generate` model call and onto the deterministic compile → in-session-generate → ingest seam (Spec 053), then delete the in-process generator, provider chain, and the GitHub.Copilot.SDK dependency, and retire `ai.providers`.

Technical approach in two ordered phases (the spec's own ordering — rewrite the skill first, remove second):

1. **Seam coverage + skill rewrite** (additive, non-breaking). Bulk is already covered by `compile-prompt`/`ingest-tests`. Add the two missing surfaces: extend `compile-prompt` with `--from-description`/`--context` (forces count=1, reuses the existing criteria-injection path), and add a new sibling pair `compile-analysis-prompt` + `ingest-analysis` for the analyze-first recommendation (deterministic dedup/breakdown/recommended-count post-processing relocated out of `BehaviorAnalyzer`). Rewrite `spectra-generate.md` so every flow drives the seam with the mandatory `spectra-critic` subagent step and the 053 fail-loud retry.
2. **Removal + config retirement** (breaking, deferred until the skill is fully on the seam). Delete `CopilotGenerationAgent`, `CopilotService`, `ProviderMapping`, `AgentFactory.CreateAgentAsync` + its hardcoded `github-models`/`gpt-4o` fallback, `ProviderChain`, the model-calling halves of `BehaviorAnalyzer` and `UserDescribedGenerator`, and the in-process model-calling execution methods in `GenerateHandler`. Drop the `GitHub.Copilot.SDK` package reference. Retire `ai.providers` via the Spec 058 ignore-with-notice pattern (`DetectDeprecatedKeys`). Preserve every deterministic helper the seam reuses (`GenerateHandler.LoadCriteriaContextAsync`, profile/template loaders, `PromptCompiler`, `TestPersistenceService`, all of `Spectra.Core`).

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine, System.Text.Json, Spectre.Console, YamlDotNet, xUnit. **Removing**: GitHub.Copilot.SDK.
**Storage**: File-based — test-cases/ markdown + `_index.json`; `spectra.config.json`; criteria YAML. No DB change.
**Testing**: xUnit (`Spectra.Core.Tests`, `Spectra.CLI.Tests`). Structured results, never throw on validation.
**Target Platform**: Cross-platform CLI (win32 primary dev host) + Claude Code skill runtime.
**Project Type**: CLI tool + bundled `.claude/skills` authoring content.
**Performance Goals**: Compile commands deterministic and model-free (zero token spend, byte-identical repeat output). No latency regression on the seam.
**Constraints**: No in-process model call may remain in any generation flow. Legacy configs MUST still validate (non-silent migration). `Spectra.Core` and the 053/055 test corpora MUST stay green and unmodified.
**Scale/Scope**: ~1 skill rewrite, +1 command extension, +2 new commands (+ compiler/result/post-processor classes), ~6 removal targets, 1 package-ref removal, 1 config-key retirement, test rewrites for every removed surface, doc updates.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ No new storage. Tests/criteria/config stay file-based and Git-tracked. Persistence path unchanged (`TestPersistenceService`). |
| **II. Deterministic Execution** | ✅ Strengthened. Generation's only remaining CLI surface is deterministic, model-free compile/ingest. MCP state machine untouched. |
| **III. Orchestrator-Agnostic Design** | ⚠️ Principle III names a BYOK "provider chain" as a goal, and this spec removes it. **Justified** in Complexity Tracking: the provider chain is dead code post-inversion — the interactive Claude Code session is now the runtime, so BYOK provider selection no longer applies to generation. MCP tool-response minimalism and self-containment are unaffected. |
| **IV. CLI-First Interface** | ✅ Every flow remains a named CLI command with deterministic exit codes. The agent still never writes to disk directly — all writes go through `ingest-tests` → validated `TestPersistenceService`. The seam *increases* CLI-first compliance. |
| **V. Simplicity (YAGNI)** | ✅ Removal-heavy spec; deletes a whole transitional layer. New surface is minimized: from-description EXTENDS an existing command (no new command) rather than adding a third; only analyze (a genuinely distinct artifact) gets a new pair. No new abstractions. |

**Gate result**: PASS (one justified deviation on Principle III, recorded in Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/059-generation-skill-inversion/
├── plan.md              # This file
├── research.md          # Phase 0 — seam-shape decision + removal strategy
├── data-model.md        # Phase 1 — analysis recommendation entity, retired surfaces
├── quickstart.md        # Phase 1 — end-to-end seam walkthrough per flow
├── contracts/           # Phase 1 — new/extended CLI command contracts
│   ├── compile-prompt-from-description.md
│   ├── compile-analysis-prompt.md
│   ├── ingest-analysis.md
│   └── config-providers-retirement.md
├── checklists/
│   └── requirements.md  # (created by /speckit.specify)
└── tasks.md             # Phase 2 — created by /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Commands/
│   ├── Ai/AiCommand.cs                       # register compile-analysis-prompt + ingest-analysis
│   └── Generate/
│       ├── CompilePromptCommand.cs           # EXTEND: --from-description / --context (count=1)
│       ├── IngestTestsCommand.cs             # reused verbatim (already handles 1..N tests)
│       ├── CompileAnalysisPromptCommand.cs   # NEW (model-free analyze prompt → stdout)
│       ├── IngestAnalysisCommand.cs          # NEW (agent behavior JSON → deterministic recommendation)
│       ├── GenerateCommand.cs                # REMOVE model flows (see research for disposition)
│       └── GenerateHandler.cs                # KEEP static deterministic helpers; REMOVE model-calling methods
├── Generation/
│   ├── PromptCompiler.cs                      # reused; gains from-description user-prompt shaping if needed
│   ├── AnalysisPromptCompiler.cs             # NEW (relocated from BehaviorAnalyzer prompt build)
│   └── AnalysisRecommendation*.cs            # NEW result + post-processor (relocated dedup/breakdown)
├── Agent/
│   ├── AgentFactory.cs                        # REMOVE CreateAgentAsync + hardcoded fallback
│   ├── ProviderChain.cs                       # REMOVE
│   └── Copilot/
│       ├── CopilotService.cs                 # REMOVE
│       ├── GenerationAgent.cs                # REMOVE (CopilotGenerationAgent)
│       ├── ProviderMapping.cs                # REMOVE
│       └── BehaviorAnalyzer.cs               # REMOVE model call; relocate deterministic post-processing
│   └── …UserDescribedGenerator.cs            # REMOVE model call (BuildPrompt logic relocated to seam)
└── Skills/Content/Skills/spectra-generate.md # REWRITE onto the seam

src/Spectra.Core/Models/Config/
└── AiConfig.cs                                # retire Providers (ignore-with-notice) — see contract
src/Spectra.Core/Config/ConfigLoader.cs        # add ai.providers to DeprecatedKeyPaths; drop MISSING_PROVIDERS

src/Spectra.CLI/Spectra.CLI.csproj             # REMOVE GitHub.Copilot.SDK PackageReference

tests/
├── Spectra.Core.Tests/Config/                 # ProviderRetirementTests → update (ai.providers now retired)
└── Spectra.CLI.Tests/Commands/                # rewrite generate/provider tests; net-new seam tests
```

**Structure Decision**: Single-project CLI layout (existing). No new project. The seam lives under `Commands/Generate/` and `Generation/` alongside the 053/054/055 commands; removals are confined to `Agent/` plus the model-calling methods in `Generate/`. `Spectra.MCP` is untouched.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Removing the BYOK provider chain named in Constitution Principle III | After the generation handoff is inverted (053–059), the provider chain is dead code — generation runs in the interactive Claude Code session, which IS the runtime; there is no in-process model call left to select a provider for. Keeping it would be an unused, misleading config surface. | Keeping the provider chain "for optionality" was the Spec 058 narrow stance; it only made sense while the in-process generator still read `ai.providers`. Once FR-001 lands, nothing reads it, so retention violates YAGNI and Principle V more than its removal strains Principle III. The critic model selector (`ai.critic.model`) and BYOK at the *session* level remain. |
