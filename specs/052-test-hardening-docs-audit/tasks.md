---
description: "Task list for Spec 052 — Test Hardening & Documentation Audit (047–051)"
---

# Tasks: Test Hardening & Documentation Audit (047–051)

**Input**: Design documents from `specs/052-test-hardening-docs-audit/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/test-inventory.md

**Tests**: This feature *is* tests + docs; the "tests" below ARE the deliverables (no test-first inversion — there is no production code to drive).

**Organization**: Grouped by user story (US1–US5) per spec priorities. Implementation order follows the spec: Part B → Part A → Part C → Part D → Part E → Part F.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1=named regression, US2=cross-spec, US3=scale, US4=doc/SKILL audit, US5=changelog/knowledge

## Path Conventions
- New project: `tests/Spectra.Integration.Tests/`
- Scale guard: `tests/Spectra.CLI.Tests/Extraction/`
- Deliverables: `docs/specs/`, `CHANGELOG.md`, `PROJECT-KNOWLEDGE.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the new integration test project and the hermetic test support helpers everything else depends on.

- [x] T001 Create `tests/Spectra.Integration.Tests/Spectra.Integration.Tests.csproj` (net8.0, xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.3.0, coverlet.collector 8.0.1; ProjectReferences to `src/Spectra.CLI`, `src/Spectra.MCP`, `src/Spectra.Core`).
- [x] T002 Register the new project in `Spectra.slnx` under the `/tests/` folder.
- [x] T003 Add `<InternalsVisibleTo Include="Spectra.Integration.Tests" />` to `src/Spectra.CLI/Spectra.CLI.csproj` and `src/Spectra.MCP/Spectra.MCP.csproj` (CLI already has it for `Spectra.CLI.Tests`; mirror the pattern).
- [x] T004 Verify `tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj` builds (already has `InternalsVisibleTo`); confirm `dotnet build Spectra.slnx` succeeds with the empty new project before writing tests.

**Checkpoint**: Solution builds with the new (empty) integration project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared test-support types used by both Part A and Part B. **Blocks US1 and US2.**

- [x] T005 [P] Implement `tests/Spectra.Integration.Tests/Support/FakeAgentRuntime.cs` — deterministic `IAgentRuntime`: records `criteriaContext` (`LastCriteriaContext`), returns a configurable `GenerationResult` whose `Tests[0]` carries id/title/priority/tags/component and a populated `Criteria`; `IsAvailableAsync→true`; `ProviderName→"fake"`. Include a factory helper returning `AgentCreateResult.Succeeded(this)` for the `agentFactory` seam.
- [x] T006 [P] Implement `tests/Spectra.Integration.Tests/Support/OnDiskIndexLoader.cs` — `Func<string,IEnumerable<TestIndexEntry>>` that reads real `test-cases/{suite}/_index.json` (via `Spectra.Core` index reader / `MetadataIndex`) and maps to `TestIndexEntry` (id, file, title, priority, tags, component).
- [x] T007 [P] Implement `tests/Spectra.Integration.Tests/Support/IntegrationWorkspace.cs` — `IDisposable` temp project dir (mirrors `TempWorkspace`): creates `spectra.config.json`, `test-cases/`, `docs/`, `.spectra/`; helpers to seed a docs corpus, seed criteria files, build an `ExecutionEngine` + `ExecutionDb` over the temp dir, and construct real `StartExecutionRunTool`/`FindTestCasesTool` with `OnDiskIndexLoader`.

**Checkpoint**: Support types compile; ready for both test suites.

---

## Phase 3: User Story 1 — Named regression guards (Priority: P1) 🎯 MVP

**Goal**: Five tests whose displayed names are the original user symptoms; reverting a fix fails the matching one.
**Independent Test**: `dotnet test tests/Spectra.Integration.Tests --filter "FullyQualifiedName~OriginalBugRegression"` — all five pass; names are the symptom phrasings.

- [x] T008 [US1] Create `tests/Spectra.Integration.Tests/OriginalBugRegression.cs` class shell with using/namespace and shared helpers.
- [x] T009 [US1] `ParseFailure_DoesNotPoisonCache` — `[Fact(DisplayName="Original bug: cache poisoning on parse failure")]`. Drive `AnalyzeHandler.ExtractWithRetryAsync` with `NoOpDelayProvider` + stub returning `ParseFailure` then `Extracted`; assert parse-failure result `IsCacheable==false`, retried, success `IsCacheable==true`. (047)
- [x] T010 [US1] `BigProjectFirstIndex_WarnsWhenZeroCriteria` — `[Fact(DisplayName="Original bug: first big-project index produced zero criteria silently")]`. Assert `DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed>0, criteriaExtractedTotal=0)` returns a non-null warning naming the recovery command; and 0 indexed ⇒ null. (048)
- [x] T011 [US1] `ExtractCriteriaOnGeneration_PopulatesCriteriaField` — `[Fact(DisplayName="Original bug: extract-criteria on generation not working")]`. Seed criteria; via `UserDescribedGenerator.GenerateAsync(agentFactory: FakeAgentRuntime)` assert the fake received non-null `criteriaContext` and the returned `TestCase.Criteria` is populated. (050)
- [x] T012 [US1] `FromDescriptionTest_AppearsInIndexWithSameShape` — `[Fact(DisplayName="Original bug: from-description test has different format and is missing from index")]`. Generate (fake agent) → `TestPersistenceService.PersistAsync` → read `_index.json`; assert the new id is present and its entry shape (fields) matches peer entries. (049)
- [x] T013 [US1] `HighPriorityFilter_FromSuite_ReturnsOnlyHighPriority` — `[Fact(DisplayName="Original bug: high priority filter from a suite returns whole suite")]`. Seed a mixed-priority suite index; real `StartExecutionRunTool` with `priorities:["high"]`; assert `test_count` == high count, not full suite. (051)

**Checkpoint**: US1 complete — MVP regression net in place.

---

## Phase 4: User Story 2 — Cross-spec end-to-end (Priority: P1)

**Goal**: Seven `EndToEndScenarios` tests, each spanning 2+ specs against fixture data.
**Independent Test**: `dotnet test tests/Spectra.Integration.Tests --filter "FullyQualifiedName~EndToEndScenarios"`.

- [x] T014 [US2] Create `tests/Spectra.Integration.Tests/EndToEndScenarios.cs` class shell.
- [x] T015 [US2] `FromDescriptionHighPriority_RunsViaFilter_EndToEnd` (049+050+051): generate high-priority from-description test (fake agent, seeded criteria) → assert in `_index.json` → assert `Criteria` populated → real `start_execution_run priorities:["high"]` enqueues exactly it (exact count + id).
- [x] T016 [US2] `IndexDeployed_AfterFromDescription_FindTestCasesReturnsIt` (049): generate+persist → real `find_test_cases` returns the new test, no rebuild.
- [x] T017 [US2] `FilterSilentDrop_NoLongerOccurs` (051): send the three Path C requests (`{priority:"high"}`, `{priorities:["high"]}`, `{filters:{priorities:["high"]}}`); assert #2 filters correctly, #1/#3 return actionable field-named errors; none returns full-suite `test_count`.
- [x] T018 [US2] `CoverageGuards_FireOnRealisticZeroCorpus` (048): index a corpus whose extractions are all inconclusive (fake extractor returns empty/parse-failure) → assert success exit + populated `criteria_warning` in result.
- [x] T019 [US2] `GenerationNote_AppearsWhenNoCriteriaMatch` (048): generate against a no-matching-criteria suite → assert result `notes` contains the no-criteria message; assert present at quiet verbosity (inspect result object via `BuildNoCriteriaNote`, not stdout).
- [x] T020 [US2] `BatchGeneration_FromExtractedCriteria_PopulatesCriteriaField` (047+050): extract criteria (fake extractor) → load via `LoadCriteriaContextAsync` → fake agent asserts criteria forwarded → generated test `Criteria` reflects extracted ids.
- [x] T021 [US2] `LargeCorpusExtraction_NoSilentSkip_AfterPartialFailure` (047): via `ExtractCriteriaLoopAsync`, one doc fails first pass (uncached) then succeeds on re-run; assert re-attempted, not skipped.

**Checkpoint**: US2 complete — every Part A workflow passes.

---

## Phase 5: User Story 3 — Scale guard (Priority: P2)

**Goal**: Categorized large-corpus extraction guard proving per-document deadlines.
**Independent Test**: `dotnet test --filter "Category=Scale"`.

- [x] T022 [US3] Create `tests/Spectra.CLI.Tests/Extraction/ScaleTests.cs` with `SyntheticCorpusFactory` helper (N=30 default `DocumentEntry`, realistic content size, configurable per-doc latency/outcome).
- [x] T023 [US3] `LargeCorpus_PerDocumentDeadline_NotCorpusWide` — `[Fact][Trait("Category","Scale")]`. Mixed fast/slow docs through `ExtractCriteriaLoopAsync(perDocDeadline)`; assert succeeded>0, only designated slow docs timed out, elapsed consistent with per-document (not shared) budget.
- [x] T024 [US3] `LargeCorpus_SlowDocument_DoesNotAbortRemaining` — `[Fact][Trait("Category","Scale")]`. A slow doc times out without aborting subsequent docs.
- [x] T025 [US3] Confirm fast-feedback exclusion works: `dotnet test --filter "Category!=Scale"` excludes these; full `dotnet test` runs them.

**Checkpoint**: US3 complete — scale property guarded, excludable.

---

## Phase 6: User Story 4 — Documentation & SKILL audit (Priority: P2)

**Goal**: Audit report + reconciled docs + SKILL coherence with transcripts.
**Independent Test**: open `docs/specs/052-doc-audit-report.md`; confirm every file listed with disposition; "updated" files actually changed.

- [x] T026 [US4] Grep-walk `docs/` and read the in-scope files; record findings (stale pre-047 guidance, coherence gaps, missing migration notes) into a working list.
- [x] T027 [US4] Read all 15 SKILL files (`src/Spectra.CLI/Skills/Content/Skills/*.md`) + 2 agent files (`Content/Agents/*.agent.md`); note any pre-047 wording or removed escape hatches.
- [x] T028 [US4] Author `docs/specs/052-doc-audit-report.md` — table of every audited doc + SKILL/agent file with disposition (confirmed-current/updated/superseded) and notes; include a "follow-ups" section (e.g. command-level agent seam from research R2).
- [x] T029 [P] [US4] Update `docs/usage.md`, `docs/coverage.md`, `docs/test-format.md`, `docs/cli-reference.md`, the generic-MCP doc, `docs/skills-integration.md`, `docs/getting-started.md` per audit dispositions (only those marked "updated").
- [x] T030 [P] [US4] Update SKILL files per Part E: `spectra-generate.md` (render `notes`; from-description populates `criteria:` + indexed), `spectra-docs.md` (`criteria_warning`; default-ON extraction), `spectra-execution.agent.md` (one filter shape; actionable errors), `spectra-coverage.md` (`outcome` field), `spectra-criteria.md` (resilient extraction + recovery).
- [x] T031 [US4] Author `docs/specs/052-skill-transcripts.md` — one section per SKILL/agent file in scope: realistic prompt + representative rendered output (post-051 behavior) + coherence verdict. Label as representative renderings.

**Checkpoint**: US4 complete — docs/SKILLs coherent and evidenced.

---

## Phase 7: User Story 5 — Consolidated CHANGELOG & knowledge (Priority: P3)

**Goal**: One consolidated changelog entry + project-knowledge learning.
**Independent Test**: read `CHANGELOG.md` and `PROJECT-KNOWLEDGE.md`.

- [x] T032 [US5] Read current `CHANGELOG.md` head and `Directory.Build.props` version; confirm next version (`1.52.6`). Add one consolidated entry (Fixed/Added/Changed, user-facing, each line attributed `(#0NN)`) covering 047–051.
- [x] T033 [US5] Update `PROJECT-KNOWLEDGE.md`: add a Spec 052 row and a "silent-failure pattern" learning entry (lenient deserialization; returning a value for a required field; `catch { return []; }`).

**Checkpoint**: US5 complete.

---

## Phase 8: Polish & Validation

- [x] T034 Run `dotnet test --filter "Category!=Scale"` (fast pass) — all green.
- [x] T035 Run full `dotnet test` (incl. scale) — all green on the branch (SC-008).
- [x] T036 Verify deliverable acceptance: every "updated" file in the audit report is actually modified (git diff); changelog has exactly one consolidated entry; PROJECT-KNOWLEDGE has the row + learning.
- [x] T037 Run `dotnet build Spectra.slnx -warnaserror` (or review warnings) to ensure no new warnings from the integration project.

---

## Dependencies & Execution Order

- **Setup (T001–T004)** → blocks everything.
- **Foundational (T005–T007)** → blocks US1 (T011–T013 use FakeAgentRuntime/Persist) and all of US2. T009/T010 (pure-helper guards) depend only on Setup.
- **US1 (T008–T013)** and **US2 (T014–T021)** depend on Foundational; can proceed in parallel after T007.
- **US3 (T022–T025)** depends only on Setup (uses `Spectra.CLI.Tests`, internal loop) — independent of US1/US2.
- **US4 (T026–T031)** independent of the test phases (docs only) — can run anytime, but transcripts (T031) should reflect verified behavior, so ideally after US1/US2 confirm behavior.
- **US5 (T032–T033)** last (after behavior confirmed observable).
- **Polish (T034–T037)** after all.

### Parallel Opportunities
- T005, T006, T007 are different files → [P].
- Within US1, T009 & T010 (pure helpers) are independent of the fake-agent tests.
- T029 (docs) and T030 (SKILLs) touch different files → [P].
- US3 and US4 can run concurrently with US1/US2 (different files/assemblies).

## Implementation Strategy
- **MVP**: Setup + Foundational + US1 (the named regression net) — ship-readiness signal even alone.
- Then US2 (cross-spec), US3 (scale), US4 (docs/SKILLs), US5 (changelog).
- Commit after each phase. No production code changes anywhere (FR-028).
