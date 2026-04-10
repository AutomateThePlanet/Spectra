# Implementation Plan: ISTQB Test Design Techniques in Prompt Templates

**Branch**: `037-istqb-test-techniques` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/037-istqb-test-techniques/spec.md`

## Summary

Embed six ISTQB black-box test design techniques (EP, BVA, DT, ST, EG, UC) into SPECTRA's five prompt templates so AI-driven analysis and generation produces systematically distributed test cases instead of generic happy-path scenarios. Add a `Technique` field to `IdentifiedBehavior`, a `TechniqueHint` field to `AcceptanceCriterion`, expose a `technique_breakdown` map in structured analysis results / terminal output / progress page, and update the legacy fallback prompt and `spectra init` defaults. Migration is opt-in via `spectra prompts reset`.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK (sole AI runtime), System.Text.Json, Spectre.Console, xUnit
**Storage**: File-based — prompt templates in `src/Spectra.CLI/Prompts/Content/*.md` (embedded resources) and `.spectra/prompts/*.md` (per-project user copies); SHA-256 hash tracking via `SkillsManifest`
**Testing**: xUnit (`Spectra.CLI.Tests`, `Spectra.Core.Tests`)
**Target Platform**: Cross-platform CLI (Windows/Linux/macOS)
**Project Type**: Single solution, multi-project (CLI + Core + MCP + tests)
**Performance Goals**: No new perf budget — prompt-only changes; analysis latency dominated by AI provider, not template parsing
**Constraints**: Backward compatible parsing of legacy AI responses (no `technique` field); existing user-edited template files MUST NOT be silently overwritten
**Scale/Scope**: 5 prompt templates rewritten/extended, 2 model fields added, ~6 CLI/output files modified, ~15+ new tests, ~3 SKILL files and 1 agent prompt updated, ~10 doc files updated

## Constitution Check

Evaluation against `.specify/memory/constitution.md` v1.0.0:

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ PASS | All prompt templates remain markdown files in the repo; no external storage. User customization stays in `.spectra/prompts/` (already committable). |
| II. Deterministic Execution | ✅ PASS | This feature does not touch the MCP execution engine. Template content is deterministic input to the AI; AI responses are non-deterministic by nature, but parsing/output structure is deterministic and backward-compatible. |
| III. Orchestrator-Agnostic Design | ✅ PASS | No orchestrator-specific changes. Templates flow through the existing Copilot SDK runtime which already supports BYOK providers. JSON schema additions are additive. |
| IV. CLI-First Interface | ✅ PASS | All migration/reset operations use existing CLI commands (`spectra prompts reset`, `spectra update-skills`, `spectra init`). No new commands. No GUI. |
| V. Simplicity (YAGNI) | ✅ PASS | No new abstractions: technique tag is a plain string field on an existing model; technique breakdown is a `Dictionary<string,int>`; six techniques are a fixed enumeration in prompts (no config schema). The 40% cap is a prompt instruction, not a runtime filter. No backwards-compat shims needed because the parser already tolerates missing fields. |

**Quality Gates** (validate/ID-uniqueness/etc.) are unaffected — no test file schema changes.

**Result**: PASS. No constitution violations. Proceed.

## Project Structure

### Documentation (this feature)

```text
specs/037-istqb-test-techniques/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — model field changes
├── quickstart.md        # Phase 1 — how to verify after implementation
├── contracts/
│   ├── identified-behavior.schema.json
│   ├── analysis-result.schema.json
│   └── acceptance-criterion.schema.json
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (created by /speckit.tasks)
```

### Source Code (repository root)

This feature touches existing files only — no new projects, no new top-level directories.

```text
src/
├── Spectra.CLI/
│   ├── Prompts/
│   │   ├── Content/
│   │   │   ├── behavior-analysis.md         # REWRITE — full ISTQB technique sections
│   │   │   ├── test-generation.md           # MODIFY — technique-aware step rules
│   │   │   ├── test-update.md               # MODIFY — technique completeness check
│   │   │   ├── critic-verification.md       # MODIFY — technique verification rules
│   │   │   └── criteria-extraction.md       # MODIFY — technique_hint extraction
│   │   └── BuiltInTemplates.cs              # No code change — embedded resources auto-rebuild
│   ├── Agent/
│   │   ├── Analysis/IdentifiedBehavior.cs   # MODIFY — add Technique string field
│   │   └── Copilot/BehaviorAnalyzer.cs      # MODIFY — legacy fallback prompt + technique aggregation
│   ├── Results/GenerateResult.cs            # MODIFY — add TechniqueBreakdown to analysis section
│   ├── Output/
│   │   ├── AnalysisPresenter.cs             # MODIFY — render technique breakdown in terminal
│   │   └── ProgressPageWriter.cs            # MODIFY — render technique breakdown in HTML
│   ├── Commands/Init/InitHandler.cs         # No code change — uses BuiltInTemplates content
│   ├── Skills/
│   │   ├── SkillContent.cs                  # MODIFY — spectra-generate, spectra-help, spectra-quickstart
│   │   └── AgentContent.cs                  # MODIFY — spectra-generation agent prompt
└── Spectra.Core/
    └── Models/AcceptanceCriterion.cs        # MODIFY — add TechniqueHint string? property

tests/
├── Spectra.CLI.Tests/
│   ├── Prompts/
│   │   ├── BehaviorAnalysisTemplateTests.cs        # NEW — technique instructions, 40% cap, technique field
│   │   ├── TestGenerationTemplateTests.cs          # NEW (or extend) — BVA/EP/DT/ST/EG step rules
│   │   ├── TestUpdateTemplateTests.cs              # NEW — technique completeness check
│   │   ├── CriticVerificationTemplateTests.cs      # NEW — technique verification
│   │   └── CriteriaExtractionTemplateTests.cs      # NEW — technique hints
│   ├── Agent/
│   │   ├── IdentifiedBehaviorTests.cs              # MODIFY — Technique field round-trip + back-compat
│   │   └── BehaviorAnalyzerLegacyFallbackTests.cs  # NEW — fallback prompt contains techniques
│   ├── Results/GenerateResultTests.cs              # MODIFY — TechniqueBreakdown serialization
│   ├── Output/
│   │   ├── AnalysisPresenterTests.cs               # MODIFY — terminal technique breakdown
│   │   └── ProgressPageWriterTests.cs              # MODIFY — HTML technique breakdown section
│   └── Skills/SkillContentTests.cs                 # MODIFY — SKILL text references technique_breakdown
└── Spectra.Core.Tests/
    └── Models/AcceptanceCriterionTests.cs          # MODIFY — TechniqueHint serialization + null default

docs/                    # Documentation updates (no code)
├── getting-started.md
├── generation-profiles.md
├── cli-reference.md
├── coverage.md
└── skills-integration.md

CLAUDE.md                # MODIFY — Recent Changes entry
PROJECT-KNOWLEDGE.md     # MODIFY — template descriptions
README.md                # MODIFY — AI Test Generation feature blurb
USAGE.md                 # MODIFY — workflow example
```

**Structure Decision**: Single-solution layout (existing). All work fits inside the existing `Spectra.CLI` project plus one model field in `Spectra.Core`. No new projects, no new directories.

## Complexity Tracking

No constitution violations. Table omitted.
