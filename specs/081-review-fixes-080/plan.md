# Implementation Plan: Review Fixes for Spec 080 (Part C)

**Branch**: `081-review-fixes-080` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/081-review-fixes-080/spec.md`

## Summary

This plan fixes five defects found in the review of 080, which is merged in `1c8d8de`. No features are added. Every
item gets a regression test that fails on `1c8d8de`.

| Item | Fix |
|---|---|
| C1 | Criteria ownership and document lookup compare repo-aware normalised paths (R1). |
| C2 | Each command declares the config sections it reads. Missing ones are filled with defaults before deserializing, then a read command warns and a write command refuses (R2). |
| C3 | The normaliser strips more elements and accepts bare boolean attributes from a fixed list (R3). |
| C4 | One writer for the criteria failure JSON: `{command, success, error_code, errors, warnings}` plus extras (R4). |
| C5 | The trailing lines of a keep-chomped block scalar stay with its entry (R5). |

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318
**Primary Dependencies**: System.Text.Json (`JsonNode` for the section pre-scan), YamlDotNet 16.3.0. No new packages.
**Storage**: Files (`spectra.config.json`, criteria YAML, test `.md`).
**Testing**: xUnit. Baseline at `1c8d8de`: Core 735 / CLI 1498 (1 known failure: `ProfileFormatLoaderTests`) /
Execution 228 / Integration 8. Criteria and config command tests run in the `CriteriaCommands` collection.
**Target Platform**: Cross-platform CLI; CI on ubuntu.
**Project Type**: CLI and shared library.
**Performance Goals**: No change. The config pre-scan parses a small file once.
**Constraints**: Model-free and deterministic. Exit codes unchanged. Human-mode output unchanged. The shared
`SpectraConfig` model and all out-of-scope loaders are untouched.
**Scale/Scope**: About 14 edited source files, 1 new enum (`ConfigSections`), 1 skill, 3 docs, and about 8 test
classes new or extended.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | C1 stops ID churn that breaks tests' `criteria:` links in git. C5 stops silent value changes on rewrite. | ✅ |
| II. Deterministic execution | Engine untouched. | ✅ N/A |
| III. Minimal structured output | C4 makes every criteria failure one uniform JSON document. | ✅ |
| IV. CLI-first, deterministic exit codes | Exit codes unchanged. Config rules are documented per command. | ✅ |
| V. Simplicity | One failure writer replaces two. One path comparison replaces two lexical ones. The config pre-scan avoids changing a model that about 30 commands share. | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/081-review-fixes-080/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-behaviour-changes.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Criteria/CriteriaProvenance.cs      # OwnedBy(…, repoRoot), SameDocument (C1)
├── Config/ConfigLoader.cs              # ConfigSections, section pre-scan, Warnings (C2)
├── Text/TextNormalizer.cs              # elements + boolean attributes (C3)
└── Parsing/TestCaseParser.cs           # keep-chomp aware trailing split (C5)

src/Spectra.CLI/
├── Criteria/CriteriaStore.cs           # FindDocumentSource / LeaveSharedFileAsync via SameDocument/OwnedBy (C1)
├── Criteria/CriteriaCommandIO.cs       # the one failure writer; LoadConfigAsync(reads) (C2, C4)
├── Extraction/CriteriaIngestor.cs      # OwnedBy(repoRoot) (C1)
├── Commands/Generate/IngestCriteriaCommand.cs        # reads Tests; failure JSON + command/warnings (C2, C4)
├── Commands/Generate/CompileExtractionPromptCommand.cs  # lookup + OwnedBy (C1); shared writer (C4)
├── Commands/Generate/CompileCriticPromptCommand.cs, CompileRepairPromptCommand.cs, CompileRepairBatchCommand.cs  # reads Source|Tests, warnings (C2)
├── Commands/Docs/DocsChangedHandler.cs  # lookup (C1); reads Source, warnings (C2)
├── Commands/Docs/DocsRetireCommand.cs   # OwnedBy (C1); reads Tests (C2)
├── Commands/Docs/DocsApplyMoveCommand.cs, DocsMigrateCriteriaCommand.cs  # OwnedBy (C1); reads None (C2)
├── Commands/Analyze/AnalyzeHandler.cs   # --import-criteria reads None (C2)
└── Skills/Content/Skills/spectra-criteria.md  # errors[0] everywhere (C4)
```

**Structure decision**: No new files except the `ConfigSections` enum, which lives in `ConfigLoader.cs`. The fixes stay
in the files that own the behaviour.

## Implementation Order

1. **Tests first**, per item. Each must fail on `1c8d8de` (or fail to compile against a new API).
2. **C1:**
   - `CriteriaProvenance` (Core).
   - Then the five `OwnedBy` callers and the three lookups.
3. **C2:**
   - `ConfigLoader` (Core).
   - Then the ten commands' declared reads.
   - Warnings output in the four read commands that can get one.
   - Replace the 080 `MissingAiSection` test.
4. **C3:** normaliser regex and lists.
5. **C4:**
   - `CriteriaCommandIO.WriteFailure` gains warnings and extras.
   - Ingest classified failure; compile's two writers delegate.
   - Update the 080 tests on `message`/`refused`.
   - Skill Step 2a.
6. **C5:**
   - Parser keep-chomp detection. Pin the scalar start-mark position with a unit test first.
   - Check that all 078/080 layout and round-trip tests are green unchanged.
7. **Docs:**
   - `CHANGELOG.md`: "Fixed (review, Spec 081)" under the unreleased `[2.6.0]`.
   - `CLAUDE.md` "Recent Changes".
   - `docs/cli-reference.md`: the config table, the failure shape, the normaliser note.
8. Full `dotnet test`. Fails-before check in a worktree at `1c8d8de`. Quickstart spot checks. Then commit on 081 and
   merge per the local workflow when the user approves.

## Risks

| Risk | Mitigation |
|---|---|
| C2 pre-scan disagrees with the loader on what counts as present (casing, comments) | Parse with the same `JsonDocumentOptions` as the loader (comments skipped, trailing commas allowed); match keys case-insensitively, as `PropertyNameCaseInsensitive` does. Test both. |
| C4 changes the 080 JSON contract for compile-extraction-prompt | 2.6.0 is unreleased; the skill and tests are updated together; contract documented. |
| C5 start-mark position differs from expectation | Unit test pins it before the logic lands; the scan handles either position. |
| C3 new element names swallow placeholder text | Placeholder-prone names excluded (R3); test cases for `<data>`, `<time>`, `<label>` as text. |

## Complexity Tracking

No constitution violations.
