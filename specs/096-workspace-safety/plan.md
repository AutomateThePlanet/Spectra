# Implementation Plan: Workspace Safety and Hygiene (Phase 6, Part 1)

**Branch**: `096-workspace-safety` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/096-workspace-safety/spec.md`

## Summary

Nine fixes, no features:
- `update-skills` never overwrites an edited file (one manifest key form; no record → never overwrite);
- `suite delete` uses 090's deletion rules and removes the suite's leftovers;
- `show` finds standard tests and `validate` fails on parse errors;
- the gitignore offer gains two entries and is recorded in `.gitignore` itself (per repository);
- `delete` stops fully parsing every test;
- shell scripts get LF via `.gitattributes`;
- the test suite becomes fully green and deterministic;
- the unused `ai.generation_batch_size` leaves the init template.

## Technical Context

**Language/Version**: C# 12, .NET 8; `.gitattributes`.

**Primary Dependencies**:
- `SkillsManifestStore`, `InitHandler`, `UpdateSkillsHandler`, `PromptsResetHandler`, `CompileBatchPrompt`;
- `SuiteDeleteHandler`, `TestDeletionService`, `TestFileWriter`;
- `ShowHandler`, `ValidateHandler`;
- `SpectraGitIgnore`;
- `AiConfig`, the init config template.

No new packages.

**Storage**:
- the manifest keys are normalised;
- the `.gitignore` marker line is new;
- no other format changes.

**Testing**:
- xUnit 2.9.3;
- real-init `BaselineTestWorkspace` rows in `CriteriaCommands`;
- `[GitFact]` for `.gitattributes`;
- a performance row.

Baseline `23aa08c`: Core 1245 / CLI 2194 (1 known failure, fixed by F8) / Execution 228 / Integration 8; 4 warnings.

**Target Platform**: Windows and Linux.

**Project Type**: CLI.

**Performance Goals**: delete one of 2,500 tests ≥ 3× faster than `23aa08c` (measured).

**Constraints**:
- no required config section;
- `--dry-run` writes nothing;
- human summaries on stderr in JSON mode;
- every fix fails on `23aa08c`;
- verified on real init and pre-096 workspaces.

**Scale/Scope**:
- 1 new helper (`ManifestKey`) and 1 new test fixture (collection);
- ~10 edited source files;
- `.gitattributes`;
- 4 docs;
- ~8 test classes.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | The offered record moves into the committed `.gitignore`; `.gitattributes` added | ✅ |
| II. Deterministic | Manifest normalisation and the marker rule are deterministic; delete results unchanged | ✅ |
| III. Minimal output | Small additions to `suite delete` JSON; skip messages | ✅ |
| IV. CLI-first; `--dry-run` | `suite delete --dry-run` now also lists leftovers and writes nothing; `delete` keeps its dry run | ✅ (094 `ingest-analysis` gap still tracked) |
| V. Simplicity | Reuses the 090 deletion service, the 078 writer and the 090 gitignore rules; removes a regex path | ✅ |
| Test-required discipline | Every fix has a row failing on `23aa08c`; the suite becomes fully green | ✅ |

**Post-design re-check**: no violations. Deliberate changes:
- `update-skills` skips unrecorded differing files (it used to overwrite them);
- `suite delete` no longer edits list `depends_on` (warned instead);
- `validate` exit code on parse errors.

## Project Structure

```text
src/Spectra.CLI/Skills/ManifestKey.cs                      # NEW (R1)
src/Spectra.CLI/Skills/SkillsManifest.cs                   # load-time normalisation, warnings
src/Spectra.CLI/Commands/Init/InitHandler.cs               # normalised keys; gitignore marker; no GitIgnoreOffered write
src/Spectra.CLI/Commands/Init/SpectraGitIgnore.cs          # two patterns; markers; migration
src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs   # no-record rule; skip messages; key helper
src/Spectra.CLI/Commands/Prompts/PromptsResetHandler.cs    # key helper
src/Spectra.CLI/Commands/Generate/CompileBatchPrompt.cs    # key helper
src/Spectra.CLI/Commands/Suite/SuiteDeleteHandler.cs       # deletion service; leftovers; regex removed
src/Spectra.CLI/Commands/Delete/TestDeletionService.cs     # frontmatter catalog, lazy parse, index lookup; suite leftovers
src/Spectra.CLI/Results/SuiteDeleteResult (wherever defined)   # new fields
src/Spectra.CLI/Commands/Show/ShowHandler.cs               # suite-dir path
src/Spectra.CLI/Commands/Validate/ValidateHandler.cs       # failed when totalErrors > 0
src/Spectra.Core/Models/Config/AiConfig.cs                 # [Obsolete]
src/Spectra.CLI/Templates/spectra.config.json              # key removed
.gitattributes                                             # NEW

tests/Spectra.CLI.Tests/TestFixtures/WorkingDirectoryCollection.cs      # NEW (F8)
tests/Spectra.CLI.Tests/Skills/UpdateSkillsKeepsEditsTests.cs           # F1
tests/Spectra.CLI.Tests/Commands/Suite/SuiteDeleteSafetyTests.cs        # F2
tests/Spectra.CLI.Tests/Commands/ShowValidateTruthTests.cs              # F3, F4
tests/Spectra.CLI.Tests/Commands/Init/GitIgnoreMarkerTests.cs           # F5
tests/Spectra.CLI.Tests/Commands/Delete/DeletePerformanceTests.cs       # F6
tests/Spectra.CLI.Tests/Infrastructure/RepositoryAttributesTests.cs     # F7
tests/Spectra.CLI.Tests/Profile/ProfileFormatLoaderTests.cs             # F8 fix
tests/Spectra.CLI.Tests/Config/UnusedBatchSizeKeyTests.cs               # F9
```

## Implementation Order

1. Baseline on `23aa08c`. Measure the delete time (2,500 tests). Write the guard rows; proof on the untouched `src`.
2. F8 (collection, usage-guide test), so the suite is deterministic for everything after.
3. F1 (ManifestKey, manifest normalisation, update-skills rule, writers and readers).
4. F5 (patterns, markers, migration), sharing the update-skills edit.
5. F2 (suite delete via the service, leftovers) and F6 (service load).
6. F3, F4.
7. F7, F9.
8. Docs: cli-reference, usage, configuration, CHANGELOG, CLAUDE.md.
9. Full run, built-CLI quickstart checks on real init and pre-096, Notes. Ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Existing tests assert absolute manifest keys or exact `.gitignore` bytes | Inventory task; retarget deliberately with Notes |
| Making `WorkingDirectory` sequential slows the run | Measured; correctness over speed (the flakes cost reruns) |
| A frontmatter-only read misses something the full parse found | Same results asserted by the existing `SafeDeleteTests` and a parity row on a mixed workspace |
| Marker lines confuse users | One line per offer; documented; deleting it re-enables the offer |

## Complexity Tracking

No new violations.
