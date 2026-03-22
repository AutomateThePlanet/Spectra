# Implementation Plan: Bug Logging, Templates, and Execution Agent Integration

**Branch**: `016-bug-logging-templates` | **Date**: 2026-03-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/016-bug-logging-templates/spec.md`

## Summary

Add integrated bug logging to the SPECTRA execution workflow. When a test case is marked as FAILED, the execution agent offers to log a bug pre-filled from test case data using a customizable Markdown template. Supports Azure DevOps, Jira, and GitHub Issues via their MCP servers, with local Markdown fallback. Includes duplicate detection via `bugs` frontmatter field and tracker queries, configurable behavior via `bug_tracking` config section, and template creation during `spectra init`.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (config), YamlDotNet (frontmatter), Spectre.Console (CLI output)
**Storage**: File system (Markdown templates, local bug reports, YAML frontmatter)
**Testing**: xUnit
**Target Platform**: Cross-platform CLI + MCP server
**Project Type**: CLI tool + MCP server
**Performance Goals**: Bug report generation < 1 second; template substitution is simple string replacement
**Constraints**: No new NuGet dependencies (Constitution Principle V: YAGNI); template engine is plain string.Replace
**Scale/Scope**: One new config class, one new service namespace, modifications to 10+ existing files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Bug reports saved as Markdown files in git. Template stored in repo. `bugs` field in YAML frontmatter. |
| II. Deterministic Execution | PASS | Bug logging is agent-driven (conversational), not part of the deterministic state machine. Execution engine behavior unchanged. |
| III. Orchestrator-Agnostic Design | PASS | Bug logging instructions are in the agent prompt, not in MCP tools. Any orchestrator can follow the prompt instructions. External tracker integration via standard MCP tools. |
| IV. CLI-First Interface | PASS | `spectra init` creates template and config via CLI. Bug logging itself is agent-initiated during execution, not a separate CLI command. |
| V. Simplicity (YAGNI) | PASS | No new dependencies. Template engine uses `string.Replace`. No abstractions beyond what's needed. `BugReportTemplateEngine` is a single-purpose service. |

**Post-Phase 1 Re-check**: All gates still pass. No new projects, no repository pattern, no complex abstractions introduced.

## Project Structure

### Documentation (this feature)

```text
specs/016-bug-logging-templates/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── bug-tracking-config.json
│   ├── bug-report-template.md
│   └── local-bug-file.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   ├── Models/Config/
│   │   └── BugTrackingConfig.cs          # NEW: config model
│   ├── Models/
│   │   ├── TestCaseFrontmatter.cs        # MODIFIED: add Bugs field
│   │   ├── TestCase.cs                   # MODIFIED: add Bugs field
│   │   └── TestIndexEntry.cs             # MODIFIED: add Bugs field
│   ├── BugReporting/                     # NEW: namespace
│   │   ├── BugReportContext.cs           # NEW: runtime data object
│   │   ├── BugReportTemplateEngine.cs    # NEW: template variable substitution
│   │   └── BugReportWriter.cs           # NEW: local file writer
│   ├── Parsing/
│   │   └── FrontmatterUpdater.cs         # MODIFIED: add UpdateBugs()
│   └── Index/
│       └── DocumentIndexWriter.cs        # MODIFIED: include bugs in entries
├── Spectra.CLI/
│   ├── Commands/Init/
│   │   └── InitHandler.cs               # MODIFIED: add template + config
│   └── Templates/
│       ├── spectra.config.json           # MODIFIED: add bug_tracking section
│       └── bug-report.md                 # NEW: default template
├── Spectra.MCP/
│   └── Tools/TestExecution/
│       └── AdvanceTestCaseTool.cs        # MODIFIED: add bugs context to response

templates/
└── bug-report.md                         # Created by spectra init (runtime)

.github/
├── agents/
│   └── spectra-execution.agent.md        # MODIFIED: expand bug logging section
└── skills/
    └── spectra-bug-logging/
        └── SKILL.md                      # NEW: optional Copilot skill

tests/
├── Spectra.Core.Tests/
│   ├── BugReporting/                     # NEW: template engine + writer tests
│   └── Parsing/
│       └── FrontmatterUpdaterTests.cs    # MODIFIED: add UpdateBugs tests
└── Spectra.CLI.Tests/
    └── Commands/
        └── InitHandlerTests.cs           # MODIFIED: verify template + config creation
```

**Structure Decision**: Feature adds a new `BugReporting` namespace in `Spectra.Core` (3 classes) and modifies existing files across Core, CLI, and MCP. No new projects. Follows established patterns.

## Complexity Tracking

No constitution violations to justify. All changes follow existing patterns.
