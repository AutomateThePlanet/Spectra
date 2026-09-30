---
description: "Task list for Spec 079: stable acceptance-criteria identity"
---

# Tasks: Stable Acceptance-Criteria Identity (Update Workflow Phase 1)

**Input**: Design documents from `/specs/079-stable-criteria-ids/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R12, F1–F13), data-model.md, contracts/ (ingest-criteria.md,
docs-commands.md, extraction-prompt.md), quickstart.md

**Tests**: REQUIRED. The spec's Constraints and SC-011 list the allocator, ID-reuse validation, the normaliser, the
changeset, the migration, deleted/moved detection, retire and skill content. Write each story's tests first, and confirm
they fail.

**Organization**: tasks follow the spec's user stories.

| Story | Priority | Scope |
|---|---|---|
| US1 | P1 | Reworded criteria keep their ID |
| US2 | P1 | IDs unique and never reused |
| US3 | P1 | Changeset per ingest |
| US4 | P2 | Per-doc storage, migration, `source_doc` normalisation |
| US5 | P2 | Deleted and moved docs, plus the skill |

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1–US5

**Line endings**: many repo files are CRLF (`core.autocrlf=true`). Preserve each file's line ending when editing. Test
fixtures that build strings must normalise to LF explicitly (the 078 lesson: `FullFieldTestCase`).

---

## Phase 1: Setup

- [X] T001 From `fix/doc-change-update-workflow` state (branch `079-stable-criteria-ids`), run `dotnet build` and
  `dotnet test`, and record the baseline: Core 611, CLI 1355 (1 known failure, `ProfileFormatLoaderTests`), Execution 228,
  Integration 8.
- [X] T002 [P] Create the shared test fixture `tests/Spectra.CLI.Tests/TestFixtures/CriteriaWorkspace.cs`:
  - It is an `IDisposable` temp workspace containing:
    - `spectra.config.json` = `JsonSerializer.Serialize(SpectraConfig.Default)`
    - `docs/`, `docs/criteria/`, `test-cases/`
  - Helpers:
    - `WriteDoc(rel, text)`
    - `WriteIndexYaml(string yaml)`
    - `WriteCriteriaFile(relToCriteriaDir, yaml)`
    - `WriteTestIndex(suite, params (string id, string[] criteria)[])`, which writes `test-cases/{suite}/_index.json` with a
      `criteria` array
    - `ReadIndex()` via `CriteriaIndexReader`
    - `ReadCriteria(relToCriteriaDir)` via `CriteriaFileReader`
    - `Sha256(path)`
    - `RunAsync(params string[] args)`: sets the cwd, builds a `RootCommand` with `GlobalOptions`, `AiCommand.Create()`
      and `DocsCommand`, and captures stdout and stderr separately, returning `(int Exit, string Out, string Err)`
    - `Response(params object[] items)`, which serialises a model response to `.spectra/criteria.json` and returns its path
  - Tests using it go in `[Collection("WorkingDirectory")]`.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: The pure Core pieces, the locked store, and the classifier fix. Every story builds on these.

**⚠️ CRITICAL**: no user-story work starts before this phase is complete.

### Tests (write first)

- [X] T003 [P] Create `tests/Spectra.Core.Tests/Text/TextNormalizerTests.cs` with theory rows covering:
  - HTML entities (`&amp;`, `&nbsp;`) and tags (`<b>x</b>`, `<br/>`)
  - `**must**`, `__must__`, `*must*`, `_must_`, `~~x~~`, `` `code` ``
  - `[text](url)`, `[text][ref]`, `![alt](img.png)`, `<https://x.y>`
  - whitespace runs, tabs and newlines; trimming
  - lowercasing
  - untouched `snake_case_name`, `2 * 3`, `a_b_c`, `file*.md`
  - idempotence: `Normalize(Normalize(x)) == Normalize(x)`
  - culture invariance under `tr-TR` (`I` → `i`)
- [X] T004 [P] Create `tests/Spectra.Core.Tests/Criteria/CriteriaPathsTests.cs`:
  - `NormalizeSourceDoc` for `docs\x.md`, `./docs/x.md`, `/docs/x.md`, an absolute path inside the repo, and `docs//x.md`,
    each giving `docs/x.md`; outside the repo gives `INVALID_DOC_PATH` (a result, not an exception)
  - `DerivedFile("docs/a/login.md")` = `docs/a/login.criteria.yaml`
  - `DerivedFile("specs/x.txt")` = `specs/x.txt.criteria.yaml`
  - distinct docs map to distinct files
  - `IsOldLayout(index)`: true when any document source's `file` ≠ its derived path, false for imports only or an empty
    index
- [X] T005 [P] Create `tests/Spectra.Core.Tests/Criteria/CriteriaIdAllocatorTests.cs`:
  - `ParseId` handles `AC-CHECKOUT-013`, `AC-042`, `AC-MY-COMP-7`, and junk → null
  - the effective mark is the max of the stored mark and the scanned numbers
  - allocation continues above a stored mark higher than any file
  - allocation continues above file IDs when no mark is stored
  - formatting uses `D3`, widening when needed (`1000`)
  - allocating updates the mark
  - removing the highest ID and allocating again never reissues it
- [X] T006 [P] Create `tests/Spectra.Core.Tests/Criteria/CriteriaMatcherTests.cs`:
  - an explicit valid ID is kept
  - a foreign, unknown or duplicated ID → a failure listing the IDs
  - text fallback, one-to-one: two items with equal normalised text → the first matches, the second is unmatched
  - an explicit ID takes precedence over a text match for the same previous criterion
  - a kept ID with equal normalised text keeps the previous stored text verbatim
  - unmatched items are returned for allocation, in response order
- [X] T007 [P] Create `tests/Spectra.Core.Tests/Criteria/CriteriaChangesetTests.cs`:
  - added, modified (old/new), removed (old text), and metadata per field (priority, rfc2119, source_section, tags,
    technique_hint)
  - lists sorted by ID; `is_empty` for identical sets regardless of order
  - whitespace/markup-only text differences are not modified

### Implementation

- [X] T008 [P] Implement `src/Spectra.Core/Text/TextNormalizer.cs` (namespace `Spectra.Core.Text`): a static
  `Normalize(string?) → string` following data-model.md, using compiled `[GeneratedRegex]` patterns and
  `WebUtility.HtmlDecode`. Emphasis markers need a non-word char or a string boundary outside them and non-space inside.
  The result is lowercased with `ToLowerInvariant`. Document that it returns a comparison key.
- [X] T009 [P] Implement `src/Spectra.Core/Criteria/CriteriaPaths.cs` (namespace `Spectra.Core.Criteria`):
  - `SourceDocResult NormalizeSourceDoc(string input, string repoRoot)`, a record of `Path?` and `ErrorCode?`
  - `string DerivedFile(string normalizedSourceDoc)`, relative to the criteria dir with `/`; strip only a trailing
    case-insensitive `.md`
  - `bool IsOldLayout(CriteriaIndex)`, counting only `SourceType == "document"`
  - the constants `ErrorInvalidDocPath = "INVALID_DOC_PATH"` and `ErrorFileCollision = "CRITERIA_FILE_COLLISION"`
- [X] T010 [P] Add `[YamlMember(Alias = "id_high_water_marks")] public SortedDictionary<string, int> IdHighWaterMarks
  { get; set; } = new(StringComparer.Ordinal);` to `src/Spectra.Core/Models/Coverage/CriteriaIndex.cs`. Make sure the
  writer omits it when empty (keeping legacy output) and that `CriteriaIndexReader` reads it.
- [X] T011 [P] Implement `src/Spectra.Core/Criteria/CriteriaIdAllocator.cs`:
  - `static (string Prefix, int Number)? ParseId(string id)`: the prefix is everything before the last `-`; the number is
    the trailing digits
  - `static int EffectiveMark(CriteriaIndex, string prefix, IEnumerable<string> allExistingIds)`
  - `static IReadOnlyList<string> Allocate(CriteriaIndex index, string prefix, int count, IEnumerable<string>
    allExistingIds)`: mutates `index.IdHighWaterMarks[prefix]` and formats `{prefix}-{n:D3}`
- [X] T012 [P] Implement `src/Spectra.Core/Criteria/CriteriaMatcher.cs`:
  - `static MatchResult Match(IReadOnlyList<IncomingCriterion> incoming, IReadOnlyList<AcceptanceCriterion> previous)`
  - `IncomingCriterion` = `AcceptanceCriterion` + `string? RequestedId`
  - `MatchResult` = `{ bool IsValid, IReadOnlyList<string> InvalidIds, IReadOnlyList<(AcceptanceCriterion Criterion,
    string? KeptId)> Items }`
  - the algorithm follows data-model.md "Criterion matching" steps 1–3 and 5; step 4 (allocation) is done by the caller
- [X] T013 [P] Implement `src/Spectra.Core/Criteria/CriteriaChangeset.cs`:
  - records `CriteriaChangeset`, `AddedCriterion`, `ModifiedCriterion`, `RemovedCriterion` (with `ReferencingTests`,
    `TestIds`) and `MetadataChange`
  - `static CriteriaChangeset Build(string doc, IReadOnlyList<AcceptanceCriterion> previous,
    IReadOnlyList<AcceptanceCriterion> next)`, comparing text via `TextNormalizer`
  - `WithReferences(IReadOnlyDictionary<string, IReadOnlyList<string>> testsById)`
- [X] T014 [P] In `src/Spectra.Core/Parsing/CriteriaFileReader.cs`, add `ReadStrictAsync(path, ct)`: `[]` when the file is
  missing, throws `CriteriaFileUnreadableException(path, inner)` on a YAML or IO error. Keep `ReadAsync` unchanged for the
  lenient readers. Unit test in `tests/Spectra.Core.Tests/Parsing/CriteriaFileReaderWriterTests.cs`.
- [X] T015 [P] Make temp names unique (`{path}.{Guid:N}.tmp`) in `src/Spectra.Core/Parsing/CriteriaFileWriter.cs` and
  `src/Spectra.Core/Parsing/CriteriaIndexWriter.cs`. Add an optional `string? generatedAt` parameter so callers can reuse
  the existing header. Existing tests stay green.
- [X] T016 [P] Fix `src/Spectra.CLI/Extraction/CriteriaResponseClassifier.cs`:
  - add `[JsonPropertyName("source_section")]` to `SourceSection`
  - add `[JsonPropertyName("id")] public string? Id` to `ExtractedCriterion`, and carry it to the output as
    `RequestedId`. Either extend `CriteriaExtractionResult` with a parallel `IReadOnlyList<string?> RequestedIds`, or map
    it onto `AcceptanceCriterion.Id`; choose the latter, where an empty ID means none, and note it in a code comment
  - tests in `tests/Spectra.CLI.Tests/Agent/Copilot/CriteriaExtractorParseTests.cs`: `source_section` and `id` are read;
    absent → null or empty
- [X] T017 Implement `src/Spectra.CLI/Criteria/CriteriaStore.cs` (namespace `Spectra.CLI.Criteria`):
  - `static Task<T> WithLockAsync<T>(string repoRoot, Func<Task<T>> body, CancellationToken ct)`, using
    `FileLockHandle.AcquireAsync(Path.Combine(repoRoot, ".spectra", "criteria.lock"), TimeSpan.FromSeconds(10), ct)`;
    `TimeoutException` maps to the `LOCK_TIMEOUT` error
  - `LoadAsync(config, repoRoot)` → a `CriteriaWorkspaceState`:
    - the index
    - the resolved criteria dir
    - the IDs from every `*.criteria.yaml` under the criteria dir, read recursively
  - `ResolveFile(index, normalizedDoc)`: the index `file` entry, else `CriteriaPaths.DerivedFile`
  - `SaveAsync(state, …)`, which writes changed criteria files and the index
  - an auto-migration hook `Func<…>?`, wired in US4 (T052); null until then
- [X] T018 Add `.spectra/criteria.lock` to the gitignore `patterns` in `src/Spectra.CLI/Commands/Init/InitHandler.cs`, and
  extend `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs` (the Spec 078 gitignore test) to assert it.
- [X] T019 Run `dotnet test tests/Spectra.Core.Tests tests/Spectra.CLI.Tests`: T003–T007 pass and the baseline holds.

**Checkpoint**: the foundation is ready.

---

## Phase 3: User Story 1 - A reworded criterion keeps its ID (Priority: P1) 🎯 MVP

**Goal**: The prompt carries the existing criteria. Ingest honours valid IDs, falls back on normalised text, keeps stored
text, and writes nothing on a no-op (FR-001–FR-006, FR-013, FR-014).

**Independent Test**: Extract, reword one criterion, add one, drop one, then re-extract. The reworded criterion keeps its
ID, and a markup-only change leaves the file byte-identical (quickstart §1).

### Tests for US1

- [X] T020 [P] [US1] Extend `tests/Spectra.CLI.Tests/Extraction/ExtractionPromptCompilerTests.cs`:
  - with existing criteria, the prompt contains the `Existing criteria:` block and a compact JSON array in file order, in
    the key order `id`, `text`, `source_section`
  - two compilations are byte-identical
  - with none, the block is absent
  - only this doc's criteria appear, even if the index has other docs
- [X] T021 [P] [US1] Extend `tests/Spectra.CLI.Tests/Prompts/CriteriaExtractionTemplateTests.cs`: the built-in template
  references `{{existing_criteria}}` inside `{{#if existing_criteria}}`, documents an optional `id`, and states the reuse
  rule ("return that criterion's \"id\" unchanged", "Omit \"id\" for new requirements").
- [X] T022 [P] [US1] Create `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaIdentityTests.cs`, using
  `CriteriaWorkspace`:
  - **(a) Reworded keeps ID.** First ingest with 3 items (no IDs) → `AC-CHECKOUT-001..003`. Second ingest: item 1 reworded
    with `"id": "AC-CHECKOUT-001"`, item 2 `**MUST**` → `MUST` without an ID, item 3 dropped, one new item. Afterwards 001
    has the new text, 002 has the previous text verbatim, 003 is gone, and the new item is `AC-CHECKOUT-004`.
  - **(b) No-op.** Re-ingest the same response. The criteria file's SHA-256 is unchanged.
  - **(c) Markup-only.** Only `**`/whitespace changes, no IDs. File unchanged, same IDs.
  - **(d) Invalid IDs.** An ID from another doc → exit 7, stderr JSON `error_code: INVALID_CRITERION_ID` listing it, index
    and criteria files byte-identical. The same for a duplicated ID and for an unknown ID.
  - **(e) Unreadable file.** A corrupt existing criteria file → exit 1 `CRITERIA_FILE_UNREADABLE`, nothing written.
  - **(f) `source_section`.** It is persisted.
- [X] T023 [P] [US1] Add a test to `tests/Spectra.CLI.Tests/Commands/ExtractionCommandsTests.cs`: `compile-extraction-prompt`
  run against a workspace whose index lists the doc with an old base-name `file` includes those criteria, reading through
  the index, and moves no file.

### Implementation for US1

- [X] T024 [US1] Update `src/Spectra.CLI/Prompts/Content/criteria-extraction.md` per `contracts/extraction-prompt.md`:
  - add the `{{#if existing_criteria}}…{{/if}}` block with the reuse rule
  - add an optional `id` to the field list
  - add one example item with an `"id"`

  Keep every assertion from 078 and earlier template tests.
- [X] T025 [US1] In `src/Spectra.CLI/Extraction/ExtractionPromptCompiler.cs`, make `Compile` and `Assemble` take
  `IReadOnlyList<AcceptanceCriterion>? existing`, and render the `existing_criteria` value as a compact JSON array using
  `JsonSerializer` with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, no indentation, and `source_section` omitted when
  null. Pass `""` when empty. Use the same text in the lenient fallback prompt.
- [X] T026 [US1] Update `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs`:
  - load config leniently (default criteria paths when it's missing)
  - normalise `--doc` via `CriteriaPaths`
  - find the index entry by normalised `source_doc` and read its `file` with `CriteriaFileReader.ReadAsync` (lenient;
    compile never fails on a bad criteria file, it warns)
  - pass the criteria to the compiler
  - if `CriteriaPaths.IsOldLayout`, write one stderr warning; never write files
  - if the resolved template doesn't contain `existing_criteria`, write a stderr warning naming
    `spectra prompts reset criteria-extraction`
- [X] T027 [US1] Rewrite `IngestAsync` in `src/Spectra.CLI/Extraction/CriteriaIngestor.cs` on top of `CriteriaStore`
  (T017):
  1. normalise the doc, returning `INVALID_DOC_PATH` on failure
  2. classify
  3. inside `WithLockAsync`:
     - resolve the file via the index `file` entry (existing doc) or the derived path (new doc)
     - `ReadStrictAsync` the previous criteria
     - `CriteriaMatcher.Match`, returning `INVALID_CRITERION_ID` on failure
     - allocate new IDs with prefix `AC-{COMPONENT upper}`, using `CriteriaIdAllocator` against every existing ID in the
       workspace
     - set `SourceDoc` (normalised), `SourceType` and `Component`
     - build the changeset
     - no-op rule: if the changeset is empty and `doc_hash` is equal, write nothing; if the changeset is empty and the hash
       is new, update only the index entry; otherwise write the file and the index (with high-water marks)

  A dry run computes everything, including allocations against a copy of the index, and writes nothing. Extend
  `CriteriaIngestResult` with `ErrorCode`, `Changeset`, `Written`, `Doc` and `DuplicateIds`, plus a `Migration` field
  that stays null until US4.
- [X] T028 [US1] Update `src/Spectra.CLI/Commands/Generate/IngestCriteriaCommand.cs`:
  - pass the raw `--doc` to the ingestor, which normalises it
  - map the result to the exit codes in `contracts/ingest-criteria.md`, adding `ExitInvalidCriterionId = 7`
  - add `error_code` to the failure JSON (on stderr, unchanged otherwise)
  - add `doc` and `written` to the success JSON; the changeset fields come in US3
- [X] T029 [US1] Update the existing assertions in `tests/Spectra.CLI.Tests/Extraction/CriteriaIngestorTests.cs` that
  encoded the base-name file or `max+1` behaviour. List each one in the PR. Then run the US1 tests (T020–T023).

**Checkpoint**: IDs are stable across rewordings.

---

## Phase 4: User Story 2 - IDs are unique and never reused (Priority: P1)

**Goal**: The committed per-prefix high-water mark, uniqueness across docs and import, safe concurrency, duplicate
reporting, and `ai update` that doesn't crash (FR-007–FR-011).

**Independent Test**: Drop the highest criterion and add one: a higher number. Two docs with one component: distinct IDs.
Two parallel ingests: both entries present and no shared ID (quickstart §2).

### Tests for US2

- [X] T030 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaUniquenessTests.cs`:
  - **(a)** Drop `AC-CHECKOUT-003` (the highest) and add one → `AC-CHECKOUT-004`, never 003. The index
    `id_high_water_marks["AC-CHECKOUT"] == 4`.
  - **(b)** Delete the criteria file but keep the index mark, then re-ingest 1 item → `AC-CHECKOUT-005`, simulating a fresh
    state that only has the committed index.
  - **(c)** `docs/a/login.md` and `docs/b/login.md` (component `login`) → disjoint ID sets.
  - **(d)** Two `Task.WhenAll` ingests for different docs → both index entries present, with disjoint IDs.
  - **(e)** A workspace seeded with a duplicated ID in two files → the ingest result `duplicate_ids` lists the ID and both
    files, and nothing is renumbered.
- [X] T031 [P] [US2] Create `tests/Spectra.CLI.Tests/Criteria/CriteriaStoreLockTests.cs`: while an external
  `FileLockHandle` holds `.spectra/criteria.lock`, `WithLockAsync` with a short timeout maps to `LOCK_TIMEOUT`, and a writer
  that waits proceeds after release.
- [X] T032 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/Criteria/ImportCriteriaAllocationTests.cs`:
  - `ai analyze --import-criteria` of 3 new criteria with `Source: checkout` after an extraction issued `AC-CHECKOUT-001..002`
    → the import gets 003 and up, never a collision
  - re-importing the same file keeps its IDs and issues **no** new numbers (merge happens before allocation), and the mark
    is unchanged
- [X] T033 [P] [US2] Create `tests/Spectra.Core.Tests/Criteria/CriteriaDuplicateScannerTests.cs`: ID → files across
  files, case-insensitive; no duplicates → empty.
- [X] T034 [P] [US2] Add to `tests/Spectra.Core.Tests/Update/TestClassifierTests.cs`: criteria containing a duplicate ID
  classify without throwing, using the first definition.

### Implementation for US2

- [X] T035 [P] [US2] Implement `src/Spectra.Core/Criteria/CriteriaDuplicateScanner.cs`: `static
  IReadOnlyList<DuplicateCriterionId> Scan(IEnumerable<(string File, IReadOnlyList<AcceptanceCriterion> Criteria)>)`, sorted
  by ID. Expose it from `CriteriaStore.LoadAsync` state.
- [X] T036 [US2] Surface `duplicate_ids` in the ingest result JSON and the human output (`IngestCriteriaCommand.cs`,
  `CriteriaIngestor.cs`). It is a warning and never changes the exit code.
- [X] T037 [US2] Change `src/Spectra.Core/Update/TestClassifier.cs` (line ~51) to
  `criteria.GroupBy(c => c.Id, OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), OrdinalIgnoreCase)`. In
  `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`, after loading the criteria (~288-298), print one warning line naming
  each duplicated ID through `_progress.Warning`.
- [X] T038 [US2] Rework the import in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` `RunImportCriteriaAsync`
  (~390-536):
  - drop the pre-merge numbering (~386-471)
  - wrap read-merge-write in `CriteriaStore.WithLockAsync`
  - merge with `CriteriaMerger` first
  - then allocate IDs only for merged criteria that still have no ID, via `CriteriaIdAllocator`: prefix
    `AC-{SOURCE upper, spaces→-}` when `Source` is set, else `AC`, against every workspace ID
  - persist the index with high-water marks

  Keep the output file, the index entry shape and the exit codes.
- [X] T039 [US2] Run the US2 tests (T030–T034) and the existing import tests
  (`tests/Spectra.Core.Tests/Parsing/CriteriaMergerTests.cs`, `CsvCriteriaImporterTests.cs`, `JsonCriteriaImporterTests.cs`).

**Checkpoint**: no reuse, no duplicates, concurrency-safe.

---

## Phase 5: User Story 3 - Every ingest shows what changed (Priority: P1)

**Goal**: The changeset in the JSON result, referencing-test counts, and the human summary (stderr in JSON mode, stdout in
human mode). Dry run and no-op are covered (FR-012, FR-012a–c).

**Independent Test**: Ingest adds, rewords, drops and re-prioritises criteria, where the dropped one is referenced by 2
tests. Check the JSON changeset, `referencing_tests == 2` and the stderr summary. Re-ingest → empty changeset.

### Tests for US3

- [X] T040 [P] [US3] Create `tests/Spectra.CLI.Tests/Criteria/CriteriaReferenceCounterTests.cs`: two suites'
  `_index.json` referencing IDs with mixed casing → correct counts and test IDs; a missing or unreadable index is skipped
  with a warning.
- [X] T041 [P] [US3] Create `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaChangesetTests.cs`:
  - **(a)** In JSON mode, stdout parses as one JSON document containing `changeset.added`, `.modified` (old/new),
    `.removed` (with `referencing_tests: 2`, `test_ids`) and `.metadata` (priority medium → high). stderr contains the human
    summary lines `+`, `~`, `-` ("still referenced by 2 tests") and `*`.
  - **(b)** In human mode, the summary is on stdout.
  - **(c)** Re-ingest → `changeset.is_empty == true`, and the summary prints "No changes."
  - **(d)** `--dry-run` → the changeset is reported, `written: false`, and no file changed (hashes).
  - **(e)** A failure (exit 7) → stderr has only the failure JSON, with no summary lines.

### Implementation for US3

- [X] T042 [P] [US3] Implement `src/Spectra.CLI/Criteria/CriteriaReferenceCounter.cs`: `static
  Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadAsync(string repoRoot, SpectraConfig config, CancellationToken
  ct)`, reading every `{tests.dir}/*/_index.json` through `IndexWriter.ReadAsync` or `JsonSerializer`, mapping criterion
  ID (`OrdinalIgnoreCase`) → test IDs, plus warnings.
- [X] T043 [P] [US3] Implement `src/Spectra.CLI/Criteria/CriteriaChangesetPresenter.cs`: `static void Write(TextWriter w,
  CriteriaChangeset cs)`, in the exact line format of `contracts/ingest-criteria.md`, used by ingest and retire.
- [X] T044 [US3] Wire it up: `CriteriaIngestor` fills the changeset's references via `CriteriaReferenceCounter`.
  `IngestCriteriaCommand` emits `changeset` in the success JSON and calls the presenter, writing to `Console.Error` when
  `json`, else `Console.Out`. Failure paths never call the presenter.
- [X] T045 [US3] Run the US3 tests (T040–T041).

**Checkpoint**: every ingest reports its changeset.

---

## Phase 6: User Story 4 - Each doc's criteria live in their own file (Priority: P2)

**Goal**: Derived per-doc paths, the collision check, normalised `source_doc`, the explicit migration command,
auto-migration in writers, and warnings in readers (FR-015–FR-017).

**Independent Test**: `a/login.md` and `b/login.md` stay separate. Migrating twice changes nothing. `--doc docs\x.md` →
`docs changed` reports unchanged (quickstart §3).

### Tests for US4

- [X] T046 [P] [US4] Create `tests/Spectra.CLI.Tests/Criteria/CriteriaLayoutMigratorTests.cs`, seeding the old layout
  (`login.criteria.yaml`, `checkout.criteria.yaml`, `source_doc: docs\checkout.md`):
  - **(a)** Migrate → files at `docs/criteria/docs/login.criteria.yaml` etc., no old files left (recursive scan), index
    `file` updated, `source_doc` normalised in the index and inside the criteria, every ID preserved, marks seeded.
  - **(b)** A second run → `already_migrated: true`, and every file hash is unchanged.
  - **(c)** Two index entries sharing `login.criteria.yaml` → reported in `collisions` with both docs and the
    recommendation, and left untouched.
  - **(d)** `--dry-run` writes nothing.
  - **(e)** Import entries (`imported/…`) are untouched.
- [X] T047 [P] [US4] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsMigrateCriteriaTests.cs`: the command JSON shape and
  exit 0; human output lists the moves and collisions.
- [X] T048 [P] [US4] Create `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaStorageTests.cs`:
  - **(a)** `docs/a/login.md` and `docs/b/login.md` each get their own file, and neither overwrites the other.
  - **(b)** Ingest with `--doc docs\checkout.md`, then `DocsChangedHandler` → status `unchanged` for `docs/checkout.md`.
  - **(c)** An old-layout workspace + ingest → auto-migration runs first, `migration` in the result is non-null, and the
    doc is written at its derived path.
  - **(d)** A derived path already used by an import entry → exit 1 `CRITERIA_FILE_COLLISION`.
- [X] T049 [P] [US4] Extend `tests/Spectra.CLI.Tests/Commands/Docs/DocsChangedTests.cs`:
  - an index `source_doc: docs\x.md` or `./docs/x.md` with a matching hash → `unchanged`
  - an old layout → `migration_needed: true` plus a warning, and no file moved (hashes)

### Implementation for US4

- [X] T050 [US4] Implement `src/Spectra.CLI/Criteria/CriteriaLayoutMigrator.cs`: `static Task<MigrationReport>
  MigrateAsync(CriteriaWorkspaceState state, bool dryRun, CancellationToken ct)`, following research R7. It moves files
  (never copies), rewrites the `source_doc` of each criterion and the header, reports collisions and leaves them, seeds the
  marks, and is idempotent. `MigrationReport` is `{ Moved[], NormalizedSourceDocs, Collisions[], HighWaterMarksSeeded,
  AlreadyMigrated }`.
- [X] T051 [US4] Create `src/Spectra.CLI/Commands/Docs/DocsMigrateCriteriaCommand.cs` (`migrate-criteria`, `--dry-run`),
  following the `DocsChangedCommand` pattern. It runs under `CriteriaStore.WithLockAsync` and prints the JSON from
  `contracts/docs-commands.md` or a human report. Register it in `src/Spectra.CLI/Commands/Docs/DocsCommand.cs`.
- [X] T052 [US4] Wire auto-migration into `CriteriaStore`: when `IsOldLayout` and not a dry run, the write paths (ingest,
  import, retire, apply-move) call `CriteriaLayoutMigrator` inside the same lock before writing, and put the report in
  their result `migration`. Before a write, add the collision check: a derived path used by a different index source →
  `CRITERIA_FILE_COLLISION`.
- [X] T053 [US4] In `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs`, compare the normalised `source_doc` (via
  `CriteriaPaths`), add `migration_needed` and a `warnings` entry for the old layout, and never write.
- [X] T054 [US4] Update the existing tests that asserted base-name files or raw `source_doc` (`CriteriaIngestorTests.cs`,
  `ExtractionCommandsTests.cs`, `DocsChangedTests.cs`, `DataCollectorTests.cs` if affected). Then run the US4 tests
  (T046–T049).

**Checkpoint**: per-doc storage and a clean migration.

---

## Phase 7: User Story 5 - Deleted and moved docs are handled (Priority: P2)

**Goal**: `docs changed` reports deleted and moved docs, the `docs retire` and `docs apply-move` commands exist, and the
skill drives them with a changeset summary (FR-018–FR-022).

**Independent Test**: Delete a doc → deleted → retire dry run → retire. Move an unchanged doc → moved → apply-move keeps
the IDs (quickstart §4).

### Tests for US5

- [X] T055 [P] [US5] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsChangedDeletedMovedTests.cs`:
  - an indexed doc removed from disk → `deleted` with `criteria_count` and `current_hash: null`
  - an unchanged doc renamed → one `moved` entry with `from`, and no separate new/deleted
  - two new docs with the same hash as one deleted doc → no `moved` (ambiguous), new + deleted
  - an import source is never `deleted`
  - `duplicate_ids` are reported
- [X] T056 [P] [US5] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsRetireTests.cs`:
  - `--dry-run` → changeset with every criterion in `removed`, `referencing_tests` from two seeded `_index.json` files,
    `written: false`, no file changed
  - real run → index entry removed, criteria file deleted, marks unchanged, and a following `docs changed` no longer
    reports it
  - an existing doc → exit 4 `DOC_STILL_EXISTS`
  - an unknown doc → exit 4 `NOT_INDEXED`
  - the human summary is on stderr in JSON mode
- [X] T057 [P] [US5] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsApplyMoveTests.cs`:
  - a valid move → criteria file at the derived path of `to`, identical IDs and texts, `source_doc` updated in the index,
    criteria and header, and nothing compiled or extracted
  - `--dry-run` writes nothing
  - refusals, each exit 4: `from` still exists (`DOC_STILL_EXISTS`), `to` missing (`TARGET_MISSING`), `to` indexed
    (`TARGET_INDEXED`), hash mismatch (`CONTENT_CHANGED`)
- [X] T058 [P] [US5] Add to `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`. The `spectra-criteria` content:
  - handles `moved` via `spectra docs apply-move`
  - handles `deleted` via `spectra docs retire --dry-run`, then waits for explicit user confirmation before
    `spectra docs retire` without `--dry-run`
  - summarises changesets (added / modified / removed, tests still referencing removed IDs)

  Keep the 078 assertions ("it already contains the full document text", "do not\nread `{path}` separately").

### Implementation for US5

- [X] T059 [US5] Extend `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs`:
  - after walking the docs, add `deleted` entries for `source_type: document` sources whose normalised `source_doc` isn't
    on disk
  - pair each unique new ↔ deleted hash into `moved` entries (with `from`)
  - add `duplicate_ids` via `CriteriaDuplicateScanner`
  - extend `ChangedDocEntry` with `From` and `CriteriaCount`
  - human output rows per `contracts/docs-commands.md`
  - deterministic ordering (ordinal by path)
- [X] T060 [US5] Create `src/Spectra.CLI/Commands/Docs/DocsRetireCommand.cs`:
  - `retire --doc <path> [--dry-run]`, with a handler under `CriteriaStore.WithLockAsync` (auto-migration via T052)
  - preconditions and errors per the contract
  - changeset = `CriteriaChangeset.Build(doc, previous, [])` plus references
  - remove the index entry, delete the file, and never lower the marks
  - JSON output plus the presenter (stderr in JSON mode)
  - register it in `DocsCommand.cs`
- [X] T061 [US5] Create `src/Spectra.CLI/Commands/Docs/DocsApplyMoveCommand.cs`:
  - `apply-move --from <old> --to <new> [--dry-run]`, under the lock, with auto-migration
  - preconditions per the contract, with the hash via `FileHasher.ComputeFileHashAsync`
  - move the file to `CriteriaPaths.DerivedFile(to)`
  - rewrite `source_doc` in the index entry, the criteria and the header (keeping `doc_hash`), and keep IDs and every other
    field
  - register it in `DocsCommand.cs`
- [X] T062 [US5] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md`:
  - **Step 1** handles `moved` (run `spectra docs apply-move --from {from} --to {path} --output-format json`, no
    extraction) and `deleted` (run `spectra docs retire --doc {path} --dry-run --output-format json`, show the changeset
    including the referencing tests, ask the user, and only on an explicit yes run it without `--dry-run`)
  - **Step 2c** records each ingest's `changeset` from stdout JSON
  - **Step 3** prints a combined summary per doc: added / modified / removed, and the tests still referencing removed IDs
  - keep the 078 wording in Step 2b and the `.spectra-result.json` mention
- [X] T063 [US5] Run the US5 tests (T055–T058).

**Checkpoint**: deleted and moved docs are handled end to end.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T064 [P] Add a `## [2.6.0] - <date>` entry at the top of `CHANGELOG.md`, in the 2.5.0 style:
  - **Changed**: stable IDs via existing criteria and ID reuse; exit 7; changeset in the result, with a human summary on
    stderr in JSON mode; per-doc storage; `source_doc` normalisation; the `docs changed` deleted/moved/duplicates/
    migration warning; the new `docs retire`, `docs apply-move` and `docs migrate-criteria`; the committed
    `id_high_water_marks`; `.spectra/criteria.lock`; import allocation
  - **Fixed**: ID reuse and duplicates; base-name collisions; `source_section` being dropped; an unreadable file treated as
    empty; the `ai update` crash on duplicates; the unlocked read-modify-write
  - **Notes**: a one-time `source_section` metadata churn on the first re-extraction; custom `criteria-extraction`
    templates need `spectra prompts reset criteria-extraction`; how to migrate
- [X] T065 [P] Update `docs/cli-reference.md`:
  - `ai ingest-criteria`: the `id` input, the changeset result, exit 7, stderr summary
  - `ai compile-extraction-prompt`: existing criteria
  - `docs changed`: the new statuses and fields
  - new sections for `docs retire`, `docs apply-move`, `docs migrate-criteria`
  - the storage layout
- [X] T066 [P] Update the criteria/coverage docs: the "Extracting Acceptance Criteria" section in `docs/usage.md` (keep
  every heading `ProfileFormatLoaderTests` asserts), plus `docs/coverage.md` or the criteria doc if present (find it with
  `grep -rl "criteria.yaml" docs/`). Describe stable IDs, changesets, deleted/moved handling and the per-doc layout.
- [X] T067 [P] Update `CLAUDE.md`: add a `079-stable-criteria-ids (v2.6.0)` bullet at the top of "Recent Changes" in the
  078 style, and add the new `docs` commands to the Commands block.
- [X] T068 [P] Bump `<Version>` in `Directory.Build.props` from 2.5.0 to 2.6.0.
- [X] T069 Run the full `dotnet build` and `dotnet test`: all green apart from the known `ProfileFormatLoaderTests`
  failure. Record the counts in T067.
- [X] T070 Run `quickstart.md` §1–§4 in a scratch git workspace under the session scratchpad with `dotnet run --no-build
  --project src/Spectra.CLI --`, and record the results.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: after Setup. **Blocks every story.**
- **US1 (Phase 3)**: after Phase 2.
- **US2 (Phase 4)**: after Phase 2. It shares `CriteriaIngestor.cs` / `IngestCriteriaCommand.cs` with US1, so start its
  implementation after T027–T028. The US2 tests can be written in parallel with US1.
- **US3 (Phase 5)**: after US1 (T027–T028), since the changeset is wired into the ingest result.
- **US4 (Phase 6)**: after US1. T052 edits `CriteriaStore` (T017) and the write paths.
- **US5 (Phase 7)**: after US3 (presenter, reference counter) and US4 (auto-migration, `DocsChangedHandler` changes from
  T053).
- **Polish (Phase 8)**: after all stories.

### Shared-file ordering

| File | Edited by, in order |
|---|---|
| `src/Spectra.CLI/Extraction/CriteriaIngestor.cs` | T027 → T036 → T044 → T052 |
| `src/Spectra.CLI/Commands/Generate/IngestCriteriaCommand.cs` | T028 → T036 → T044 |
| `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs` | T053 → T059 |
| `src/Spectra.CLI/Commands/Docs/DocsCommand.cs` | T051 → T060 → T061 |
| `src/Spectra.CLI/Criteria/CriteriaStore.cs` | T017 → T035 → T052 |
| `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs` | T058 only |

### Within each story

Tests come first, then implementation, then the checkpoint run.

## Parallel Opportunities

- **Phase 2**:
  - tests T003–T007
  - pure implementations T008–T016, which are all different files
  - then T017, then T018
- **US1**: tests T020–T023 in parallel; implementation T024 → T025 → T026, then T027 → T028.
- **US2**: tests T030–T034 in parallel; T035 and T037 in parallel with T038.
- **US3**: T040 ∥ T041; T042 ∥ T043, then T044.
- **US4**: T046–T049 in parallel; T050 → T051, then T052, then T053.
- **US5**: T055–T058 in parallel; T059 → T060 → T061 → T062.
- **Polish**: T064–T068 in parallel.

### Parallel example: Phase 2 pure pieces

```text
Task: "T008 TextNormalizer in src/Spectra.Core/Text/TextNormalizer.cs"
Task: "T009 CriteriaPaths in src/Spectra.Core/Criteria/CriteriaPaths.cs"
Task: "T011 CriteriaIdAllocator in src/Spectra.Core/Criteria/CriteriaIdAllocator.cs"
Task: "T012 CriteriaMatcher in src/Spectra.Core/Criteria/CriteriaMatcher.cs"
Task: "T013 CriteriaChangeset in src/Spectra.Core/Criteria/CriteriaChangeset.cs"
Task: "T016 classifier id/source_section in src/Spectra.CLI/Extraction/CriteriaResponseClassifier.cs"
```

### Parallel example: US5 tests

```text
Task: "T055 DocsChangedDeletedMovedTests"   Task: "T056 DocsRetireTests"
Task: "T057 DocsApplyMoveTests"             Task: "T058 skill assertions"
```

## Implementation Strategy

### MVP

The MVP is Phase 1 + Phase 2 + **US1** + **US2**. Rewordings keep their IDs and no ID is ever reused or duplicated. This
alone stops test ↔ criterion links breaking silently.

### Incremental delivery

1. Foundation → US1 (stable IDs) → US2 (uniqueness). This is the MVP.
2. US3: the changeset becomes visible, which is the hand-off to Phase 2a.
3. US4: per-doc storage and migration.
4. US5: deleted/moved docs, plus the skill.
5. Polish.

Deliver it as one local commit on `079-stable-criteria-ids` (`feat(079): …`), then merge with `--no-ff` into
`fix/doc-change-update-workflow`. Don't push (repo convention for this workflow).
