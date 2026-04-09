# Implementation Plan: SPECTRA Update SKILL + Documentation Sync

**Branch**: `029-update-skill-docs-sync` | **Date**: 2026-04-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/029-update-skill-docs-sync/spec.md`

## Summary

Add the 10th bundled SKILL (`spectra-update`) wrapping `spectra ai update` for Copilot Chat integration. Update agent delegation tables so both agents route update requests to the SKILL. Extend `UpdateResult` with missing fields. Sync all documentation to reflect 10 SKILLs.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (serialization), Spectre.Console (terminal UX)
**Storage**: File-based (embedded resources for SKILL content, `.spectra-result.json` / `.spectra-progress.html` for progress)
**Testing**: xUnit (1354+ existing tests across 3 test projects)
**Target Platform**: Windows, macOS, Linux (cross-platform .NET CLI tool)
**Project Type**: CLI tool with VS Code Copilot Chat integration via bundled SKILLs
**Performance Goals**: N/A (no runtime performance changes)
**Constraints**: SKILL file must follow exact conventions (5-step flow, flags, step format, wait instruction) for Chat agent compatibility
**Scale/Scope**: ~150 lines new code, ~100 lines test code, ~50 lines documentation changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | SKILL files are stored in Git at `.github/skills/`, embedded in the CLI binary as resources |
| II. Deterministic Execution | PASS | No execution engine changes; SKILL delegates to existing deterministic CLI command |
| III. Orchestrator-Agnostic Design | PASS | SKILL uses standard tools format compatible with any VS Code Chat participant |
| IV. CLI-First Interface | PASS | SKILL wraps existing CLI command `spectra ai update`; no new functionality added outside CLI |
| V. Simplicity (YAGNI) | PASS | No new abstractions; follows exact pattern of 9 existing SKILLs. Embedded resource auto-discovery means minimal code changes |

All gates pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/029-update-skill-docs-sync/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/Skills/Content/Skills/
├── spectra-update.md          # NEW: 10th embedded SKILL resource

src/Spectra.CLI/Skills/
├── SkillContent.cs            # MODIFY: Add Update property
├── SkillsManifest.cs          # NO CHANGE: Auto-discovers via SkillContent.All

src/Spectra.CLI/Skills/Content/Agents/
├── spectra-generation.agent.md  # MODIFY: Move update section to delegation table
├── spectra-execution.agent.md   # MODIFY: Add update delegation row

src/Spectra.CLI/Skills/
├── AgentContent.cs              # NO CHANGE: Auto-loads from embedded resources

src/Spectra.CLI/Commands/Init/
├── InitHandler.cs               # NO CHANGE: Loops through SkillContent.All (auto-discovers)

src/Spectra.CLI/Results/
├── UpdateResult.cs              # MODIFY: Add missing fields (totalTests, testsFlagged, flaggedTests, duration, success)

tests/Spectra.CLI.Tests/Skills/
├── SkillsManifestTests.cs       # MODIFY: Update test count assertions, add spectra-update tests
```

**Structure Decision**: Follows existing project structure exactly. The SKILL is an embedded .md resource auto-discovered by `SkillResourceLoader`. No new directories or projects needed.

## Complexity Tracking

No violations. All changes follow established patterns.
