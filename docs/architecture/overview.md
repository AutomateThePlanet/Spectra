# Architecture Overview

How SPECTRA's two subsystems work together.

Related: [Full Technical Specification](../../spec-kit/architecture.md)

---

## Two Independent Subsystems

| Subsystem | Purpose | Can be used independently |
|-----------|---------|--------------------------|
| **AI CLI** | Generate, update, and analyze test cases from documentation | Yes — tests are useful even without the execution engine |
| **MCP Engine** | Execute tests through deterministic AI-orchestrated protocol | Yes — works with any Markdown tests, not just AI-generated ones |

## System Flow

```
docs/                        <- Source documentation
  |
docs/_index.md               <- Pre-built document index (incremental)
  |
AI Test Generation CLI       <- GitHub Copilot SDK (sole AI runtime)
  |                            Supports: github-models, azure-openai,
tests/                       <-          azure-anthropic, openai, anthropic
  |
MCP Execution Engine         <- Deterministic state machine
  |
LLM Orchestrator             <- Copilot Chat, Claude, any MCP client
  | (as needed)
Azure DevOps / Jira / Teams  <- Bug logging, notifications via their MCPs
```

## Tech Stack

- **Language:** C# 12, .NET 8+
- **AI Runtime:** GitHub Copilot SDK (sole runtime for all AI operations)
- **CLI Framework:** System.CommandLine + Spectre.Console
- **Serialization:** System.Text.Json (data), YamlDotNet (frontmatter)
- **MCP Server:** ASP.NET Core (stdio transport, JSON-RPC 2.0)
- **Storage:** Microsoft.Data.Sqlite (execution state), file system (tests, reports)

## Project Structure

```
src/
├── Spectra.CLI/       # AI test generation CLI
├── Spectra.Core/      # Shared library (parsing, validation, models, coverage)
├── Spectra.MCP/       # MCP execution server
└── Spectra.GitHub/    # GitHub integration (future)
```

## Key Design Decisions

- **Single AI runtime**: All AI operations go through the GitHub Copilot SDK. No separate agent implementations per provider.
- **File-based test storage**: Tests are Markdown files with YAML frontmatter. No database for test definitions.
- **Deterministic execution**: The MCP engine is a state machine. The AI orchestrator doesn't hold execution state.
- **Three coverage dimensions**: Documentation, Acceptance Criteria, and Automation coverage are analyzed independently and reported together.
- **Dual-model verification**: Generator and critic are separate models to catch hallucination.

For the full technical specification, see [spec-kit/architecture.md](../../spec-kit/architecture.md).
