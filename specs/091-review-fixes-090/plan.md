# Implementation Plan: Review Fixes for Spec 090

**Branch**: `091-review-fixes-090` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/091-review-fixes-090/spec.md`

## Summary

Four review fixes for 090; no new features.

- **F1 — old verdicts.** The fingerprint record gains `compiled_at`. `VerdictFreshness.Check` also treats a verdict
  file written more than 2 s before `compiled_at` as stale (`VERDICT_STALE`, new message). One function serves all four
  ingest paths; 090 records keep the fingerprint rule (research R1, R2).
- **F2 — `--ids`.** `ingest-grounding --all` gains `--ids` and `not_found`; `--all --repaired` requires it
  (`IDS_REQUIRED`, exit 4). `--test` with `--all` and `--ids` without `--all` are invalid (exit 1). Both skills pass
  the re-critiqued ids; the update skill re-critiques no failed repair (R3, R6).
- **F3 — kept hallucinated tests.** In `--all`, a parseable hallucinated verdict for an existing test is
  `skipped_hallucinated` / `hallucinated`, not `errors`; the human summary names it, on stderr in JSON mode (R4, R5).
- **F4 — text.** The critic agent's step 1 and the leftover cli-reference line describe what the compile writes (R7).

## Technical Context

**Language/Version**: C# 12, .NET 8.
**Primary Dependencies**: `VerdictFreshness` / `VerdictRecord` (090), `CompileCriticPromptCommand`,
`IngestVerdictCommand`, `IngestGroundingCommand`, `GroundingWriteBackService` (unchanged), `CriteriaCommandIO`
(shared failure shape), the bundled skills and the critic agent. No new packages.
**Storage**: the fingerprint record gains one optional field (`compiled_at`). No test-file, index, baseline or config
change.
**Testing**: xUnit 2.9.3; `VerdictWorkspace` / `UpdatePromptWorkspace` over `BaselineTestWorkspace` (`CriteriaCommands`
collection), created with the real `spectra init`; explicit file times via `File.SetLastWriteTimeUtc`, no sleeps.
Baseline at `5c10c44`: Core 1219, CLI 2001 (one known failure), Execution 228, Integration 8; 4 known warnings.
**Target Platform**: Windows and Linux.
**Project Type**: CLI.
**Performance Goals**: one extra `File.GetLastWriteTimeUtc` per verdict checked; the batch parses each verdict JSON
once (today up to twice).
**Constraints**:
- model-free;
- shared failure shape for `IDS_REQUIRED`;
- human summaries kept in JSON mode (stderr);
- no config section required;
- the generate skill's NON-STOP contract and written==0 guard text stay verbatim;
- each fix has a test that fails on `5c10c44`.

**Scale/Scope**: edits to `VerdictFreshness.cs`, `CompileCriticPromptCommand.cs`, `IngestVerdictCommand.cs`,
`IngestGroundingCommand.cs`; 2 skills and 1 agent; 4 docs; 3 new test classes, `SkillsManifestTests` rows, 2 known
assertion changes (research R8).

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | No test-file format change; grounding blocks are written only for the tests the skill names. Nothing touches git | ✅ |
| II. Deterministic execution | Engine untouched. Freshness is a pure function of the record, the test content and the verdict file's time | ✅ |
| III. Orchestrator-agnostic, minimal output | Model-free. `compiled_at` is written by the CLI, never by the model. JSON additions are one count and two id lists | ✅ |
| IV. CLI-first, deterministic exit codes | `IDS_REQUIRED` exits 4 (refusal) with a named code; `not_found` and `skipped_hallucinated` don't change exit codes. `ingest-grounding --all` / `ingest-verdict --all` have no `--dry-run` / `--no-review` (pre-existing; see Complexity Tracking) | ✅ (resolved by 097: `--dry-run` added) |
| V. Simplicity | One freshness function gains one comparison; one batch loop gains a filter and one branch; no new command | ✅ |
| Test-required discipline | T1–T8 from the spec; each fix proven against `5c10c44` (T1, T4–T6, T7, T8); T2/T3 are guards | ✅ |

**Post-design re-check**: no new violations; the pre-existing constitution IV gap is tracked below. One behaviour tightens on purpose: `--all --repaired` without `--ids` was
accepted and is now refused (FR-007); both bundled skills are updated in the same change.

## Project Structure

### Documentation

```text
specs/091-review-fixes-090/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.CLI/
├── Verification/VerdictFreshness.cs                  # VerdictRecord.CompiledAt; Check(…, verdictPath) time rule; StaleReason; messages
├── Commands/Generate/CompileCriticPromptCommand.cs   # records compiled_at
├── Commands/Generate/IngestVerdictCommand.cs         # passes the verdict path to Check (single + --all)
├── Commands/Generate/IngestGroundingCommand.cs       # --ids, IDS_REQUIRED, not_found, skipped_hallucinated, stderr summary
├── Skills/Content/Skills/spectra-generate.md         # 8b: --ids; skipped_hallucinated is not a failure
├── Skills/Content/Skills/spectra-update.md           # 5a: --ids <id>
└── Skills/Content/Agents/spectra-critic.agent.md     # step 1 text

tests/Spectra.CLI.Tests/
├── Commands/Generate/VerdictOlderThanPromptTests.cs  # T1 (fails on 5c10c44), T2, T3 (guards)
├── Commands/Generate/RepairedIdsTests.cs             # T4–T6 (fail on 5c10c44)
├── Commands/Generate/KeptHallucinatedTests.cs        # T7 (fails on 5c10c44)
├── Skills/SkillsManifestTests.cs                     # T8 (fails on 5c10c44)
├── Commands/IngestGroundingCommandTests.cs           # R8: --ids on the --all --repaired row
└── Commands/Generate/RepairLoopAfterEditTests.cs     # R8: --ids TC-102
```

## Implementation Order

1. **Baseline:** measure on `5c10c44` (build + test). Write T1, T4–T7 and the T8 guards first and run them on the
   untouched sources; record which fail and why (proof against `5c10c44`, as in 089/090).
2. **F1:** `VerdictRecord.CompiledAt`, `Check(…, verdictPath)`, the record write, the four call sites; T1–T3.
3. **F2:** `--ids`, `IDS_REQUIRED`, `not_found`; T4–T6; the two R8 assertion changes.
4. **F3:** the single verdict parse, `skipped_hallucinated` / `hallucinated`, the human summary on stderr in JSON mode;
   T7.
5. **Skills and agent (F2–F4):** 8b, 5a, the critic agent; T8.
6. **Docs:** cli-reference (incl. the "Writes nothing" leftover), usage, CHANGELOG, CLAUDE.md.
7. **Finish:** full build (no new warnings) and test run, the quickstart with the built CLI, the Notes. Ask before
   committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| A fresh verdict refused because of clock or timestamp resolution | 2 s allowance (R1); equal or later times are fresh; a refusal only means re-running the critic, never a wrong verdict |
| Skills run `--all --repaired` with an empty id list | The skill text says to skip the call when the list is empty; the CLI refuses an empty list (`IDS_REQUIRED`), so the mistake is loud |
| A workspace's customised skill still calls `--all --repaired` without `--ids` | It now fails loud with `IDS_REQUIRED` naming the fix; `update-skills` refreshes unmodified copies; CHANGELOG notes it |
| The new stderr summary in JSON mode breaks a caller | Skills parse stdout only; no test asserts an empty stderr (checked) |
| 090 records | `compiled_at` optional; T3 guards the fingerprint-only rule |

## Complexity Tracking

None. The constitution IV row tracked here was resolved by Spec 097 (`--dry-run` on the batch ingests).
