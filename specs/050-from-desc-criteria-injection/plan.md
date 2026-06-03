# Implementation Plan: From-Description Criteria Injection

**Branch**: `050-from-desc-criteria-injection` | **Date**: 2026-06-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/050-from-desc-criteria-injection/spec.md`

## Summary

Fix a one-argument bug in the from-description generation call site that causes the loaded acceptance-criteria context to be discarded before reaching the generation agent, suppressing the MANDATORY "you MUST map criteria" instruction the batch flow already activates. The fix forwards the loaded `criteriaContext` to `GenerationAgent.GenerateTestsAsync` and removes the now-redundant loose `## Related Acceptance Criteria` body block from `UserDescribedGenerator.BuildPrompt` so the criteria content appears exactly once with proper framing. `grounding.verdict` stays `manual` by deliberate decision (no critic is run for this flow). No new commands, no new tool surfaces, no enum changes, no migrations.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK (sole AI runtime), Spectre.Console, System.CommandLine, System.Text.Json, YamlDotNet
**Storage**: File-based — generated tests as Markdown + YAML frontmatter in `test-cases/{suite}/`; per-suite `_index.json`; criteria index at `docs/criteria/_criteria_index.yaml`
**Testing**: xUnit (`tests/Spectra.CLI.Tests/`); existing seam is `UserDescribedGenerator.BuildPrompt` (public static); new seam introduced for agent-call forwarding (see Phase 1)
**Target Platform**: Cross-platform CLI (.NET 8+), primary dev/test on Windows 11
**Project Type**: Single repository with multiple .NET projects (Spectra.CLI, Spectra.Core, Spectra.MCP, Spectra.GitHub)
**Performance Goals**: From-description generation latency MUST NOT regress measurably — the fix passes a string parameter and removes a prompt block; no extra I/O, no extra AI calls
**Constraints**: No public surface changes (CLI flags, MCP tools, frontmatter schema, enum values, JSON shapes). Existing batch generation output MUST be byte-identical for identical inputs. `grounding.verdict` MUST remain `manual` for from-description tests.
**Scale/Scope**: Behavioral change touches one method (`UserDescribedGenerator.GenerateAsync`) and one prompt builder (`UserDescribedGenerator.BuildPrompt`). Test additions in one file. Docs touches in four files (per Spec § Documentation Update Checklist).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applies? | Compliance | Notes |
|-----------|----------|------------|-------|
| I. GitHub as Source of Truth | Yes | ✅ | No external storage introduced. Generated tests remain `.md` + YAML frontmatter in `test-cases/{suite}/`. The `criteria:` field already exists in the YAML schema — this fix populates it more reliably; it does not invent a new field. |
| II. Deterministic Execution | Yes | ✅ | No change to the MCP execution engine, state machine, or queue semantics. The fix is in the CLI generation pipeline only. Deterministic inputs (same description, same suite, same criteria index) produce a more consistent output (the MANDATORY instruction is now always present when criteria match), which strengthens determinism rather than weakening it. |
| III. Orchestrator-Agnostic Design | Yes | ✅ | No MCP tool added or modified. No tool-response shape changed. The fix is internal to the CLI `ai generate` flow. |
| IV. CLI-First Interface | Yes | ✅ | No new CLI command, flag, or option. Existing `spectra ai generate --from-description ...` behavior is corrected; its surface is unchanged. The AI agent still writes through validated handlers (`TestPersistenceService` from Spec 049). |
| V. Simplicity (YAGNI) | Yes | ✅ | One-line behavioral fix plus a small prompt cleanup. No abstractions introduced. No feature flag — the prior behavior was a defect, not an option to preserve. Test seam (single delegate parameter) is justified by the test plan and is the minimum needed; declined alternatives recorded in research.md. |

**Gate result**: PASS. No violations. No complexity-tracking entries required.

### Post-design re-check (after Phase 1)

After producing `research.md`, `data-model.md`, `contracts/prompt-contract.md`, and `quickstart.md`, all five principles re-evaluated:

- **I. GitHub as Source of Truth** — confirmed in data-model.md: no new fields, no new files, no migrations. ✅
- **II. Deterministic Execution** — confirmed in research.md D1: forwarding the loaded value strengthens determinism (same input → same prompt → same MANDATORY block when criteria match). ✅
- **III. Orchestrator-Agnostic Design** — confirmed in contracts/prompt-contract.md: no MCP tool added or modified. ✅
- **IV. CLI-First Interface** — confirmed in contracts/prompt-contract.md "Surfaces NOT modified": CLI command shape and exit codes unchanged. ✅
- **V. Simplicity (YAGNI)** — confirmed in research.md D4: the introduced test seam is one defaulted parameter on one method, justified by six test cases the spec explicitly requires. No DI container, no internal partials, no `InternalsVisibleTo`. ✅

**Re-check result**: PASS. No new violations introduced by design. Complexity Tracking remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/050-from-desc-criteria-injection/
├── plan.md              # This file (/speckit.plan command output)
├── spec.md              # Already written (/speckit.specify)
├── research.md          # Phase 0 output (/speckit.plan)
├── data-model.md        # Phase 1 output (/speckit.plan)
├── quickstart.md        # Phase 1 output (/speckit.plan)
├── contracts/
│   └── prompt-contract.md   # Phase 1 — documents the existing IAgentRuntime criteriaContext contract this fix relies on
├── checklists/
│   └── requirements.md  # Already written (/speckit.specify validation)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

The repo is a multi-project .NET solution. This feature touches one source file and one test file. The structure below shows the relevant slice; unrelated projects are listed elsewhere in `CLAUDE.md`.

```text
src/
├── Spectra.CLI/
│   ├── Agent/
│   │   ├── IAgentRuntime.cs              # Existing contract — NOT modified
│   │   └── Copilot/
│   │       └── GenerationAgent.cs        # NOT modified — line 527 MANDATORY block activates on non-empty criteriaContext
│   └── Commands/
│       └── Generate/
│           ├── GenerateHandler.cs        # NOT modified — already calls LoadCriteriaContextAsync and forwards the result
│           └── UserDescribedGenerator.cs # MODIFIED — forward criteriaContext; remove loose body block; introduce IAgentRuntime test seam

tests/
└── Spectra.CLI.Tests/
    └── Commands/
        └── Generate/
            └── UserDescribedGeneratorTests.cs # MODIFIED — adapt one existing prompt assertion (loose block removed); add six new tests per spec § Test Plan

docs/                                          # MODIFIED — four documentation files (see spec § Documentation Update Checklist)
├── skills-integration.md
├── coverage.md
└── PROJECT-KNOWLEDGE.md
.spectra/prompts/ or src/Spectra.CLI/Skills/   # MODIFIED — spectra-generate SKILL content
```

**Structure Decision**: Single-project change inside the existing `Spectra.CLI` project. The fix is local to `UserDescribedGenerator` and its xUnit test file. No new files, no new projects, no new dependencies. Documentation updates are markdown-only.

## Complexity Tracking

> Constitution Check passed without violations — this section is intentionally empty.
