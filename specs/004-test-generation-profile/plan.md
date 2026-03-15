# Implementation Plan: Test Generation Profile

**Branch**: `004-test-generation-profile` | **Date**: 2026-03-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-test-generation-profile/spec.md`

## Summary

Implement a profile system that allows teams to customize AI test case generation. The `spectra init-profile` command creates a Markdown profile file through an interactive questionnaire, capturing preferences for detail level, formatting, domain needs, and exclusions. The `spectra ai generate` command automatically loads and applies the profile to its AI context. Suite-level overrides are supported.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: Spectra.CLI (command integration), Spectra.Core (config, parsing), System.CommandLine (interactive prompts)
**Storage**: File-based (spectra.profile.md at repo root, _profile.md in suites)
**Testing**: xUnit with test fixtures
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI extension
**Performance Goals**: Profile loading <1s; questionnaire completion <5 minutes
**Constraints**: Profile must be human-readable Markdown; backward compatible with existing generation
**Scale/Scope**: Single repository; typically 1 repo profile + 0-5 suite overrides

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Profiles stored as committed Markdown files |
| II. Deterministic Execution | PASS | Same profile produces same AI context |
| III. Orchestrator-Agnostic Design | PASS | Profile is LLM-agnostic; works with any provider |
| IV. CLI-First Interface | PASS | `spectra init-profile` and `spectra profile show` commands |
| V. Simplicity (YAGNI) | PASS | Extends existing CLI; no new projects |

**Quality Gates Compliance**:
- Profile validation integrates with existing `spectra validate` pattern
- Commands support deterministic exit codes (0=success, 1=error)
- No new dependencies beyond existing System.CommandLine

## Project Structure

### Documentation (this feature)

```text
specs/004-test-generation-profile/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (profile format)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   ├── Models/
│   │   └── Profile/              # NEW: Profile models
│   │       ├── GenerationProfile.cs
│   │       ├── ProfileOptions.cs
│   │       └── ProfileValidationResult.cs
│   └── Profile/                  # NEW: Profile services
│       ├── ProfileLoader.cs
│       ├── ProfileParser.cs
│       ├── ProfileValidator.cs
│       └── ProfileWriter.cs
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── InitProfileCommand.cs     # NEW: spectra init-profile
│   │   └── ProfileCommand.cs         # NEW: spectra profile show
│   ├── Profile/                  # NEW: CLI profile support
│   │   ├── ProfileQuestionnaire.cs
│   │   └── ProfileRenderer.cs
│   └── Agent/
│       └── AgentRuntime.cs       # MODIFY: load profile into context
└── Spectra.MCP/                  # Existing (no changes)

tests/
├── Spectra.Core.Tests/
│   └── Profile/                  # NEW: Profile tests
│       ├── ProfileLoaderTests.cs
│       ├── ProfileParserTests.cs
│       └── ProfileValidatorTests.cs
└── Spectra.CLI.Tests/
    └── Profile/                  # NEW: Profile command tests
        ├── InitProfileCommandTests.cs
        └── ProfileShowTests.cs
```

**Structure Decision**: Extend existing Spectra.CLI and Spectra.Core projects. Profile models go in Core for reuse; profile commands go in CLI. No new solution projects required.

## Complexity Tracking

> No Constitution violations identified. All features fit within existing project structure.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | - | - |
