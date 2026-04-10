# AGENTS.md

> **This file exists for the [agents.md](https://agents.md) convention** so external coding agents can find a project guide by name. The authoritative agent context for SPECTRA is **[`CLAUDE.md`](CLAUDE.md)** — it is auto-maintained by the spec-kit workflow and reflects the current technology stack, project structure, recent changes, and conventions.

## Quick orientation

SPECTRA is an AI-native test generation and execution framework written in C# / .NET 8. Three projects under `src/`:

- **`Spectra.CLI`** — CLI tool for test generation, coverage, dashboard, validation. Uses the GitHub Copilot SDK as its sole AI runtime.
- **`Spectra.MCP`** — Deterministic MCP execution server for AI-driven test execution.
- **`Spectra.Core`** — Shared models, parsing, validation, indexing, coverage analyzers.

Tests are split mirror-style across `tests/Spectra.CLI.Tests/`, `tests/Spectra.Core.Tests/`, and `tests/Spectra.MCP.Tests/`.

## Where to look next

| You want to... | Read |
|---|---|
| Understand the current architecture, file layout, conventions | [`CLAUDE.md`](CLAUDE.md) |
| Build and run locally | [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) |
| Use SPECTRA from VS Code Copilot Chat | [`USAGE.md`](USAGE.md) |
| Customize prompts, profiles, branding | [`CUSTOMIZATION.md`](CUSTOMIZATION.md) |
| Project knowledge and component reference | [`PROJECT-KNOWLEDGE.md`](PROJECT-KNOWLEDGE.md) |
| Original architecture design spec | [`architecture-v5.md`](architecture-v5.md) (historical) |
| Contribute | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Feature specifications | [`specs/`](specs/) (one directory per feature, numbered 001..NNN) |
