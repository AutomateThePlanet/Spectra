# Implementation Plan: Quickstart SKILL & Usage Guide

**Branch**: `032-quickstart-skill-usage-guide` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/032-quickstart-skill-usage-guide/spec.md`

## Summary

Add a 12th bundled SKILL (`spectra-quickstart`) that provides workflow-oriented onboarding for users interacting with SPECTRA via VS Code Copilot Chat, plus a companion offline reference document (`USAGE.md`) in the project root. Both artifacts ship as embedded resources, are written by `spectra init`, and are tracked by the existing `update-skills` hash system. The bundled generation and execution agent prompts are updated to defer onboarding requests to the new SKILL.

This is a content + wiring feature only — no new code paths, no new commands, no new infrastructure. It reuses the existing `SkillContent`/`SkillsManifest`/`InitCommandHandler` patterns established by spec 022 (bundled-skills) and extended by spec 029 (spectra-update SKILL) and spec 030 (spectra-prompts SKILL + CUSTOMIZATION.md doc bundling).

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine, Spectre.Console, embedded resources via `<EmbeddedResource>` in `Spectra.CLI.csproj`
**Storage**: File system only — `.github/skills/spectra-quickstart/SKILL.md` and `USAGE.md` written to project root by init
**Testing**: xUnit (`Spectra.CLI.Tests`)
**Target Platform**: Cross-platform CLI (.NET 8 runtime)
**Project Type**: Single-project CLI (Spectra.CLI) with shared library (Spectra.Core) and MCP server (Spectra.MCP)
**Performance Goals**: N/A — content delivery only, no runtime hot path
**Constraints**: Must follow the existing hash-tracking discipline so `update-skills` does not clobber user customizations
**Scale/Scope**: 2 new embedded resources (one SKILL, one doc), 2 new manifest entries, 2 agent prompt edits, ~3 new tests, ~12 file touches

## Constitution Check

Reviewed all five principles in `.specify/memory/constitution.md`:

| Principle | Compliance |
|-----------|------------|
| I. GitHub as Source of Truth | ✅ Both new artifacts are markdown files committed to the repo and deployed to user projects via init. |
| II. Deterministic Execution | ✅ N/A — no execution engine changes. Init/update flows remain deterministic file writes guarded by hash comparison. |
| III. Orchestrator-Agnostic Design | ✅ The quickstart SKILL is teaching content for any orchestrator that loads `.github/skills/`. No orchestrator-specific bindings. |
| IV. CLI-First Interface | ✅ No new command. Existing `spectra init` and `spectra update-skills` commands deliver the new content. |
| V. Simplicity (YAGNI) | ✅ Reuses existing embedded-resource + manifest pattern. No new abstractions. The two artifacts are mirrors of each other for two contexts (in-chat vs offline) — duplication is intentional and minimal. |

**Result**: PASS. No violations. No entries needed in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/032-quickstart-skill-usage-guide/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — pattern research (existing SKILL bundling)
├── data-model.md        # Phase 1 — entities (SKILL file, USAGE.md doc, manifest entries)
├── quickstart.md        # Phase 1 — how to verify the feature locally
├── contracts/           # Phase 1 — output contract for init / update-skills
│   └── init-contract.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created)
└── tasks.md             # Phase 2 (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Skills/
│   ├── Content/
│   │   ├── Skills/
│   │   │   ├── spectra-coverage.md
│   │   │   ├── spectra-criteria.md
│   │   │   ├── spectra-dashboard.md
│   │   │   ├── spectra-docs.md
│   │   │   ├── spectra-generate.md
│   │   │   ├── spectra-help.md
│   │   │   ├── spectra-init-profile.md
│   │   │   ├── spectra-list.md
│   │   │   ├── spectra-prompts.md
│   │   │   ├── spectra-update.md
│   │   │   ├── spectra-validate.md
│   │   │   └── spectra-quickstart.md            # NEW
│   │   ├── Docs/
│   │   │   ├── CUSTOMIZATION.md
│   │   │   └── USAGE.md                          # NEW
│   │   └── Agents/
│   │       ├── spectra-generation.agent.md       # EDIT (defer onboarding to quickstart)
│   │       └── spectra-execution.agent.md        # EDIT (defer onboarding to quickstart)
│   ├── SkillContent.cs                           # EDIT — add QuickstartSkill, UsageGuide accessors
│   └── SkillsManifest.cs                         # EDIT — add 2 hash entries
├── Commands/Init/InitCommandHandler.cs           # EDIT — write the 2 new files
└── Spectra.CLI.csproj                            # EDIT — register USAGE.md as EmbeddedResource

tests/Spectra.CLI.Tests/
├── Skills/SkillContentTests.cs                   # EDIT — 2 new tests
└── Commands/Init/InitCommandHandlerTests.cs      # EDIT — 1 new test

# Project-root docs touched:
README.md                                          # EDIT — add Copilot Chat section
CHANGELOG.md                                       # EDIT — document the feature
PROJECT-KNOWLEDGE.md                               # EDIT — bump SKILL count, mention USAGE.md
CLAUDE.md                                          # EDIT — bump SKILL count, mention USAGE.md
```

**Structure Decision**: Single-project CLI. All new code lives in `Spectra.CLI`. The two new content files plug into the existing `Skills/Content/` embedded-resource tree the same way every prior bundled SKILL and the existing `CUSTOMIZATION.md` do. No new directories required.

## Complexity Tracking

> **Constitution Check passed with no violations. This section intentionally empty.**
