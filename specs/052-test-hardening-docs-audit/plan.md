# Implementation Plan: Test Hardening & Documentation Audit (047–051)

**Branch**: `052-test-hardening-docs-audit` | **Date**: 2026-06-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/052-test-hardening-docs-audit/spec.md`

## Summary

A ship-readiness pass over the 047–051 reliability block. It adds (A) cross-spec end-to-end tests that exercise the CLI generation/persistence path and the MCP execution/discovery tools in one flow, (B) named regression guards whose displayed names are the original user symptoms, and (C) a categorized large-corpus extraction scale guard — then (D) audits and reconciles the documentation, (E) verifies SKILL coherence with captured transcripts, and (F) consolidates the CHANGELOG and records the silent-failure-pattern lesson. **No new product functionality and no new bug fixes.** Tests run hermetically by injecting a fake `IAgentRuntime` through the existing `agentFactory` seam and driving the real production services (`UserDescribedGenerator` → `TestPersistenceService` → `StartExecutionRunTool`/`FindTestCasesTool` reading the real on-disk `_index.json`) and the `internal` extraction loop (`DocsIndexHandler.ExtractCriteriaLoopAsync`, `AnalyzeHandler.ExtractWithRetryAsync`).

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.3.0, coverlet.collector 8.0.1 (test only); GitHub Copilot SDK, System.CommandLine, System.Text.Json, YamlDotNet (under test, not invoked live)  
**Storage**: File-based fixtures (`test-cases/{suite}/_index.json`, `docs/`, `spectra.config.json`); SQLite via `ExecutionDb` for MCP run state in temp dirs  
**Testing**: xUnit; temp-dir fixtures (`TempWorkspace`, per-test `Path.GetTempPath()` dirs); `[Collection("WorkingDirectory")]` for CWD-mutating tests; `[Trait("Category","Scale")]` for the scale guard  
**Target Platform**: Cross-platform .NET 8 (CI on a fresh checkout)  
**Project Type**: CLI + MCP server (multi-assembly); this feature adds one test project  
**Performance Goals**: Full test suite incl. scale guard completes within a normal CI budget; scale guard excludable via `Category!=Scale` for fast feedback  
**Constraints**: Hermetic/offline tests (no live Copilot/network); no production code change; deterministic on fresh checkout  
**Scale/Scope**: 2 new test classes (Part A 7 tests, Part B 5 tests) + scale class (Part C); 1 new test project; ~9 doc files audited; 15 SKILL + 2 agent files reviewed; CHANGELOG + PROJECT-KNOWLEDGE updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ Pass | All artifacts are files in the repo (tests, docs, changelog). No external store. |
| II. Deterministic Execution | ✅ Pass | Tests assert the existing deterministic engine; fakes are deterministic; no randomness/time dependence in assertions. |
| III. Orchestrator-Agnostic | ✅ Pass | No MCP tool surface change; tests assert existing self-contained responses. |
| IV. CLI-First | ✅ Pass | No UI; exercises existing CLI/MCP behavior. Documented limitation: from-description has no command-level agent seam, so tests drive the same services one level below the command (no production change). |
| V. Simplicity (YAGNI) | ⚠️ Justified | One new test project (`Spectra.Integration.Tests`). See Complexity Tracking. No new abstractions, flags, or production code. |

**Quality Gates**: `spectra validate` behavior is unchanged; the suite must pass on fresh checkout (SC-008). **Test discipline**: integration tests cover the CLI↔MCP seam and MCP tool contracts, matching the constitution's required integration coverage.

**Post-Phase-1 re-check**: ✅ Still passing. Design introduces only test-only helper types and documentation; the single justified new project is unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/052-test-hardening-docs-audit/
├── plan.md              # This file
├── research.md          # Phase 0 — seams, decisions (DONE)
├── data-model.md        # Phase 1 — test/doc entities (DONE)
├── quickstart.md        # Phase 1 — how to run/verify (DONE)
├── contracts/
│   └── test-inventory.md # Phase 1 — test identity + observable assertions (DONE)
├── checklists/
│   └── requirements.md   # Spec quality checklist (DONE)
└── tasks.md             # Phase 2 — /speckit.tasks output

docs/specs/
├── 052-doc-audit-report.md   # Part D deliverable
└── 052-skill-transcripts.md  # Part E deliverable
```

### Source Code (repository root)

```text
tests/
├── Spectra.Integration.Tests/            # NEW project (refs Spectra.CLI + Spectra.MCP + Spectra.Core)
│   ├── Spectra.Integration.Tests.csproj
│   ├── Support/
│   │   ├── FakeAgentRuntime.cs           # deterministic IAgentRuntime
│   │   ├── OnDiskIndexLoader.cs          # reads real _index.json → TestIndexEntry
│   │   └── IntegrationWorkspace.cs       # temp project dir (config + docs + suites)
│   ├── EndToEndScenarios.cs              # Part A (7 tests)
│   └── OriginalBugRegression.cs          # Part B (5 named tests)
└── Spectra.CLI.Tests/
    └── Extraction/
        └── ScaleTests.cs                 # Part C ([Trait Category=Scale])

src/Spectra.CLI/Skills/Content/Skills/    # Part E audit targets (spectra-generate/docs/coverage/criteria + others)
src/Spectra.CLI/Skills/Content/Agents/    # spectra-execution.agent.md, spectra-generation.agent.md
docs/                                     # Part D audit targets
CHANGELOG.md                              # Part F
PROJECT-KNOWLEDGE.md                      # Part F
Spectra.slnx                              # add the new test project
```

**Structure Decision**: Multi-assembly CLI+MCP solution. The cross-spec and named-regression suites land in a new `tests/Spectra.Integration.Tests/` project because they are the only tests that must reference both `Spectra.CLI` and `Spectra.MCP`; the scale guard stays in `Spectra.CLI.Tests` (CLI-only). Documentation deliverables live under `docs/specs/`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New test project `Spectra.Integration.Tests` | The headline cross-spec workflows (`FromDescriptionHighPriority_RunsViaFilter_EndToEnd`, `IndexDeployed_AfterFromDescription_FindTestCasesReturnsIt`) must drive the CLI generation/persistence path **and** the MCP `start_execution_run`/`find_test_cases` tools in one continuous test. The CLI↔MCP seam is precisely what this spec covers. | No existing project references both assemblies (`Spectra.CLI.Tests`→CLI only, `Spectra.MCP.Tests`→MCP only). Adding a CLI reference to `Spectra.MCP.Tests` would invert layering and put a CLI concern under MCP unit tests; splitting a single user journey across two assemblies makes the continuous assertion impossible. The constitution explicitly permits new projects with justification and requires integration coverage of the CLI↔MCP seam. |
