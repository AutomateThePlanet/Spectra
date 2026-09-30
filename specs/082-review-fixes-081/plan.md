# Implementation Plan: Review Fixes for Spec 081

**Branch**: `082-review-fixes-081` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/082-review-fixes-081/spec.md`

## Summary

This plan fixes four defects in 081's new behaviour (merged as `7df3fef`). No features are added. Every item gets a
regression test that fails on `7df3fef`.

| Item | Fix |
|---|---|
| A1 | Criteria write commands require `coverage` (the section they write to); `tests` only warns (R1). |
| A2 | A known tag is markup only in HTML form: lowercase, closing, with attributes, or opened then closed. Capitalised bare placeholders stay text (R2). |
| A3 | A `null` section is a missing section (R3). |
| A4 | Named config error codes in every JSON failure document; the skill names them (R4). |

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318
**Primary Dependencies**: System.Text.Json (`JsonNode` pre-scan from 081), `GeneratedRegex`. No new packages.
**Storage**: Files (`spectra.config.json`, criteria YAML).
**Testing**: xUnit. Baseline at `7df3fef` is expected to match 081's final run: Core 785 / CLI 1535 (1 known failure:
`ProfileFormatLoaderTests`) / Execution 228 / Integration 8. Re-measure in T001.
**Target Platform**: Cross-platform CLI; CI on ubuntu.
**Project Type**: CLI and shared library.
**Performance Goals**: No change.
**Constraints**: Model-free and deterministic. Exit codes and human-mode wording unchanged. `SpectraConfig` untouched.
**Scale/Scope**: About 14 edited source files, 1 skill, 3 docs, and about 5 test classes extended.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | A1 stops criteria landing in an unconfigured location. A2 stops real doc changes being hidden. | ✅ |
| II. Deterministic execution | Engine untouched. | ✅ N/A |
| III. Minimal structured output | A4 adds named codes to the existing single failure document. | ✅ |
| IV. CLI-first, deterministic exit codes | Exit codes unchanged. Config errors stay exit 1, now with codes. | ✅ |
| V. Simplicity | One loader rule (requires/reads) and one pre-scan. No new types beyond one flag value and one result field. | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/082-review-fixes-081/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-behaviour-changes.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Config/ConfigLoader.cs            # ConfigSections.Coverage; requires/reads; null = missing; ErrorCode (A1, A3, A4)
└── Text/TextNormalizer.cs            # HTML-form tag rule (A2)

src/Spectra.CLI/
├── Criteria/CriteriaCommandIO.cs     # LoadConfigAsync(requires, reads) → config, error, code, warnings (A1, A4)
├── Commands/Generate/IngestCriteriaCommand.cs        # requires Coverage, reads Tests; warnings; code (A1, A4)
├── Commands/Docs/DocsRetireCommand.cs                # requires Coverage, reads Tests; warnings; code (A1, A4)
├── Commands/Docs/DocsApplyMoveCommand.cs, DocsMigrateCriteriaCommand.cs   # requires Coverage; code (A1, A4)
├── Commands/Analyze/AnalyzeHandler.cs                # import requires Coverage; ErrorResult.ErrorCode (A1, A4)
├── Results/CommandResult.cs                          # ErrorResult.error_code (A4)
├── Commands/Generate/CompileExtractionPromptCommand.cs  # reads Coverage; config warnings; code (A1, A4)
├── Commands/Generate/CompileCriticPromptCommand.cs   # reads Source|Tests|Coverage; JSON config failure (A1, A4)
├── Commands/Generate/CompileRepairPromptCommand.cs, CompileRepairBatchCommand.cs   # unchanged reads (text only)
├── Commands/Docs/DocsChangedHandler.cs               # reads Source|Coverage; JSON config failure (A1, A4)
└── Skills/Content/Skills/spectra-criteria.md         # named config codes (A4)
```

**Structure decision**: No new files. The fixes stay in 081's loader and the files that call it.

## Implementation Order

1. **Tests first**, per item. Each must fail on `7df3fef`.
2. **A3 + A1 in the loader:**
   - null counts as missing;
   - `Coverage` flag;
   - `requires`/`reads` for write loads.

   Then the command declarations and warning output. Replace the two 081 tests that encoded the `tests`-on-write rule.
3. **A4:**
   - `ErrorCode` on `ConfigLoadResult`;
   - plumbing through `CriteriaCommandIO` and every caller;
   - `ErrorResult.error_code`;
   - JSON config failures for the critic and `docs changed`;
   - the skill.
4. **A2:** the regex rewrite. Keep every 080 and 081 row green.
5. **Docs:**
   - `CHANGELOG.md`: a Spec 082 subsection under the unreleased `[2.6.0]`;
   - `CLAUDE.md` "Recent Changes";
   - `docs/cli-reference.md`: the config table, the codes, the placeholder rule.
6. Full `dotnet test`. Fails-before check in a worktree at `7df3fef`. Quickstart spot checks. Then commit and merge per
   the local workflow when the user approves.

## Risks

| Risk | Mitigation |
|---|---|
| Configs without `coverage` start failing on writes | This is the intended A1 behaviour. Documented in the CHANGELOG. `spectra init` has always written `coverage`. |
| The A2 matching-closing-tag lookahead is slow on long texts | Criterion texts are short. The lookahead is lazy and bounded by the text. |
| The A2 rule changes some 081 rows (e.g. `<INPUT type=checkbox>`) | That row has an attribute, so it is still markup. Every 080 and 081 row is kept as a regression guard. |
| The critic's and `docs changed`'s new JSON config-failure output surprises a consumer | JSON mode only; human mode unchanged; covered by the contract and the CHANGELOG. |

## Complexity Tracking

No constitution violations.
