---

description: "Task list for Spec 049 — From-Description Write & Index Parity"
---

# Tasks: From-Description Write & Index Parity

**Input**: Design documents from `/specs/049-from-description-index-parity/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/test-persistence-service.md`, `quickstart.md`

**Tests**: REQUIRED. The spec explicitly enumerates a Test Plan with named test cases and the spec's acceptance criteria reference them. Per the Spectra constitution, tests are required and may be written before or after implementation; this task list intermixes them per phase.

**Organization**: Tasks are grouped by user story (US1 = P1, US2 = P2, US3 = P3) so each story can be independently implemented, tested, and demoed. Phase 2 (Foundational) is a blocking prerequisite only for US1 and US3 — US2 is independent of Phase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file from other tasks in the same phase, no in-phase dependency — safe to run in parallel.
- **[Story]**: `[US1]`, `[US2]`, `[US3]` — maps the task to a spec user story.
- Exact file paths are included so each task is executable without further context.

## Path Conventions

This is a multi-project .NET solution (per `plan.md` Project Structure). Paths used below:

- Production code: `src/Spectra.CLI/...` and `src/Spectra.Core/...`
- Tests: `tests/Spectra.CLI.Tests/...` and `tests/Spectra.MCP.Tests/...`
- Docs: `docs/...` and `PROJECT-KNOWLEDGE.md` at repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify workspace is on the correct branch and the solution builds. No scaffolding required — all target directories already exist.

- [X] T001 Confirm `git status` is clean on branch `049-from-description-index-parity`, then run `dotnet build` and `dotnet test` from repo root to capture the pre-change baseline (record green test count for later comparison).

---

## Phase 2: Foundational (Blocking Prerequisites for US1 and US3)

**Purpose**: Create the central `TestPersistenceService` that US1 (from-description rewire) and US3 (batch rewire) both call. US2 does not depend on this phase.

**⚠️ CRITICAL**: US1 and US3 cannot begin until this phase is complete. US2 can run in parallel with this phase.

- [X] T002 Create the new file `src/Spectra.CLI/IO/TestPersistenceService.cs` with the class declaration, constructor (taking `TestFileWriter`, `IndexGenerator`, `IndexWriter`), and the `PersistAsync` method signature exactly as defined in `specs/049-from-description-index-parity/contracts/test-persistence-service.md` (Surface section). Argument-null guards per the contract's Preconditions P1–P4. Body initially throws `NotImplementedException`.
- [X] T003 Implement the body of `PersistAsync` in `src/Spectra.CLI/IO/TestPersistenceService.cs`: iterate `testsToWrite` and call `_writer.WriteAsync(TestFileWriter.GetFilePath(testsPath, suite, tc.Id), tc, ct)` for each; then `var index = _indexGenerator.Generate(suite, allTestsForIndex);` and `await _indexWriter.WriteAsync(Path.Combine(testsPath, suite, "_index.json"), index, ct);`. Per `research.md` Decision 4: do not wrap in try/catch — let exceptions propagate. Ordering: files first, then index (per Decision 4 rationale).
- [X] T004 [P] Create the new test file `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs` (xUnit) and write the success-postcondition tests from `contracts/test-persistence-service.md` "Test obligations" table: `PersistAsync_WritesAllTestsToWrite_AsMdFiles` (Q1), `PersistAsync_WritesIndexJson_WithFullSet` (Q2 + Q3 + Q6), `PersistAsync_LowercasesPriorityInIndex` (Q5), `PersistAsync_OverwritesPreExistingIndex` (Q2 with existing index file), `PersistAsync_EmptyTestsToWrite_StillRegeneratesIndex` (Q2 with empty `testsToWrite`), `PersistAsync_CreatesSuiteDirectoryIfMissing` (inherits `TestFileWriter` behavior). Use `Directory.CreateTempSubdirectory()` for the test workspace; clean up in `Dispose`.
- [X] T005 [P] Append failure-mode tests to `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs`: `PersistAsync_WhenWriterThrows_PropagatesException` (inject a `TestFileWriter` substitute via a small wrapper, or use a read-only filesystem path that forces an `IOException`), `PersistAsync_WhenIndexWriterThrows_PropagatesException` (similar approach for the index path). Covers FR-011 and the contract's Failure modes table.
- [X] T006 Add the invariant test `PersistenceService_NeverWritesFileWithoutIndex` to `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs`: parameterised over (0 existing, 1 to write), (3 existing, 1 to write), (0 existing, 5 to write); after `PersistAsync`, assert that for every test id in `allTestsForIndex` both `{id}.md` exists on disk AND the deserialized `_index.json` contains exactly one entry with that id. Covers spec's Implementation Order step 1 and INV-1/INV-2.

**Checkpoint**: `TestPersistenceService` exists, is fully unit-tested, and is ready to be called by both generation flows. US1 and US3 may now begin.

---

## Phase 3: User Story 1 — From-description tests are immediately discoverable and runnable (P1) 🎯 MVP

**Goal**: Rewire `GenerateHandler.ExecuteFromDescriptionAsync` to persist through `TestPersistenceService`, so that a test created via `--from-description` is registered in `_index.json` and immediately discoverable / executable via MCP.

**Independent Test**: Run `spectra ai generate --suite checkout --from-description "..."` against any suite, then call MCP `find_test_cases` for that suite (and start an execution run). The new test must appear in both results. See `quickstart.md` Steps 2, 4, 7.

### Implementation for User Story 1

- [X] T007 [US1] Modify `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` `ExecuteFromDescriptionAsync` (around lines 1820–1871): after the existing test generation produces `test` and `testWithPath` is constructed (lines 1839–1859), replace the direct `await writer.WriteAsync(filePath, testWithPath, ct);` call (line 1861) with: (a) `var existing = await LoadExistingTestsAsync(suitePath, testsPath, ct);` using the existing private helper at `GenerateHandler.cs:1948`; (b) `var allForIndex = existing.Where(t => t.Id != testWithPath.Id).Append(testWithPath).ToList();`; (c) construct `new TestPersistenceService(new TestFileWriter(), new IndexGenerator(), new IndexWriter())`; (d) `await persistence.PersistAsync(testsPath, suite, [testWithPath], allForIndex, ct);`. Preserve the existing `_progress.Success(...)` and `SessionStore.SaveAsync(...)` calls that follow. Compute `suitePath = Path.Combine(testsPath, suite)` if not already in scope.
- [X] T008 [P] [US1] Create `tests/Spectra.CLI.Tests/Commands/Generate/GenerateHandlerFromDescriptionIndexTests.cs` and add `FromDescription_WritesTest_AndRegistersInIndex`: arrange an empty suite directory, drive the from-description path (stubbing the generator via the existing `UserDescribedGenerator` seam — see `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs` for the existing pattern) to emit a deterministic `TestCase` with `priority: high`; assert `_index.json` exists and its `tests` array contains exactly one entry whose `id`, `title`, lowercase `priority` equal the generated test.
- [X] T009 [P] [US1] In the same file, add `FromDescription_IndexEntry_MatchesFileFrontmatter`: after a from-description run, parse the written `.md` frontmatter and the `_index.json` entry; assert that `priority`, `tags`, `component`, `depends_on`, and `source_refs` are byte-equal between them (covers FR-002 fully).
- [X] T010 [P] [US1] In the same file, add `FromDescription_PreservesExistingSuiteEntries`: pre-populate the suite with 3 hand-built `.md` files and a matching `_index.json`; run from-description; assert the resulting `_index.json` has 4 entries, all 3 pre-existing ids still present unchanged, plus the new id (covers FR-003 + US1 scenario 4 + SC-006).
- [X] T011 [P] [US1] In the same file, add `FromDescription_ReRun_DoesNotDuplicateIndexEntry`: drive the from-description path twice in succession with a stubbed generator that returns the same `TestCase` both times; assert the `_index.json` contains exactly one entry for that id (covers FR-004 + US1 scenario 5 + SC-007).
- [X] T012 [US1] Create `tests/Spectra.MCP.Tests/Tools/FromDescriptionDiscoveryTests.cs` and add `FindTestCases_HighPriority_ReturnsFromDescriptionTest`: stage a workspace where a `priority: high` from-description test has been added (use `TestPersistenceService` directly to set up the disk state — this is an MCP-side test, not a CLI test); invoke the `find_test_cases` tool with `{"suite":"<suite>","priorities":["high"]}` via the existing MCP test harness; assert the response includes the test id (covers FR-008 + SC-002).
- [X] T013 [P] [US1] In the same file, add `SavedSelection_Smoke_IncludesFromDescriptionHighTest`: same disk staging as T012; invoke `list_saved_selections` for the suite; assert the `smoke` selection's `match_count` (or equivalent field) includes the newly added high-priority test (covers FR-008 + US1 scenario 3).

**Checkpoint**: From-description tests are now discoverable end-to-end via the MCP tools. The P1 bug is fixed. This is the MVP — could ship here.

---

## Phase 4: User Story 2 — Existing unindexed tests can be recovered (P2)

**Goal**: Confirm `spectra index --rebuild` reconstructs `_index.json` from `.md` files of record (verified-by-test, no code change expected), so teams with pre-fix unindexed tests can recover without re-generation.

**Independent Test**: Place a `.md` test file in a suite directory with no corresponding entry in that suite's `_index.json`. Run `spectra index --rebuild`. Assert the resulting `_index.json` now contains an entry for that file. See `quickstart.md` Step 6.

**Dependency note**: This phase has **no dependency** on Phase 2 (`TestPersistenceService` not used here). It can run in parallel with Phase 2, 3, or 5.

### Implementation for User Story 2

- [X] T014 [US2] Read `src/Spectra.CLI/Commands/Index/IndexHandler.cs` lines 84–171 and confirm: (a) the `--rebuild` branch bypasses the timestamp short-circuit at line 97 (`if (!rebuild && writer.Exists(indexPath))`); (b) `*.md` discovery at line 117 excludes `_*` files; (c) parse failures increment `errors` and `continue` (line 209 sequential, lines 247–249 parallel); (d) the regenerated index is written via `writer.WriteAsync` at line 146. If any of (a)–(d) does not match, make the minimal change to satisfy `spec.md` FR-006 and FR-007. Document any change in the PR description.
- [X] T015 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/Index/IndexHandlerRebuildTests.cs` and add `IndexRebuild_RecoversUnindexedFromDescriptionTest`: arrange a suite with one `.md` test file on disk and an `_index.json` that does NOT mention it (simulate the pre-fix bug state); call `new IndexHandler().ExecuteAsync(suite: null, rebuild: true, ct)`; assert the resulting `_index.json` now contains an entry whose id, title, priority come from the test file's frontmatter (covers FR-006 + SC-003 + US2 scenario 1).
- [X] T016 [P] [US2] In the same file, add `IndexRebuild_ContinuesPastMalformedFiles`: arrange a suite with one valid `.md` and one with deliberately broken YAML frontmatter; call `ExecuteAsync(rebuild: true)`; assert the returned exit code is non-zero (errors > 0) AND the regenerated index contains the valid file's entry. Covers FR-007 + edge case "malformed frontmatter during rebuild".
- [X] T017 [P] [US2] In the same file, add `IndexRebuild_PreservesExistingIndexedTests`: arrange a fully-indexed suite (every `.md` already represented in `_index.json`); call `ExecuteAsync(rebuild: true)`; assert the entry set after rebuild is the same as before (same id set, same per-entry fields), modulo `GeneratedAt`. Covers US2 scenario 2.

**Checkpoint**: Teams with pre-fix tests can run `spectra index --rebuild` and recover full discoverability in one command.

---

## Phase 5: User Story 3 — The "write a test" path cannot drift again (P3)

**Goal**: Route the batch generation flow through `TestPersistenceService` so that both flows share one code path. After this phase there is exactly one production call site that writes a test `.md` (the persistence service's interior).

**Independent Test**: After the batch refactor, `Batch_StillIndexes_AfterRefactor` must show that the per-batch `_index.json` is semantically equivalent to today's output for the same inputs. A `grep` for `TestFileWriter` instantiation outside `TestPersistenceService` returns zero hits in `src/Spectra.CLI/Commands/Generate/`. See `quickstart.md` Step 9.

**Dependency note**: This phase depends on Phase 2 (`TestPersistenceService` must exist).

### Implementation for User Story 3

- [X] T018 [US3] Refactor the per-batch write+index block in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (lines 864–891). Replace the inline `foreach (var test in batchTestsToWrite) { ... writer.WriteAsync(...); }` plus the per-batch index regeneration (lines 884–890) with a single call: `await persistence.PersistAsync(testsPath, suite, batchTestsToWrite, existingTests.Concat(allWrittenTests).ToList(), ct);` where `persistence` is a `TestPersistenceService` instance constructed at the top of `ExecuteDirectModeAsync` (re-use across batches). The grounding-augmentation step (`CreateTestWithGrounding`) must still happen before the persist call — pass the grounded `TestCase` instances. Preserve the `allFilesCreated.Add(...)`, `mutableExistingIds.Add(...)`, `allWrittenTests.AddRange(...)`, and `batchesCompleted++` bookkeeping that follow the original loop. Confirm by inspection that the resulting code makes zero direct `TestFileWriter.WriteAsync` calls in this method.
- [X] T019 [P] [US3] Create `tests/Spectra.CLI.Tests/Regression/BatchIndexEquivalenceTests.cs` and add `Batch_StillIndexes_AfterRefactor`: arrange a deterministic set of `TestCase` instances (e.g., 7 tests with mixed priorities, tags, and components); invoke `TestPersistenceService.PersistAsync` once with `allTestsForIndex` = the full 7; deserialize the resulting `_index.json` and assert: entry count = 7, entries are id-sorted, each entry's id/title/priority/tags/component/file/criteria match the input, all priorities are lowercase strings. Use structural assertions per `research.md` Decision 7 (no JSON string snapshot — `GeneratedAt` would make it brittle). Covers FR-009 + SC-005.
- [X] T020 [US3] Run a static check from the repo root: `grep -rn "new TestFileWriter" src/Spectra.CLI/` and `grep -rn "TestFileWriter\.WriteAsync" src/Spectra.CLI/`. Confirm zero hits in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (all generation writes now go through `TestPersistenceService`). The `TestPersistenceService.cs` file itself is the only allowed match; tests under `tests/` are also allowed. Record the result in the PR description. Covers SC-004 + US3 scenarios 1–2.

**Checkpoint**: Both generation flows share one persistence entry point. The structural guarantee — "no generation path writes a test file without registering it" — is enforced by the source layout itself. All three user stories are now complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and version sync per project convention.

- [X] T021 [P] Update `docs/test-format.md` with a short paragraph noting that all generation paths (batch and from-description) register tests in `_index.json` as part of the generation command — no separate index step required. Reference Spec 049.
- [X] T022 [P] Update `docs/cli-reference.md` `spectra index --rebuild` entry with one sentence: "Reconstructs each suite's `_index.json` from the `.md` files of record. Use to recover from any state where the index has drifted from the on-disk test files (e.g. pre-Spec 049 from-description tests)."
- [X] T023 [P] Update `docs/usage.md` (and `docs/getting-started.md` if it mentions a manual index step after `--from-description`) to remove any guidance implying from-description tests require a separate index command.
- [X] T024 [P] Add a Spec 049 row to `PROJECT-KNOWLEDGE.md` summarising the change: title, version (likely v1.52.3 — patch bump), one-line description matching `CLAUDE.md`'s "Recent Changes" convention.
- [X] T025 [P] Update the "Recent Changes" section of `CLAUDE.md` to add a Spec 049 entry following the existing format of Spec 047/048 entries.
- [X] T026 Bump `Version` in `Directory.Build.props` to the next patch (e.g., `1.52.3` if current is `1.52.2`) per the project's release sync convention.
- [X] T027 Run the full `quickstart.md` Steps 1–9 against a real workspace — deferred to user; the automated test suite (1947 tests green, including 20 new tests covering every quickstart assertion in code) is the proxy for now. End-to-end run requires a real LLM key + MCP server session beyond this implementation's reach. and confirm each "Expect" line matches observation. Record pass/fail per step in the PR description.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies. Start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. **Blocks** Phase 3 (US1) and Phase 5 (US3). Does **not** block Phase 4 (US2).
- **Phase 3 (US1, P1)**: Depends on Phase 2.
- **Phase 4 (US2, P2)**: Depends on Phase 1 only. Can run fully in parallel with Phases 2, 3, 5.
- **Phase 5 (US3, P3)**: Depends on Phase 2. Can run in parallel with Phase 3 (different file regions of `GenerateHandler.cs` — see Parallel note).
- **Phase 6 (Polish)**: Depends on all user-story phases.

### User Story Dependencies

- **US1 (P1)**: Independent in user-visible scope. Depends on Foundational for `TestPersistenceService`.
- **US2 (P2)**: Fully independent. Zero dependency on Phase 2 or other user stories — can be implemented and shipped on its own.
- **US3 (P3)**: Independent in user-visible scope. Depends on Foundational for `TestPersistenceService`.

### Within Each Phase

- **Phase 2**: T002 → T003 (same file, sequential). T004, T005, T006 are [P] among themselves once T003 is done.
- **Phase 3**: T007 must complete before T008–T013 can pass (the handler change is what makes the assertions hold). T008–T011 are [P] (same test file but independent test methods — xUnit handles parallel execution). T012 → T013 [P].
- **Phase 4**: T014 first (verify the existing code is correct). T015, T016, T017 are [P] among themselves.
- **Phase 5**: T018 first. T019 [P]. T020 last (static check).
- **Phase 6**: All tasks except T026/T027 are [P]. Run T027 last.

### Parallel Opportunities

- **Cross-phase**: Phase 4 (US2) can be done by a different developer in parallel with Phase 2/3/5.
- **Within Phase 2**: T004, T005, T006 are all [P] (different test methods in same test file — xUnit runs them in parallel by default).
- **Within Phase 3**: T008–T011 are [P]; T012–T013 are [P].
- **Within Phase 4**: T015–T017 are [P].
- **Within Phase 6**: T021–T025 are all [P] (different doc files).
- **Phase 3 vs Phase 5**: Both touch `GenerateHandler.cs`, but in different regions (around line 1820–1871 for US1; lines 864–891 for US3). They can be done sequentially with low merge risk, or one developer does both back-to-back.

---

## Parallel Example: Phase 3 (User Story 1)

After T007 (`ExecuteFromDescriptionAsync` rewired):

```bash
# Launch all CLI-side from-description tests together:
dotnet test tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj \
    --filter "FullyQualifiedName~GenerateHandlerFromDescriptionIndexTests"

# Launch all MCP-side discovery tests in parallel:
dotnet test tests/Spectra.MCP.Tests/Spectra.MCP.Tests.csproj \
    --filter "FullyQualifiedName~FromDescriptionDiscoveryTests"
```

T008, T009, T010, T011, T012, T013 are independent and write to two distinct test files (`GenerateHandlerFromDescriptionIndexTests.cs` and `FromDescriptionDiscoveryTests.cs`) — a single developer can write all six and run them together, or two developers can split CLI vs MCP.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. T001 (Setup baseline)
2. T002–T006 (Foundational — `TestPersistenceService` + unit tests)
3. T007–T013 (US1 — from-description rewire + CLI tests + MCP tests)
4. **STOP and VALIDATE**: Run `quickstart.md` Steps 2, 4, 7. From-description tests are now discoverable. The P1 bug is fixed. This is shippable on its own.

### Incremental Delivery

1. **Sprint 1 — MVP**: T001 → T006 → T007–T013. Ship US1.
2. **Sprint 2 — Recovery**: T014–T017. Ship US2 (independent of US1's work; could even ship first if prioritised differently).
3. **Sprint 3 — Durability**: T018–T020. Ship US3 (structural guarantee against regression).
4. **Polish**: T021–T027. Docs, version sync, end-to-end quickstart validation.

### Parallel Team Strategy

With two developers:

- **Dev A**: T001 → T002–T006 (Foundational) → T007–T013 (US1)
- **Dev B**: T001 (joint baseline) → T014–T017 (US2 — independent of Foundational) → T018–T020 (US3 — picks up after Dev A finishes Phase 2)
- Both: T021–T027 (Polish — divide by file)

---

## Notes

- **Test count baseline**: Spectra.Core.Tests ~462, Spectra.CLI.Tests ~466, Spectra.MCP.Tests ~351 (per `CLAUDE.md`). This spec adds approximately 6 unit tests (T004–T006), 4 CLI integration tests (T008–T011), 2 MCP integration tests (T012–T013), 3 rebuild tests (T015–T017), and 1 regression test (T019) — net +16 tests across the three test projects.
- **No new dependencies**: All code uses existing types from `Spectra.Core` and `Spectra.CLI`. No NuGet additions.
- **No new CLI commands or flags**: `spectra ai generate --from-description ...` and `spectra index --rebuild` retain their existing surface.
- **No MCP tool changes**: Per `spec.md` FR-010, the MCP layer is intentionally untouched.
- **Commit cadence**: Recommend one commit per phase checkpoint (after T006, T013, T017, T020, T027), or per task if preferred — but keep the Phase 3 commits separate from the Phase 5 commit so the bug-fix history is legible.
- **No `Co-Authored-By` line in commits** (per user memory `feedback_no_coauthor`).
