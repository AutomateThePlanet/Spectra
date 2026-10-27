---
description: "Task list for Spec 078: lossless test writes and doc-reference resolution"
---

# Tasks: Lossless Test Writes and Doc-Reference Resolution (Update Workflow Phase 0)

**Input**: Design documents from `/specs/078-lossless-test-writes/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: REQUIRED. The spec's constraints and SC-009 ask for four things: a writer round-trip test over every field,
resolver unit tests, an UpdateHandler orphan-marking test, and a default-profile ref regression test. Within each story,
write the test tasks first and confirm they fail before implementing.

**Organization**: Tasks are grouped by the user stories in spec.md:

| Story | Priority | Scope |
|---|---|---|
| US1 | P1 | Lossless writes |
| US2 | P1 | Ref resolution |
| US3 | P1 | Classify without writes |
| US4 | P2 | Orphan marking |
| US5 | P3 | Criteria single read |

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: the user story (US1–US5)

Research references (R1–R10, F1–F15) point to `research.md`. Guarantees G1–G6 point to
`contracts/test-file-format.md`.

---

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Establish the baseline and shared fixtures that several stories use.

- [X] T001 Run `dotnet build` and `dotnet test` from the repo root to record the baseline. It needs SDK 9.0.x, and 9.0.318
  is now installed. Expected: Core 568, CLI 1276, Execution 228 and Integration 8 tests. One CLI test is a known
  pre-existing failure unrelated to 078: `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown` in
  `tests/Spectra.CLI.Tests/Profile/ProfileFormatLoaderTests.cs:129`, which expects the heading "Executing Tests via MCP".
  Do not fix it in this spec unless the user approves. Record the counts in the PR description.
- [X] T002 [P] Create the test-only deep comparer `TestCaseEquivalence` in
  `tests/Spectra.CLI.Tests/TestFixtures/TestCaseEquivalence.cs`:
  - Provide a static `AssertEquivalent(TestCase expected, TestCase actual, bool includeLayout = true)`.
  - Compare every `TestCase` property except `FilePath`.
  - Compare `IReadOnlyList` members by sequence.
  - Compare `Custom` by deep structure, recursing into `Dictionary<object,object>`, `List<object>` and scalars.
  - Compare `Grounding` field by field, including list members by sequence.
  - When `includeLayout` is set, compare `Layout` by `Newline`, the `FrontmatterEntries` (key, raw text) sequence and the
    `BodySegments` (kind, raw text) sequence.
  - On failure, report the first differing property name.
- [X] T003 [P] Create the shared fixture `FullFieldTestCase` in `tests/Spectra.CLI.Tests/TestFixtures/FullFieldTestCase.cs`.
  It exposes one `const string Markdown`, a test file with:
  - **Every known frontmatter key** (see the key order in data-model.md), with non-empty values:
    - `id`, `priority: high`, `tags` (2 items)
    - `component`, `description`, `environment` (2 items)
    - `estimated_duration: 1h30m15s`, `depends_on`
    - `source_refs: ["docs/checkout.md#Payment"]`, `scenario_from_doc`
    - `related_work_items`, `criteria` (2 ids), `requirements`, `bugs`, `automated_by`
    - `custom` with a nested map, a list, a quoted numeric string `'5'`, a boolean and a multi-line literal block
    - `status: orphaned`, `orphaned_reason`, `orphaned_date: 2026-01-15`
    - a `grounding` block with every field: verdict `partial`, `score: 0.873`, `generator`, `critic`, `verified_at` with
      milliseconds, `unverified_claims`, `flagged_for_review`, `repair_attempts`, `repaired`, `condensed_findings`,
      `source`, `created_by`, `note`
  - **Unknown content**:
    - two unknown top-level keys, `owner: qa-team` and `x_ticket: {id: 42, system: jira}`, placed between known keys
    - a YAML comment line
  - **Body**:
    - text before the first `##`
    - `## Preconditions`
    - `## Steps` with 3 numbered steps, the second followed by an indented `- sub-bullet` line
    - an unknown `## Notes` section between Steps and Expected Result
    - `## Expected Result`
    - `## Test Data`

  Also expose `CrlfMarkdown`, the same content with CRLF line endings. Also expose `CanonicalMarkdown`: the fixture as
  SPECTRA itself would write it, with keys in the fixed order and no unknown content. US4 uses it for exact-diff checks.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `TestCase` model change that US1 and US4 build on. It goes first so any fallout from the `class` →
`record` switch shows up before other edits (research R4).

**⚠️ CRITICAL**: US1 and US4 cannot start until this phase is complete. US2, US3 and US5 do not depend on it.

- [X] T004 Change `public sealed class TestCase` to `public sealed record TestCase` in
  `src/Spectra.Core/Models/TestCase.cs`, and add `public TestCaseLayout? Layout { get; init; }` with an XML doc comment.
  Create `src/Spectra.Core/Models/TestCaseLayout.cs` with the types from data-model.md:
  - `public sealed record TestCaseLayout(TestCase Baseline, IReadOnlyList<FrontmatterEntry> FrontmatterEntries, IReadOnlyList<BodySegment> BodySegments, string Newline)`
  - `public sealed record FrontmatterEntry(string Key, string RawText, bool IsKnown)`
  - `public sealed record BodySegment(BodySegmentKind Kind, string RawText)`
  - `public enum BodySegmentKind { Leading, Title, Intro, Preconditions, Steps, ExpectedResult, TestData, Unknown }`

  The parser does not fill `Layout` yet.
- [X] T005 Run `dotnet build` and the full `dotnet test`, and fix any compile or test fallout from the record conversion
  **without changing behaviour**, for example code relying on reference equality or `ToString()`. Expected result: the
  same counts as T001.

**Checkpoint**: the model is ready, and the suite matches the baseline.

---

## Phase 3: User Story 1 - Rewriting a test never loses data (Priority: P1) 🎯 MVP

**Goal**: Every writer path preserves every field, unknown key and body section. Output is byte-identical on a second
write (FR-001–FR-005, research R1–R7).

**Independent Test**: Parse `FullFieldTestCase.Markdown`, rewrite it through each writer path, re-parse it, and assert
equivalence and byte-identity.

### Tests for User Story 1 (write first, confirm they fail)

- [X] T006 [P] [US1] Create layout invariant tests in `tests/Spectra.Core.Tests/Parsing/TestCaseLayoutTests.cs`. Use an
  inline fixture string, since Core.Tests has no reference to CLI.Tests. Assert that:
  - (a) the concatenated `FrontmatterEntries[i].RawText` equals the original frontmatter text exactly
  - (b) the concatenated `BodySegments[i].RawText` equals the original body exactly
  - (c) unknown keys `owner` and `x_ticket` appear with `IsKnown=false`, in their original relative order
  - (d) a `---` line inside a block scalar is kept, which fixes the old `ExtractYamlContent` behaviour
  - (e) flow collections, block scalars and a comment line are all captured inside entry slices
  - (f) a second `## Steps` section is classified `Unknown`
  - (g) `## Test Steps` and `## Pre-conditions` headers are classified as known kinds
  - (h) `Newline` is `"\r\n"` for CRLF input and `"\n"` otherwise
  - (i) `Layout.Baseline.Layout` is null, and its values equal the returned test's values
- [X] T007 [P] [US1] Add parser tests to `tests/Spectra.Core.Tests/Parsing/TestCaseParserTests.cs`:
  - `status`, `orphaned_reason` and `orphaned_date` are read, with `orphaned_date` parsed as 2026-01-15 at midnight UTC.
  - An unparseable `orphaned_date` gives null.
  - For a manual `grounding` verdict with `verified_at`, that value is used.
  - For a manual verdict without `verified_at`, the value is `DateTimeOffset.MinValue`, and two parses of the same file
    give equal `VerifiedAt`.
  - For a manual verdict, the flagged, repair and findings fields are mapped.
  - Grounding `source`, `created_by` and `note` map to `GroundingMetadata.Source`, `CreatedBy` and `Note`.
- [X] T008 [P] [US1] Create writer round-trip tests in `tests/Spectra.CLI.Tests/IO/TestFileWriterRoundTripTests.cs`.
  Use `TestCaseParser` + `TestFileWriter.FormatTestCase` + `TestCaseEquivalence`.
  - **G1**: `parse(write(parse(FullField)))` is equivalent to `parse(FullField)`, for both LF and CRLF.
  - **G2**: `write(parse(write(t)))` is byte-equal to `write(t)`, for the fixture and for a layout-less in-memory test.
  - **G3**: writing an unmodified parsed `CanonicalMarkdown` reproduces the input byte for byte.
  - **G4**: `owner` and `x_ticket` are emitted verbatim after all known keys, and `## Notes` stays between Steps and
    Expected Result. When `Steps` is changed via `with`, the Steps section is re-rendered canonically, `## Notes` stays in
    place, and the sub-bullet disappears only because Steps changed.
  - **G5**: CRLF input gives CRLF output. A layout-less test gives `\n` output.
  - **G6**: `[Theory]` over changed values for `component`, a tag, a `source_ref`, `scenario_from_doc`, `description`
    and a step. Each value must round-trip exactly: `a: b`, `x #y`, `- lead`, `[br`, `*star`, `&amp`, `!bang`, `|pipe`,
    `>gt`, `%pct`, `@at`, a leading backtick, a leading `"quote"`, `C:\path\to`, `line1\r\nline2`, `~`, `null`, `true`
    and `5`.
  - **Culture**: with `CultureInfo.CurrentCulture = de-DE`, a changed grounding writes `score: 0.873` and a UTC
    `verified_at` with fraction; with a zero fraction there is no `.` at all.
  - **Duration**: `TimeSpan.FromSeconds(90)` is written as `1m30s`.
  - **Layout-less output**: a layout-less test writes `criteria: []` and keys in data-model.md order.
  - **Absent keys**: an unchanged parsed file that had no `criteria` key does not gain one.
  - **Flow style**: an unchanged file with flow-style `tags: [a, b]` keeps that exact line.
  - **Invalid priority**: an unchanged file with `priority: urgent` keeps `priority: urgent`.
- [X] T009 [P] [US1] Create writer-path preservation tests in `tests/Spectra.CLI.Tests/IO/WriterPathPreservationTests.cs`.
  Use a temp workspace (`tests/Spectra.CLI.Tests/TestFixtures/TempWorkspace.cs`) seeded with `FullFieldTestCase.Markdown`
  as `test-cases/smoke/TC-100.md`.
  - **`TestPersistenceService`** (`src/Spectra.CLI/IO/TestPersistenceService.cs`): persist the parsed test unchanged. The
    file is byte-identical and the re-parse is equivalent.
  - **ingest-update** (`UpdatedTestIngestor.IngestAsync` in `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs`), with an
    edited candidate that changes only Steps:
    - `EstimatedDuration`, `AutomatedBy`, `Requirements`, `Bugs`, `Custom`, `Description`, `Environment`,
      `RelatedWorkItems`, `Grounding`, the lifecycle fields, `owner`, `x_ticket` and `## Notes` are all preserved
    - only the Steps section differs
  - **`GroundingWriteBackService.WriteAsync`** (`src/Spectra.CLI/IO/GroundingWriteBackService.cs`): only the `grounding:`
    entry lines differ from the original. Diff the file line by line.
  - **`ReviewFlaggedHandler` accept** (`src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`): only the `grounding:`
    entry differs.
  - **`BatchWriteTestsTool`** (`src/Spectra.CLI/Agent/Tools/BatchWriteTestsTool.cs`): a new test with every typed field
    set round-trips with equivalent values.
  - **Coverage**: after grounding write-back, `automated_by` is still present and `LinkReconciler` still links the test
    (`src/Spectra.Core/Coverage/LinkReconciler.cs`).

### Implementation for User Story 1

- [X] T010 [P] [US1] Add `[YamlMember(Alias="status")] string? Status`, `[YamlMember(Alias="orphaned_reason")] string?
  OrphanedReason` and `[YamlMember(Alias="orphaned_date")] string? OrphanedDate` to
  `src/Spectra.Core/Models/TestCaseFrontmatter.cs`.
- [X] T011 [P] [US1] Update the grounding model (research R5):
  - Add `string? Source`, `string? CreatedBy` and `string? Note` to `src/Spectra.Core/Models/Grounding/GroundingMetadata.cs`.
  - In `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs` `ToMetadata`, map `source`, `created_by` and `note`
    for every verdict.
  - In the manual branch:
    - parse `verified_at` when present, and fall back to `DateTimeOffset.MinValue` otherwise (not `UtcNow`)
    - map `flagged_for_review`, `repair_attempts`, `repaired` and `condensed_findings` like the other branches
    - keep `Score=1.0`, `Generator="user"` and `Critic="none"`
- [X] T012 [US1] Update `src/Spectra.Core/Parsing/MarkdownFrontmatterParser.cs` so the typed parse result also exposes
  the **raw frontmatter text** and the **raw body**:
  - The frontmatter text is the exact slice between the opening `---` line and the closing `---` line, found by the
    Markdig `YamlFrontMatterBlock` span.
  - The body is everything after the closing delimiter line, with the delimiter's own line break removed and **no**
    further trimming.
  - Replace `ExtractYamlContent`'s "drop every `---` line" logic with the slice, so the DTO deserializes the same text.
  - Existing callers of `Parse<T>` keep working.
- [X] T013 [US1] Build the layout in `src/Spectra.Core/Parsing/TestCaseParser.cs`. Depends on T010–T012.
  - **Frontmatter entries**: run `YamlDotNet.Core.Parser` over the raw frontmatter text. For each depth-1 mapping key
    `Scalar`, record `Start.Index`. Each entry's `RawText` runs from its key start to the next key's start, or to the end.
    Leading text before the first key (comments) goes into the first entry. Set `IsKnown` from the data-model key list.
  - **Body segments**: split on the first `^#\s+` H1 line and on each `^##\s+` line, and classify with the existing header
    regexes. Only the first match of each known kind counts; later ones are `Unknown`.
  - **Newline**: detect it from the first line break in the file.
  - **Lifecycle fields**: map `Status`, `OrphanedReason` and `OrphanedDate`. `OrphanedDate` uses `yyyy-MM-dd` with
    `CultureInfo.InvariantCulture`, becomes a `DateTimeOffset` at midnight with offset zero, and is null on failure.
  - **Baseline**: build the `TestCase` as today into `baseline`, then return
    `baseline with { Layout = new TestCaseLayout(baseline, entries, segments, newline) }`.
  - Keep the existing extraction semantics for title, steps, preconditions, expected result and test data unchanged.
- [X] T014 [US1] Rewrite `src/Spectra.CLI/IO/TestFileWriter.cs` `FormatTestCase` as the baseline-diff serialiser
  (research R1, R3, R7; data-model.md). Depends on T013.
  - **Frontmatter**:
    - Iterate the 21 known keys in the fixed order.
    - **Unchanged** vs `Layout.Baseline` (value equality for scalars and enums; sequence equality for lists; reference
      equality for `Custom` and `Grounding`): emit the entry's `RawText` if the key was present in the original, and
      nothing if it was absent.
    - **Changed, or no layout**: emit canonically by building YamlDotNet `YamlNode`s and running them through `Emitter`
      with implicit document start and end.
      - Lists use block style with plain-where-safe scalars.
      - Free-text fields use `ScalarStyle.DoubleQuoted`: `description`, `scenario_from_doc`, `orphaned_reason`, grounding
        `unverified_claims`, `condensed_findings.*` and `note`.
      - `custom` is emitted from its object graph.
      - `score` uses `ToString("0.00##############", CultureInfo.InvariantCulture)`.
      - `verified_at` uses `UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", CultureInfo.InvariantCulture)`.
      - `orphaned_date` uses `yyyy-MM-dd`.
      - Duration is `{h}h{m}m{s}s` with zero parts omitted, and `0s` for zero.
      - Empty lists and nulls are omitted, except `criteria: []` for a layout-less test.
    - Frontmatter `preconditions` is emitted only as raw when present and unchanged. When Preconditions changed, it lives
      in the body only.
    - Then emit the unknown entries verbatim, in original order.
  - **Body**:
    - Walk `Layout.BodySegments`. `Leading`, `Intro` and `Unknown` are verbatim. `Title` is raw if unchanged, else
      `# {Title}`.
    - Each known section is raw if unchanged, re-rendered in place if changed, and dropped if now empty.
    - Known sections that are new in the model are inserted after the nearest preceding known section, in canonical
      order.
    - With no layout, render today's canonical body.
  - **Newline**: use `Layout.Newline`, or `"\n"` without a layout.
  - Delete `EscapeYamlString` and the old `FormatDuration`.
- [X] T015 [P] [US1] Replace `CopyWithGrounding` in `src/Spectra.CLI/IO/GroundingWriteBackService.cs` with
  `original with { Grounding = newGrounding }`, and delete the helper.
- [X] T016 [P] [US1] Replace `CopyWithGrounding` in `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs` with a
  `with` expression, and delete the helper.
- [X] T017 [P] [US1] Rewrite `ApplyEdit` in `src/Spectra.CLI/Generation/UpdatedTestIngestor.cs` as
  `original with { Title, Preconditions, Steps, ExpectedResult, TestData, ScenarioFromDoc, SourceRefs, Criteria }`, taking
  those values from the candidate. This keeps `EstimatedDuration` and `Layout` from the original (F7). Correct the class
  doc comment's claim about grounding notes (lines 18–20). The drift guard is unchanged.
- [X] T018 [US1] Audit that no other writer path rebuilds a parsed test field by field. Grep `new TestCase` under
  `src/Spectra.CLI`, and confirm that `src/Spectra.CLI/IO/TestPersistenceService.cs` and
  `src/Spectra.CLI/Agent/Tools/BatchWriteTestsTool.cs` pass tests through unchanged. Convert any other copy of a parsed
  test to `with`. `UpdateHandler`'s orphan branch is US4 (T041).
- [X] T019 [US1] Update existing assertions that encoded the old writer output:
  - `tests/Spectra.CLI.Tests/IO/TestFileWriterTests.cs`
  - `tests/Spectra.CLI.Tests/IO/TestFileWriterGroundingTests.cs`
  - seeding in `tests/Spectra.CLI.Tests/IO/GroundingWriteBackServiceTests.cs` and
    `tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdate*Tests.cs`

  Change only assertions that relied on a bug, such as a culture-dependent score or a missing field, and list each one in
  the PR. Then run `dotnet test tests/Spectra.Core.Tests tests/Spectra.CLI.Tests`: T006–T009 pass and the baseline holds.

**Checkpoint**: US1 is complete, and every writer path is lossless (SC-001, SC-002, SC-005b).

---

## Phase 4: User Story 2 - Update classifies tests against the docs they actually reference (Priority: P1)

**Goal**: One resolver (FR-011) is used by `ai update` and by the critic, repair-prompt and repair-batch loaders.
ORPHANED "not found" only when no ref resolves (FR-012–FR-015, research R9,
`contracts/source-ref-resolution.md`).

**Independent Test**: Tests with `docs/<f>.md#<S>` refs to existing docs get 0 orphans. Deleting a test's only doc
orphans exactly that test.

### Tests for User Story 2 (write first, confirm they fail)

- [X] T020 [P] [US2] Create `tests/Spectra.Core.Tests/Coverage/SourceRefResolverTests.cs`:
  - A `[Theory]` over every row of the examples table in `contracts/source-ref-resolution.md`, using a temp dir with
    `docs/a.md`, and building fully-qualified refs from the temp root at runtime.
  - When both `a.md` and `docs/a.md` exist, `a.md` resolves to the repo root.
  - `DistinctFullPaths` deduplicates `docs/a.md#X`, `docs\a.md` and `docs/a.md#Y` to one path.
  - The original order is preserved in `Resolved` and `Missing`.
  - `Resolve` never throws, for null-free inputs including `""`, `"#"`, `"../../x"` and invalid path characters.
    Invalid characters give `Missing(NotFound)`.
  - `AnyResolved` is correct.
- [X] T021 [P] [US2] Add tests to `tests/Spectra.Core.Tests/Update/TestClassifierTests.cs`:
  - A test with 2 refs, one present and one absent from the dictionary, is **not** ORPHANED.
  - A test with 2 present refs is classified against both docs' text. Assert that a word only in the second doc counts
    toward relevance.
  - With no ref present, the result is ORPHANED "Source document not found or empty", with the reason string unchanged.
- [X] T022 [P] [US2] Create the default-profile regression test
  `tests/Spectra.CLI.Tests/Commands/UpdateDefaultProfileRefsTests.cs`:
  - **Setup**: in a `TempWorkspace` with `spectra.config.json` (default `source.local_dir`), `docs/checkout.md` and
    `docs/login.md`, create suite `checkout` with 4 tests. Their refs are `docs/checkout.md#Payment`, `docs\checkout.md`,
    `checkout.md` (legacy), and `docs/login.md#Form` plus `docs/checkout.md#Payment`.
  - Run `spectra ai update checkout --no-review` through the CLI entry point used in
    `tests/Spectra.CLI.Tests/Commands/UpdateCommandTests.cs`.
  - Assert that **no** test file contains `status: orphaned`. Use raw text, so the test is independent of US1.
  - Restore the files, delete `docs/checkout.md`, and re-run. Exactly the 3 tests that reference only `checkout.md`
    contain `status: orphaned`; the 4th, which still has `docs/login.md`, does not.
- [X] T023 [P] [US2] Add critic-prompt ref tests to `tests/Spectra.CLI.Tests/Commands/CriticVerificationCommandsTests.cs`:
  - Run `compile-critic-prompt` without `--docs` for a test whose refs are `docs/a.md#S`, `docs\b.md` and the legacy
    `c.md`, which lives at `docs/c.md`. All three documents are embedded, each once.
  - A ref to a missing file exits 1, with `Source ref not found: {ref}` on stderr.
  - A ref `../outside.md` exits 1.

### Implementation for User Story 2

- [X] T024 [US2] Create `src/Spectra.Core/Coverage/SourceRefResolver.cs` per data-model.md and the rules in
  `contracts/source-ref-resolution.md`:
  - `SourceRefResolver(string repoRoot, string sourceDir)` and `SourceRefResolution Resolve(IEnumerable<string> refs)`.
  - Records `SourceRefResolution`, `ResolvedSourceRef`, `MissingSourceRef` and the enum `MissingReason`.
  - Reuse `SourceRefNormalizer.StripFragment`.
  - The "under root" check uses `Path.GetFullPath`.
  - The dedupe comparer is `Ordinal` on Linux and `OrdinalIgnoreCase` otherwise.
  - `File.Exists` is the only I/O. Catch `ArgumentException` and `NotSupportedException` from path APIs and turn them
    into `Missing(NotFound)`.
- [X] T025 [US2] In `src/Spectra.Core/Update/TestClassifier.cs` `ClassifyBatch`, replace first-hit-wins
  (lines 174–181) with joining the **distinct** contents of all refs present in `sourceContents`, separated by
  `"\n\n"`. Keep the public signatures and every reason string.
- [X] T026 [US2] Rewrite `LoadSourceDocumentsAsync` in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`
  (lines 564–604):
  - Build `new SourceRefResolver(basePath, Path.Combine(basePath, config.Source.LocalDir ?? "docs"))`.
  - Resolve each test's refs, and read each distinct full path once.
  - Key the dictionary by the **original raw ref**.
  - Remove the walk over every `*.md` and the early return when the source dir is missing.
  - Pass `basePath` and `config` through from `ProcessSuiteAsync` as needed.
- [X] T027 [P] [US2] Replace the body of `LoadDocumentsFromRefsAsync` in
  `src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs` (lines 342–366) with the resolver. `sourceDir` comes
  from the loaded config's `source.local_dir`, defaulting to `docs`. For any `Missing`, throw the same
  `FileNotFoundException($"Source ref not found: {r}")`, so the caller's existing exit-1 path is unchanged. Keep the
  output shape (repo-relative `/` path plus content).
- [X] T028 [P] [US2] Make the same replacement in `src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs`
  (lines 146–168). It stays fail-loud.
- [X] T029 [P] [US2] Make the same replacement in `src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs`
  (lines 147–168). It stays lenient: skip `Missing` refs and keep the surrounding `catch`.

**Checkpoint**: US2 is complete (SC-003, SC-004, SC-007). Run `dotnet test tests/Spectra.Core.Tests tests/Spectra.CLI.Tests`.

---

## Phase 5: User Story 3 - Classifying a suite makes no changes (Priority: P1)

**Goal**:
- `--no-interaction` without `--no-review` classifies and reports only.
- `--dry-run` emits the full result.
- `applied` is reported.
- The skills pass `--dry-run`.
- Init gitignores the run-status files.

This covers FR-008–FR-010, research R8 and `contracts/ai-update-cli.md`.

**Independent Test**: The classification command leaves the tree unchanged, prints the tables, and produces
`.spectra-result.json` with `applied: false`.

### Tests for User Story 3 (write first, confirm they fail)

- [X] T030 [P] [US3] Create `tests/Spectra.CLI.Tests/Commands/UpdateCommandFlagMatrixTests.cs`.
  - **Setup**: seed a `TempWorkspace` with a suite `smoke` of 2 tests, one referencing an existing doc and one a missing
    doc (so it is ORPHANED).
  - **Snapshot**: take SHA-256 hashes of every file under the workspace, excluding `.spectra-result.json`,
    `.spectra-progress.html` and `.spectra/` logs.
  - For each non-interactive row of the flag matrix in `contracts/ai-update-cli.md`:
    - `--dry-run --no-interaction`
    - `--dry-run --no-review`
    - `--no-interaction`
    - `--no-interaction --no-review`
    - `--no-review`

    each combined with `--diff` and `--output-format json`, assert:
    - the exit code is 0 in every row
    - the hashes are unchanged for the first three rows and changed for the last two
    - `.spectra-result.json` exists and contains the classification counts (`orphaned: 1`)
    - `applied` is `false` for the first three rows and `true` for the last two
    - the human classification summary table text is present in captured stdout. Capture it by assigning
      `AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) })` and
      restoring it in `Dispose`.
    - the hint `Pass --no-review to apply.` appears only in the `--no-interaction` row
- [X] T031 [P] [US3] Add to `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs`: after `spectra init`, `.gitignore`
  contains `.spectra-result.json` and `.spectra-progress.html`. A second `init` adds no duplicates.
- [X] T032 [P] [US3] Add to `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`: every line containing
  `spectra ai update` in `spectra-update`, `spectra-generate` and `spectra-execute` contains `--dry-run`. Load them via
  the same embedded-content accessor the file already uses. This covers table rows starting with `|` too, unlike the
  existing `spectra `-prefixed filters.

### Implementation for User Story 3

- [X] T033 [P] [US3] Add `[JsonPropertyName("applied")] public bool Applied { get; init; }` to
  `src/Spectra.CLI/Results/UpdateResult.cs`. Follow the file's existing attribute and naming style, and check that
  `ProgressManager.Complete` serialises it.
- [X] T034 [US3] Restructure `ProcessSuiteAsync` in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`:
  - Compute `var classifyOnly = _dryRun || (_noInteraction && !_noReview);`.
  - Remove the `if (_dryRun) { …; return ExitCodes.Success; }` inside the `showDiff` block (lines 355–359).
  - After the diff, when `classifyOnly`:
    - skip review, apply and index regeneration
    - print `Dry run - no changes were made.` for dry runs, or otherwise the hint
      `Classification only — no changes written. Pass --no-review to apply.` via `_progress.Info`
    - build the same `RunSummary` and `UpdateResult` with `TestsUpdated = 0`, `TestsRemoved = 0` and `Applied = false`
    - call `_progressManager.Complete(updateResult)`, render the run summary as the existing code does, and return
      `ExitCodes.Success`
  - Otherwise use the existing path, choosing `AutoReview` when `_noReview`, else `Review`, and set `Applied = true`.
  - Remove the now-redundant `_dryRun` checks inside the apply path. `isNonInteractive` auto-detection stays unchanged.
- [X] T035 [P] [US3] In `src/Spectra.CLI/Commands/Update/UpdateCommand.cs`, change the `--diff` description to "Show
  diff of proposed changes" (drop "without applying").
- [X] T036 [P] [US3] Add `.spectra-result.json` and `.spectra-progress.html` to the gitignore `patterns` array in
  `src/Spectra.CLI/Commands/Init/InitHandler.cs` (lines 380–398).
- [X] T037 [P] [US3] In `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` Step 1 (line 46), change the command
  to `spectra ai update --suite <suite> --diff --dry-run --no-interaction --output-format json --verbosity quiet`. Add one
  sentence saying this step classifies only and never writes files.
- [X] T038 [P] [US3] Add `--dry-run` after `--suite {suite}` in the "Update test cases" delegation rows of
  `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` (line 411) and
  `src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md` (line 138).
- [X] T039 [US3] Re-run `tests/Spectra.CLI.Tests/Commands/UpdateCommandTests.cs`. Every existing test passes
  `--no-review`, so no change is expected (F10). Then run `dotnet test tests/Spectra.CLI.Tests`: T030–T032 pass.

**Checkpoint**: US3 is complete (SC-005, SC-005a).

---

## Phase 6: User Story 4 - Orphan marking changes only orphan fields (Priority: P2)

**Goal**: The orphan branch is `original with { Status, OrphanedReason, OrphanedDate }`, and an existing date is kept
(FR-006, FR-007).

**Depends on**: Phase 2 and US1 (lossless writer). It shares `UpdateHandler.cs` with T026/T034, so do it after them.

**Independent Test**: The before/after diff of a full-field file touches only the 3 orphan lines.

### Tests for User Story 4 (write first, confirm they fail)

- [X] T040 [P] [US4] Create `tests/Spectra.CLI.Tests/Commands/UpdateHandlerOrphanMarkingTests.cs`.
  - **Setup**: seed `FullFieldTestCase.CanonicalMarkdown`, with the lifecycle fields removed, as the only test in suite
    `smoke`. Its only ref points to a doc that doesn't exist.
  - **(a)** Run `spectra ai update smoke --no-interaction --no-review`. The line diff between the before and after files
    is exactly the added `status: orphaned`, `orphaned_reason: "…"` and `orphaned_date: <today UTC yyyy-MM-dd>` lines.
  - **(b)** Seed the same file with `status: orphaned` and `orphaned_date: 2026-01-15` and re-run. `orphaned_date` stays
    `2026-01-15`.
  - **(c)** Using `FullFieldTestCase.Markdown` (with unknown content), re-run. `owner`, `x_ticket`, `## Notes`, the
    sub-bullet, `criteria`, `scenario_from_doc`, `grounding`, `description`, `automated_by`, `requirements` and `bugs`
    are byte-identical.

### Implementation for User Story 4

- [X] T041 [US4] In `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` `ApplyChangesAsync` (orphan loop, lines 656–687),
  replace the hand-built `new TestCase { … }` with:

  ```csharp
  originalTest with {
      Status = "orphaned",
      OrphanedReason = proposal.Reason ?? "Source documentation no longer exists",
      OrphanedDate = originalTest.Status == "orphaned" && originalTest.OrphanedDate is not null
          ? originalTest.OrphanedDate
          : new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)
  }
  ```

  Run `dotnet test tests/Spectra.CLI.Tests`: T040 and T022 pass.

**Checkpoint**: US4 is complete (SC-006).

---

## Phase 7: User Story 5 - Criteria extraction reads each document once (Priority: P3)

**Goal**: The compiled prompt is the only source of document text (FR-016, research R10).

**Independent Test**: The skill content has no instruction to read the document file.

- [X] T042 [P] [US5] Add a test to `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`. The `spectra-criteria`
  content:
  - does not contain `file tools to read`
  - does not contain `reading the document with your file tools`
  - does contain `compiled prompt` together with an instruction not to read the document separately

  Run it after T032, since both tasks edit this file.
- [X] T043 [P] [US5] Edit `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md`:
  - **Line 11**: replace "(reading the document with your file tools)" with wording saying the compiled prompt carries
    the document text.
  - **Step 2b** (line 69): change it to "Read the compiled prompt — it already contains the full document text; do not
    read `{path}` separately — and identify every testable acceptance criterion exactly as the prompt specifies." Keep
    the rest of 2b (JSON-only output, Write `.spectra/criteria.json`) unchanged.

**Checkpoint**: US5 is complete (SC-008).

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T044 [P] Add a `## [2.5.0] - <release date>` entry at the top of `CHANGELOG.md`, in the existing style
  (bold-lead bullets):
  - **Fixed**: lossless writes (P1/P2); lifecycle fields now read; grounding `source`/`created_by`/`note` preserved;
    deterministic manual grounding; YAML escaping; `ai update` ref resolution; classification uses all refs; criteria
    skill single read.
  - **Changed — Breaking**: `spectra ai update --no-interaction` no longer applies changes; add `--no-review` to apply in
    CI. Also: `--dry-run` now writes `.spectra-result.json`; `applied` field; critic, repair-prompt and repair-batch
    resolve refs with the shared resolver, and refs outside the repo now fail loud in critic and repair-prompt.
  - **Note**: fields already lost in existing workspaces are not recovered. Use Git history (`git log -p -- test-cases/`)
    to restore them.
  - Also list the out-of-scope observations from research.md under "Known issues".
- [X] T045 [P] Update `docs/cli-reference.md`, `ai update` section (lines 462–477 and the global options at 20–21):
  - add the flag matrix from `contracts/ai-update-cli.md`
  - replace `spectra ai update checkout --no-interaction   # CI mode` with
    `spectra ai update checkout --no-interaction --no-review   # CI: apply`, and add a `--dry-run` classify example
  - document `applied`
  - add a short "source_refs resolution" subsection mirroring the contract rules
- [X] T046 [P] Update the "Updating Test Cases After Doc Changes" section of `docs/usage.md` (line 126). Say that
  classification writes nothing and that applying needs `--no-review` or interactive review. Keep every `##` heading that
  `ProfileFormatLoaderTests` asserts, because usage.md is an embedded resource.
- [X] T047 [P] Update `CLAUDE.md`:
  - add a `- **078-lossless-test-writes (v2.5.0)**: …` bullet at the top of "Recent Changes", following the existing
    dense style (FR summary plus final test counts)
  - change the `ai update` line under Commands to `spectra ai update [suite] [--diff] [--dry-run] [--no-interaction] [--no-review]  # --no-interaction alone = classify only`
- [X] T048 [P] Bump `<Version>` in `Directory.Build.props` from 2.4.0 to 2.5.0.
- [X] T049 Run the full `dotnet build` and `dotnet test`. All suites should be green apart from the T001 pre-existing
  failure, if it's still unfixed. Put the final Core, CLI and Execution counts into the T047 bullet.
- [X] T050 Run the manual checks in `specs/078-lossless-test-writes/quickstart.md` (sections 1–4) with
  `dotnet run --project src/Spectra.CLI -- …` in a scratch workspace, and record the results in the PR description.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: after Setup. Blocks US1 and US4.
- **US1 (Phase 3)**: after Phase 2.
- **US2 (Phase 4)**: after Setup only. It is independent of US1, because T022 checks raw text.
- **US3 (Phase 5)**: after Setup only. It is independent of US1 and US2 (T030 uses a missing-doc ref, which is orphaned
  under both old and new resolution).
- **US4 (Phase 6)**: after US1 (lossless writer) and Phase 2.
- **US5 (Phase 7)**: after Setup only.
- **Polish (Phase 8)**: after every story in scope.

### Shared-file ordering

These rules matter when stories run in parallel.

- **`src/Spectra.CLI/Commands/Update/UpdateHandler.cs`** is edited by T026 (US2), T034 (US3) and T041 (US4). Do them in
  that order, or serialise them if they run in parallel.
- **`tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`** is edited by T032 (US3) and then T042 (US5).
- **`tests/Spectra.Core.Tests/Parsing/TestCaseParserTests.cs`**: only T007 edits it.

### Within each story

- Tests come before implementation, and should fail first.
- Model and DTO → parser → writer → callers.
- Finish each story's checkpoint test run before moving on.

## Parallel Opportunities

- **Phase 1**: T002 ∥ T003.
- **US1**: tests T006 ∥ T007 ∥ T008 ∥ T009. Then T010 ∥ T011, then T012 → T013 → T014, then T015 ∥ T016 ∥ T017.
- **US2**: tests T020 ∥ T021 ∥ T022 ∥ T023. Then T024 → (T025 ∥ T027 ∥ T028 ∥ T029) → T026.
- **US3**: tests T030 ∥ T031 ∥ T032. Then T033 ∥ T035 ∥ T036 ∥ T037 ∥ T038, then T034.
- **Across stories**: once Setup is done, US2, US3 and US5 can proceed alongside Phase 2 and US1, subject to the
  `UpdateHandler.cs` ordering above.
- **Polish**: T044 ∥ T045 ∥ T046 ∥ T047 ∥ T048.

### Parallel example: User Story 2

```text
Task: "T020 SourceRefResolverTests in tests/Spectra.Core.Tests/Coverage/SourceRefResolverTests.cs"
Task: "T021 multi-ref cases in tests/Spectra.Core.Tests/Update/TestClassifierTests.cs"
Task: "T022 default-profile regression in tests/Spectra.CLI.Tests/Commands/UpdateDefaultProfileRefsTests.cs"
Task: "T023 critic ref loading in tests/Spectra.CLI.Tests/Commands/CriticVerificationCommandsTests.cs"
# after T024:
Task: "T027 CompileCriticPromptCommand resolver"   Task: "T028 CompileRepairPromptCommand resolver"   Task: "T029 CompileRepairBatchCommand resolver"
```

### Parallel example: User Story 1

```text
Task: "T006 TestCaseLayoutTests"   Task: "T007 parser lifecycle/grounding tests"
Task: "T008 TestFileWriterRoundTripTests"   Task: "T009 WriterPathPreservationTests"
# after T014:
Task: "T015 GroundingWriteBackService with"   Task: "T016 ReviewFlaggedHandler with"   Task: "T017 ApplyEdit with"
```

## Implementation Strategy

### MVP (smallest set that stops the data loss)

The mass-loss chain is P3 (writes) × P4 (misclassification) × P1/P2 (lossy writes). The fastest way to stop it is **US3**:
once classification never writes, the skill can't damage anything.

1. Phase 1 (Setup).
2. **US3** (T030–T039): stops unreviewed writes immediately.
3. Phase 2 + **US1**: makes every remaining write lossless.
4. **US4**: orphan marking is lossless and minimal.
5. **US2**: correct classification.
6. **US5** + Polish.

This order differs from the phase numbering, which follows spec priority. Both are valid, because US3 and US2 don't depend
on US1.

### Incremental delivery

Each checkpoint leaves the suite green and the story demonstrable through `quickstart.md`. Ship as one PR for Spec 078,
with commits per story, so review can follow the story boundaries.
