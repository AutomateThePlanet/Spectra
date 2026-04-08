# Implementation Plan: Universal Progress/Result for SKILL-Wrapped Commands

**Branch**: `025-universal-skill-progress` | **Date**: 2026-04-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/025-universal-skill-progress/spec.md`

## Summary

Extract progress/result file logic from GenerateHandler and DocsIndexHandler into a shared `ProgressManager` service, then add progress/result support to all remaining SKILL-wrapped commands (update, coverage, extract-criteria, dashboard, validate). Fix `AnalyzeCoverageResult.requirements` field rename. Update SKILL files and agent prompts with universal progress flow.

## Technical Context

**Language/Version**: C# 12, .NET 8+  
**Primary Dependencies**: System.Text.Json (serialization), Spectre.Console (terminal UX), System.CommandLine (CLI)  
**Storage**: File system (`.spectra-result.json`, `.spectra-progress.html` at workspace root)  
**Testing**: xUnit (462 Core + 466 CLI + 351 MCP = 1279 total tests)  
**Target Platform**: Windows, macOS, Linux (cross-platform CLI)  
**Project Type**: CLI tool  
**Performance Goals**: Progress file writes < 50ms, no measurable impact on command execution time  
**Constraints**: Atomic file writes required for NTFS reliability, progress files are informational (must not fail the command)  
**Scale/Scope**: 9 SKILL-wrapped commands, 6 need progress pages, 3 need result-only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Progress files are transient (.gitignored), not stored in Git |
| II. Deterministic Execution | PASS | ProgressManager is a side-effect layer; does not affect command logic or state |
| III. Orchestrator-Agnostic Design | PASS | Result JSON schema is generic; any SKILL/agent can read it |
| IV. CLI-First Interface | PASS | All commands remain CLI-first; progress files supplement `--output-format json` |
| V. Simplicity (YAGNI) | PASS | ProgressManager is the 3rd use case (Generate, DocsIndex, now 4 more) — abstraction justified |

**Post-Phase-1 Re-check**: All gates still pass. ProgressManager is a thin wrapper over existing patterns, not a new abstraction layer.

## Project Structure

### Documentation (this feature)

```text
specs/025-universal-skill-progress/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # ProgressManager entity model
├── quickstart.md        # Developer quickstart guide
├── contracts/
│   ├── result-json-schema.md    # .spectra-result.json contract
│   └── progress-html-contract.md # .spectra-progress.html contract
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (from /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Progress/
│   │   ├── ProgressPageWriter.cs      # MODIFY: Add phase configs for new commands
│   │   ├── ProgressManager.cs         # CREATE: Shared progress/result service
│   │   └── ProgressPhases.cs          # CREATE: Static phase definitions
│   ├── Commands/
│   │   ├── Generate/GenerateHandler.cs  # MODIFY: Refactor to use ProgressManager
│   │   ├── Update/UpdateHandler.cs      # MODIFY: Add ProgressManager integration
│   │   ├── Docs/DocsIndexHandler.cs     # MODIFY: Refactor to use ProgressManager
│   │   ├── Analyze/AnalyzeHandler.cs    # MODIFY: Add ProgressManager for coverage + extract-criteria
│   │   ├── Dashboard/DashboardHandler.cs # MODIFY: Add ProgressManager
│   │   └── Validate/ValidateHandler.cs  # MODIFY: Write result file (no progress page)
│   ├── Results/
│   │   ├── AnalyzeCoverageResult.cs     # MODIFY: Rename requirements → acceptanceCriteria
│   │   └── UpdateResult.cs              # CREATE: Typed result for update command
│   ├── Skills/Content/Skills/
│   │   ├── spectra-coverage.md          # MODIFY: Add progress page flow
│   │   ├── spectra-criteria.md          # MODIFY: Add progress page flow for extract
│   │   ├── spectra-dashboard.md         # MODIFY: Add progress page flow
│   │   └── spectra-validate.md          # MODIFY: Add result file reading
│   └── Skills/Content/Agents/
│       ├── spectra-generation.agent.md  # MODIFY: Add universal progress instructions
│       └── spectra-execution.agent.md   # MODIFY: Add progress instructions

tests/
├── Spectra.CLI.Tests/
│   ├── Progress/
│   │   └── ProgressManagerTests.cs     # CREATE: Unit tests for ProgressManager
│   └── Commands/
│       ├── UpdateHandlerProgressTests.cs     # CREATE
│       ├── AnalyzeHandlerProgressTests.cs    # CREATE
│       ├── DashboardHandlerProgressTests.cs  # CREATE
│       └── ValidateHandlerProgressTests.cs   # CREATE
```

**Structure Decision**: All new code fits within the existing `Spectra.CLI` project structure. New files go in the existing `Progress/` directory and `Results/` directory. No new projects needed.

## Implementation Phases

### Phase 1: Shared Infrastructure (ProgressManager)

**Goal**: Extract duplicated progress/result logic into a reusable service.

**Steps**:
1. Create `ProgressPhases.cs` with static phase arrays for all commands
2. Create `ProgressManager.cs` with Reset/Start/Update/Complete/Fail methods
3. Extend `ProgressPageWriter.cs` with new phase stepper configs and summary renderers
4. Refactor `GenerateHandler` to use ProgressManager (behavior-preserving)
5. Refactor `DocsIndexHandler` to use ProgressManager (behavior-preserving)
6. Write ProgressManager unit tests
7. Verify all existing tests pass

**Risk**: GenerateHandler refactor could break existing progress behavior. Mitigated by running full test suite after refactor.

### Phase 2: Terminology Rename

**Goal**: Complete the requirements → acceptance criteria rename.

**Steps**:
1. Rename `AnalyzeCoverageResult.Requirements` → `AcceptanceCriteria` with `[JsonPropertyName("acceptanceCriteria")]`
2. Update all code that references `AnalyzeCoverageResult.Requirements`
3. Audit remaining "requirement" strings in user-facing CLI output
4. Verify no dashboard regressions (JS already uses correct names)

**Risk**: Low — field rename is mechanical. JSON property attribute ensures backward-compatible output.

### Phase 3: Per-Command Progress Integration

**Goal**: Add ProgressManager to all remaining handlers.

**Steps**:
1. `UpdateHandler` — add ProgressManager with Classifying/Updating/Verifying/Completed phases
2. `AnalyzeHandler` (coverage path) — add ProgressManager with coverage phases
3. `AnalyzeHandler` (extract-criteria path) — add ProgressManager with extraction phases
4. `DashboardHandler` — add ProgressManager with Collecting Data/Generating HTML phases
5. `ValidateHandler` — add result file write only (no progress page)
6. `AnalyzeHandler` (import-criteria, list-criteria paths) — add result file write only
7. Create `UpdateResult` model if not already present
8. Write per-command progress tests

**Risk**: Medium — touching 5 handlers. Each handler integration is independent and can be tested in isolation.

### Phase 4: SKILL & Agent Updates

**Goal**: Update all SKILL files and agent prompts with universal progress flow.

**Steps**:
1. Update `spectra-coverage.md` SKILL with progress page flow
2. Update `spectra-criteria.md` SKILL with progress page flow for extract sub-command
3. Update `spectra-dashboard.md` SKILL with progress page flow
4. Update `spectra-validate.md` SKILL with result file reading
5. Update both agent prompts with universal progress instructions
6. Update `SkillsManifest` hashes
7. Write SKILL content tests (verify `--no-interaction --output-format json` flags)

**Risk**: Low — SKILL files are static markdown. Changes are additive.

### Phase 5: DataCollector Hardening

**Goal**: Ensure DataCollector never produces null coverage sections.

**Steps**:
1. Add null-coalescing at individual section level in `BuildCoverageSummaryAsync`
2. Verify empty-state rendering in dashboard (already handled per research)
3. Write DataCollector null-safety tests

**Risk**: Very low — defensive coding addition.

## Complexity Tracking

No constitution violations to justify. The `ProgressManager` abstraction is warranted by the "third use case" rule (Principle V) — Generate and DocsIndex are the first two, and four more commands are being added.
