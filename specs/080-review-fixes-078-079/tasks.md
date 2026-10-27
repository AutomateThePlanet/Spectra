# Tasks: Review Fixes for Specs 078 and 079

**Input**: Design documents from `/specs/080-review-fixes-078-079/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R12), data-model.md, contracts/cli-behaviour-changes.md, quickstart.md

**Tests**: REQUIRED. FR-015 asks for a regression test per review item that fails on `1ba172a` and passes after the fix.
In every story, write the test first, run it, and confirm it fails for the stated reason before implementing.

**Conventions**:
- CLI command tests that capture the console or share the working directory go in `[Collection("CriteriaCommands")]`
  (`tests/Spectra.CLI.Tests/TestFixtures/CriteriaCommandsCollection.cs`).
- Workspaces come from `CriteriaTestWorkspace`, `OldLayoutCriteria` and `UpdateWorkspace` in
  `tests/Spectra.CLI.Tests/TestFixtures/`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: US1–US6 from spec.md.

---

## Phase 1: Setup

- [X] T001 Record the baseline on branch `080-review-fixes-078-079` (at `1ba172a`). Run `dotnet build` and `dotnet test`
  and note the counts: Core 694 / CLI 1423 with the one known `ProfileFormatLoaderTests` failure / Execution 228 /
  Integration 8. Paste them into the Notes section at the bottom of `specs/080-review-fixes-078-079/tasks.md`.

---

## Phase 2: Foundational (blocks US2 and US6)

**Purpose**: The two ConfigLoader entry points (R5). US2 needs `LoadForReadAsync` to get the source dir; US6 rewires
every in-scope command onto them.

- [X] T002 Write `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`. Each case runs in a temp dir:
  - `LoadForReadAsync` with no file returns `Config == SpectraConfig.Default` (compare `Source.LocalDir` and
    `Tests.Dir`) and `Error == null`.
  - `LoadForWriteAsync` with no file returns `Config == null` and `Error == ConfigLoader.MissingConfigMessage`, which
    is exactly "No spectra.config.json found. Run 'spectra init' first.".
  - Both entry points, given malformed JSON (`{"source": {`), return an `Error` that starts with
    "spectra.config.json is invalid: ".
  - Both entry points, given valid JSON missing the `ai` section, return the same prefix.
  - Both entry points, given a valid full config, return the config with `Error == null`.

  These fail to compile before T003, which counts as failing.
- [X] T003 Add `ConfigLoadResult(SpectraConfig? Config, string? Error)` in `src/Spectra.Core/Config/ConfigLoader.cs`,
  with `IsSuccess => Error is null`. Add the constants `MissingConfigMessage` and `InvalidConfigPrefix`
  ("spectra.config.json is invalid: "). Add `LoadForReadAsync(string repoRoot, CancellationToken ct = default)` and
  `LoadForWriteAsync(...)` as `public static` methods on the existing `sealed class ConfigLoader`, constructing a
  loader internally so callers need no instance:
  - Both locate `Path.Combine(repoRoot, "spectra.config.json")`.
  - Both reuse the existing `LoadFromFileAsync` / `Validate` path.
  - Both map every parse or validation failure to `InvalidConfigPrefix + firstError.Message`.
  - Only the missing-file branch differs between them.

  Make T002 pass.

**Checkpoint**: `dotnet test tests/Spectra.Core.Tests --filter FullyQualifiedName~ConfigLoader` is green.

---

## Phase 3: User Story 1 — Update writes to the test's own file (P1) 🎯 MVP

**Goal**: FR-001 / SC-001 (A1, R1).
**Independent Test**: `TC-100-login.md` (`id: TC-100`) whose only doc is deleted. `--no-review` marks that file, and
`--no-review --delete-orphaned` deletes it; `TC-100.md` never appears.

- [X] T004 [US1] Write `tests/Spectra.CLI.Tests/Commands/UpdateHandlerFilePathTests.cs` (collection
  `CriteriaCommands`, workspace from `UpdateWorkspace`). The suite `checkout` contains `TC-100-login.md` with
  `id: TC-100` and `source_refs: [docs/gone.md]`, and a nested `sub/TC-101.md` with a missing ref. Cases:
  - (a) `ai update checkout --no-review`: `TC-100-login.md` has `status: orphaned`, `sub/TC-101.md` has
    `status: orphaned`, and neither `TC-100.md` nor `TC-101.md` exists at the suite root. The file count is unchanged.
  - (b) `ai update checkout --no-review --delete-orphaned`: both files are gone and no new file exists.
  - (c) A test in a file named exactly `TC-102.md` behaves as before.
  - (d) Run `--no-review` twice. After both runs, the suite's file set equals the original file set, and
    `TC-100-login.md` after run 2 is byte-identical to after run 1 (078's `orphaned_date` is kept). This fails before
    the fix because of the stray `TC-100.md`.

  Confirm (a), (b) and (d) fail on the current code.
- [X] T005 [US1] In `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` (`ApplyChangesAsync`, the update, orphan and
  delete loops, ~lines 661/684/694), replace `Path.Combine(suitePath, $"{test.Id}.md")` with a private helper
  `TestFilePathOf(TestCase test, string suitePath)`:
  - It returns `test.FilePath` when that is rooted.
  - Otherwise it returns `Path.GetFullPath(Path.Combine(suitePath, test.FilePath))`.
  - It falls back to `{Id}.md` only when `FilePath` is empty.

  Make T004 pass. `UpdateHandlerOrphanMarkingTests` must stay green.

**Checkpoint**: US1 is complete and independently testable.

---

## Phase 4: User Story 2 — Docs outside the repository still resolve (P1)

**Goal**: FR-002, FR-002a, FR-002b / SC-002 (A2, R2).
**Independent Test**: With `source.local_dir: ../product-docs/docs`, classification finds the docs, and the critic
compile loads bare, relative and absolute refs. A ref escaping both roots is refused with the "outside" message.

- [X] T006 [P] [US2] Extend `tests/Spectra.Core.Tests/Coverage/SourceRefResolverTests.cs`. The temp layout is `root/repo` plus
  `root/product-docs/docs/checkout.md`. Cases, all with the source dir `../product-docs/docs`:
  - A bare `checkout.md#Payment` resolves.
  - A relative `../product-docs/docs/checkout.md` resolves.
  - An absolute path into the source dir resolves.
  - `../../elsewhere/x.md` (an existing file outside both roots) is refused with
    `MissingReason.OutsideAllowedRoots`.
  - A missing file inside the source dir gives `NotFound`.
  - A ref valid under both roots still picks the repo-root candidate.
  - The default in-repo source dir behaves as before.

  Confirm the out-of-repo cases fail.
- [X] T007 [P] [US2] Write `tests/Spectra.CLI.Tests/Commands/UpdateOutsideSourceDirTests.cs` (collection
  `CriteriaCommands`). The workspace config has `source.local_dir = "../product-docs/docs"`, and a test references
  `checkout.md#Payment`. Run `ai update <suite> --dry-run --output-format json`: the test is not classified ORPHANED,
  and no file is written. Confirm it fails.
- [X] T008 [P] [US2] Extend `tests/Spectra.CLI.Tests/Commands/CriticVerificationCommandsTests.cs` with the out-of-repo
  source dir:
  - `compile-critic-prompt` for a test whose ref is an absolute path inside the source dir succeeds, and the prompt
    contains the doc text.
  - For a ref escaping both roots, the error contains "Source ref is outside the repository and the configured source
    directory: ".
  - For a missing ref, the error contains "Source ref not found: ".

  Confirm the first two fail.
- [X] T009 [US2] In `src/Spectra.Core/Coverage/SourceRefResolver.cs`:
  - Rename `MissingReason.OutsideRepository` to `OutsideAllowedRoots`.
  - Compute the allowed roots as {full repo root, full source dir}, both normalised with a trailing separator and
    compared `OrdinalIgnoreCase` on Windows (as today).
  - A candidate is accepted only when it exists and lies under one of the roots.
  - Keep the candidate order: repo root first, then the source dir for bare refs.
  - Update every `OutsideRepository` reference (`grep -rn OutsideRepository src tests`).

  Make T006 pass.
- [X] T010 [US2] In `src/Spectra.CLI/Source/SourceRefDocumentLoader.cs`:
  - Delete `ReadSourceDirAsync`. The loader now takes the source dir as a parameter from its callers, which get it
    from `ConfigLoader.LoadForReadAsync` (T003).
  - Map `NotFound` to "Source ref not found: {ref}" and `OutsideAllowedRoots` to "Source ref is outside the repository
    and the configured source directory: {ref}".
  - Keep the three modes as they are (fail-loud / fail-loud / lenient).
- [X] T011 [US2] Update the loader's callers to pass the source dir from `ConfigLoader.LoadForReadAsync`, returning the
  config error (exit 1) when `Error != null`:
  - `src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs`, replacing `ResolveTestsDirAndConfigAsync`'s
    hand-rolled read.
  - `src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs`.
  - `src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs`, replacing their `ResolveTestsDirAsync` reads.

  `UpdateHandler` already passes the config's source dir; verify it uses the renamed reason. Make T007 and T008 pass.

**Checkpoint**: US2 is complete. `SourceRefResolverTests`, `UpdateOutsideSourceDirTests` and
`CriticVerificationCommandsTests` are green.

---

## Phase 5: User Story 3 — Test-file rewrites keep every key and comment in place (P1)

**Goal**: FR-003, FR-004, FR-004a, FR-004b / SC-003 (A3 R3, A4 R4).
**Independent Test**: A file with a leading comment, a comment above `source_refs` and one above an unknown key keeps
all three in place after the preceding fields change. A guard test fails if the key lists diverge.

- [X] T012 [P] [US3] Write `tests/Spectra.Core.Tests/Models/FrontmatterKeysGuardTests.cs`:
  - (a) Reflect over `TestCaseFrontmatter`'s `[YamlMember(Alias=…)]` properties; the alias set equals
    `TestCaseFrontmatterKeys.Ordered` as a set.
  - (b) `TestCaseFrontmatterKeys.Ordered` has no duplicates.
  - (c) It is the same instance or sequence that `TestCaseParser` uses for known keys (expose it
    `internal`/`public static` and assert `SequenceEqual`).

  It fails to compile before T015.
- [X] T013 [P] [US3] Write `tests/Spectra.CLI.Tests/IO/TestFileWriterAllKeysTests.cs`: a test with every known key
  set, written with `TestFileWriter` and parsed back, has every key present. Also assert that
  `TestFileWriter.KeyOrder` `SequenceEqual`s `TestCaseFrontmatterKeys.Ordered`. It fails to compile before T015.
- [X] T014 [P] [US3] Write `tests/Spectra.CLI.Tests/IO/FrontmatterCommentPlacementTests.cs`. The fixture frontmatter
  has, in order: `# file header comment`, a blank line, `id`, …, `# deps`, `depends_on: [TC-001]`, a blank line, `# refs below`,
  `source_refs: [...]`, …, `# custom note`, `x_custom: keep`, and a trailing `# end` line. Cases:
  - (a) Change `depends_on` and rewrite: `# deps` is still directly above `depends_on`, `# refs below` is still on
    the line directly above `source_refs:`, and the blank line is kept.
  - (b) Change only fields so that the canonical order moves keys: `# file header comment` is still the first
    frontmatter line.
  - (c) Change the known key just before `x_custom`: `# custom note` is still directly above `x_custom`.
  - (d) The trailing `# end` is still the last frontmatter line.
  - (e) Remove `depends_on` (set it to empty): `# deps` is gone, and `# refs below` and its blank line stay directly
    above `source_refs`.
  - (f) An unchanged file rewrites byte-identically.

  Confirm (a)–(d) fail.
- [X] T015 [US3] Create `src/Spectra.Core/Models/TestCaseFrontmatterKeys.cs`, holding `public static
  IReadOnlyList<string> Ordered` with the 21 keys in the current `TestFileWriter.KeyOrder` order. Then:
  - Replace `TestCaseParser.KnownFrontmatterKeys` in `src/Spectra.Core/Parsing/TestCaseParser.cs` with a set built from
    it.
  - Replace `TestFileWriter.KeyOrder` in `src/Spectra.CLI/IO/TestFileWriter.cs` with a reference to it.

  Make T012 and T013 pass.
- [X] T016 [US3] In `src/Spectra.Core/Models/TestCaseLayout.cs`:
  - Add `string FrontmatterLeading` and `string FrontmatterTrailing`.
  - Give `FrontmatterEntry` a `Prelude` string before `RawText`.
  - Document the invariant `Leading + Σ(Prelude+RawText) + Trailing == frontmatter` in an XML comment.
- [X] T017 [US3] In `src/Spectra.Core/Parsing/TestCaseParser.cs` `CaptureFrontmatterEntries`:
  - Text before the first key's mark becomes `FrontmatterLeading`.
  - For each entry, trim trailing lines that are blank or start with `#` at column 0 off its raw span. They become the
    next entry's `Prelude`, or `FrontmatterTrailing` for the last entry.
  - Indented `#` lines stay inside the value.
  - Add a debug assertion for the invariant.
- [X] T018 [US3] In `src/Spectra.CLI/IO/TestFileWriter.cs` `AppendFrontmatter`:
  - Emit `FrontmatterLeading` first.
  - For each emitted key, emit its baseline entry's `Prelude`, then the raw or canonical text. New keys have no
    prelude, and removed keys drop theirs.
  - Emit `FrontmatterTrailing` last.
  - Make T014 pass.
- [X] T019 [US3] Update `tests/Spectra.Core.Tests/Parsing/TestCaseLayoutTests.cs`
  (`Entries_CaptureFlowCollectionsBlockScalarsAndComments`) to assert the new shape: the comment in the next entry's
  `Prelude`, `RawText` holding the key and value only, plus an invariant assertion. This is the one layout assertion
  that encoded the bug. Run `TestFileWriterRoundTripTests`, `WriterPathPreservationTests` and the other 078 round-trip
  tests unmodified; all must be green.

**Checkpoint**: US3 is complete, with no 078 round-trip regressions.

---

## Phase 6: User Story 4 — Shared criteria files are handled by provenance everywhere (P1)

**Goal**: FR-006, FR-007, FR-007a, FR-008 / SC-004 (B1–B3, R6).
**Independent Test**: A shared file with criteria of `docs/a/login.md` and `docs/b/login.md`. Compile A, ingest A,
apply-move B and retire, checking each file after each step.

- [X] T020 [P] [US4] Write `tests/Spectra.Core.Tests/Criteria/CriteriaProvenanceTests.cs`:
  - `IsShared` is true when two index sources have the same `file` (lexical, normalised) and false otherwise.
  - `OwnedBy` matches `source_doc` after `CriteriaPaths.NormalizeLexical`, `OrdinalIgnoreCase` (backslashes, `./`,
    casing), which is the rule the existing ingest and retire filters use.
  - A `null` `source_doc` is owned by no document.

  It fails to compile before T023.
- [X] T021 [US4] Write `tests/Spectra.CLI.Tests/Commands/Criteria/SharedCriteriaFileTests.cs` (collection
  `CriteriaCommands`). Seed via `OldLayoutCriteria` a shared `docs/criteria/login.criteria.yaml` holding A's
  (`AC-LOGIN-001/002`, `source_doc: docs/a/login.md`), B's (`AC-LOGIN-003`, `source_doc: docs/b/login.md`) and one
  unowned criterion (no `source_doc`), with both index sources pointing at it. Cases:
  - (B3) `compile-extraction-prompt --doc docs/a/login.md`: `existing_criteria` contains only 001 and 002.
  - (B3→ingest) Ingest A returning 001 and 002 succeeds (exit 0) with those IDs kept.
  - (B2 re-ingest) After ingesting A into its own file, the shared file no longer contains 001/002 but still contains
    003 and the unowned criterion.
  - (B1) Delete-and-move B to `docs/c/login.md` and run `docs apply-move`: the new file contains only 003 with
    `source_doc: docs/c/login.md`, and nothing of A's.
  - (B2 retire) Delete `docs/b/login.md` and run `docs retire`: 003 is removed from the shared file, and
    `ai analyze --coverage` no longer counts it.
  - (FR-007a) With both documents gone and no unowned criteria, the shared file is deleted. With the unowned criterion
    present, it is kept.
  - `--dry-run` on apply-move and retire writes nothing (byte-compare the files).
  - (FR-016) `ingest-criteria --dry-run` for A, returning 001 and 002, leaves the shared file and the index
    byte-identical, and creates no file for A.

  Confirm the B1, B2 and B3 cases fail.
- [X] T022 [P] [US4] Extend `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaStorageTests.cs`
  (`PastCollision…`) so that after the split the shared file contains no criterion owned by the re-ingested doc.
  Confirm it fails.
- [X] T023 [US4] Create `src/Spectra.Core/Criteria/CriteriaProvenance.cs` with `IsShared(CriteriaIndex index,
  CriteriaSource source)` and `OwnedBy(IEnumerable<AcceptanceCriterion> criteria, string doc)`, comparing with `CriteriaPaths.NormalizeLexical`.
  Make T020 pass.
- [X] T024 [US4] In `src/Spectra.CLI/Criteria/CriteriaStore.cs` (`CriteriaWorkspace`), add
  `LeaveSharedFileAsync(string file, string doc, CancellationToken ct)`:
  - Read strictly, then remove `OwnedBy(doc)`.
  - If nothing remains and no index source references `file`, delete it.
  - Otherwise, if anything was removed, rewrite it.
  - Return a flag saying whether anything was removed.
- [X] T025 [US4] Use `CriteriaProvenance` in `src/Spectra.CLI/Extraction/CriteriaIngestor.cs`, replacing its inline
  shared-file filter. Call `LeaveSharedFileAsync` after the doc's own file is written, when its previous `file` was
  shared and differs from the new derived file, and never on `--dry-run`. Make T022, the B2 re-ingest case and the
  ingest `--dry-run` case pass.
- [X] T026 [US4] In `src/Spectra.CLI/Commands/Docs/DocsApplyMoveCommand.cs`:
  - When the old source's file is shared, copy only `OwnedBy(from)` into the new file.
  - Relabel `source_doc` only on those criteria.
  - Call `LeaveSharedFileAsync(oldFile, from)`.
  - Skip all writes on `--dry-run`.
  - Make the B1 case pass.
- [X] T027 [US4] In `src/Spectra.CLI/Commands/Docs/DocsRetireCommand.cs`, replace the inline provenance filter with
  `CriteriaProvenance` and use `LeaveSharedFileAsync` for the shared file (the dry run reports without writing). Make
  the B2 retire and FR-007a cases pass.
- [X] T028 [US4] In `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs` (`LoadExistingCriteriaAsync`),
  filter to `CriteriaProvenance.OwnedBy(criteria, doc)` when the doc's index source is shared. Make the B3 case pass.

**Checkpoint**: US4 is complete, and `SharedCriteriaFileTests` is green.

---

## Phase 7: User Story 5 — Criteria results are honest and machine-readable (P2)

**Goal**: FR-009, FR-011, FR-012, FR-013, FR-014 / SC-005, SC-006 (B4 R7, B6 R9, B7 R10, B8 R11, B9 R12).
**Independent Test**: Each sub-item below has its own test and is independent of the others.

### B9 — normaliser

- [X] T029 [P] [US5] Extend `tests/Spectra.Core.Tests/Text/TextNormalizerTests.cs`:
  - `total<limit and qty>5` and `total<limit and qty>6` give different keys, and both contain `limit and qty`.
  - `a<b` is kept.
  - `x > 3 and y < 2` is kept.
  - `<b>x</b>` → `x`.
  - `<b>a < b</b>` → `a < b`.
  - `<br/>`, `<span class="k">v</span>` and `<!-- note -->` are stripped.
  - `<Limit>` (not whitelisted) is kept.

  Confirm the comparison cases fail.
- [X] T030 [US5] In `src/Spectra.Core/Text/TextNormalizer.cs`, replace the tag regex with the R12 whitelist regex plus
  the HTML-comment regex (`RegexOptions.IgnoreCase | Compiled`). Make T029 pass. `CriteriaMatcher` tests must stay
  green.

### B4 — tags

- [X] T031 [P] [US5] Extend `tests/Spectra.Core.Tests/Criteria/CriteriaChangesetTests.cs`:
  - `[ux, Login]` vs `[login, UX]` is not modified.
  - `[a]` vs `[a, b]` is modified.
  - The reported old/new keep their original text.
  - A stored `UX` re-ingested as `ux` is not a change, and the file still says `UX`.

  Extend `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaChangesetTests.cs`: re-ingesting the same criteria
  with tags reordered and recased leaves the changeset empty and the criteria file byte-identical. Confirm both fail.
- [X] T032 [US5] In `src/Spectra.Core/Criteria/CriteriaChangeset.cs`, compare `tags` with the normalised key (trim,
  distinct `OrdinalIgnoreCase`, sort `OrdinalIgnoreCase`, lower, join `", "`), and display the original values. Make
  T031 pass.

### B6 — Rejected outcome

- [X] T033 [P] [US5] Extend `tests/Spectra.CLI.Tests/Commands/Criteria/IngestCriteriaIdentityTests.cs`:
  - A foreign-ID ingest (exit 7) gives a failure JSON with `"outcome":"Rejected"` and `"success":false`.
  - A unit case: `CriteriaIngestResult.Rejected(...)`, with `Outcome == ExtractionOutcome.Rejected` and
    `IsSuccess == false`.

  Confirm they fail.
- [X] T034 [US5] Add `Rejected` to `src/Spectra.CLI/Extraction/ExtractionOutcome.cs`. Set it in
  `CriteriaIngestResult.Rejected` in `src/Spectra.CLI/Extraction/CriteriaIngestor.cs`, with
  `IsSuccess => Outcome == Extracted`. Audit every `switch` or comparison on the outcome — `CriteriaIngestor.cs`, `IngestCriteriaCommand.cs` and
  `CriteriaResponseClassifier.cs` (the only users of `ExtractionOutcome`) — so none treats `Rejected` as extracted. Keep the exit codes. Make T033 pass.

### B7 — compile failure JSON

- [X] T035 [P] [US5] Extend `tests/Spectra.CLI.Tests/Commands/Criteria/CompileExtractionPromptExistingTests.cs`:
  - (a) `compile-extraction-prompt --doc docs/missing.md --output-format json`: exit 1, and the whole of stderr parses
    with `JsonDocument.Parse` as one document with `error_code: "DOCUMENT_NOT_FOUND"` and `"warnings": []`.
  - (b) An unreadable document (hold the file open with `FileShare.None` while the command runs) gives exit 1 and one
    JSON document with `error_code: "DOCUMENT_UNREADABLE"` and `"warnings": []`.
  - (c) `--doc` omitted gives exit 4, and stderr is one refusal JSON document with `"warnings": []`.
  - (d) The shape: call the command's failure-writing helper (made `internal`, visible to tests through the existing
    `InternalsVisibleTo`) with two warnings, in JSON mode. The captured stderr is one JSON document whose `warnings`
    array has both. This covers the compiler-refusal path, which the CLI cannot reach today (research R10).
  - (e) Unchanged: an empty document still exits 0 with the short-circuit JSON on stdout. In an old-layout workspace,
    the success case still prints the layout warning to stderr as text, with the prompt on stdout.

  Confirm (a), (b) and (d) fail on `1ba172a`. For (d), the helper doesn't exist yet, so it fails to compile.
- [X] T036 [US5] In `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs`:
  - Collect warnings into a list instead of printing them eagerly.
  - Add one `internal static` failure helper that every failure path calls. In JSON mode it writes one object to
    stderr, always with `warnings`: `{refused, missing_input, message, warnings}` for refusals (exit 4) and
    `{error_code, message, warnings}` for errors (exit 1). In human mode it prints the warnings, then the error line.
  - Route the missing-document (`DOCUMENT_NOT_FOUND`), unreadable-document (`DOCUMENT_UNREADABLE`), `--doc` refusal
    and compiler refusal paths through it.
  - Leave the empty-document short-circuit (stdout JSON, exit 0) unchanged.
  - On success, print each warning to stderr (as today) after the compile.

  Make T035 pass.

### B8 — one scan

- [X] T037 [P] [US5] Write `tests/Spectra.CLI.Tests/Criteria/CriteriaScanTests.cs`:
  - (a) Reflection: `DocsChangedHandler` declares no method named `ScanDuplicatesAsync`, and `CriteriaStore` has a
    public static `ScanFilesAsync`.
  - (b) Behavioural, in collection `CriteriaCommands`: a nested `docs/criteria/sub/x.criteria.yaml` with an ID
    duplicated in a top-level file appears in both the `docs changed --output-format json` `duplicate_ids` and the
    ingest result's `duplicate_ids`.

  Confirm (a) fails.
- [X] T038 [US5] Add `public static Task<IReadOnlyList<(string File, IReadOnlyList<AcceptanceCriterion> Criteria)>>
  ScanFilesAsync(string criteriaDir, CancellationToken ct)` to `src/Spectra.CLI/Criteria/CriteriaStore.cs`, extracted
  from `LoadAsync`'s scan (recursive `*.criteria.yaml`, ordinal order, lenient read). Then:
  - Make `LoadAsync` use it.
  - Delete `ScanDuplicatesAsync` from `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs` and call `ScanFilesAsync`
    there.

  Make T037 pass. `DocsChangedDeletedMovedTests` must stay green.

**Checkpoint**: US5 is complete.

---

## Phase 8: User Story 6 — The skill handles every criteria error, and config loads one way (P2)

**Goal**: FR-005, FR-005a, FR-005b, FR-010 / SC-006a (A5 R5, B5 R8). Depends on Phase 2; T011 already moved the
critic/repair loaders.
**Independent Test**: Skill-content assertions, plus a theory running each in-scope command with no config and with a
malformed config.

### B5 — skill

- [X] T039 [P] [US6] Extend `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs` with a `spectra-criteria` test. The
  ingest, apply-move and retire steps each contain the exit-1 instruction:
  - it mentions `exit 1` and `error_code`
  - it says stop for that document
  - it says `LOCK_TIMEOUT` retry once
  - it names `CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION` and `INVALID_DOC_PATH`

  Confirm it fails.
- [X] T040 [US6] In `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md`, add the exit-1 handling line to Steps
  1a (apply-move), 1b (retire) and 2c (ingest-criteria) per R8. Make T039 pass. Then run
  `dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Skills"`, which covers `SkillsManifestTests` and
  the bundled-skill content tests, and update any content hash or snapshot the edit invalidates.

### A5 — config loading

- [X] T041 [US6] Write `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs` (collection
  `CriteriaCommands`). It has two theories over all ten in-scope commands, each invoked with its minimum valid
  arguments:
  - **Read commands:** `ai compile-extraction-prompt`, `ai compile-critic-prompt`, `ai compile-repair-prompt`,
    `ai compile-repair-batch`, `docs changed`.
  - **Write commands:** `ai ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`,
    `ai analyze --import-criteria`.

  Cases:
  - (a) A malformed `spectra.config.json` gives exit 1, and the output contains "spectra.config.json is invalid: " in
    both human and JSON mode. In JSON mode, check each command on the stream and in the shape given by the table in
    `contracts/cli-behaviour-changes.md` § Configuration:
    - retire, apply-move, migrate-criteria and ingest-criteria: stderr is one `WriteFailure` JSON document.
    - `--import-criteria`: stdout is one `ErrorResult` JSON document.
    - `compile-extraction-prompt`: stderr is one B7 failure JSON document with `warnings`.
    - The other four read commands: the message is a plain stderr line; do not add JSON error formats to them.
  - (b) With no config, every write command exits 1 with "No spectra.config.json found. Run 'spectra init' first.".
  - (c) With no config, every read command runs on the defaults. Each read command gets a fixture laid out for the
    default paths (`docs/`, `test-cases/`, `docs/criteria/`) that holds exactly what it needs to succeed: a doc for
    `compile-extraction-prompt`; a test and its doc for the critic, repair-prompt and repair-batch compiles; nothing
    extra for `docs changed`. Assert exit 0, and that neither config message appears on stdout or stderr.
  - (d) With a malformed config, no write command modifies any file (snapshot the tree).
  - (e) `ai ingest-criteria --doc docs/a.md --from missing.json --output-format json` (valid config): exit 1, and
    stderr is one `WriteFailure` JSON document with no `outcome` field. Likewise with `--doc` omitted. Both fail before
    the fix (plain text today).

  Confirm the failing rows before the rewiring.
- [X] T042 [US6] Make `src/Spectra.CLI/Criteria/CriteriaCommandIO.cs` `LoadConfigAsync` a thin call to
  `ConfigLoader.LoadForWriteAsync` (or remove it and call ConfigLoader directly), and surface `Error` through each
  caller's existing failure JSON or human path. Callers:
  - `src/Spectra.CLI/Commands/Generate/IngestCriteriaCommand.cs`. Replace its hand-rolled config read (~lines 58–81),
    and route **all** its early errors (`--doc` missing, missing or invalid config, `--from` file not found) through
    `CriteriaCommandIO.WriteFailure(json, "ingest-criteria", null, message)` instead of `Console.Error.WriteLine`, so
    JSON mode gives one document (SC-006). These carry no `outcome`. Make T041(e) pass.
  - `src/Spectra.CLI/Commands/Docs/DocsRetireCommand.cs`
  - `src/Spectra.CLI/Commands/Docs/DocsApplyMoveCommand.cs`
  - `src/Spectra.CLI/Commands/Docs/DocsMigrateCriteriaCommand.cs`
- [X] T043 [US6] In `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` (`RunImportCriteriaAsync`), load config via
  `ConfigLoader.LoadForWriteAsync`, returning exit 1 with the message through its existing path: `ErrorResult` via
  `JsonResultWriter` (stdout) in JSON mode, stderr text in human mode. Don't change the stream.
- [X] T044 [US6] Load config via `ConfigLoader.LoadForReadAsync` in:
  - `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs`, removing its lenient fallback block
    (~lines 133–145). Load the config once at the start of `RunAsync`, before the document checks, and pass it to
    `LoadExistingCriteriaAsync`. A config error goes out through T036's failure helper: in JSON mode, one
    `{ "error_code": null, "message": "<config message>", "warnings": [...] }` document on stderr, including any
    warnings already collected; in human mode, the warnings and then the message. Exit 1.
  - `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs`: a missing config now runs on the defaults (the contract
    change).

  Make T041 pass. Update any existing `DocsChangedTests` case that asserted exit 1 on a missing config, and note it in
  the CHANGELOG (T046).
- [X] T045 [US6] Grep for leftover hand-rolled reads in `src/Spectra.CLI` among the in-scope files
  (`grep -rn "spectra.config.json" src/Spectra.CLI --include=*.cs`). Confirm every in-scope command goes through
  `ConfigLoader.LoadForReadAsync` / `LoadForWriteAsync`. Leave out-of-scope commands unchanged, and list them in the
  Notes. These include at least `ai update` (`UpdateHandler.LoadConfigAsync`) and `docs index`
  (`DocsIndexHandler.LoadConfigAsync`), which FR-005 does not list.

**Checkpoint**: US6 is complete; all 14 items are fixed.

---

## Phase 9: Polish & Cross-Cutting

- [X] T046 [P] Update `CHANGELOG.md` with a "Fixed (review)" subsection under the unreleased `[2.6.0]` entry, listing A1–A5 and B1–B9. Call out the behaviour changes: `docs changed` with no config now uses defaults;
  config errors give the uniform message; the `Rejected` outcome; the compile failure JSON `warnings`; out-of-repo
  source dirs.
- [X] T047 [P] Update `CLAUDE.md` "Recent Changes" with a `080-review-fixes-078-079` entry summarising R1–R12, the new
  types (`TestCaseFrontmatterKeys`, `CriteriaProvenance`, `ConfigLoadResult`) and the final test counts.
- [X] T048 [P] Update `docs/cli-reference.md` per `contracts/cli-behaviour-changes.md`: the config table, the
  out-of-repo source dir, the `Rejected` outcome, the compile-extraction-prompt failure JSON `warnings`, and that tags
  are compared as a set.
- [X] T049 Run the full `dotnet build` and `dotnet test`. Compare with the T001 baseline; the only allowed failure is
  the known `ProfileFormatLoaderTests` one. Record the counts in the Notes and in `CLAUDE.md` (T047).
- [X] T050 Verify FR-015 / SC-007:
  - For each item's regression test (T004, T006–T008, T012–T014, T020–T022, T029, T031, T033, T035, T037, T039,
    T041, plus T002 for the A5 core), confirm it fails when run against `1ba172a`, e.g. by stashing the `src/` changes
    or using a `git worktree` at `1ba172a` with the new test files copied in.
  - Record the before/after table in the Notes.
- [X] T051 Run the quickstart manual spot checks in a scratch workspace (`specs/080-review-fixes-078-079/quickstart.md`
  steps 1–3).

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2**: Phase 2 blocks US2 (T011 uses `LoadForReadAsync`) and US6.
- **US1** depends only on Phase 1 and can start immediately after T001, in parallel with Phase 2.
- **US3** is independent of Phase 2 and of the other stories.
- **US4**: T025–T028 share `CriteriaStore.cs` (T024) and touch `CompileExtractionPromptCommand.cs`, which US5 B7 (T036)
  and US6 (T044) also edit, so do T028 → T036 → T044 in that order.
- **US5**:
  - B9, B4, B6 and B8 are independent of each other.
  - B8 (T038) edits `CriteriaStore.cs`, so do it after T024.
  - B6 (T034) edits `CriteriaIngestor.cs`, so do it after T025.
- **US6**: T042 edits `DocsRetireCommand.cs` / `DocsApplyMoveCommand.cs`, so do it after T026 and T027. T044 edits
  `DocsChangedHandler.cs`, so do it after T038.
- **Polish** comes after all stories.

Recommended order: T001 → T002–T003 → US1 → US3 → US2 → US4 → US5 → US6 → Polish.

## Parallel Opportunities

- The test-writing tasks marked [P] within a story (e.g. T006/T007/T008; T012/T013/T014; T029/T031/T033/T035/T037) touch
  different files and can be written together.
- US1 (UpdateHandler) and US3 (parser/writer/layout) share no files and can proceed in parallel.
- Polish docs T046/T047/T048 can be done in parallel.

```text
# Example: US5 tests in parallel
T029 TextNormalizerTests.cs | T031 CriteriaChangesetTests.cs | T033 IngestCriteriaIdentityTests.cs
T035 CompileExtractionPromptExistingTests.cs | T037 CriteriaScanTests.cs
```

## Implementation Strategy

- **MVP**: US1 (A1). It is the only item that corrupts suites through duplicate IDs, and it is fixed by a single-file
  change.
- **Increments**: US3 and US2 (the remaining 078 data-loss and misclassification), then US4 (079 shared files), then
  the P2 stories US5 and US6.
- After each story, run `dotnet test` for the touched projects before moving on.
- Commit once at the end as `fix(080): …`. Merge into `fix/doc-change-update-workflow` only with the user's approval,
  and never push.

## Notes

- **Baseline (T001)**, at `1ba172a`: Core 694 / CLI 1423 (1 known failure: `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown`) / Execution 228 / Integration 8.
- **Final counts (T049)**: Core 735 / CLI 1498 (the same 1 known failure) / Execution 228 / Integration 8. Build has 0 warnings.
- **Out-of-scope config readers left unchanged (T045)**: `ai update` (`UpdateHandler.LoadConfigAsync`), `docs index`
  (`DocsIndexHandler.LoadConfigAsync`), the other `ai analyze` modes (`AnalyzeHandler.cs:68`, `:566`; the config
  path-migration helper at `:1058`), and every other command that reads `spectra.config.json` directly (config,
  dashboard, delete, docs list-suites/show-suite, doctor, compile-prompt/analysis/update, ingest-analysis/grounding/
  tests/update/verdict, record-drop, index, init, list, review, run, show, suite, testimize, validate).
- **Fails-before / passes-after (T050)**. Run in a detached worktree at `1ba172a` with the new test files copied in.
  References to new APIs were adapted only where needed to compile: enum rename → old name; message constants →
  literals; tests of brand-new helpers dropped, since they fail to compile by design.

  | Item | Fails on `1ba172a` | Passes now |
  |---|---|---|
  | A1 | `UpdateHandlerFilePathTests` OrphanMark / DeleteOrphaned / OrphanedTwice | ✓ |
  | A2 | `SourceRefResolverOutsideSourceDirTests` (5 cases), `UpdateOutsideSourceDirTests`, `CriticVerificationCommandsTests` AbsoluteRefIntoOutOfRepoSourceDir / RefEscapingBothRoots_SaysWhy / RefOutsideRepo | ✓ |
  | A3 | `FrontmatterKeysGuardTests`, `TestFileWriterAllKeysTests` (compile: `TestCaseFrontmatterKeys` is new) | ✓ |
  | A4 | `FrontmatterCommentPlacementTests` CommentAboveAKey / LeadingComment / CommentAboveAnUnknownKey / RemovedKey | ✓ |
  | A5 | `ConfigLoaderEntryPointTests` (compile); `ConfigLoadingConsistencyTests` 22 rows (malformed config in all 10 commands, JSON shapes, `docs changed` defaults, ingest `--from` early error) | ✓ |
  | B1 | `SharedCriteriaFileTests.ApplyMove_CarriesOnlyTheMovedDocsCriteria` | ✓ |
  | B2 | `SharedCriteriaFileTests` ReIngest / Retire / UnownedCriteria | ✓ |
  | B3 | `SharedCriteriaFileTests.Compile_OffersOnlyTheDocsOwnCriteria` | ✓ |
  | B4 | `CriteriaChangesetTests.Tags_ReorderedOrRecased_AreNotAChange`, `IngestCriteriaChangesetTests.ReIngest_WithTagsReorderedAndRecased…` | ✓ |
  | B5 | `SkillsManifestTests.CriteriaSkill_WriteSteps_HandleExit1` (3 sections) | ✓ |
  | B6 | `IngestCriteriaIdentityTests.RejectedIngest_ReportsOutcomeRejected_NotExtracted`; `RejectedResult_…` (compile) | ✓ |
  | B7 | `CompileExtractionPromptExistingTests` MissingDocument / UnreadableDocument / MissingDocArgument; FailureHelper tests (compile) | ✓ |
  | B8 | `CriteriaScanTests.DocsChanged_HasNoScanOfItsOwn…` (structural; the behavioural check passes before and after, as R11 states) | ✓ |
  | B9 | `TextNormalizerTests` Comparisons_AreNotTags + 3 AngleBrackets cases | ✓ |

- **Deviations found while implementing**:
  - **T004**: `ai update` reads only top-level suite files (`BatchReadTestsTool` uses `GetFiles(suitePath, "*.md")`),
    so the nested-subfolder case can't happen through `update` and was dropped. The mismatched-name cases cover A1.
  - **T022**: the old collision fixture's shared file holds none of the re-ingested doc's criteria, so the extension
    would check nothing. It is covered by `SharedCriteriaFileTests.ReIngest_TakesTheDocsCriteriaOutOfTheSharedFile`.
  - **B2 / FR-007a**: `LeaveSharedFileAsync` runs only while a file is shared (two or more entries), so at least one
    entry still references the file afterwards. Once a single entry references the file, it is that doc's file
    (079 rule). Migration then moves it to the doc's own path, or `retire` deletes it whole. A former shared file
    leaves the workspace that way, and an unowned criterion in it is reported as removed by the last doc's
    re-ingest, not silently lost. spec.md Edge Cases records this.
  - **T041(e)**: `--doc` is a required option, so System.CommandLine rejects its absence before the handler runs.
    The handler's `--doc` check (now through `WriteFailure`) is defensive only, and that test row was dropped.
  - **T035(b)** (U5): the unreadable-document case runs on Windows only (`FileShare.None` is not a reliable read
    block elsewhere); (a) and (d) carry B7 on every platform.
  - **T044** (A5): the `--doc` refusal stays first, then config loads, then the document checks.
- **Quickstart spot checks (T051)**, with the built `Spectra.CLI.exe` in a scratch workspace:
  - A2: `source.local_dir: ../product-docs/docs` → `orphaned = 0`.
  - A1: `TC-100-login.md` is marked orphaned in place, with no `TC-100.md`.
  - A5: a malformed config makes `docs changed` and `ingest-criteria` both exit 1 with `spectra.config.json is invalid: …`.
    `ingest-criteria` in JSON mode gives one failure document.
