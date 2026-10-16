# AGENTS.md

> **This file exists for the [agents.md](https://agents.md) convention** so external coding agents can find a project
> guide by name. The authoritative agent context for SPECTRA is **[`CLAUDE.md`](CLAUDE.md)**: technology stack, project
> structure, the command list, conventions and recent changes. This file keeps no command list of its own.

## Quick orientation

SPECTRA is an AI-native test generation and execution framework written in C# / .NET 8. It makes no model calls of its
own: generation, criteria extraction, test updates and verification run in the user's Claude Code session through the
bundled skills (`.claude/skills/`) and subagents (`.claude/agents/`), which drive deterministic `spectra ai compile-*` /
`ingest-*` commands. Three projects under `src/`:

- **`Spectra.CLI`** — the `spectra` command: indexing, the compile/ingest seam, change detection and update triage,
  coverage, dashboard, validation, and the `spectra run` execution commands.
- **`Spectra.Core`** — shared models, parsing, validation, indexing, baselines, git reading, coverage analyzers.
- **`Spectra.Execution`** — the transport-neutral execution engine behind `spectra run` (SQLite state, reports).

Tests: `tests/Spectra.Core.Tests/`, `tests/Spectra.CLI.Tests/`, `tests/Spectra.Execution.Tests/` and
`tests/Spectra.Integration.Tests/`.

## Where to look next

| You want to... | Read |
|---|---|
| Understand the current architecture, file layout, commands, conventions | [`CLAUDE.md`](CLAUDE.md) |
| Build and run locally | [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) |
| Use SPECTRA from Claude Code | [`docs/usage.md`](docs/usage.md) |
| Customize prompts, profiles, branding | [`docs/customization.md`](docs/customization.md) |
| Contribute | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| Feature specifications | [`specs/`](specs/) (one directory per feature, numbered 001..NNN) |
| History (v1, superseded) | [`PROJECT-KNOWLEDGE.md`](PROJECT-KNOWLEDGE.md), [`architecture-v5.md`](architecture-v5.md) |
