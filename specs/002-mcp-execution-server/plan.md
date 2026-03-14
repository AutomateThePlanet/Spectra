# Implementation Plan: MCP Execution Server

**Branch**: `002-mcp-execution-server` | **Date**: 2026-03-14 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-mcp-execution-server/spec.md`

## Summary

Build the MCP Execution Server (Spectra.MCP) that enables testers to execute manual test suites through any LLM-based assistant. The server implements a deterministic state machine for test execution, stores state in SQLite, and exposes MCP tools for run management, test execution, and reporting. Self-contained responses enable any orchestrator (Copilot, Claude, custom agents) to drive execution without holding state.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: ASP.NET Core (MCP server), Microsoft.Data.Sqlite (state storage), System.Text.Json (serialization)
**Storage**: SQLite database (`.execution/spectra.db`) for execution state; file system for reports
**Testing**: xUnit for unit/integration tests
**Target Platform**: Cross-platform (.NET 8 runtime: Windows, macOS, Linux)
**Project Type**: MCP server (ASP.NET Core) with shared library
**Performance Goals**: 5 concurrent users per SC-004; tool responses <100ms for local operations
**Constraints**: Offline-capable (all execution local after test files loaded); crash-resilient via SQLite atomic writes
**Scale/Scope**: 500+ test files per repository; indefinite history retention per spec clarification

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. GitHub as Source of Truth | PASS | Tests read from `tests/{suite}/`, reports written to `reports/`, execution state in `.execution/` (gitignored) |
| II. Deterministic Execution | PASS | State machine with explicit states (CREATED/RUNNING/PAUSED/COMPLETED/CANCELLED); validated transitions; self-contained responses per FR-028 |
| III. Orchestrator-Agnostic Design | PASS | MCP API works with any LLM; responses include `run_status`, `progress`, `next_expected_action` |
| IV. CLI-First Interface | PASS | All operations exposed as MCP tools with explicit parameters; no chat loops |
| V. Simplicity (YAGNI) | PASS | SQLite for state (no external DB); standard .NET patterns; no premature abstractions |

**Quality Gates Compliance**:
- Schema Validation: Reuses Spectra.Core validation from Phase 1
- ID Uniqueness: Relies on validated `_index.json` from Phase 1
- Index Currency: Validates index freshness on run start (FR-018)
- Dependency Resolution: Reuses DependsOnValidator from Phase 1
- Priority Enum: Reuses Priority model from Phase 1

**Development Workflow Compliance**:
- Test-Required: xUnit tests for MCP tools, state machine transitions, report generation
- Target coverage: Core 80%+, MCP 60%+

## Project Structure

### Documentation (this feature)

```text
specs/002-mcp-execution-server/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (MCP tool schemas)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/              # Existing CLI application (Phase 1)
├── Spectra.Core/             # Shared library (Phase 1) - extended for MCP
│   ├── Models/               # Existing + new Run, TestResult, TestHandle models
│   ├── Parsing/              # Existing parsers
│   ├── Validation/           # Existing validators
│   └── Index/                # Existing index operations
├── Spectra.MCP/              # NEW: MCP server application
│   ├── Server/               # MCP protocol implementation
│   │   ├── McpServer.cs      # ASP.NET Core host
│   │   └── McpProtocol.cs    # JSON-RPC handling
│   ├── Tools/                # MCP tool implementations
│   │   ├── RunManagement/    # list_available_suites, start/pause/resume/cancel/finalize
│   │   ├── TestExecution/    # get_test_case_details, advance, skip, retest, add_note
│   │   └── Reporting/        # get_execution_summary, get_run_history
│   ├── Execution/            # State machine and queue management
│   │   ├── ExecutionEngine.cs
│   │   ├── StateMachine.cs
│   │   ├── TestQueue.cs
│   │   └── DependencyResolver.cs
│   ├── Storage/              # SQLite data access
│   │   ├── ExecutionDb.cs
│   │   ├── RunRepository.cs
│   │   └── ResultRepository.cs
│   ├── Reports/              # Report generation
│   │   ├── ReportGenerator.cs
│   │   └── ReportWriter.cs
│   ├── Identity/             # User identity resolution
│   │   └── UserIdentityResolver.cs
│   └── Infrastructure/       # Logging, configuration
│       ├── McpLogging.cs
│       └── McpConfig.cs
└── Spectra.GitHub/           # GitHub integration (future phase)

tests/
├── Spectra.Core.Tests/       # Existing unit tests
├── Spectra.CLI.Tests/        # Existing integration tests
├── Spectra.MCP.Tests/        # NEW: MCP server tests
│   ├── Tools/                # Tool contract tests
│   ├── Execution/            # State machine tests
│   ├── Storage/              # Repository tests
│   └── Integration/          # End-to-end MCP flow tests
└── TestFixtures/             # Existing sample data
```

**Structure Decision**: Extends the existing multi-project solution. Spectra.MCP is a new ASP.NET Core project that depends on Spectra.Core for shared models and validation. No modifications to Spectra.CLI required.

## Complexity Tracking

> No Constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | - | - |

---

## Phase 0: Research (Completed)

See [research.md](./research.md) for technical decisions:

1. **MCP Protocol**: JSON-RPC 2.0 over stdio; ASP.NET Core minimal API for transport
2. **SQLite Access**: Microsoft.Data.Sqlite with async operations and connection pooling
3. **State Machine**: Simple enum-based states with validated transition matrix
4. **Test Handle Generation**: UUID prefix + test ID + random suffix for security
5. **Report Generation**: System.Text.Json for JSON; custom Markdown writer
6. **Logging**: Microsoft.Extensions.Logging with Serilog file sink

## Phase 1: Design (Completed)

See design artifacts:

1. **[data-model.md](./data-model.md)**: Core entities (Run, TestResult, TestHandle, ExecutionQueue)
2. **[contracts/mcp-tools.md](./contracts/mcp-tools.md)**: MCP tool specifications (14 tools)
3. **[quickstart.md](./quickstart.md)**: User-facing usage guide

## Phase 2: Tasks

Run `/speckit.tasks` to generate the implementation task list.

---

## Constitution Re-Check (Post-Design)

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. GitHub as Source of Truth | PASS | All models designed for file-based test storage; execution state local |
| II. Deterministic Execution | PASS | StateMachine class enforces transitions; responses include next_expected_action |
| III. Orchestrator-Agnostic | PASS | Self-contained responses; no session memory required |
| IV. CLI-First Interface | PASS | All 14 MCP tools defined with explicit params |
| V. Simplicity (YAGNI) | PASS | Standard .NET patterns; SQLite without ORM |

**All constitution principles satisfied. Ready for task generation.**
