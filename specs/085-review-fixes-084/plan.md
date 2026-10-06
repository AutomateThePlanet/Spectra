# Implementation Plan: Review Fixes for Spec 084 — Source Change Baseline

**Branch**: `085-review-fixes-084` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/085-review-fixes-084/spec.md`

## Summary

Six review fixes to the 084 source-change baseline, with no new features and no format change:

- **F1:** one heading matcher (text key after markup removal → explicit ID → GitHub `-n` duplicate) for extraction
  and `no_tests`, with cleaned heading text.
- **F2:** pruning happens only when every test file in the suite parsed. It is decided in the store from its own
  scan.
- **F3:** full rebuilds keep the previous fingerprint of refs and criteria the test still lists but that are gone.
- **F4:** the extended meaning-flip word list, with apostrophe folding for the check.
- **F5:** `ingest-update --keep-baseline` for critic repairs, passed by the two repair skills.
- **F6:** UTF-8 writers for redirected streams instead of setting the console encoding.

Every fix has a test that fails on `2a43298`.

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318
**Primary Dependencies**: The existing `TextNormalizer`, `SectionExtractor`, `BaselineStore`, `BaselineRecorder`,
`TestCaseParser` and System.CommandLine. No new packages.
**Storage**: Files. The baseline, snapshot and detail formats are unchanged.
**Testing**: xUnit. The baseline at `2a43298` is Core 1025 / CLI 1699 (1 known failure: `ProfileFormatLoaderTests`) /
Execution 228 / Integration 8; re-measured in T001. CLI tests run in the non-parallel `CriteriaCommands` collection.
**Target Platform**: Cross-platform CLI. Windows console behaviour matters for F6.
**Project Type**: CLI and shared library.
**Performance Goals**: No regression. The 084 perf test (2,500 tests, under 30 s, stdout ≤ 8,000 chars) stays
green. F2 adds one suite scan per ingest save.
**Constraints**:
- No baseline migration; every fingerprint 084 recorded for plain, emphasised and inline-code headings stays valid.
- Model-free and deterministic; nothing is written on `--dry-run`.
- The failure shape and exit codes are unchanged.
**Scale/Scope**:
- about 8 source files edited and 2 new types (`SuiteScan`, `ConsoleEncoding`);
- 3 skills;
- 4 docs;
- about 6 test classes edited or added.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Files are unchanged; F2 and F3 make the committed baseline harder to lose silently. | ✅ |
| II. Deterministic execution | The execution engine is untouched. Matching and classification stay pure and ordered. | ✅ N/A |
| III. Minimal structured output | One `mode` field and one `warnings` array are added to the ingest JSON. | ✅ |
| IV. CLI-first, deterministic exit codes | One new flag, no new exit codes. | ✅ |
| V. Simplicity | Two duplicate key comparisons become one matcher, five prune decisions become one, and no new dependency is added. | ✅ |
| Test-required discipline | Every fix has a test that fails before; T2 and T9 guard compatibility. | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/085-review-fixes-084/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-behaviour-changes.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Text/TextNormalizer.cs          # + Clean (Normalize without lowercasing)
└── Baseline/
    ├── SectionExtractor.cs         # MarkdownHeading CleanText/TextKey/ExplicitIds; Match (3 rules); Extract + CoveredHeadings use it
    └── ChangeClassifier.cs         # extended LOAD word list; apostrophe folding for membership

src/Spectra.CLI/
├── Baseline/BaselineStore.cs       # ScanSuiteAsync + SuiteScan; SaveSuiteAsync prunes only when all parsed, else warns
├── Baseline/BaselineRecorder.cs    # Replace keeps dead fingerprints; RecordMode; RecordPersistedAsync scans after persist
├── Baseline/BaselineCommandIO.cs   # LoadSuiteTestsAsync → ScanSuiteAsync
├── Baseline/TriageEngine.cs        # pass scans; no_tests uses CleanText
├── Commands/Baseline/*Handler.cs   # pass scans + warnings to SaveSuiteAsync
├── Commands/Generate/IngestTestsCommand.cs, IngestUpdateCommand.cs   # --keep-baseline (update); warnings in success JSON
├── Infrastructure/ConsoleEncoding.cs   # NEW: UTF-8 writers for redirected streams
├── Program.cs                      # call ConsoleEncoding; no Console.OutputEncoding
└── Skills/Content/Skills/spectra-generate.md, spectra-review-flagged.md   # --keep-baseline on repair ingests

tests/
├── Spectra.Core.Tests/Baseline/SectionExtractorTests.cs   # + T1 matching table, T2 guarantee, T3 agreement
├── Spectra.Core.Tests/Baseline/ChangeClassifierTests.cs   # + T6 rows
├── Spectra.Core.Tests/Text/TextNormalizerTests.cs         # Clean == Normalize without lowercasing
├── Spectra.CLI.Tests/Commands/Baseline/BaselineReviewFixTests.cs   # NEW: T4, T5, T7, T9 (+ no_tests cleaned text)
├── Spectra.CLI.Tests/Commands/Baseline/Utf8StdoutTests.cs          # + stderr; source guard (T8)
└── Spectra.CLI.Tests/Skills/SkillsManifestTests.cs                  # --keep-baseline assertions
```

## Implementation Order

1. **Baseline measurement** (T001).
2. **Tests first:** T1/T3 tables, T6 rows, T4/T5/T7 command tests, the T8 source guard and the skill assertions.
   Confirm they fail on `2a43298` in a detached worktree.
3. **F1:** `TextNormalizer.Clean`, the heading properties, `Match`, then `Extract` / `CoveredHeadings` / `no_tests`.
   Then T2 and T9.
4. **F4:** word list and folding.
5. **F2:** `ScanSuiteAsync` / `SaveSuiteAsync`, then the five callers and the ingest warnings.
6. **F3:** `Replace` with the previous entry.
7. **F5:** `RecordMode`, `--keep-baseline`, then the skills.
8. **F6:** `ConsoleEncoding`, `Program.Main`, and the stderr case.
9. **Docs:**
   - `CHANGELOG.md` (085 subsection under `[2.6.0]`);
   - `CLAUDE.md` "Recent Changes";
   - `docs/cli-reference.md` (anchor rules, `--keep-baseline`, pruning, LOAD words, encoding);
   - `docs/usage.md` (repairs keep the baseline; unparseable files).
10. **Finish:** full `dotnet test`, the fails-before worktree run and the quickstart. Then commit and merge on
    approval; never push.

## Risks

| Risk | Mitigation |
|---|---|
| F1 changes which section an existing ref resolves to, breaking fingerprints | T2 compares against a copy of the 084 rule on plain, emphasised and inline-code headings. The residuals (entities, HTML inside inline code) are documented. T9 compares a whole workspace triage with 084's |
| `Clean` drifts from `Normalize` | `Normalize` is implemented as `Clean(...).ToLowerInvariant()`, and a test asserts the identity over the existing normaliser table |
| The F2 rescan slows ingest | It is one suite scan per ingest, and the perf test still runs |
| Replacing the console writers reorders interleaved stdout/stderr | `AutoFlush = true` on both; only redirected streams are replaced |
| A source guard is brittle | It checks for one specific assignment, with a message explaining why the line must not return |

## Complexity Tracking

No constitution violations.
