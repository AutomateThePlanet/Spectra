# Implementation Plan: Terminology, Folder Rename & Landing Page

**Branch**: `043-terminology-folder-landing` | **Date**: 2026-04-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/043-terminology-folder-landing/spec.md`

## Summary

Rename the default SPECTRA output directory from `tests/` to `test-cases/`, standardize "test case" terminology across all documentation/SKILLs/agents, rewrite the landing page with a value proposition, and replace the 4800-word cli-vs-chat analysis with a 150-word page. Four phases: code change → terminology sweep → content rewrites. No migration logic needed (zero external users).

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json
**Storage**: File system (Markdown + YAML + JSON config)
**Testing**: xUnit (462 + 466 + 351 = 1279 tests)
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI tool + library
**Performance Goals**: N/A (no runtime behavior changes)
**Constraints**: Zero external users — no backward compatibility needed
**Scale/Scope**: ~6 source files changed (Phase 1), ~40 documentation files reviewed (Phase 2-4), 2 demo repos updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | VIOLATION | Constitution line 30 says `tests/{suite}/` — must update to `test-cases/{suite}/` |
| II. Deterministic Execution | PASS | No execution engine changes |
| III. Orchestrator-Agnostic Design | PASS | No MCP or orchestrator changes |
| IV. CLI-First Interface | PASS | No CLI command changes |
| V. Simplicity (YAGNI) | PASS | No new abstractions; simple default value change + documentation updates |

**Post-design re-check**: Same results. Principle I violation is resolved by updating the constitution as part of Phase 2. Version bump 1.0.0 → 1.1.0 (MINOR — materially changed guidance on folder path).

## Project Structure

### Documentation (this feature)

```text
specs/043-terminology-folder-landing/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: scope analysis
├── data-model.md        # Phase 1: no structural changes (default value only)
├── quickstart.md        # Phase 1: implementation guide
├── checklists/
│   └── requirements.md  # Spec validation checklist
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code Changes (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/Config/TestsConfig.cs           # Default "tests/" → "test-cases/"
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── Init/InitHandler.cs                # Constant TestsDir → "test-cases"
│   │   └── Config/ConfigHandler.cs            # Fallback display string
│   ├── Templates/spectra.config.json          # Default dir value
│   └── Skills/Content/
│       ├── Skills/*.md                        # 11 SKILL files (terminology)
│       └── Agents/*.agent.md                  # 2 agent files (terminology)

tests/
├── Spectra.Core.Tests/Config/ConfigLoaderTests.cs  # Test fixture data
└── Spectra.CLI.Tests/Prompts/PromptTemplateLoaderTests.cs  # Uses default

docs/
├── index.md                                   # Landing page rewrite (Phase 3)
├── analysis/cli-vs-chat-generation.md         # Replace with 150 words (Phase 4)
├── *.md                                       # Terminology sweep (Phase 2)

.specify/memory/constitution.md                # tests/{suite}/ → test-cases/{suite}/
.github/workflows/*.yml.template               # tests/** → test-cases/** triggers

PROJECT-KNOWLEDGE.md                           # Tagline + terminology
README.md                                      # Tagline + terminology
CLAUDE.md                                      # Project structure references
```

**Structure Decision**: No new directories or projects. All changes modify existing files. The only "structural" change is the default output directory name.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Constitution Principle I update | Default folder path changed from `tests/` to `test-cases/` | Cannot leave constitution referencing a path that no longer matches the code default |
