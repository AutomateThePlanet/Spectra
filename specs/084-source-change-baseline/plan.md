# Implementation Plan: Source Change Baseline — Deterministic Change Detection Without Git (Phase 2a)

**Branch**: `084-source-change-baseline` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/084-source-change-baseline/spec.md`

## Summary

Record, per test, a fingerprint of every doc section it references and of every criterion it links:
- in a committed per-suite `_source-baseline.json`;
- plus content-addressed normalised snapshots in `.spectra/source-snapshots/`.

Three new model-free commands work on it:
- `update-triage` compares the baseline with current content. It classifies each ref (UNCHANGED, COSMETIC,
  LIGHT, LOAD, REMOVED) and each criterion with deterministic rules and a bounded word diff, buckets each test by its
  worst state, groups tests that share the same changes, auto-acks cosmetic changes, and emits a compact summary plus
  a detail file;
- `update-ack` refreshes reviewed tests;
- `source-baseline --init` bootstraps existing workspaces.

`ingest-tests` and `ingest-update` record entries. The `spectra-update` skill drives triage instead of the legacy
word-overlap classifier, which stays as it is. No git, no model.

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318
**Primary Dependencies**:
- `System.Security.Cryptography.SHA256`;
- `System.Text.Json` (an explicit `Utf8JsonWriter` for deterministic output);
- the existing `SourceRefResolver`, `TextNormalizer`, `CriteriaStore.ScanFilesAsync`, `FileLockHandle`,
  `CriteriaCommandIO` and `TestCaseParser`.
No new packages.
**Storage**: Files.
- `test-cases/{suite}/_source-baseline.json` (committed);
- `.spectra/source-snapshots/{hex}.txt` (committed);
- `.spectra/update-triage.json` (gitignored scratch);
- `.spectra/baseline.lock` (gitignored).
**Testing**: xUnit. The baseline at `63531e3` is Core 839 / CLI 1592 (1 known failure: `ProfileFormatLoaderTests`) /
Execution 228 / Integration 8; re-measured in T001. CLI command tests run in the non-parallel `CriteriaCommands`
collection.
**Target Platform**: Cross-platform CLI (Windows dev, Linux CI). Fingerprints are line-ending- and BOM-independent.
**Project Type**: CLI and shared library.
**Performance Goals**: `update-triage --all-suites` under 30 s at 2,500 tests; stdout ≤ about 8,000 chars at about
10% affected.
**Constraints**:
- Model-free and deterministic: byte-identical outputs, no timestamps or absolute paths.
- Nothing is written on `--dry-run`, not even the lock file.
- The 083 config rule: no required section; `update` is filled silently.
- Human summaries are never suppressed; one failure document on stderr.
- `ai update` and `TestClassifier` are unchanged.
**Scale/Scope**:
- about 14 new source files (8 Core, 6 CLI) and about 6 edited (config, loader, init, two ingest commands,
  `AiCommand`);
- 4 skills;
- 4 docs;
- about 12 new test classes plus edits to `SkillsManifestTests`, `ConfigRuleMatrixTests`, `ConfigLoaderEntryPointTests`
  and the init tests.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | The baseline and snapshots are committed files next to the tests. The lock and triage scratch are gitignored. Git stays the undo path; no external store. | ✅ |
| II. Deterministic execution | The execution engine is untouched. Triage is a pure function of the files: same inputs give byte-identical outputs (T9). | ✅ |
| III. Orchestrator-agnostic, minimal output | Model-free. The compact stdout JSON (≤ about 2K tokens) goes to the agent, the detail goes to a file, and every command re-derives state from disk. | ✅ |
| IV. CLI-first, deterministic exit codes, `--dry-run` | Three named commands, exit codes 0/1/4 with named error codes, `--dry-run` on every writer. | ✅ |
| V. Simplicity | No new dependency (no Markdown library; one bounded LCS). `CriteriaCommandIO`, `FileLockHandle`, the resolver and the normaliser are reused. Classification rules are static tables. New types sit in two folders, Core `Baseline/` (pure) and CLI `Baseline/` (I/O). | ✅ |
| Test-required discipline | T1–T9 as tables mirroring the spec rules, plus the perf test, plus real-template and pre-existing-workspace guards. | ✅ |

**Post-design re-check**: no violations. A Markdown-parser dependency was considered and rejected (research R1).

## Project Structure

### Documentation

```text
specs/084-source-change-baseline/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Baseline/                          # NEW — pure, model-free
│   ├── SectionExtractor.cs            # headings outside fences, section bounds, AnchorOf
│   ├── AnchorKey.cs                   # lowercase letters+digits key, percent-decoding
│   ├── SourceFingerprint.cs           # sha256 over LF/BOM-normalised raw text; criterion fingerprint
│   ├── WordDiff.cs                    # tokenise, 5000 cap, prefix/suffix trim + bounded LCS
│   ├── ChangeClassifier.cs            # ref/criterion states, LOAD features, why strings
│   ├── BucketRules.cs                 # precedence
│   ├── TriageGrouper.cs               # group key, chunking, G-ids
│   └── SourceBaseline.cs              # model + SourceBaselineSerializer (deterministic writer, strict reader)
├── Models/Config/UpdateConfig.cs      # + group_size, light_threshold_words (int?, effective defaults)
└── Config/ConfigLoader.cs             # CheckedSections += update; Validate range errors

src/Spectra.CLI/
├── Baseline/                          # NEW — I/O
│   ├── BaselineStore.cs               # lock, strict load, prune+atomic save, snapshots
│   ├── BaselineContext.cs             # per-run caches (files, sections, criteria, snapshots), notes
│   ├── BaselineRecorder.cs            # entry for a test from current content
│   ├── TriageEngine.cs                # verdicts, auto-ack, groups, no_tests, documents
│   └── TriagePresenter.cs             # human summary table
├── Commands/Baseline/                 # NEW
│   ├── UpdateTriageCommand.cs  UpdateTriageHandler.cs
│   ├── UpdateAckCommand.cs     UpdateAckHandler.cs
│   └── SourceBaselineCommand.cs SourceBaselineHandler.cs
├── Commands/Ai/AiCommand.cs           # register the three commands
├── Commands/Generate/IngestTestsCommand.cs, IngestUpdateCommand.cs   # lock → strict load → persist → record
├── Commands/Init/InitHandler.cs       # gitignore += baseline.lock, update-triage.json
└── Skills/Content/Skills/spectra-update.md (rewrite), spectra-generate.md, spectra-execute.md, spectra-help.md

tests/Spectra.Core.Tests/Baseline/     # NEW: SectionExtractorTests (T1), AnchorKeyTests (T2),
                                       #      SourceFingerprintTests, WordDiffTests, ChangeClassifierTests (T3),
                                       #      BucketAndGroupingTests (T4), SourceBaselineSerializerTests (T9 part)
tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs   # update null / range rows
tests/Spectra.CLI.Tests/TestFixtures/BaselineTestWorkspace.cs    # NEW fixture
tests/Spectra.CLI.Tests/Commands/Baseline/                        # NEW: BaselineWritersTests (T5),
                                       #      BaselineInitTemplateTests (T6, T7), BaselineFileGuardTests (T8),
                                       #      BaselineDeterminismTests (T9), UpdateTriageErrorTests,
                                       #      UpdateTriagePerformanceTests (SC-003/004)
tests/Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs        # + update rows
tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs             # gitignore patterns
tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs            # skill assertions (R12)
```

**Structure decision.** Pure rules live in `Spectra.Core/Baseline/`, where they are unit-tested with tables (T1–T4).
I/O and orchestration live in `Spectra.CLI/Baseline/`, following the `Spectra.CLI/Criteria/` precedent. Commands
live in `Commands/Baseline/`.

## Implementation Order

1. **Baseline measurement** (T001).
2. **Core rules with their tables first**:
   - `AnchorKey`, `SectionExtractor` (T1, T2);
   - `SourceFingerprint`, `WordDiff`, `ChangeClassifier` (T3);
   - `BucketRules`, `TriageGrouper` (T4);
   - `SourceBaseline` serializer.
3. **Config**:
   - the `UpdateConfig` keys;
   - the loader filling `update`, plus the range validation;
   - `ConfigRuleMatrixTests` and `ConfigLoaderEntryPointTests` rows.
4. **CLI store**: `BaselineStore`, `BaselineContext`, `BaselineRecorder`.
5. **Writers**:
   - `source-baseline --init`, then `ingest-tests` / `ingest-update` recording;
   - `update-ack --ids`;
   - the T5 tests.
6. **Triage**:
   - `TriageEngine` (verdicts, auto-ack, groups, `no_tests`, detail file), `TriagePresenter`, `update-triage`;
   - then `update-ack --group` with the stale check.
7. **Guards**:
   - T6 and T7 (the real `InitHandler` template; a pre-existing workspace);
   - T8 (the enumerating commands with and without the file; rename and delete);
   - T9 (determinism);
   - the perf test.
8. **Init gitignore and skills**:
   - `spectra-update` rewrite, delegation rows, `spectra-help`;
   - `SkillsManifestTests`.
9. **Docs**:
   - `CHANGELOG.md` (`[2.6.0]` unreleased, Spec 084);
   - `CLAUDE.md` "Recent Changes";
   - `docs/cli-reference.md` (three commands, states, storage, config keys, error codes, `ai update` legacy);
   - `docs/usage.md` (the update workflow).
10. **Finish**: full `dotnet test`, then the quickstart with the built CLI. Commit and merge into
    `fix/doc-change-update-workflow` with `--no-ff` on approval; never push.

## Risks

| Risk | Mitigation |
|---|---|
| CRLF/LF checkouts make every ref COSMETIC, and auto-ack churns the committed baseline | Fingerprints normalise line endings and the BOM (R2). The T3 row checks that a CRLF-only change is UNCHANGED. |
| `.NET 8` `Utf8JsonWriter` writes CRLF on Windows, so baselines would differ by OS | Normalise to LF after writing. T9 asserts there are no `\r` bytes. |
| The LCS on two 5,000-word sections uses too much memory | Trim the common prefix and suffix, use a 4M-cell budget with a conservative fallback, and the 5,000-word cap (R3). A `WordDiffTests` row covers the budget fallback. |
| Ingest succeeds but the baseline save fails, so the test and baseline disagree | The lock and strict load happen **before** persisting. A save failure after persisting is a named error with the recovery command; triage then shows the test as UNBASELINED, never as changed. |
| A stale `update-ack --group` swallows a change triage never showed | The detail file records current fingerprints; a mismatch → `TRIAGE_STALE` (R8). |
| A future command enumerates `*.json` in suite folders | T8 runs every enumerating command with and without the baseline file. |
| Skill tests assert the old flow (`.spectra-result.json`, `spectra ai update` with `--dry-run`) | The assertions are adjusted deliberately (R12), and new assertions pin the triage flow. |
| The stdout budget is exceeded on wide changes | Unbaselined IDs and diffs live only in the detail file. The perf test asserts ≤ 8,000 chars on the reference workspace. |

## Complexity Tracking

No constitution violations.
