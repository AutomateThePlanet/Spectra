# Implementation Plan: Unified Critic Provider List

**Branch**: `039-unify-critic-providers` | **Date**: 2026-04-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/039-unify-critic-providers/spec.md`

## Summary

Align the critic provider validation set with the generator provider list (`github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`). Add legacy alias mapping: `github` → `github-models` (soft warn), `google` → hard error. Wire actual validation into `CriticFactory.TryCreate` (currently blindly constructs the critic without validating). Update `CriticConfig` stale docstrings and switch-statement defaults. Update `docs/configuration.md` and `docs/grounding-verification.md`. No runtime/architectural change.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK (existing); no new dependencies
**Storage**: File-based — `spectra.config.json` schema unchanged on disk; only the validator's accepted set changes
**Testing**: xUnit (`Spectra.CLI.Tests`)
**Target Platform**: Cross-platform CLI
**Project Type**: Existing single solution
**Performance Goals**: N/A — pure validation/docs change
**Constraints**: Backward-compatible (no break for users on `openai`, `anthropic`, `github-models`); soft mapping for legacy `github`; hard error for legacy `google`
**Scale/Scope**: 2 modified code files, 2 modified docs, ~5 new tests, 0 new files

## Constitution Check

Evaluation against `.specify/memory/constitution.md` v1.0.0:

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ PASS | Config schema and docs are in Git. No external storage. |
| II. Deterministic Execution | ✅ PASS | Does not touch the MCP execution engine. Validation is deterministic. |
| III. Orchestrator-Agnostic Design | ✅ PASS | Aligns the critic with the same provider set the generator uses, both via the Copilot SDK runtime. Strengthens BYOK story. |
| IV. CLI-First Interface | ✅ PASS | No new commands; pure validation behind existing config loading. |
| V. Simplicity (YAGNI) | ✅ PASS | Removes drift between two independently-maintained provider lists. Net code is *fewer* concepts, not more. |

**Result**: PASS. Proceed.

## Project Structure

### Documentation (this feature)

```text
specs/039-unify-critic-providers/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — config field semantics
├── quickstart.md        # Phase 1 — verification scenarios
├── contracts/
│   └── critic-provider.schema.json
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/Config/CriticConfig.cs              # MODIFY — update docstring + switch defaults
└── Spectra.CLI/
    └── Agent/Critic/CriticFactory.cs              # MODIFY — canonical SupportedProviders + alias map + TryCreate validation

tests/
└── Spectra.CLI.Tests/
    └── Agent/Critic/CriticFactoryProviderTests.cs # NEW — 5 tests covering azure-openai accept, azure-anthropic accept, github→github-models alias, google hard error, unknown rejection

docs/
├── configuration.md               # MODIFY — critic provider list + Azure-only example
└── grounding-verification.md      # MODIFY — supported providers + remove google/GOOGLE_API_KEY

CLAUDE.md                          # MODIFY — Recent Changes entry for 039
```

**Structure Decision**: Single-solution layout, two modified files, one new test file. The change site is a single static class (`CriticFactory`) plus a stale docstring on `CriticConfig`. Surgical.

## Complexity Tracking

No constitution violations. Table omitted.
