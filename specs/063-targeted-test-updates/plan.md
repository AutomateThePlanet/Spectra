# Implementation Plan: Targeted test updates (inverted update seam)

**Branch**: `063-targeted-test-updates` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/063-targeted-test-updates/spec.md`

## Summary

Add the missing **update** counterpart to the inverted compile→in-session→ingest seam that generation (053/059), criteria extraction (054), critic (055), and analysis (059) already have. Today the update flow makes **no AI calls** — `TestClassifier` flags tests as UP_TO_DATE / OUTDATED / ORPHANED / REDUNDANT but nothing rewrites OUTDATED tests against changed docs; the `spectra-update` skill's claim that the command "rewrites affected test cases" is false.

This feature adds two new CLI commands — **`spectra ai compile-update-prompt`** (deterministic prompt compiler, mirrors `CompilePromptCommand`) and **`spectra ai ingest-update`** (fail-loud validate+persist boundary, mirrors `IngestTestsCommand`/`GeneratedTestIngestor`) — plus an `UpdatedTestIngestor` that deterministically protects invariants (id from original, pre-existing `Manual` verdict + manual notes re-asserted, a drift guard that surfaces out-of-scope field changes). `TestClassifier` is reused **unchanged** as the selector; `TestPersistenceService` is the only persist path. The `spectra-update` skill is rewritten to drive the compile→edit→ingest loop with bounded fail-loud retry (mirroring `spectra-generate`), and the stale "rewrites" text is corrected.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI surface), System.Text.Json (ingest parsing), YamlDotNet (frontmatter), existing Spectra prompt-template engine (`PromptTemplateLoader`/`PlaceholderResolver`)
**Storage**: File-based — test cases as Markdown+YAML under `test-cases/{suite}/`, `_index.json` per suite (regenerated through `TestPersistenceService`)
**Testing**: xUnit (`Spectra.CLI.Tests`, `Spectra.Core.Tests`) — structured results, never throw on validation errors
**Target Platform**: Cross-platform CLI (Windows/macOS/Linux), runs inside the Claude Code interactive session
**Project Type**: CLI tool + skill content (single repo, multi-project solution)
**Performance Goals**: Deterministic, sub-second compile/ingest per test batch; no model calls in-process (editing happens in-session)
**Constraints**: No in-process model, no Copilot SDK dependency (runs in interactive session); persistence MUST go through the single write+index path; reused components (`TestClassifier`, `TestPersistenceService`, `Spectra.Core`) MUST NOT be modified
**Scale/Scope**: Two new CLI commands + one new ingestor + one new prompt template + one new prompt compiler + skill/doc rewrites. ~1 new template, ~4 new source files, ~5 new/rewritten test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. GitHub as Source of Truth** | ✅ PASS | Updated tests persist as Markdown+YAML under `test-cases/`; `_index.json` regenerated and committed. No external store. |
| **II. Deterministic Execution** | ✅ PASS | `compile-update-prompt` and `ingest-update` are deterministic and model-free. The model edit happens in-session (orchestrator), never in-process. Ingest is a validated state boundary — invalid edits rejected, nothing persisted. |
| **III. Orchestrator-Agnostic Design** | ✅ PASS | New commands are self-contained CLI calls with explicit args and stdin/file I/O; no orchestrator memory required. Output is minimal JSON/human. Mirrors the existing seam exactly. |
| **IV. CLI-First Interface** | ✅ PASS | Both capabilities are named CLI commands with deterministic exit codes (0/1/4/5/6, mirroring the generation seam). The AI never writes files directly — all persistence flows through `ingest-update` → `TestPersistenceService`. |
| **V. Simplicity (YAGNI)** | ✅ PASS | Reuses `TestPersistenceService`, `TestClassifier`, the template engine, and the generation ingest/validation patterns verbatim. Whole-test-edit chosen over per-field surgical merge (simpler; per-field documented as fallback only). No new abstractions beyond the one ingestor the seam requires. |

**Quality Gates** (constitution §Quality Gates): The new ingest path persists through `TestPersistenceService`, which regenerates `_index.json` (Index Currency), preserves unique ids (ID Uniqueness — id taken from original, never reallocated), and writes valid frontmatter (Schema Validation, enforced by the ingest validator before persist). `spectra validate` remains green.

**Result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/063-targeted-test-updates/
├── plan.md              # This file (/speckit.plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output — CLI command contracts
│   ├── compile-update-prompt.md
│   └── ingest-update.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

New and modified files, anchored to the existing seam layout:

```text
src/Spectra.CLI/
├── Commands/Ai/
│   └── AiCommand.cs                         # MODIFY: register the two new subcommands
├── Commands/Generate/                        # the existing seam lives here; mirror it
│   ├── CompileUpdatePromptCommand.cs        # NEW: mirrors CompilePromptCommand
│   └── IngestUpdateCommand.cs               # NEW: mirrors IngestTestsCommand
├── Generation/
│   ├── UpdatePromptCompiler.cs              # NEW: mirrors PromptCompiler (Assemble/Compile)
│   └── UpdatedTestIngestor.cs               # NEW: mirrors GeneratedTestIngestor + invariant
│                                            #      protection (id, manual fields, drift guard)
├── IO/
│   └── TestPersistenceService.cs            # REUSE UNCHANGED (persist boundary)
└── Skills/Content/Skills/
    └── spectra-update.md                    # REWRITE: drive compile→edit→ingest loop;
                                             #          remove false "rewrites" text

src/Spectra.Core/
├── Update/
│   └── TestClassifier.cs                    # REUSE UNCHANGED (selector only)
└── Models/                                  # REUSE UNCHANGED (TestCase, GroundingMetadata,
                                             #   VerificationVerdict.Manual)

prompts/                                      # template engine storage
└── test-update.md (or equivalent)           # NEW: the update prompt template

tests/
├── Spectra.CLI.Tests/Generation/
│   ├── UpdatePromptCompilerTests.cs         # NEW
│   └── UpdatedTestIngestorTests.cs          # NEW (invariant + drift guard + bounded retry)
├── Spectra.CLI.Tests/Commands/Generate/
│   └── IngestUpdateBoundaryTests.cs         # NEW (end-to-end seam, exit codes)
├── Spectra.CLI.Tests/Commands/
│   └── UpdateCommandTests.cs                # REWRITE assertions that pin "no AI" flow
├── Spectra.Core.Tests/Update/
│   └── TestClassifierTests.cs               # DO NOT TOUCH (regression net)
└── Spectra.CLI.Tests/IO/
    └── TestPersistenceServiceTests.cs       # DO NOT TOUCH (regression net)
```

**Structure Decision**: Single-project CLI layout (constitution Project Type). The update seam is built **alongside** the generation seam in `Commands/Generate/` and `Generation/` because it mirrors those classes one-for-one and shares their helpers (`TestPersistenceService`, the template engine, the `TestValidator`). This keeps the four seams visually parallel and maximizes reuse, satisfying YAGNI. No new project, no new dependency.

## Complexity Tracking

> No Constitution Check violations — table intentionally empty.
