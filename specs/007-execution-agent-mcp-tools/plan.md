# Implementation Plan: Bundled Execution Agent & MCP Data Tools

**Branch**: `007-execution-agent-mcp-tools` | **Date**: 2026-03-19 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-execution-agent-mcp-tools/spec.md`

## Summary

Create bundled execution agent prompts for Copilot Chat/CLI and Claude, add three deterministic MCP data tools (`validate_tests`, `rebuild_indexes`, `analyze_coverage_gaps`), update `spectra init` to install agent files, and provide usage documentation.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: Spectra.Core (parsing, validation, indexing), Spectra.MCP (tool registry, protocol), System.Text.Json, System.CommandLine
**Storage**: File system (Markdown test files, JSON indexes), embedded resources for bundled agent prompts
**Testing**: xUnit with test fixtures
**Target Platform**: Cross-platform CLI and MCP server (Windows, macOS, Linux)
**Project Type**: CLI tool + MCP server
**Performance Goals**: MCP tools complete in <5s for 500 test files
**Constraints**: No AI/model dependency for data tools; deterministic operations only
**Scale/Scope**: Repositories with up to 500 test files across multiple suites

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. GitHub as Source of Truth | PASS | Agent prompts stored as `.github/agents/*.agent.md` and `.github/skills/*/SKILL.md` in Git |
| II. Deterministic Execution | PASS | All three MCP tools are deterministic data operations - no AI, same inputs produce same outputs |
| III. Orchestrator-Agnostic Design | PASS | Agent prompt works with Copilot Chat, Copilot CLI, Claude, and generic MCP clients |
| IV. CLI-First Interface | PASS | `spectra init` installs agent files via CLI; MCP tools exposed through existing MCP server |
| V. Simplicity (YAGNI) | PASS | Reuses existing parsers, validators, and index handlers; no new abstractions |

## Project Structure

### Documentation (this feature)

```text
specs/007-execution-agent-mcp-tools/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── mcp-tools.md     # MCP tool contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Commands/Init/
│   │   ├── InitCommand.cs       # Add agent file installation
│   │   └── InitHandler.cs       # Update with agent file logic
│   ├── Agent/
│   │   └── Resources/           # NEW: Embedded agent prompt files
│   │       ├── spectra-execution.agent.md
│   │       └── SKILL.md
│   └── Spectra.CLI.csproj       # Add embedded resources
│
├── Spectra.MCP/
│   ├── Tools/
│   │   └── Data/                # NEW: Data tools (no AI dependency)
│   │       ├── ValidateTestsTool.cs
│   │       ├── RebuildIndexesTool.cs
│   │       └── AnalyzeCoverageGapsTool.cs
│   └── Program.cs               # Register new tools
│
└── Spectra.Core/
    ├── Validation/
    │   └── TestValidator.cs     # Already exists, reuse
    ├── Index/
    │   └── IndexGenerator.cs    # Already exists, reuse
    └── Coverage/
        └── GapAnalyzer.cs       # Already exists in CLI, may move to Core

tests/
├── Spectra.CLI.Tests/
│   └── Commands/Init/
│       └── InitAgentFilesTests.cs
└── Spectra.MCP.Tests/
    └── Tools/Data/
        ├── ValidateTestsToolTests.cs
        ├── RebuildIndexesToolTests.cs
        └── AnalyzeCoverageGapsToolTests.cs

docs/
└── execution-agent/             # NEW: Usage documentation
    ├── copilot-chat.md
    ├── copilot-cli.md
    ├── claude.md
    └── generic-mcp.md
```

**Structure Decision**: Extends existing project structure. New MCP tools go in `src/Spectra.MCP/Tools/Data/` to distinguish from execution-related tools. Agent prompts are embedded resources in CLI for portability.

## Complexity Tracking

> No Constitution violations detected. All additions follow established patterns.

| Component | Justification |
|-----------|---------------|
| Embedded resources for agent files | Required for `spectra init` to install files without network dependency |
| Three new MCP tools | Each serves distinct purpose (validate/rebuild/analyze) per spec requirements |

## Constitution Check (Post-Design)

*Re-evaluated after Phase 1 design completion.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. GitHub as Source of Truth | PASS | Agent prompts in `.github/`, tests in `tests/`, docs in `docs/` - all Git-tracked |
| II. Deterministic Execution | PASS | MCP tools validate/rebuild/analyze are pure data operations; no AI, no randomness |
| III. Orchestrator-Agnostic Design | PASS | Single agent prompt works across Copilot Chat, CLI, Claude; MCP tools self-contained |
| IV. CLI-First Interface | PASS | `spectra init` CLI command installs files; `spectra mcp call <tool>` for data tools |
| V. Simplicity (YAGNI) | PASS | Reuses TestValidator, IndexGenerator, GapAnalyzer; no new abstractions introduced |

## Phase Completion

| Phase | Status | Artifacts |
|-------|--------|-----------|
| Phase 0: Research | Complete | [research.md](research.md) |
| Phase 1: Design | Complete | [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md) |
| Phase 2: Tasks | Complete | [tasks.md](tasks.md) - 76 tasks across 10 phases |
