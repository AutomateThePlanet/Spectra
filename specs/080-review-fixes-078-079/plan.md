# Implementation Plan: Review Fixes for Specs 078 and 079

**Branch**: `080-review-fixes-078-079` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/080-review-fixes-078-079/spec.md`

## Summary

This plan fixes the fourteen code-review defects in 078 (merged) and 079 (`1ba172a`, not merged). No features are added.
Every item gets a regression test that fails before the fix.

| Item | Fix |
|---|---|
| A1 | Update writes to and deletes the test's own file (R1). |
| A2 | Refs resolve under the repo root or the configured source dir, even when the latter is outside the repo; the reason for a refusal is reported (R2). |
| A3 | One list of frontmatter keys, plus a DTO reflection guard (R3). |
| A4 | Comments and blank lines attach to the key below them; the leading block stays at the top (R4). |
| A5 | `ConfigLoader.LoadForReadAsync` / `LoadForWriteAsync` for every in-scope command (R5). |
| B1–B3 | One `CriteriaProvenance` rule and `LeaveSharedFileAsync` (R6). |
| B4 | Tags compared as a set (R7). |
| B5 | Skill instructions for exit 1 (R8). |
| B6 | New outcome `Rejected` (R9). |
| B7 | Compile failure JSON carries its warnings (R10). |
| B8 | One criteria-file scan (R11). |
| B9 | The normaliser strips only whitelisted, well-formed HTML tags (R12). |

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318
**Primary Dependencies**: YamlDotNet 16.3.0, System.Text.Json, System.CommandLine. No new packages.
**Storage**: Files (test `.md`, criteria `.criteria.yaml`, `_criteria_index.yaml`, `spectra.config.json`).
**Testing**: xUnit. Baseline at `1ba172a`: Core 694 / CLI 1423 (1 known failure) / Execution 228 / Integration 8.
Criteria and update CLI tests run in the non-parallel `CriteriaCommands` collection.
**Target Platform**: Cross-platform CLI.
**Project Type**: CLI and shared library.
**Performance Goals**: No change.
**Constraints**: Model-free and deterministic (III). Deterministic exit codes, unchanged except as the contract states
(IV). No shims (V).
**Scale/Scope**: About 20 edited source files, 2 new small Core types (`TestCaseFrontmatterKeys`,
`CriteriaProvenance`), 1 skill, 3 docs. About 12 new or extended test classes.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Fixes stop duplicate test files and stale criteria; git remains the undo path. | ✅ |
| II. Deterministic execution | Engine untouched. | ✅ N/A |
| III. Model-free / minimal structured output | All fixes are CLI logic. B7 makes JSON-mode failure output a single document. The human-summary exception recorded in 079 is unchanged. | ✅ |
| IV. CLI-first, deterministic exit codes | Exit codes are unchanged. Config errors are exit 1 everywhere. `docs changed` gains default-config behaviour (documented). | ✅ |
| V. Simplicity | Removes duplication: key lists, config loaders and the scan. The only new abstractions are two tiny helpers, each with three or more callers. | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/080-review-fixes-078-079/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-behaviour-changes.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Models/TestCaseFrontmatterKeys.cs          # NEW (A3)
├── Models/TestCaseLayout.cs                   # + FrontmatterLeading/Trailing, FrontmatterEntry.Prelude (A4)
├── Parsing/TestCaseParser.cs                  # keys from A3; prelude/leading/trailing capture (A4)
├── Config/ConfigLoader.cs                     # + LoadForReadAsync/LoadForWriteAsync, ConfigLoadResult (A5)
├── Coverage/SourceRefResolver.cs              # allowed roots, OutsideAllowedRoots (A2)
├── Criteria/CriteriaProvenance.cs             # NEW (B1–B3)
├── Criteria/CriteriaChangeset.cs              # tags set key (B4)
└── Text/TextNormalizer.cs                     # whitelist tag regex (B9)

src/Spectra.CLI/
├── IO/TestFileWriter.cs                       # KeyOrder from A3; emit leading/prelude/trailing (A4)
├── Commands/Update/UpdateHandler.cs           # own-file paths (A1)
├── Source/SourceRefDocumentLoader.cs          # takes sourceDir; reason-specific messages (A2, A5)
├── Commands/Generate/CompileCriticPromptCommand.cs, CompileRepairPromptCommand.cs, CompileRepairBatchCommand.cs  # read config (A5)
├── Commands/Generate/CompileExtractionPromptCommand.cs   # read config (A5), provenance (B3), failure JSON (B7)
├── Commands/Generate/IngestCriteriaCommand.cs # write config (A5), Rejected (B6)
├── Commands/Analyze/AnalyzeHandler.cs         # import: write config (A5)
├── Commands/Docs/DocsChangedHandler.cs        # read config (A5), shared scan (B8)
├── Commands/Docs/DocsApplyMoveCommand.cs, DocsRetireCommand.cs   # provenance + LeaveSharedFileAsync (B1, B2)
├── Criteria/CriteriaCommandIO.cs              # delegates to ConfigLoader.LoadForWriteAsync (A5)
├── Criteria/CriteriaStore.cs                  # ScanFilesAsync (B8), LeaveSharedFileAsync (B2)
├── Extraction/ExtractionOutcome.cs, CriteriaIngestor.cs   # Rejected (B6), LeaveSharedFileAsync (B2)
└── Skills/Content/Skills/spectra-criteria.md  # exit-1 handling (B5)
```

**Structure decision**: No new projects or folders. The fixes stay in the files that own the behaviour.

## Implementation Order

1. **Tests first for every item.** Each new test must fail on `1ba172a`. Record the failing names in the PR.
2. **Core:**
   - A3 (keys)
   - A4 (layout and parser)
   - A2 (resolver)
   - A5 (ConfigLoader)
   - B9 (normaliser)
   - B4 (changeset)
   - B6 enum value
   - provenance helper
3. **CLI:**
   - A1 (UpdateHandler)
   - A4 writer
   - A2/A5 loaders
   - A5 command rewiring
   - B8 scan
   - B1–B3 (provenance at every call site plus `LeaveSharedFileAsync`)
   - B6 wiring
   - B7 compile failure JSON
4. **Skill** (B5).
5. **Docs:**
   - `CHANGELOG.md`: a "Fixed (review)" subsection under `[2.6.0]`. 2.6.0 has no tag and is not merged into
     `fix/doc-change-update-workflow`, so no released version is being patched (see Risks).
   - `CLAUDE.md` "Recent Changes".
   - `docs/cli-reference.md`: config behaviour, out-of-repo source dir, `Rejected`, compile failure JSON.
6. Full `dotnet test`. Then commit on 080, and merge per the local workflow when the user approves.

## Risks

| Risk | Mitigation |
|---|---|
| The A4 layout shape change breaks 078 round-trip guarantees | The byte-identity and round-trip tests (`TestFileWriterRoundTripTests`, `WriterPathPreservationTests`) must stay green unchanged. Only the one layout assertion that encoded the bug is updated. |
| A5 strictness breaks tests that run with minimal configs | Test fixtures already write complete configs (`SpectraConfig.Default` or `source`/`tests`/`ai`). Read commands tolerate a missing config. Run the full suite after A5. |
| Version numbering (079 = 2.6.0 is unreleased) | Decided: fold the fixes into `[2.6.0]` under "Fixed (review)". Checked 2026-09-29: no `v2.6*` tag, and `1ba172a` is only on `079-stable-criteria-ids` and this branch. |
| B8 has no behavioural "fails before" test | A structural reflection guard plus a behavioural equivalence check (R11), stated openly. |

## Complexity Tracking

No constitution violations.
