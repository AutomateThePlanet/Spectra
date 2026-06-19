# Tasks: Verdict Disposition Policy

**Input**: Design documents from `specs/071-verdict-disposition/`
**Spec**: spec.md (FR1–FR8) | **Plan**: plan.md | **Branch**: `071-verdict-disposition`
**Tests**: Included (xUnit, Spectra.Core.Tests + Spectra.CLI.Tests)

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel (different files, no shared state dependencies)
- **[US1/US2/US3/US4]**: User story label (from spec phase mapping)
- Sequential rules: data model tasks before command tasks; command tasks before skill tasks; skills before tests

## User Story Mapping (from spec.md phases)

| Label | Spec Phase | Coverage |
|-------|-----------|---------|
| US1 | Phase 1 — Durable & visible | FR1 (condensed block), FR2 (per-test verdict JSON), FR3 (drop trail) |
| US2 | Phase 2 — Repair loop | FR4 (compile-repair-prompt), FR5 (batch non-blocking) |
| US3 | Phase 3 — Human review | FR6 (review-flagged command + skill) |
| US4 | Phase 4 — Docs cleanup | FR7 (consistency contract), FR8 (stale comments + docs) |

---

## Phase 1: Setup

**Purpose**: Branch and gitignore — shared prerequisites before any code change.

- [x] T001 Create and check out branch `071-verdict-disposition` from `main`
- [x] T002 Add `.spectra/verdicts/` and `.spectra/dropped-tests.json` to `.gitignore` in `.gitignore`

---

## Phase 2: Foundational (Core Model — blocks all user story phases)

**Purpose**: New C# record types and extended grounding model used by every US1+ command. Must be complete before any command implementation begins.

**Independent test criteria**: `dotnet build` succeeds; existing `Spectra.Core.Tests` remain green; `GroundingMetadata.IsValid()` enforces new field invariants; `GroundingFrontmatter.ToMetadata()` roundtrips all new fields.

- [x] T003 [P] Create `CondensedFinding` record in `src/Spectra.Core/Models/Grounding/CondensedFinding.cs` — fields: `Element` (string, required), `Reason` (string, required)
- [x] T004 [P] Create `CondensedFindingFrontmatter` YAML DTO in `src/Spectra.Core/Models/Grounding/CondensedFindingFrontmatter.cs` — `YamlMember` aliases `element`/`reason`; nullable setters
- [x] T005 Extend `GroundingMetadata` with new fields in `src/Spectra.Core/Models/Grounding/GroundingMetadata.cs` — add `FlaggedForReview` (bool, default false), `RepairAttempts` (int, default 0), `Repaired` (bool, default false), `CondensedFindings` (IReadOnlyList\<CondensedFinding\>, default [])
- [x] T006 Extend `GroundingFrontmatter` with matching YAML properties and update `ToMetadata()` in `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs` — add `flagged_for_review`, `repair_attempts`, `repaired`, `condensed_findings` (List\<CondensedFindingFrontmatter\>); map all in `ToMetadata()` and `FromMetadata()` (or the static builder equivalent)
- [x] T007 [P] Fix stale enum doc comments in `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs` — rewrite `Hallucinated` doc (was "NOT written to disk" which was pre-Spec-059 and false) and `Partial` doc (was "written with warning marker" which was never implemented) to describe actual current behavior

---

## Phase 3: US1 — Verdicts Durable & Visible

**Purpose**: Implement grounding write-back, drop trail, per-test verdict file naming. By end of this phase every kept test has a grounding block; every dropped test has a trail entry; per-test verdict JSONs survive the batch.

**Independent test criteria**: Run a generate batch → every kept test's `.md` has a `grounding:` frontmatter block; `.spectra/verdicts/critic-verdict-TC-NNN.json` files exist for each tested test; `dropped-tests.json` has one NDJSON entry per hallucinated test; hallucinated tests are gone from disk + `_index.json`; `dotnet test` green.

- [x] T008 [US1] Update `TestFileWriter.cs` grounding block writer to include new optional fields in `src/Spectra.CLI/IO/TestFileWriter.cs` — write `repaired: true` only when `Repaired=true`; `flagged_for_review: true` only when `FlaggedForReview=true`; `repair_attempts: N` only when `RepairAttempts > 0`; `condensed_findings:` list only when `CondensedFindings.Count > 0` (YAML block under grounding section)
- [x] T009 [US1] Create `DroppedTestsTrail` append-only NDJSON writer in `src/Spectra.CLI/IO/DroppedTestsTrail.cs` — `AppendAsync(DroppedTestEntry entry)` appends one JSON object line to `.spectra/dropped-tests.json`; creates file if absent; throws on I/O error (no silent fallback)
- [x] T010 [US1] Create `GroundingWriteBackService` in `src/Spectra.CLI/IO/GroundingWriteBackService.cs` — `WriteAsync(suite, testId, verdictJson, repairAttempts, repaired)`: reads test `.md` via `TestCaseParser`, classifies verdict via `VerdictIngestor.Classify()`, refuses hallucinated (exit 4), builds `GroundingMetadata` (Generator from test frontmatter or `"claude-code-session"`, CondensedFindings from findings where status != grounded), writes updated `TestCase` via `TestFileWriter.WriteAsync`
- [x] T011 [US1] Implement `IngestGroundingCommand` in `src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs` — options: `--suite`, `--test`, `--from` (default `.spectra/verdicts/critic-verdict-{id}.json`), `--repaired` (flag), `--repair-attempts` (int, default 0); delegates to `GroundingWriteBackService`; exit codes per contract (0/1/4/5/6); JSON + human output
- [x] T012 [US1] Implement `RecordDropCommand` in `src/Spectra.CLI/Commands/Generate/RecordDropCommand.cs` — options: `--suite`, `--test`, `--from`, `--reason` (enum: hallucinated|user_decided, default hallucinated); reads title from `_index.json`; for hallucinated reads verdict JSON for `contradicting_claim`/`doc_ref`; delegates to `DroppedTestsTrail.AppendAsync`; exit codes per contract (0/1/5/6); JSON output
- [x] T013 [US1] Fix stale comment in `IngestVerdictCommand.cs` lines 17–18 in `src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs` — remove/rewrite the comment claiming "grounding write-back stays in the reused CreateTestWithGrounding" (function does not exist; write-back is now IngestGroundingCommand)
- [x] T014 [US1] Register `IngestGroundingCommand` and `RecordDropCommand` in `AiCommand.cs` in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` — add `AddCommand(new IngestGroundingCommand())` and `AddCommand(new RecordDropCommand())`
- [x] T015 [US1] Update `spectra-critic.agent.md` to write per-test verdict file in `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md` — change line 89 from `Write your verdict JSON to .spectra/verdicts/critic-verdict.json` to `Write your verdict JSON to .spectra/verdicts/critic-verdict-{id}.json` where `{id}` is the test ID being evaluated; update any surrounding instructions referencing the fixed filename
- [x] T016 [US1] Update `spectra-generate.md` Step 8 for grounded path and hallucinated trail+delete path in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` — after `ingest-verdict`: grounded → call `ingest-grounding --suite {s} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json`; hallucinated → call `record-drop --suite {s} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json` then `spectra delete {id} --force --no-interaction ...`; partial path remains as-is for now (US2 expands it)
- [x] T017 [P] [US1] Add unit tests for `GroundingMetadata` new fields in `tests/Spectra.Core.Tests/Grounding/GroundingMetadataTests.cs` — cover: new field defaults (false/0/empty), `IsValid()` rules (FlaggedForReview only valid with Partial; Repaired only valid with Grounded; RepairAttempts >= 0), roundtrip via `GroundingFrontmatter.ToMetadata()`
- [x] T018 [P] [US1] Add unit tests for `TestFileWriter` new grounding block fields in `tests/Spectra.CLI.Tests/IO/TestFileWriterGroundingTests.cs` — verify: `repaired` omitted when false, emitted when true; `flagged_for_review` omitted when false, emitted when true; `repair_attempts` omitted when 0, emitted when > 0; `condensed_findings` omitted when empty, emitted as YAML list when populated
- [x] T019 [P] [US1] Add unit tests for `DroppedTestsTrail` in `tests/Spectra.CLI.Tests/IO/DroppedTestsTrailTests.cs` — cover: creates file on first write; appends (not overwrites) on subsequent writes; each line is valid JSON parseable as DroppedTestEntry; field presence for hallucinated vs user_decided reasons
- [x] T020 [P] [US1] Add unit tests for `GroundingWriteBackService` in `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` — cover: grounded verdict produces grounding block with no flagged_for_review; partial verdict sets FlaggedForReview=true + CondensedFindings; hallucinated refuses (exit 4); Generator falls back to "claude-code-session" when frontmatter has none
- [x] T021 [P] [US1] Add integration tests for `IngestGroundingCommand` in `tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs` — cover: grounded writes block; partial writes flagged block; hallucinated exits 4; missing verdict file exits 5; --repaired + --repair-attempts reflected in output; existing grounding block is overwritten on re-run
- [x] T022 [P] [US1] Add integration tests for `RecordDropCommand` in `tests/Spectra.CLI.Tests/Commands/RecordDropCommandTests.cs` — cover: hallucinated appends trail entry with contradicting_claim; user_decided appends entry with null claim; sequential calls accumulate entries; missing verdict file for hallucinated reason exits 5

---

## Phase 4: US2 — Repair Loop

**Purpose**: Bounded 1-attempt repair for partial tests. Compile a targeted repair prompt (test artifact + non-grounded findings + source docs), agent patches in-session, re-critic, `ingest-grounding` writes final verdict. Batch stays non-blocking throughout.

**Independent test criteria**: Run with a known-partial test → repair prompt compiled correctly (contains test + specific findings + docs); skill runs repair cycle; grounding block on success shows `repaired: true, repair_attempts: 1`; failure flags-and-continues without halting batch; final report shows all four counts; `dotnet test` green.

- [x] T023 [US2] Implement `RepairPromptCompiler` in `src/Spectra.CLI/Verification/RepairPromptCompiler.cs` — `Compile(TestCase test, IList\<Finding\> nonGroundedFindings, IList\<LoadedDocument\> sourceDocs)` → `string`; emits plain text prompt with sections: test artifact (title/preconditions/steps/expected), critic findings (element + claim + reason per non-grounded finding), source docs (same truncation logic as `CriticPromptBuilder`, max 5 docs × 8000 chars); instruction block specifying to return JSON array of ONE corrected test preserving the test ID
- [x] T024 [US2] Implement `CompileRepairPromptCommand` in `src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs` — options: `--suite`, `--test`, `--from` (default `.spectra/verdicts/critic-verdict-{id}.json`); loads test via index, classifies verdict, refuses non-partial (exit 4), extracts non-grounded findings, loads source docs via `LoadDocumentsFromRefsAsync` (same as `CompileCriticPromptCommand`), emits `RepairPromptCompiler.Compile()` plain text to stdout; exit codes per contract (0/1/4/5/6)
- [x] T025 [US2] Register `CompileRepairPromptCommand` in `AiCommand.cs` in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` — add `AddCommand(new CompileRepairPromptCommand())`
- [x] T026 [US2] Update `spectra-generate.md` Step 8 partial path with bounded repair loop in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` — replace the bare partial `gate pass → keep` with: `compile-repair-prompt --suite {s} --test {id}` → agent reads prompt and writes `.spectra/repaired.json` → `ingest-update {suite} --test-id {id} --from .spectra/repaired.json` → re-invoke spectra-critic for `{id}` → read re-verdict from `ingest-verdict` → `ingest-grounding --suite {s} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json [--repaired --repair-attempts 1]` → if re-verdict hallucinated: `record-drop` + delete; if still partial: grounding block stays with flagged_for_review=true
- [x] T027 [US2] Update Step 9 summary report in `spectra-generate.md` in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` — add four-bucket count report: `Kept grounded (N)`, `Repaired to grounded (N)`, `Flagged partial (N)`, `Dropped hallucinated (N)`; if any flagged: print hint `Run: spectra ai review-flagged --suite {suite}`
- [x] T028 [P] [US2] Add unit tests for `RepairPromptCompiler` in `tests/Spectra.CLI.Tests/Verification/RepairPromptCompilerTests.cs` — cover: plain text output (no JSON envelope); contains test title; contains each non-grounded finding element + reason; contains source doc content (truncated correctly); preserves test ID in instruction block; empty source docs still emits valid prompt
- [x] T029 [P] [US2] Add integration tests for `CompileRepairPromptCommand` in `tests/Spectra.CLI.Tests/Commands/CompileRepairPromptCommandTests.cs` — cover: grounded test exits 4; hallucinated test exits 4; partial test emits plain text to stdout with correct sections; missing verdict file exits 5; invalid verdict JSON exits 6; --from overrides default path

---

## Phase 5: US3 — Human Review Phase

**Purpose**: Interactive + non-interactive review surface for flagged (still-partial-after-repair) tests. Accept clears the flag; delete trails + three-phase-deletes; retry-repair is handled by the `spectra-review-flagged` skill.

**Independent test criteria**: `spectra ai review-flagged --no-interaction --output-format json` lists flagged tests; accept clears `flagged_for_review` in frontmatter without changing verdict; delete appends trail entry + removes file + removes index entry; `dotnet test` green.

- [x] T030 [US3] Create `ReviewFlaggedCommand.cs` and `ReviewFlaggedHandler.cs` in `src/Spectra.CLI/Commands/Review/` — `ReviewFlaggedCommand` registers `--suite` (optional), `--no-interaction`, `--output-format`; `ReviewFlaggedHandler.ExecuteAsync`: scans test files for `grounding.flagged_for_review: true`; non-interactive: outputs JSON list + exits 0 (none found) / 2 (tests listed, undisposed); interactive (Spectre.Console): per-test menu [A]ccept/[D]elete/[S]kip/[Q]uit — accept calls `GroundingWriteBackService` overwrite with `FlaggedForReview=false`; delete calls `RecordDropCommand` (user_decided) then `DeleteHandler`
- [x] T031 [US3] Register `ReviewFlaggedCommand` in `AiCommand.cs` in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` — add `AddCommand(new ReviewFlaggedCommand())`
- [x] T032 [US3] Create `spectra-review-flagged.md` skill in `src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md` — skill for retry-repair cycles on a specific flagged test: list flagged via `spectra ai review-flagged --suite {s} --no-interaction --output-format json`, for target test call `compile-repair-prompt`, agent patches in-session, `ingest-update`, re-critic, `ingest-grounding [--repaired --repair-attempts N]`; if grounded: `review-flagged` will clear flag on next run (or operator can accept); output final disposition
- [x] T033 [P] [US3] Add integration tests for `ReviewFlaggedCommand` in `tests/Spectra.CLI.Tests/Commands/ReviewFlaggedCommandTests.cs` — cover: no flagged tests → exits 0; --no-interaction with flagged tests → exits 2 + JSON output lists them; accept via handler clears `flagged_for_review` in file, verdict stays partial; delete via handler appends trail + removes file + removes index entry; --suite scope filters to one suite

---

## Phase 6: Polish & Cross-Cutting (US4 — Docs Cleanup)

**Purpose**: Correct stale comments, update CLI reference and usage docs, verify full build + test suite passes, sync skills in demo projects.

**Independent test criteria**: No source file contains the four stale comment patterns from `FINDINGS-verdict-disposition.md`; CLI reference lists all four new verbs; `dotnet build` exits 0; `dotnet test` reports Core 557+ / CLI 1150+ passing; zero regressions.

- [x] T034 [P] Fix stale comment in `spectra-critic.agent.md` line 105 in `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md` — update "You do not write or modify test files (the grounding write-back is the CLI's job)" to reflect that grounding write-back is now live via `ingest-grounding`; add note on per-test verdict file naming
- [x] T035 Update `docs/usage.md` and `docs/cli-reference.md` with new verbs in `docs/` — document: `spectra ai ingest-grounding`, `spectra ai record-drop`, `spectra ai compile-repair-prompt`, `spectra ai review-flagged`; include option tables, exit codes, and example invocations per contract; document the grounding block frontmatter format; document the drop trail format
- [x] T036 Run `dotnet build` and `dotnet test` to verify all changes compile and existing tests pass — canary: Core 568 / CLI 1234 / Execution 228 green; zero regressions; all new test files present and passing
- [x] T037 [P] Verify CLAUDE.md remains under 40K chars after any updates in `CLAUDE.md` — count characters; compact if over limit per memory feedback (24.4 KB — OK)

---

## Dependencies

```
Phase 1 (T001-T002)
  → Phase 2 (T003-T007)  [all US phases blocked until core model is done]
    → Phase 3/US1 (T008-T022)  [grounding write-back, trail, per-test verdict files]
      → Phase 4/US2 (T023-T029)  [repair loop reads US1 verdict JSON + uses US1 ingest-grounding]
        → Phase 5/US3 (T030-T033)  [review reads US1 grounding blocks + US2 repair flag]
          → Phase 6/Polish (T034-T037)  [docs updated last; build validates all]
```

Within Phase 3 (US1) sequential order:
```
T003, T004 (parallel)
  → T005, T006 (parallel; need CondensedFinding type)
    → T007 (parallel; independent fix)
    → T008 (TestFileWriter needs new GroundingMetadata fields)
    → T009 (DroppedTestsTrail; independent)
    → T010 (GroundingWriteBackService; needs T008)
      → T011 (IngestGroundingCommand; delegates to T010)
      → T012 (RecordDropCommand; delegates to T009)
        → T013 (stale comment fix; independent)
        → T014 (register commands; needs T011, T012)
          → T015, T016 (skill changes; can be parallel; need commands registered)
            → T017-T022 (tests; all parallel)
```

Within Phase 4 (US2) sequential order:
```
T023 (RepairPromptCompiler; needs access to Finding type from verdict JSON)
  → T024 (CompileRepairPromptCommand; delegates to T023 + LoadDocumentsFromRefsAsync)
    → T025 (register; needs T024)
    → T026, T027 (skill updates; can be parallel; need T024 registered)
      → T028, T029 (tests; parallel)
```

---

## Parallel Execution Examples

**Phase 2 start** (run simultaneously):
- T003 + T004 (two new record files, zero shared state)
- T007 (enum comment fix, independent file)

**Phase 3/US1 tests** (all parallel after T016 is complete):
- T017 + T018 + T019 + T020 + T021 + T022

**Phase 4/US2** (after T023):
- T025 (register) + T026 (skill step 8) + T027 (skill step 9) in parallel

**Phase 4/US2 tests** (parallel):
- T028 + T029

**Phase 5/US3 test** + **Phase 6 cleanup** (T033 + T034 + T037 can run in parallel after Phase 5 implementation is done)

---

## Implementation Strategy

**MVP Scope (Phase 3 / US1 alone)**: After Phase 1+2+3, every kept test has a grounding block, per-test verdict JSONs are durable, and drop trail is written. This alone makes verdicts visible and gives the audit trail. Phases 4–6 add repair and review.

**Deliver in phase-gate order** (spec enforces sequential gates): complete Phase 3, verify the gate, then Phase 4, then Phase 5, then Phase 6. Do NOT parallelize across phases — Phase 4 reads the verdict JSON that Phase 3's skill writes; Phase 5's review reads the flags that Phase 4 writes.

**REPACK + REINSTALL check**: After Phase 3, Phase 5, and Phase 6 changes to bundled skill content: if testing against a packed NuGet, run `spectra update-skills` to sync skill content. For in-repo testing (`dotnet run`), skills are loaded from source directly and no repack is needed.

---

## Task Count Summary

| Phase | Tasks | Parallelizable | Story |
|-------|-------|---------------|-------|
| 1: Setup | T001–T002 | 0 | — |
| 2: Foundational | T003–T007 | T003+T004+T007 | — |
| 3: US1 | T008–T022 | T017–T022 | US1 |
| 4: US2 | T023–T029 | T028+T029 | US2 |
| 5: US3 | T030–T033 | T033 | US3 |
| 6: Polish | T034–T037 | T034+T037 | US4 |
| **Total** | **37** | **~15** | |
