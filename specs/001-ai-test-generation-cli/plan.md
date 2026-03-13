# Implementation Plan: AI Test Generation CLI

**Branch**: `001-ai-test-generation-cli` | **Date**: 2026-03-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-ai-test-generation-cli/spec.md`

## Summary

Build the AI Test Generation CLI (Spectra.CLI) that generates manual test cases from documentation using the GitHub Copilot SDK. The CLI reads documentation from `docs/`, generates test cases as Markdown files with YAML frontmatter in `tests/{suite}/`, and maintains metadata indexes for efficient querying. Key capabilities include batch generation with review workflow, validation, indexing, and multi-provider fallback support.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI), Microsoft.Extensions.AI (AI tools), Markdig (Markdown parsing), YamlDotNet (frontmatter), GitHub Copilot SDK (AI runtime)
**Storage**: File system (Markdown + JSON), no database for CLI subsystem
**Testing**: xUnit for unit/integration tests
**Target Platform**: Cross-platform (.NET 8 runtime: Windows, macOS, Linux)
**Project Type**: CLI application with shared library
**Performance Goals**: Validation <2s for 500 tests, Index rebuild <5s for 500 tests (per SC-004, SC-005)
**Constraints**: Offline-capable for validation/indexing; AI operations require network
**Scale/Scope**: Repositories with up to 500 test files initially

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. GitHub as Source of Truth | ✅ PASS | Tests stored as Markdown in `tests/{suite}/`, config in `spectra.config.json`, indexes committed |
| II. Deterministic Execution | ✅ PASS (N/A for CLI) | CLI subsystem does not implement MCP execution engine |
| III. Orchestrator-Agnostic Design | ✅ PASS | Provider chain supports BYOK for Copilot, OpenAI, Azure, Anthropic |
| IV. CLI-First Interface | ✅ PASS | All operations are named commands with explicit parameters; supports `--dry-run` and `--no-review` |
| V. Simplicity (YAGNI) | ✅ PASS | No abstractions beyond architecture spec; standard .NET patterns |

**Quality Gates Compliance**:
- Schema Validation: FR-015 implements this
- ID Uniqueness: FR-016 implements this
- Index Currency: FR-020 implements this
- Dependency Resolution: FR-017 implements this
- Priority Enum: FR-018 implements this

**Development Workflow Compliance**:
- Test-Required: xUnit tests for Core (parsing, validation, indexing) and CLI (command workflows)
- Target coverage: Core 80%+, CLI 60%+

## Project Structure

### Documentation (this feature)

```text
specs/001-ai-test-generation-cli/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (CLI command schemas)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/              # .NET CLI application
│   ├── Commands/             # Command handlers (init, validate, index, ai generate, etc.)
│   ├── Agent/                # Copilot SDK integration
│   │   ├── Tools/            # Custom tools for the AI agent
│   │   └── Skills/           # Skill loader
│   ├── Source/               # Document map builder, source doc reader
│   ├── Index/                # _index.json builder and reader
│   ├── Validation/           # Test case validation, dedup
│   ├── Review/               # Interactive terminal review UI
│   ├── Provider/             # Multi-provider chain with fallback
│   ├── Git/                  # Branch/commit operations
│   ├── Config/               # Configuration loader
│   └── IO/                   # File writers with lock support
├── Spectra.Core/             # Shared library
│   ├── Models/               # TestCase, Suite, MetadataIndex, DocumentMap, SpectraConfig
│   ├── Parsing/              # Markdown + YAML frontmatter parser
│   ├── Validation/           # Schema validation rules
│   └── Index/                # Index read/write
└── Spectra.GitHub/           # GitHub integration (Octokit) - future phase

tests/
├── Spectra.Core.Tests/       # Unit tests for parsing, validation, models
├── Spectra.CLI.Tests/        # Integration tests for CLI commands
└── TestFixtures/             # Sample docs and test files for testing
```

**Structure Decision**: Multi-project solution following the architecture spec. Spectra.Core contains shared models and parsing logic. Spectra.CLI contains the CLI application. Spectra.MCP and Spectra.GitHub are deferred to later phases.

## Complexity Tracking

> No Constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

---

## Phase 0: Research (Completed)

See [research.md](./research.md) for technical decisions:

1. **Copilot SDK Integration**: Use `CopilotClient` + `SessionConfig` with `AIFunctionFactory.Create()` for tool registration
2. **System.CommandLine**: Static command builders with global options (`Recursive = true`)
3. **Markdown/YAML Parsing**: Markdig + YamlDotNet with result pattern (no exceptions)
4. **Terminal UI**: Spectre.Console for rich output, streaming, and interactive review
5. **Concurrency**: Suite-level lock files with 10-minute auto-expiry
6. **Logging**: Microsoft.Extensions.Logging with verbosity flags (-v/-vv)

## Phase 1: Design (Completed)

See design artifacts:

1. **[data-model.md](./data-model.md)**: Core entities (TestCase, TestSuite, MetadataIndex, DocumentMap, SpectraConfig)
2. **[contracts/cli-commands.md](./contracts/cli-commands.md)**: CLI command specifications
3. **[contracts/ai-tools.md](./contracts/ai-tools.md)**: AI agent tool interfaces
4. **[quickstart.md](./quickstart.md)**: User-facing usage guide

## Phase 2: Tasks

Run `/speckit.tasks` to generate the implementation task list.

---

## Constitution Re-Check (Post-Design)

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. GitHub as Source of Truth | ✅ PASS | All models designed for file-based storage (Markdown, JSON) |
| II. Deterministic Execution | ✅ PASS (N/A) | CLI does not implement MCP engine |
| III. Orchestrator-Agnostic | ✅ PASS | ProviderConfig model supports all major providers |
| IV. CLI-First Interface | ✅ PASS | All commands defined with explicit params, --dry-run, --no-review |
| V. Simplicity (YAGNI) | ✅ PASS | Standard .NET patterns; result types instead of exceptions |

**All constitution principles satisfied. Ready for task generation.**
