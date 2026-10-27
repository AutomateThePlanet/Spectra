# Implementation Plan: Review Fixes for Spec 088 — Update Prompt Context

**Branch**: `089-review-fixes-088` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/089-review-fixes-088/spec.md`

## Summary

Six review fixes to the 088 update seam. No new features, and no change to the baseline, test-file or prompt structure.

- **F1:** `ingest-update --all` reads `.spectra/updates/<suite>/`. `--all --keep-baseline` reads only
  `.spectra/repairs/<suite>/repaired-<id>.json`. The generation skill's repair loop writes there.
- **F2:** an edit holding more than one test object fails with `MULTIPLE_TESTS` before anything is parsed or written.
- **F3:** without `--keep-baseline`, an unchanged edit is classified as triage would classify it. If the bucket needs
  an edit, the outcome is `unchanged_pending` and nothing is recorded. If it doesn't, the outcome is `unchanged` and
  the baseline is rebuilt.
- **F4:** a linked criterion with no baseline value is `NotBaselined` and is labelled so in the prompt.
- **F5:** a batch with a failed file exits 8.
- **F6:** one shared gitignore list gains both staging folders. `init` and `update-skills` both apply it; the second
  was the user's choice for existing workspaces.

## Technical Context

**Language/Version**: C# 12, .NET 8.
**Primary Dependencies**: `IngestUpdateCommand`, `UpdatedTestIngestor`, `GeneratedTestIngestor`, `TriageEngine`
(`TriageTestAsync`), `BaselineContext`, `BaselineStore`, `BaselineRecorder`, `UpdateContextBuilder` and
`UpdatePromptRenderer`, `InitHandler`, `UpdateSkillsHandler`. No new packages.
**Storage**: a new scratch folder, `.spectra/repairs/<suite>/` (gitignored). No format changes.
**Testing**: xUnit 2.9.3, with `BaselineTestWorkspace` and `UpdatePromptWorkspace` (`CriteriaCommands` collection) and
the real `InitHandler` for the init and pre-089 rows. Baseline at `6ea7520`: Core 1219, CLI 1870 (one known failure),
Execution 228, Integration 8. T001 measures it again.
**Target Platform**: Windows and Linux.
**Project Type**: CLI.
**Performance Goals**: F3 builds one `BaselineContext` per run, lazily, so a batch with no unchanged edit pays
nothing.
**Constraints**:
- Model-free; no config section becomes required (083).
- Human summaries are kept in JSON mode, on stderr.
- Every fix needs a regression test that fails on `6ea7520`.
- No build warnings.

**Scale/Scope**:
- 2 new source files: `PendingChangeCheck` and `SpectraGitIgnore`.
- Edits to about 8 files: `IngestUpdateCommand`, `UpdatedTestIngestor`, `GeneratedTestIngestor`, `UpdateContext`,
  `UpdatePromptRenderer`, `InitHandler`, `UpdateSkillsHandler`, and both skills.
- 4 docs and the repository `.gitignore`.
- About 5 new test classes, plus 11 deliberately changed assertions (research R8).

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Only gitignored scratch folders are added; edited tests and baselines stay committed files | ✅ |
| II. Deterministic execution | The engine is untouched. Prompts stay byte-identical (F4 adds one label). The pending check reuses triage, so ingest and triage can't disagree | ✅ |
| III. Orchestrator-agnostic, minimal output | Additive JSON fields (`outcome`, `unchanged_pending`, `bucket`, `why`); model-free | ✅ |
| IV. CLI-first, deterministic exit codes | Exit 8 makes a partly failed batch visible to scripts and CI. Exit 1 is kept for whole-command errors and exit 5 for content errors | ✅ |
| V. Simplicity | No new storage, config or flag. The folder follows an existing flag, and the gitignore list is shared instead of duplicated | ✅ |
| Test-required discipline | T1–T7 fail on `6ea7520`. Real-init and pre-089 workspace rows are included (083 lesson) | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/089-review-fixes-088/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.CLI/
├── Baseline/PendingChangeCheck.cs              # NEW: ClassifyAsync → PendingChange(Bucket, Why) | null; NeedsEdit
├── Commands/
│   ├── Generate/IngestUpdateCommand.cs         # folder per mode; unchanged_pending; exit 8; outcome in single JSON
│   ├── Init/SpectraGitIgnore.cs                # NEW: Patterns (+ .spectra/updates/, .spectra/repairs/), EnsureAsync
│   ├── Init/InitHandler.cs                     # uses SpectraGitIgnore
│   └── UpdateSkills/UpdateSkillsHandler.cs     # applies SpectraGitIgnore; reports .gitignore under "Updated:"
├── Generation/
│   ├── GeneratedTestIngestor.cs                # + ArrayObjects(content) → ids of the array's objects | null
│   ├── UpdatedTestIngestor.cs                  # + MULTIPLE_TESTS check before parsing
│   ├── UpdateContext.cs                        # LinkedCriterionState.NotBaselined; precedence across tests
│   └── UpdatePromptRenderer.cs                 # "(not baselined — compare the test with the current text)"
└── Skills/Content/Skills/
    ├── spectra-update.md                       # Step 5: exit 8, unchanged_pending question
    └── spectra-generate.md                     # 8b: repairs folder, exit 8 retry, table row, NON-STOP sentence

.gitignore                                       # + .spectra/updates/

tests/Spectra.CLI.Tests/
├── Commands/Generate/StagingFolderSplitTests.cs     # T1 (incl. real init, back to back)
├── Commands/Generate/MultipleTestsTests.cs           # T2
├── Commands/Generate/UnchangedPendingTests.cs        # T3 (one row per bucket class; triage dry run after)
├── Generation/UpdateContextBuilderTests.cs           # T4 rows added
├── Commands/Generate/IngestUpdateExitCodeTests.cs    # T5
├── Commands/Init/StagingGitIgnoreTests.cs            # T6 (real init; update-skills on a pre-089 workspace)
└── Skills/SkillsManifestTests.cs                     # T7
```

## Implementation Order

1. **T001:** measure the baseline. Write T1–T6 first and confirm each fails on `6ea7520`, which is the code before any
   source edit.
2. **F2** (`ArrayObjects`, `MULTIPLE_TESTS`), then **F1** (folder per mode), both in the ingest path.
3. **F3:** `PendingChangeCheck`, the `unchanged_pending` outcome, single and batch.
4. **F5:** exit 8. Update the R8 assertions.
5. **F4:** the criterion state and label.
6. **F6:** `SpectraGitIgnore` in `init` and `update-skills`, and the repository `.gitignore`.
7. **Skills** (T7).
8. **Docs:** cli-reference (exit 8 next to 5 and 6, the folders, the outcomes, `MULTIPLE_TESTS`), usage, CHANGELOG
   (including the pre-089 leftovers) and CLAUDE.md.
9. **Finish:** a full build (no warnings) and test run, the quickstart and the Notes. Ask before committing and
   merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Existing repair leftovers in `.spectra/updates/` get ingested by the next update batch | Documented in the CHANGELOG with how to find and delete them. Detection isn't possible (R1) |
| The pending check disagrees with triage | It calls `TriageEngine.TriageTestAsync` with the same threshold expression; T3 asserts the next triage dry run |
| Exit 8 is read as a stop by the generation skill's NON-STOP rule | An explicit sentence in the restated NON-STOP paragraph, and a T7 assertion; the `written == 0` guard is untouched |
| `update-skills` writes `.gitignore` in a workspace without one | The same behaviour as init, which already creates it; reported under "Updated:" |
| Other tests depend on exit 0 or on the shared folder | T001's full run and research R8; each change is listed in tasks.md |

## Complexity Tracking

No constitution violations.
