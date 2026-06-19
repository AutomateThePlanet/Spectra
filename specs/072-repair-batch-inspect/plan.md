# Implementation Plan: Repair-Orchestration Hardening & Inspection Surface

**Branch**: `072-repair-batch-inspect` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

## Summary

Add two new model-free CLI commands (`compile-repair-batch`, `audit-grounding`) and a one-field fix (`show` → `file`) to reduce per-test agent operations in the repair loop from 9 to ~5, make the loop resumable across sessions, and eliminate the raw-shell improvisation that halts unattended runs. The critic subagent remains agent-driven (context-fork, different model family). The grounding block written by `ingest-grounding` is the checkpoint that makes resume free — re-running `compile-repair-batch` automatically skips already-grounded tests.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine, System.Text.Json, Spectra.Core (existing — no new NuGet packages)
**Storage**: `.spectra/verdicts/critic-verdict-{id}.json` (read), `test-cases/{suite}/*.md` (read — grounding block check via `TestCaseParser`), `.spectra/repairs/repaired-{id}.json` (scratch — gitignored)
**Testing**: xUnit (existing test pattern); ~24 new tests across CLI.Tests
**Target Platform**: .NET 8 CLI tool
**Project Type**: CLI extension — 2 new commands, 1 field fix, 1 skill rewrite, 1 doc update
**Performance Goals**: `audit-grounding` sub-2s for 100 tests (sequential file reads)
**Constraints**: No new NuGet packages; `compile-repair-batch` reuses `RepairPromptCompiler.Compile()` verbatim — no duplicated prompt logic
**Scale/Scope**: Operates on per-suite verdict files (10–200 tests)

## Constitution Check

| Principle | Check | Status |
|-----------|-------|--------|
| I. GitHub as Source of Truth | Grounding blocks written to `.md` frontmatter (in git); verdict JSONs and repair scratch files gitignored | PASS |
| II. Deterministic Execution | Both new commands are model-free and deterministic; critic stays agent-driven | PASS |
| III. Orchestrator-Agnostic | All new commands are `spectra ai *` CLI verbs with `--output-format json`; no model calls | PASS |
| IV. CLI-First | New functionality exposed as named CLI commands; no chat loops; CI-friendly exit codes | PASS |
| V. Simplicity (YAGNI) | 2 new commands (~250 LOC combined), 1 field, skill prose rewrite — no abstractions beyond what the task requires | PASS |

No constitution violations. Complexity Tracking table not required.

## Project Structure

### Documentation (this feature)

```text
specs/072-repair-batch-inspect/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/           ← Phase 1 output
│   ├── compile-repair-batch.md
│   ├── audit-grounding.md
│   └── show-update.md
└── tasks.md             ← /speckit.tasks output
```

### Source Code (new / modified)

```text
src/Spectra.CLI/
  Commands/
    Generate/
      CompileRepairBatchCommand.cs    [NEW]   compile-repair-batch
    Review/
      AuditGroundingCommand.cs        [NEW]   audit-grounding (command registration shell)
      AuditGroundingHandler.cs        [NEW]   audit-grounding logic (mirrors ReviewFlaggedHandler pattern)
    Ai/
      AiCommand.cs                    [MOD]   register 2 new commands
    Show/
      ShowHandler.cs                  [MOD]   pass filePath to TestDetail
  Results/
    ShowResult.cs                     [MOD]   add File property to TestDetail
    AuditGroundingResult.cs           [NEW]   typed JSON result for audit-grounding
  Skills/Content/Skills/
    spectra-generate.md               [MOD]   Step 8 rewrite (numbered manifest-driven loop)

tests/Spectra.CLI.Tests/Commands/
  CompileRepairBatchCommandTests.cs   [NEW]   ~10 tests
  AuditGroundingCommandTests.cs       [NEW]   ~10 tests
  ShowHandlerFileFieldTests.cs        [NEW]   ~4 tests

docs/
  cli-reference.md                    [MOD]   new verbs + show file field
  usage.md                            [MOD]   repair loop section updated

.gitignore                            [MOD]   add .spectra/repairs/
```
