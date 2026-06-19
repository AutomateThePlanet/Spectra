# Implementation Plan: Verdict Disposition Policy

**Branch**: `071-verdict-disposition` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)
**Input**: `specs/071-verdict-disposition/spec.md`

## Summary

Make the critic's judgment durable, visible, and actionable. Today partial verdicts leave no trace on the test artifact; hallucinated drops have no audit trail; there is no repair path. This spec revives the dead `TestFileWriter` grounding write-back (FR1), adds per-test verdict files (FR2), a drop trail (FR3), a bounded repair loop for partial tests (FR4/FR5), a human review phase (FR6), consistency enforcement (FR7), and docs cleanup (FR8). New CLI verbs: `ingest-grounding`, `record-drop`, `compile-repair-prompt`, `review-flagged`. No new lifecycle state; no score gating. Implementation is strictly sequential: Phase 1 → Phase 2 → Phase 3 → Phase 4 (each is a prerequisite for the next).

---

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI verbs), Spectre.Console (interactive review TUI), System.Text.Json (verdict JSON), YamlDotNet (grounding frontmatter DTO), Microsoft.Extensions.FileSystemGlobbing (file scan in review-flagged)
**Storage**: File-based: `test-cases/{suite}/*.md` (test artifacts), `test-cases/{suite}/_index.json` (test index), `.spectra/verdicts/critic-verdict-{id}.json` (per-test full verdict JSON), `.spectra/dropped-tests.json` (NDJSON drop trail)
**Testing**: xUnit (`Spectra.CLI.Tests`, `Spectra.Core.Tests`); structured result assertions (never throw on validation errors per code style); all new commands have unit + integration tests
**Target Platform**: CLI / dotnet tool (win32 / Linux / macOS)
**Project Type**: CLI tool (System.CommandLine)
**Performance Goals**: Each new CLI command completes in <500ms (all are model-free; they read a handful of files + write back)
**Constraints**: No model calls in any new C# command. Fail-loud on every boundary (no silent fallbacks). `_index.json` modifications must go through the validated write path (not raw JSON write).

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I — GitHub as Source of Truth (determinism at boundaries)
**Status: PASS.** All new CLI verbs are deterministic (model-free). No model call in C# code. The repair prompt is compiled deterministically; the agent turn is in-session. The seam pattern (compile → agent → ingest) is used throughout and is already the established pattern.

### Principle II — Deterministic Execution
**Status: PASS.** `ingest-grounding`, `record-drop`, `compile-repair-prompt`, `review-flagged` — all four are model-free C# commands. No inference at ingest time. The repair agent turn is a skill step, not a C# command.

### Principle III — Orchestrator-Agnostic
**Status: PASS (inherited).** No MCP surface; no MCP dependency. The skill updates drive CLI commands — same as `spectra-generate` and `spectra-update`. Review command is CLI-only (matching Spec 070 "CLI-first" direction).

### Principle IV — CLI-First
**Status: PASS.** Every new capability is a CLI verb under `spectra ai`. The interactive `review-flagged` command is a Spectre.Console TUI (same pattern as existing interactive commands). No new dotnet-tool; adds to existing `spectra` tool.

### Principle V — Simplicity / YAGNI
**Status: PASS.** Bounded repair loop (1 attempt, not configurable in Phase 2 — human decides more in Phase 3). No new `status: rejected` lifecycle state. Grounding block is condensed (not full JSON in frontmatter). `DroppedTestsTrail` is a thin file-appender, not a full repository. `ingest-update` reused for repair persistence (no duplicate command). `DeleteHandler` reused for trail+delete. `VerdictIngestor.Classify()` reused for verdict parsing in all new commands.

**Complexity Tracking**: No violations. No new projects added. No new persistence layers.

---

## Project Structure

### Documentation (this feature)

```text
specs/071-verdict-disposition/
├── plan.md              # This file
├── research.md          # Phase 0 — all decisions resolved
├── data-model.md        # Phase 1 — entities, YAML format, C# types
├── quickstart.md        # Phase 1 — end-to-end flow examples
├── contracts/
│   ├── ingest-grounding.md
│   ├── record-drop.md
│   ├── compile-repair-prompt.md
│   └── review-flagged.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (changes and additions)

```text
src/
  Spectra.Core/
    Models/
      Grounding/
        GroundingMetadata.cs          # MODIFIED: +FlaggedForReview, +RepairAttempts, +Repaired, +CondensedFindings
        GroundingFrontmatter.cs       # MODIFIED: +matching YAML properties
        VerificationVerdict.cs        # MODIFIED: fix 2 stale enum doc comments
        CondensedFinding.cs           # NEW: { Element, Reason }
        CondensedFindingFrontmatter.cs # NEW: YAML DTO

  Spectra.CLI/
    Commands/
      Generate/
        IngestGroundingCommand.cs     # NEW: spectra ai ingest-grounding
        RecordDropCommand.cs          # NEW: spectra ai record-drop
        CompileRepairPromptCommand.cs # NEW: spectra ai compile-repair-prompt
        IngestVerdictCommand.cs       # MODIFIED: fix 1 stale comment (line 17-18)
      Review/
        ReviewFlaggedCommand.cs       # NEW: spectra ai review-flagged
        ReviewFlaggedHandler.cs       # NEW: accept / delete logic (interactive + non-interactive)
      Ai/
        AiCommand.cs                  # MODIFIED: register 4 new commands
    Verification/
      RepairPromptCompiler.cs         # NEW: deterministic repair prompt builder
      VerdictIngestor.cs              # UNCHANGED (reused as-is)
      VerdictIngestResult.cs          # UNCHANGED (reused as-is)
    IO/
      TestFileWriter.cs               # MODIFIED: write new optional grounding fields
      DroppedTestsTrail.cs            # NEW: append-only NDJSON writer
      GroundingWriteBackService.cs    # NEW: load test → set Grounding → write via TestFileWriter
    Skills/
      Content/
        Skills/
          spectra-generate.md         # MODIFIED: Step 8 expanded (grounding + repair loop)
          spectra-review-flagged.md   # NEW: skill for retry-repair cycles
        Agents/
          spectra-critic.agent.md     # MODIFIED: per-test verdict file naming

tests/
  Spectra.Core.Tests/
    Grounding/
      CondensedFindingTests.cs        # NEW: model roundtrip
      GroundingMetadataTests.cs       # MODIFIED: cover new fields, extended IsValid()
  Spectra.CLI.Tests/
    Commands/
      IngestGroundingCommandTests.cs  # NEW
      RecordDropCommandTests.cs       # NEW
      CompileRepairPromptCommandTests.cs # NEW
      ReviewFlaggedCommandTests.cs    # NEW
    Verification/
      RepairPromptCompilerTests.cs    # NEW
    IO/
      DroppedTestsTrailTests.cs       # NEW
      GroundingWriteBackServiceTests.cs # NEW
      TestFileWriterGroundingTests.cs # MODIFIED: cover new grounding block fields

.gitignore                            # MODIFIED: add .spectra/verdicts/ and .spectra/dropped-tests.json
```

**Structure Decision**: Single-project layout (CLI + Core), existing project structure. All new types follow the established patterns (`Commands/`, `IO/`, `Verification/`). `Review/` subfolder created under `Commands/` following the `Run/` pattern from Spec 065.

---

## Complexity Tracking

No violations to justify. All five principles pass.
