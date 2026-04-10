# Implementation Plan: Testimize Integration

**Branch**: `038-testimize-integration` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/038-testimize-integration/spec.md`

## Summary

Add an OPTIONAL integration layer that lets SPECTRA's `spectra ai generate` pipeline call the external Testimize.MCP.Server tool for algorithmic test data optimization (BVA, EP, pairwise, ABC). Integration is OFF by default, fully graceful (warning + AI fallback when the tool is missing or fails), and never adds a NuGet dependency on Testimize. Two new AI tools (`GenerateTestData`, `AnalyzeFieldSpec`) are conditionally registered alongside the existing 7 generation tools when the Testimize MCP child process is healthy. New `spectra testimize check` health command. Prompt templates gain a `{{#if testimize_enabled}}` block that activates Testimize-aware instructions.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK (existing), `System.Diagnostics.Process` for child-process lifecycle, `System.Text.Json` for JSON-RPC framing — NO Testimize NuGet package
**Storage**: File-based — `spectra.config.json` gains a `testimize` section; `.spectra/prompts/*.md` templates gain a conditional block; optional `testimizeSettings.json` is read by the Testimize server itself, not by SPECTRA
**Testing**: xUnit (`Spectra.CLI.Tests`, `Spectra.Core.Tests`); integration tests gated on whether `testimize-mcp` is on PATH
**Target Platform**: Cross-platform CLI (Windows/Linux/macOS)
**Project Type**: Existing single solution, multi-project (CLI + Core + MCP + tests)
**Performance Goals**: Per-MCP-call timeout of 30 seconds; no measurable overhead when `testimize.enabled = false` (zero processes spawned)
**Constraints**: Zero regression for users who do not opt in; graceful degradation when the tool is missing/crashes/times out; MCP child process MUST be killed in all exit paths (success, exception, Ctrl-C)
**Scale/Scope**: ~3 new code files, ~1 new model file, ~5 modified files, ~25 new tests, 1 new CLI command, 1 new doc page; only `spectra ai generate` is touched — `update`, `analyze`, `dashboard` are out of scope

## Constitution Check

Evaluation against `.specify/memory/constitution.md` v1.0.0:

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ PASS | All new state is in `spectra.config.json` and prompt templates (already in Git). No external storage. The optional `testimizeSettings.json` is also committable. |
| II. Deterministic Execution | ✅ PASS | Does not touch the MCP execution engine. The Testimize MCP client is on the generation side, not the execution side. Optional `seed` field exposes Testimize's deterministic mode for users who want reproducibility. |
| III. Orchestrator-Agnostic Design | ✅ PASS | Tools are registered as standard `AIFunction` instances — same shape as the existing 7 generation tools — so any LLM orchestrator that already works with SPECTRA will see them. No provider-specific code. |
| IV. CLI-First Interface | ✅ PASS | All new functionality is CLI-driven: one new command (`spectra testimize check`), one new config section, no GUI. Health check supports `--output-format json` for CI. |
| V. Simplicity (YAGNI) | ✅ PASS WITH NOTE | Adds one external integration. Justified because: (a) the integration is OFF by default — zero impact on users who don't enable it; (b) it replaces a known weakness (AI-approximated boundary values) with a measurable improvement (algorithmic precision); (c) no abstraction layer is introduced — the new code is two AI tools + one process wrapper, mirroring patterns the codebase already uses for the existing 7 tools. The MCP child process IS new infrastructure, but it lives entirely inside one class with a strict disposal contract. |

**Quality Gates** (validate, ID-uniqueness, etc.) are unaffected — no test file schema changes.

**Result**: PASS. No constitution violations. Proceed.

## Project Structure

### Documentation (this feature)

```text
specs/038-testimize-integration/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — config and runtime entities
├── quickstart.md        # Phase 1 — verification scenarios
├── contracts/
│   ├── testimize-config.schema.json
│   ├── generate-test-data.tool.json
│   └── testimize-check-result.schema.json
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (created by /speckit.tasks)
```

### Source Code (repository root)

This feature touches existing files plus a small set of new ones — no new projects, no new top-level directories beyond `Agent/Testimize/` and `Commands/Testimize/`.

```text
src/
├── Spectra.Core/
│   └── Models/Config/
│       ├── TestimizeConfig.cs               # NEW — root config + sub-objects
│       └── SpectraConfig.cs                 # MODIFY — add Testimize property
└── Spectra.CLI/
    ├── Agent/
    │   ├── Copilot/
    │   │   └── GenerationAgent.cs            # MODIFY — conditional tool registration + disposal
    │   └── Testimize/                        # NEW directory
    │       ├── TestimizeMcpClient.cs         # NEW — process lifecycle, JSON-RPC, health probe
    │       └── TestimizeTools.cs             # NEW — AIFunction factories
    ├── Commands/
    │   ├── Testimize/                        # NEW directory
    │   │   ├── TestimizeCheckHandler.cs      # NEW — health check command handler
    │   │   └── TestimizeCommandBuilder.cs    # NEW — System.CommandLine wiring
    │   └── Init/InitHandler.cs               # MODIFY — default testimize section, optional detect prompt
    ├── Prompts/
    │   ├── Content/
    │   │   ├── behavior-analysis.md          # MODIFY — add {{#if testimize_enabled}} block
    │   │   └── test-generation.md            # MODIFY — add {{#if testimize_enabled}} block
    │   └── PromptTemplateLoader.cs           # MODIFY — populate testimize_enabled placeholder
    ├── Results/
    │   └── TestimizeCheckResult.cs           # NEW — typed JSON result model
    └── Program.cs                            # MODIFY — register `spectra testimize` parent command

tests/
├── Spectra.Core.Tests/
│   └── Models/Config/
│       └── TestimizeConfigTests.cs           # NEW — defaults, deserialization, fallbacks
└── Spectra.CLI.Tests/
    ├── Agent/Testimize/
    │   ├── TestimizeMcpClientTests.cs        # NEW — start/health/dispose, no-tool fallback
    │   ├── TestimizeMcpClientGracefulTests.cs # NEW — crash, timeout, malformed JSON
    │   └── TestimizeToolsTests.cs            # NEW — tool schema, field mapping
    ├── Agent/Copilot/
    │   └── GenerationAgentTestimizeTests.cs  # NEW — tool count with/without Testimize
    ├── Prompts/
    │   └── TestimizeConditionalBlockTests.cs # NEW — {{#if testimize_enabled}} renders/hides
    ├── Commands/Testimize/
    │   └── TestimizeCheckHandlerTests.cs     # NEW — installed/not-installed/json output
    └── Commands/Init/
        └── InitTestimizeTests.cs             # NEW — default disabled, preservation

docs/
├── testimize-integration.md                  # NEW — full integration guide
├── getting-started.md                        # MODIFY — optional Testimize setup paragraph
├── configuration.md                          # MODIFY — testimize section reference
└── cli-reference.md                          # MODIFY — `spectra testimize check` entry

CLAUDE.md                                     # MODIFY — Recent Changes entry for 038
```

**Structure Decision**: Existing single-solution layout. Two small new sub-namespaces (`Agent/Testimize/`, `Commands/Testimize/`) keep the new code physically isolated from the existing generation pipeline so it can be removed cleanly if Testimize ever gets dropped. The conditional registration site in `GenerationAgent.cs` is a single new `if (config.Testimize.Enabled)` block immediately after the existing tool list construction at line 94 — surgical, easy to review.

## Complexity Tracking

No constitution violations. Table omitted.
