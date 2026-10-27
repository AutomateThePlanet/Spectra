# Implementation Plan: Re-verify After Update (Phase 4)

**Branch**: `090-reverify-after-update` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/090-reverify-after-update/spec.md`

## Summary

After an update edit the test is re-checked by the critic, and a verdict can only land on the content it judged.

- **A — Verdicts.**
  - A written edit drops the test's critic grounding block and verdict file. Manual verdicts are kept.
  - `compile-critic-prompt` records a content fingerprint per test. `ingest-verdict` / `ingest-grounding` refuse a
    stale verdict (`VERDICT_STALE`) and never overwrite a Manual one (`MANUAL_VERDICT`).
  - The update skill re-verifies edited tests: critic, ingest, one repair, and asking the user about a test that stays
    hallucinated.
- **B — Staging.** A failed staging file becomes `failed-<id>.json` and is never read again. `ingest-update` gains
  `--dry-run`.
- **C — Delete.** A new `TestDeletionService`. It drops a scalar `depends_on` through the 078 writer and warns on list
  forms. It removes baseline entries, staging files and verdict files, and legacy `ai update` uses it too.
- **D — Gitignore.** `update-skills` offers each pattern once (recorded in the skills manifest), never creates
  `.gitignore`, checks presence by whole line, and appends without changing existing bytes.

## Technical Context

**Language/Version**: C# 12, .NET 8.
**Primary Dependencies**:
- the ingest seam: `IngestUpdateCommand`, `UpdatedTestIngestor`, `TestPersistenceService`;
- the 078 writer: `TestFileWriter`, `TestCaseLayout`;
- the critic seam: `CompileCriticPromptCommand`, `IngestVerdictCommand`, `IngestGroundingCommand`,
  `GroundingWriteBackService`, `CompileRepairPromptCommand`, `AuditGroundingHandler`;
- the baseline layer: `BaselineStore`;
- `DeleteHandler`, `UpdateHandler`, `InitHandler`, `UpdateSkillsHandler`, `SkillsManifest`, `SpectraGitIgnore`;
- YamlDotNet `YamlStream`, for the list-form warning only.

No new packages.
**Storage**: new scratch files `.spectra/verdicts/critic-input-<id>.json` (gitignored with `.spectra/verdicts/`) and
`failed-<id>.json` in the staging folders (gitignored by 089). The skills manifest gains `gitignore_offered`. There is
no test-file, baseline or index format change.
**Testing**: xUnit 2.9.3, with `BaselineTestWorkspace` / `UpdatePromptWorkspace` (`CriteriaCommands` collection),
`TempWorkspace` for delete, and the real `InitHandler`. Baseline at `fc82b7a`: Core 1219, CLI 1928 (one known failure),
Execution 228, Integration 8. T001 measures it again.
**Target Platform**: Windows and Linux.
**Project Type**: CLI.
**Performance Goals**: the fingerprint is one SHA-256 over a short JSON per test. Delete's dependent scan parses each
test file once (as today's regex scan read each once).
**Constraints**:
- Model-free.
- Nothing written on `--dry-run`; human summaries kept in JSON mode.
- The shared failure shape for the new codes.
- No config section becomes required.
- The generate skill's NON-STOP and `written == 0` guard text stay verbatim.
- 089-review fixes (B1, D1–D3) fail on `fc82b7a`.

**Scale/Scope**:
- about 5 new source files: `TestContentFingerprint`, `VerdictFreshness` + `VerdictFiles`, `TestDeletionService`,
  `Newlines`;
- edits to about 15 files (incl. `AuditGroundingHandler`);
- 2 skills;
- 4 docs;
- about 10 new test classes (incl. `AuditGroundingUnverifiedTests`) and 4 known assertion changes (research R9).

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Test files keep lossless writes; a delete changes only `depends_on` values, the index and the baseline. Restoring an edited test is left to git (the user runs it) | ✅ |
| II. Deterministic execution | The engine is untouched. The fingerprint is a pure function of the test's content. `depends_on` semantics are unchanged (scalar) | ✅ |
| III. Orchestrator-agnostic, minimal output | Model-free. The fingerprint record is written by the CLI, never trusted from the model. JSON additions are counts and id lists | ✅ |
| IV. CLI-first, `--dry-run`, deterministic exit codes | `ingest-update` gains `--dry-run` (closing the 089 analyze D1 gap). New refusals exit 4 with named codes. Exit 8 keeps its 089 meaning | ✅ |
| V. Simplicity | Reuses the parser, the 078 writer, triage's pending check, `compile-repair-prompt` and the existing skip-if-present rule (no `--replace`). One deletion service for two callers | ✅ |
| Test-required discipline | T1–T13 from the spec, plus real-init and pre-090 workspace rows. B1 and D1–D3 are proven against `fc82b7a` | ✅ |

**Post-design re-check**: no violations. One existing command's refusal rule changes: `compile-repair-prompt` accepts
hallucinated verdicts (research R8). That is behaviour within constitution IV, not a violation.

## Project Structure

### Documentation

```text
specs/090-reverify-after-update/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/Text/Newlines.cs                         # NEW: Detect(text) (moved from TestCaseParser)
src/Spectra.Core/Parsing/TestCaseParser.cs                # uses Newlines.Detect
src/Spectra.CLI/
├── Verification/
│   ├── TestContentFingerprint.cs                         # NEW: Of(TestCase) → "sha256:…"
│   └── VerdictFreshness.cs                               # NEW: record write/read, Check → Fresh|Stale|NoRecord; VerdictFiles.RemoveAsync
├── Generation/UpdatedTestIngestor.cs                     # ApplyEdit drops non-Manual grounding; IngestAsync(persist)
├── Generation/IngestResult.cs                            # + ManualVerdictKept
├── Commands/Generate/
│   ├── IngestUpdateCommand.cs                            # verdict cleanup; failed-<id>.json; --dry-run; manual_verdict_kept
│   ├── CompileCriticPromptCommand.cs                     # writes critic-input-<id>.json per compiled test
│   ├── IngestVerdictCommand.cs                           # VERDICT_STALE / skipped_stale
│   ├── IngestGroundingCommand.cs                         # VERDICT_STALE, MANUAL_VERDICT / skipped_stale, skipped_manual
│   └── CompileRepairPromptCommand.cs                     # FR-A6: accepts hallucinated (refuses grounded)
├── Commands/Review/AuditGroundingHandler.cs              # FR-A7: lists tests without a verdict file; unverified count
├── Commands/Delete/
│   ├── TestDeletionService.cs                            # NEW: plan/apply (scalar depends_on via 078 writer, list warnings, baseline, staging, verdicts)
│   └── DeleteHandler.cs                                  # uses the service; regexes removed; JSON additions
├── Commands/Update/UpdateHandler.cs                      # ToDelete through TestDeletionService
├── Baseline/BaselineStore.cs                             # + RemoveEntriesAsync
├── Commands/Init/SpectraGitIgnore.cs                     # offered set, whole-line presence, negation, byte-preserving append
├── Commands/Init/InitHandler.cs                          # records the offered set
├── Commands/UpdateSkills/UpdateSkillsHandler.cs          # offers only new patterns; no create; names added
├── Skills/SkillsManifest.cs                              # + gitignore_offered
└── Skills/Content/Skills/spectra-update.md               # Step 5a re-verify; summary; NON-STOP writes

tests/Spectra.CLI.Tests/
├── Commands/Generate/EditDropsVerdictTests.cs            # T1
├── Commands/Generate/VerdictStaleTests.cs                # T2
├── Commands/Generate/ManualVerdictTests.cs               # T3
├── Commands/Generate/RepairLoopAfterEditTests.cs         # T4 (+ flagged-review repair row)
├── Commands/Review/AuditGroundingUnverifiedTests.cs      # T14
├── Commands/Generate/FailedStagingTests.cs               # T6 (fails on fc82b7a)
├── Commands/Generate/IngestUpdateDryRunTests.cs          # T7
├── Commands/Delete/SafeDeleteTests.cs                    # T8, T9
├── Commands/Update/LegacyUpdateDeleteTests.cs            # T10
├── Commands/Init/GitIgnoreOfferTests.cs                  # T11–T13 (fail on fc82b7a)
└── Skills/SkillsManifestTests.cs                         # T5
```

## Implementation Order

1. **T001:** measure the baseline. Write the B1 and D1–D3 regression tests first and run them on the untouched sources
   (proof against `fc82b7a`, as in 089).
2. **B:** `failed-<id>.json`, then `--dry-run` (`persist` flag).
3. **A:**
   - the fingerprint and record (`compile-critic-prompt`);
   - the freshness checks (`ingest-verdict`, `ingest-grounding`);
   - Manual protection;
   - the edit dropping grounding and verdict files;
   - `compile-repair-prompt` accepting hallucinated verdicts;
   - the repair-loop regression tests (T4).
4. **C:** `TestDeletionService`, `BaselineStore.RemoveEntriesAsync`, `DeleteHandler` and the legacy `UpdateHandler`.
5. **D:** `Newlines`, `SpectraGitIgnore`, the manifest, `init` and `update-skills`.
6. **Skill:** Step 5a and the summary (T5).
7. **Docs:** cli-reference, usage, CHANGELOG, CLAUDE.md.
8. **Finish:** a full build (no new warnings) and test run, the quickstart and the Notes. Ask before committing and
   merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| The fingerprint changes on fields the critic doesn't care about (e.g. tags), refusing a good verdict | It errs safe: a stale refusal means re-running the critic, never a wrong verdict. The fingerprint uses exactly `SerializeTest`, the same content shape the update prompt shows |
| `compile-critic-prompt` now writes a file | One scratch file under the gitignored `.spectra/verdicts/`; nothing on a refusal; documented |
| The generation repair loop breaks | T4 runs the 8a/8b CLI sequence end to end; the guard text is asserted verbatim |
| The delete rewrite changes other bytes | T8 asserts byte-identity of everything but `depends_on` (and its prelude), for LF and CRLF files |
| `update-skills` on a pre-090 workspace re-adds old patterns | The pre-090 default offered set (all but the two staging folders); T11 covers a user-removed old pattern |
| Existing delete and ingestor tests | Research R9; each change listed in the Notes |

## Complexity Tracking

No constitution violations.
