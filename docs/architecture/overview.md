---
title: Overview
parent: Architecture
nav_order: 1
---

# Architecture Overview

How SPECTRA's two subsystems work together, as of the v2 (Claude Code) migration.

Related: [Claude Code v2 vs. the GitHub Copilot SDK v1](../claude-code-v2-migration.md) |
[How Test Case Generation Works](analysis/cli-vs-chat-generation.md)

---

## Two Independent Subsystems

| Subsystem | Purpose | Can be used independently |
|-----------|---------|--------------------------|
| **Authoring (generate/update/analyze)** | Generate, update, and analyze test cases from documentation, driven by Claude Code skills over a deterministic CLI seam | Yes, test cases are useful even without the execution engine |
| **Execution (`spectra run`)** | Execute test cases through a deterministic state machine over SQLite | Yes, works with any Markdown test cases, not just AI-generated ones |

Neither subsystem runs its own AI session anymore. There is **no MCP server** (removed) and **no
in-process model runtime** (the GitHub Copilot SDK is gone), so every model call, on either side, is
a turn (or subagent call) inside your interactive Claude Code session.

## System Flow

```
docs/                              <- Source documentation
  |
docs/_index/                       <- Per-suite manifest + checksums (v2 layout)
  |
spectra ai compile-prompt          <- Deterministic prompt, no model call
  |
(a turn in YOUR Claude Code        <- Generation/analysis/criteria/update all run here
 session, driven by a skill)
  |
spectra ai ingest-tests            <- Fail-loud validate + persist
  |
spectra-critic subagent            <- context: fork verification, per test
  |
test-cases/                        <- Markdown + YAML frontmatter test cases
  |
spectra run                        <- Deterministic execution state machine (SQLite)
  |
spectra run console (optional)     <- Local browser UI for manual verdict recording
  |
Azure DevOps / Jira / Teams        <- Bug logging via their own separate MCPs
```

## Tech Stack

- **Language:** C# 12, .NET 8+
- **AI Runtime:** Claude Code (the user's interactive session). SPECTRA makes no model calls of
  its own; authoring orchestration ships as `.claude/skills/` + `.claude/agents/`
- **CLI Framework:** System.CommandLine + Spectre.Console
- **Serialization:** System.Text.Json (data), YamlDotNet (frontmatter)
- **Execution store:** Microsoft.Data.Sqlite, WAL mode (`.execution/spectra.db`), CLI-only with no
  server process
- **Storage:** file system (test cases, docs, reports)

## Project Structure

```
src/
├── Spectra.CLI/        # CLI: authoring commands (compile-*/ingest-* seam) + spectra run
├── Spectra.Core/       # Shared library (parsing, validation, models, coverage)
├── Spectra.Execution/  # Transport-neutral execution engine, driven solely by spectra run
└── Spectra.GitHub/     # GitHub integration (future)
```

## Key Design Decisions

- SPECTRA has no in-process AI runtime: every model-touching flow (generation, analysis, criteria
  extraction, updates, grounding verification) is a deterministic `compile-*` → in-session turn →
  `ingest-*` seam, and SPECTRA never opens a model session itself.
- Test case storage is file-based: test cases are Markdown files with YAML frontmatter, and there's no database for test case definitions.
- Execution is deterministic: `spectra run` drives a state machine over SQLite, and the AI orchestrator (or the browser console) doesn't hold execution state itself.
- Coverage spans three dimensions: Documentation, Acceptance Criteria, and Automation coverage are analyzed independently and reported together.
- Critic verification is isolated: the `spectra-critic` subagent runs in a fresh, forked context per test, so it sees only the artifact and its source docs, never the generator's reasoning.

For the full history of how this replaced the pre-v2 architecture, see
[Claude Code v2 vs. the GitHub Copilot SDK v1](../claude-code-v2-migration.md).
