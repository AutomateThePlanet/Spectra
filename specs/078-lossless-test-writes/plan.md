# Implementation Plan: Lossless Test Writes and Doc-Reference Resolution (Update Workflow Phase 0)

**Branch**: `078-lossless-test-writes` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/078-lossless-test-writes/spec.md`

## Summary

This phase stops the update workflow destroying test data, and makes `spectra ai update` find the documents that tests
actually reference. There are four changes.

1. **Lossless writes** (research R1–R7). Every writer path already goes through `TestFileWriter`. It becomes a
   baseline-diff serialiser:
   - The parser records a `TestCaseLayout`: raw frontmatter entries, raw body segments, the baseline values and the newline
     style.
   - The writer emits known keys in a fixed order. A value that is unchanged since parsing is reproduced verbatim; a
     changed or new value is rendered canonically with correct YAML escaping.
   - Unknown keys and unknown body sections are kept verbatim, in place.
   - `TestCase` becomes a `sealed record`, so the four field-by-field copies (orphan marking, grounding write-back, review
     accept, ingest-update) become `with` expressions.
   - The parser starts reading `status`, `orphaned_reason` and `orphaned_date`.
   - Grounding `source`, `created_by` and `note` round-trip, and manual-verdict parsing becomes deterministic.
2. **Classify without writing** (R8). In `spectra ai update`:
   - `--no-interaction` without `--no-review` classifies and reports only.
   - `--dry-run` no longer returns early, so `.spectra-result.json` is always produced.
   - `UpdateResult` gains `applied`.
   - The three skill call sites pass `--dry-run`, and `spectra init` gitignores the run-status files.
   - This is a breaking change for CI that relies on `--no-interaction` to apply; it now needs `--no-review`.
3. **One ref resolver** (R9). A new `SourceRefResolver` in Spectra.Core:
   - strips the fragment, normalises separators, resolves against the repo root first and then falls back to the source
     dir, and refuses paths outside the repo
   - is adopted by `UpdateHandler` / `TestClassifier` (classification now uses all resolved docs) and by the critic,
     repair-prompt and repair-batch loaders
   - identifier-compare consumers are listed and left alone
4. **Criteria skill** (R10). Remove the separate document read in `spectra-criteria` Step 2b.

## Technical Context

**Language/Version**: C# 12, .NET 8 (`net8.0`).
- `global.json` pins SDK 9.0.314, which is **not installed** on this machine (6.0.428 and 10.0.102 are).
- Before building, install 9.0.314 or run with `DOTNET_ROLL_FORWARD`-compatible SDK selection. Treat this as a local
  environment prerequisite, not a code change.

**Primary Dependencies**: YamlDotNet 16.3.0 (the low-level `Parser` for raw spans, and the `Emitter` for canonical
scalars), Markdig (frontmatter block), System.CommandLine, Spectre.Console. No new dependencies.

**Storage**: Files only: `test-cases/{suite}/*.md`, `_index.json`, `.spectra-result.json`, and `.gitignore` (init).

**Testing**: xUnit.
- The baseline to keep green is Core ~568, CLI ~1276 and Execution 228 tests.
- New tests:
  - the writer round-trip property over every field
  - layout invariants
  - resolver units
  - an UpdateHandler orphan-marking test
  - `ai update` flag-matrix tests
  - the default-profile ref regression
  - skill-content assertions

**Target Platform**: Cross-platform CLI (Windows, Linux, macOS). Newline and path-comparer behaviour is specified per
platform.

**Project Type**: CLI and shared library (`Spectra.CLI` plus `Spectra.Core`).

**Performance Goals**: No regression. The resolver does one `File.Exists` per candidate. The writer does one extra
YamlDotNet event pass per parse.

**Constraints**:
- Model-free and deterministic (III).
- Deterministic exit codes, with the exit-code set unchanged (IV).
- No shims or abstractions beyond the resolver (V).

**Scale/Scope**:
- About 12 source files touched: TestCase, TestCaseFrontmatter, GroundingMetadata/Frontmatter, MarkdownFrontmatterParser,
  TestCaseParser, TestFileWriter, UpdatedTestIngestor, GroundingWriteBackService, ReviewFlaggedHandler, UpdateHandler,
  UpdateCommand, UpdateResult, TestClassifier, the three compile commands and InitHandler.
- 2 new source files: `TestCaseLayout`, `SourceRefResolver`.
- 4 skill files.
- 5 docs files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. GitHub as Source of Truth | Test files remain the files of record. The fix stops them being corrupted, and Git stays the recovery path for past losses. | ✅ |
| II. Deterministic Execution | The execution engine is untouched. | ✅ N/A |
| III. Orchestrator-Agnostic / model-free | Resolver, parser and writer are pure and deterministic. Skill edits remove a redundant model read. There is no in-process inference. | ✅ |
| IV. CLI-First | `--dry-run` and `--no-review` are the explicit automation flags the principle requires, and `--no-review` is now **required** to apply without review. The exit-code set is unchanged, and classify-only and apply give the same code for the same outcome. Writes still happen only through validated handlers. | ✅ (breaking default documented) |
| V. Simplicity | There is one new abstraction, `SourceRefResolver`, which the spec explicitly allows and which has 5 consumers. `TestCaseLayout` is a data record, not an abstraction. Switching to `record` + `with` **removes** four copy helpers. There are no feature flags, and no deprecation or compatibility path for the flag change (per the user's decision). | ✅ |
| Quality gates | `spectra validate` is unaffected. Frontmatter stays valid YAML, and escaping is now stricter. | ✅ |
| Test-required discipline | Parser, writer and resolver get unit tests. The CLI workflow gets flag-matrix integration tests. | ✅ |

**Post-design re-check (after Phase 1)**: no violations. The Complexity Tracking table is empty.

## Project Structure

### Documentation (this feature)

```text
specs/078-lossless-test-writes/
├── spec.md
├── plan.md                        # this file
├── research.md                    # R1–R10, adoption table, out-of-scope observations
├── data-model.md                  # TestCase record, TestCaseLayout, key order, resolver types, UpdateResult.applied
├── quickstart.md
├── contracts/
│   ├── test-file-format.md        # G1–G6 write guarantees, per-writer invariants
│   ├── ai-update-cli.md           # flag matrix, outputs, exit codes, skill call sites
│   └── source-ref-resolution.md   # rules + examples + consumer behaviour
├── checklists/requirements.md
└── tasks.md                       # /speckit.tasks (not created here)
```

### Source Code (repository root)

```text
src/Spectra.Core/
├── Models/
│   ├── TestCase.cs                     # class → sealed record; + Layout
│   ├── TestCaseLayout.cs               # NEW: Baseline, FrontmatterEntries, BodySegments, Newline
│   ├── TestCaseFrontmatter.cs          # + status, orphaned_reason, orphaned_date
│   └── Grounding/GroundingMetadata.cs, GroundingFrontmatter.cs   # + Source/CreatedBy/Note; manual mapping deterministic
├── Parsing/
│   ├── MarkdownFrontmatterParser.cs    # raw frontmatter slice by delimiters; stop stripping inner '---'
│   └── TestCaseParser.cs               # raw entry spans (YamlDotNet Parser), body segments, lifecycle fields, Layout
├── Coverage/
│   └── SourceRefResolver.cs            # NEW (beside SourceRefNormalizer)
└── Update/TestClassifier.cs            # use all resolved refs' distinct contents

src/Spectra.CLI/
├── IO/TestFileWriter.cs                # baseline-diff serialiser, fixed key order, YamlDotNet emitter, newline
├── IO/GroundingWriteBackService.cs     # CopyWithGrounding → `with`
├── Commands/Review/ReviewFlaggedHandler.cs   # CopyWithGrounding → `with`
├── Commands/Update/UpdateHandler.cs    # classifyOnly flow; no early return; resolver; orphan `with`
├── Commands/Update/UpdateCommand.cs    # --diff help text
├── Results/UpdateResult.cs             # + Applied
├── Commands/Ai/*/CompileCriticPromptCommand.cs, CompileRepairPromptCommand.cs, CompileRepairBatchCommand.cs  # resolver
├── Update/UpdatedTestIngestor.cs (ApplyEdit)  # `original with {…}`
├── Commands/Init/InitHandler.cs        # gitignore .spectra-result.json, .spectra-progress.html
└── Skills/Content/Skills/spectra-update.md, spectra-generate.md, spectra-execute.md, spectra-criteria.md

tests/
├── Spectra.Core.Tests/Parsing/TestCaseLayoutTests.cs            # NEW: segment/entry concat invariants, unknown keys/sections
├── Spectra.Core.Tests/Coverage/SourceRefResolverTests.cs        # NEW: contract examples table
├── Spectra.Core.Tests/Update/TestClassifierTests.cs             # + multi-ref, fragment refs
├── Spectra.CLI.Tests/IO/TestFileWriterRoundTripTests.cs         # NEW: every field, G1/G2/G4/G5/G6, escaping cases
├── Spectra.CLI.Tests/IO/WriterPathPreservationTests.cs          # NEW: each writer path keeps all fields
├── Spectra.CLI.Tests/Commands/UpdateHandlerOrphanMarkingTests.cs  # NEW
├── Spectra.CLI.Tests/Commands/UpdateCommandFlagMatrixTests.cs   # NEW: 5 rows, result file, applied flag, tables printed
├── Spectra.CLI.Tests/Commands/UpdateDefaultProfileRefsTests.cs  # NEW: docs/<f>.md#<S> regression
├── Spectra.CLI.Tests/Commands/CriticVerificationCommandsTests.cs  # + ref auto-load / fail-loud via resolver
├── Spectra.CLI.Tests/Skills/SkillsManifestTests.cs              # + --dry-run call sites, criteria no double read
└── TestSupport: TestCaseEquivalence (deep comparer)
```

**Structure Decision**: Use the existing two-project layout. Model and parsing changes go in `Spectra.Core`. The writer,
the commands and the skills go in `Spectra.CLI`. No new projects.

## Implementation order (dependency-driven)

1. **Model**: `TestCase` → record, `TestCaseLayout`, DTO fields, grounding fields. Build, and fix any compile fallout from
   `class` → `record`.
2. **Parser**: raw frontmatter slice and entry spans, body segments, lifecycle fields, deterministic manual grounding.
   Add layout invariant tests.
3. **Writer**: baseline-diff emission, key order, YamlDotNet canonical scalars, duration, newline. Add the round-trip
   property tests.
4. **Writer paths**: replace the copy helpers with `with` (orphan, grounding, review accept, ApplyEdit). Add the
   per-path preservation tests.
5. **Resolver**: add it with unit tests, then adopt it in UpdateHandler/TestClassifier and the three compile commands.
   Add the regression tests.
6. **`ai update` flags**: `classifyOnly` flow, `applied`, hint, help text, init gitignore. Add the flag-matrix tests.
7. **Skills**: the three `--dry-run` sites and criteria Step 2b plus line 11. Add the skill tests.
8. **Docs**: CHANGELOG `[2.5.0]` (Fixed / Changed–Breaking / no-recovery note), CLAUDE.md, cli-reference.md, usage.md,
   and the version bump.
9. Run the full `dotnet test`, then the quickstart.

## Risks

| Risk | Mitigation |
|---|---|
| `class` → `record` changes equality semantics somewhere | A grep found no TestCase keyed collections or reference checks. Run the full suite after step 1, before any other change. |
| Existing tests assert today's exact writer output (e.g. unquoted scalars) | Canonical style keeps today's conventions: plain lists, double-quoted free text. Update any `Contains` assertion that encoded a bug (e.g. culture-dependent score). |
| Raw span extraction misbehaves on exotic YAML (anchors, flow maps across lines) | Spans come from YamlDotNet event marks, not regexes. Layout invariant tests include flow collections, block scalars and comments. |
| Breaking change surprises CI users | CHANGELOG breaking note, the cli-reference example, the classify-only hint line, and `applied: false` in the result. |

## Complexity Tracking

No constitution violations. Nothing to justify.
