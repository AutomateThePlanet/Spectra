# AGENTS.md

## Project Overview

SPECTRA is an AI-native test generation and execution framework built in C# / .NET. It has two independent subsystems:

1. **Spectra.CLI** — AI agent that generates and maintains manual test cases from documentation. Uses the GitHub Copilot SDK as its AI runtime.
2. **Spectra.MCP** — Deterministic MCP execution engine that any LLM orchestrator can drive to execute test suites.

Both share **Spectra.Core** for parsing, validation, models, and indexing.

## Architecture

Read `spec-kit/architecture.md` before making any structural changes. It is the authoritative source for all design decisions.

Key architectural rules:
- Tests are Markdown files with YAML frontmatter in `tests/{suite}/`
- Documentation lives in `docs/` — the CLI reads from docs, writes to tests
- The MCP server never parses all Markdown files at runtime — it reads `_index.json`
- The AI generation agent reads `docs/_index.md` (document index) instead of scanning all docs at runtime
- The AI agent never writes files directly — all output goes through tool handlers that validate first
- Every MCP response includes `next_expected_action` and is fully self-contained
- State machine transitions are enforced — the MCP server rejects invalid tool call sequences

## Project Structure

```
src/
├── Spectra.CLI/              # .NET CLI application
│   ├── Commands/             # Command handlers (init, validate, index, docs, ai generate, etc.)
│   ├── Agent/                # Copilot SDK integration
│   │   ├── Tools/            # Custom tools for the AI agent
│   │   └── Skills/           # Skill loader
│   ├── Source/               # Document map builder, document index service, source doc reader
│   ├── Index/                # _index.json builder and reader
│   ├── Validation/           # Test case validation, dedup
│   ├── Review/               # Interactive terminal review UI
│   ├── Provider/             # Multi-provider chain with fallback
│   ├── Git/                  # Branch/commit operations
│   ├── Config/               # Configuration loader
│   └── IO/                   # File writers
├── Spectra.MCP/              # ASP.NET Core MCP server
│   ├── Tools/                # MCP tool handlers
│   ├── Engine/               # State machine, execution queue
│   └── Storage/              # SQLite repository
├── Spectra.Core/             # Shared library
│   ├── Models/               # TestCase, Suite, RunState, etc.
│   ├── Parsing/              # Markdown + YAML frontmatter parser
│   ├── Validation/           # Schema validation rules
│   └── Index/                # Index read/write
└── Spectra.GitHub/           # GitHub integration (Octokit)

spec-kit/                     # Architecture specs and ADRs
tests/                        # Sample test suites (used for integration testing)
docs/                         # Sample docs (used for testing generation)
```

## Conventions

- **Language:** C# 12, .NET 8+
- **Naming:** PascalCase for types and methods, camelCase for locals
- **Async:** All I/O operations are async. Use `Async` suffix on method names.
- **Nullability:** Nullable reference types enabled project-wide
- **Tests:** xUnit for unit tests. Project: `Spectra.Tests`
- **Configuration:** `spectra.config.json` at repo root. Model: `SpectraConfig` in Spectra.Core

## Key Types

When these exist, understand them before modifying related code:

- `TestCase` — Parsed Markdown test with frontmatter metadata
- `TestSuite` — Collection of tests in a folder with an _index.json
- `MetadataIndex` — The _index.json model
- `DocumentMap` — Lightweight listing of all docs in source folder
- `DocumentIndex` — Pre-built document index with rich metadata (sections, entities, tokens, hashes)
- `DocumentIndexEntry` — Per-document metadata in the index
- `DocumentIndexService` — Orchestrates incremental/full index builds
- `ExecutionRun` — A test execution run with state
- `TestHandle` — Opaque reference to a test in a run
- `SpectraConfig` — Root configuration model

## Architecture Reference

**Always read `spec-kit/architecture.md` before making structural changes.** It is the authoritative source for all design decisions: models, MCP tools, state machine, CLI commands, configuration schema, and data flows.

## Spec-Driven Development

This project uses [GitHub Spec Kit](https://github.com/github/spec-kit) for specification-driven development. Specs, plans, and tasks live in `.specify/`. Follow the spec-kit workflow for new features.

## Implementation Guidelines

### Spectra.Core
- Models are plain C# records/classes, no framework dependencies
- Parsing uses Markdig + YamlDotNet
- Validation returns structured results (ParseResult with errors list), never throws

### Spectra.CLI
- Commands use System.CommandLine
- Each command is a thin orchestrator — business logic lives in services
- AI tools are registered via `AIFunctionFactory.Create()` from Microsoft.Extensions.AI
- The Copilot SDK session is created by `CopilotSessionFactory`
- Tool handlers validate before accepting — they are the safety gate

### Spectra.MCP
- MCP tools are ASP.NET Core endpoints using ModelContextProtocol.AspNetCore
- State machine transitions must be validated before executing
- SQLite access goes through a repository pattern
- Every response includes run_status, progress, and next_expected_action

## Active Technologies
- C# 12, .NET 8+ + Spectra.CLI (command integration), Spectra.Core (config, parsing), System.CommandLine (interactive prompts) (004-test-generation-profile)
- File-based (spectra.profile.md at repo root, _profile.md in suites) (004-test-generation-profile)

## Recent Changes
- 010-document-index: Added persistent `docs/_index.md` with per-document metadata, incremental updates via SHA-256 hashing, `spectra docs index` command, auto-refresh before AI generation
- 004-test-generation-profile: Added C# 12, .NET 8+ + Spectra.CLI (command integration), Spectra.Core (config, parsing), System.CommandLine (interactive prompts)
