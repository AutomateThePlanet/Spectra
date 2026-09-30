# Tasks: Review Fixes for Spec 080 (Part C)

**Input**: Design documents from `/specs/081-review-fixes-080/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R5), data-model.md, contracts/cli-behaviour-changes.md, quickstart.md

**Tests**: REQUIRED. FR-016: every item gets a regression test that fails on `1c8d8de` and passes after the fix. In
every story, write the test first, run it, and confirm it fails for the stated reason before implementing. A test of
a brand-new API that fails to compile before the fix counts as failing.

**Conventions**:
- CLI command tests that capture the console or change the working directory go in
  `[Collection(CriteriaCommandsCollection.Name)]`, or in `[Collection("WorkingDirectory")]` for `UpdateWorkspace`
  tests.
- Workspaces come from `CriteriaTestWorkspace` / `OldLayoutCriteria` in `tests/Spectra.CLI.Tests/TestFixtures/`.
- For file edits that need `\` escapes, write the edit script to the scratchpad with the Write tool. Bash heredocs
  strip backslashes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: US1–US5 from spec.md.

---

## Phase 1: Setup

- [X] T001 Record the baseline on branch `081-review-fixes-080` (at `1c8d8de`). Run `dotnet build` and
  `dotnet test`. Expected: Core 735 / CLI 1498 with the one known `ProfileFormatLoaderTests` failure / Execution 228 /
  Integration 8. Paste the numbers into the Notes section of `specs/081-review-fixes-080/tasks.md`.

No foundational phase: the five stories share no prerequisite. Shared files are ordered under Dependencies.

---

## Phase 2: User Story 1 — Old shared criteria files keep their owners (P1) 🎯 MVP

**Goal**: FR-001–FR-003 / SC-001 (C1, R1).
**Independent Test**: A shared file whose criteria store absolute in-repo `source_doc` paths. Compile, ingest and
retire each respect ownership, and a doc whose index entry has an absolute `source_doc` is found.

- [X] T002 [P] [US1] Extend `tests/Spectra.Core.Tests/Criteria/CriteriaProvenanceTests.cs` for the new signature
  `OwnedBy(criteria, doc, repoRoot)`. Use a temp dir as the repository root. Cases:
  - a `source_doc` of `Path.Combine(root, "docs", "a", "login.md")` (absolute, inside) is owned by `docs/a/login.md`;
  - an absolute path outside the root is owned by none;
  - `docs\a\login.md` and `./docs/a/login.md` are still owned (the 080 cases, moved to the new signature);
  - null or empty is owned by none.

  Add `SameDocument(a, b, root)` cases: absolute vs relative → true; different docs → false; outside → false.

  Fails to compile before T005.
- [X] T003 [P] [US1] Write `tests/Spectra.CLI.Tests/Commands/Criteria/AbsoluteSourceDocTests.cs` (collection
  `CriteriaCommands`). Seed the absolute paths with `_ws.PathOf(...)`, written with `/` separators into YAML. Cases:
  - (a) **Shared file** (seed as in `SharedCriteriaFileTests.SeedAsync`, but A's per-criterion `source_doc` is
    absolute): `compile-extraction-prompt --doc docs/a/login.md` offers `AC-LOGIN-001`/`002`.
  - (b) Same seed: ingesting A with those IDs exits 0 and keeps them. Afterwards the shared file holds none of A's
    criteria.
  - (c) Same seed, with B's per-criterion `source_doc` written as `./docs\b\login.md`: after deleting B, `docs retire
    --doc docs/b/login.md` removes only `AC-LOGIN-003`.
  - (d) **Index lookup**: a per-doc criteria file plus an index entry whose `source_doc` is absolute, with
    `doc_hash` equal to the doc's hash. `docs changed --output-format json` reports no `new` or `deleted` entry for
    it.
  - (e) Same seed as (d): `ingest-criteria --dry-run` with the existing ID exits 0 (not 7). `--dry-run` skips
    migration, so this exercises the workspace lookup.
  - (f) Same seed as (d): `compile-extraction-prompt` offers the existing ID.

  Confirm (a)–(f) fail on the current code.
- [X] T004 [US1] Update the 080 callers of the old two-argument `OwnedBy` in
  `tests/Spectra.Core.Tests/Criteria/CriteriaProvenanceTests.cs`. They are folded into T002; delete the old-signature
  cases so the file compiles.
- [X] T005 [US1] In `src/Spectra.Core/Criteria/CriteriaProvenance.cs`:
  - Change `OwnedBy` to `OwnedBy(IEnumerable<AcceptanceCriterion> criteria, string doc, string repoRoot)`. It
    normalises the doc and each `source_doc` with `CriteriaPaths.NormalizeSourceDoc(value, repoRoot)`, compares
    `.Path` with `OrdinalIgnoreCase`, and treats a failed normalisation as owned by none.
  - Add `public static bool SameDocument(string? a, string? b, string repoRoot)` with the same rule.
  - Update the XML docs (the lexical rule is gone).

  Make T002 pass.
- [X] T006 [US1] Pass the repository root at every `OwnedBy` call:
  - `src/Spectra.CLI/Extraction/CriteriaIngestor.cs` (`currentDir`);
  - `src/Spectra.CLI/Commands/Docs/DocsApplyMoveCommand.cs` and `src/Spectra.CLI/Commands/Docs/DocsRetireCommand.cs`
    (`root`);
  - `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs` (`currentDir`);
  - `src/Spectra.CLI/Criteria/CriteriaStore.cs` `LeaveSharedFileAsync` (`RepoRoot`).
- [X] T007 [US1] Use `CriteriaProvenance.SameDocument` for index lookups:
  - `CriteriaWorkspace.FindDocumentSource` in `src/Spectra.CLI/Criteria/CriteriaStore.cs`;
  - the source lookup in `CompileExtractionPromptCommand.LoadExistingCriteriaAsync`;
  - `DocsChangedHandler.FindDocumentSource` in `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs`. Give it
    `currentDir`, and check that its moved/deleted detection compares the same normalised form.

  Make T003 pass. `SharedCriteriaFileTests`, `IngestCriteriaStorageTests`, `DocsChanged*Tests`, `DocsRetireTests` and
  `DocsApplyMoveTests` must stay green.

**Checkpoint**: US1 is complete.

---

## Phase 3: User Story 2 — Config sections a command doesn't use don't block it (P1)

**Goal**: FR-004–FR-006 / SC-002 (C2, R2).
**Independent Test**: A config with no `ai` is accepted everywhere. A missing `source`/`tests` gives defaults and a
warning in read commands, and exit 1 naming the section in the write commands that read it.

- [X] T008 [P] [US2] Update `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`:
  - **Replace** `MissingAiSection_IsAnError_ForBothEntryPoints`, which encoded the defect, with
    `MissingAiSection_IsAccepted_ForBothEntryPoints` (reads `None`): the config loads, with no warnings.
  - `LoadForReadAsync(root, ConfigSections.Tests)` on a config without `tests` returns `TestsConfig` defaults
    (`Dir == "test-cases/"`) and one warning containing `'tests'`.
  - `LoadForWriteAsync(root, ConfigSections.Tests)` on a config without `tests` returns
    `Error == "spectra.config.json is invalid: missing 'tests' section"`.
  - `LoadForWriteAsync(root, ConfigSections.None)` with neither `source` nor `tests` succeeds, with no warnings.
  - `"source": 5` (wrong type) gives the invalid prefix from both entry points.
  - Section keys match case-insensitively (`"Tests": {…}` counts as present), and a config with `//` comments and a
    trailing comma still loads.

  Fails to compile before T010.
- [X] T009 [US2] Extend `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs` with a `WriteConfig(json)`
  helper:
  - (a) Theory over all ten commands, with a config of `source`, `tests`, `coverage` and no `ai`: none exits 1 for
    config reasons. Neither config message appears; `docs retire`/`apply-move` may refuse with exit 4 for their own
    reasons, so assert not exit 1.
  - (b) No `tests`: `compile-critic-prompt --suite s --test TC-1` exits 0 and stderr has a `Warning:` line naming
    `'tests'`.
  - (c) No `tests`: `ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json` and `docs retire --doc
    docs/gone.md` exit 1 with `spectra.config.json is invalid: missing 'tests' section`. The snapshot is unchanged.
  - (d) No `source` and no `tests`: `docs apply-move --from docs/gone.md --to docs/a.md`, `docs migrate-criteria` and
    `ai analyze --import-criteria import.yaml` are not rejected with the missing-section message.
  - (e) No `source`: `docs changed --output-format json` exits 0 and its JSON `warnings` contains a message naming
    `'source'`.

  Confirm (a), (b), (c) and (e) fail on the current code.
- [X] T010 [US2] In `src/Spectra.Core/Config/ConfigLoader.cs`:
  - Add `[Flags] public enum ConfigSections { None = 0, Source = 1, Tests = 2 }`.
  - Extend `ConfigLoadResult` with `IReadOnlyList<string> Warnings` (empty by default).
  - Give `LoadForReadAsync` and `LoadForWriteAsync` a `ConfigSections reads = ConfigSections.None` parameter.
  - In `LoadEntryAsync`, after the file-exists check:
    1. Read the text.
    2. `JsonNode.Parse` it with `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }`. On a
       `JsonException` → `InvalidConfigPrefix + message`.
    3. If the root is not a `JsonObject` → invalid.
    4. For each of `source`, `tests`, `ai` with no case-insensitive match among the root's keys, add an empty
       `JsonObject` and record it as missing.
    5. Serialize and pass the result to the existing `Load` (keeps `Validate` and type errors).
    6. For each recorded section that is in `reads`: for a write load, return the first as
       `InvalidConfigPrefix + $"missing '{name}' section"`; for a read load, add
       `$"spectra.config.json has no '{name}' section; using the built-in defaults."` to `Warnings`.

  Make T008 pass. `ConfigLoaderTests` stays green.
- [X] T011 [US2] Make `src/Spectra.CLI/Criteria/CriteriaCommandIO.cs` `LoadConfigAsync(repoRoot, ConfigSections reads,
  ct)` forward `reads` to `LoadForWriteAsync`. Pass the declared reads:
  - `ConfigSections.Tests` in `IngestCriteriaCommand.cs` and `DocsRetireCommand.cs`;
  - `ConfigSections.None` in `DocsApplyMoveCommand.cs` and `DocsMigrateCriteriaCommand.cs`;
  - `ConfigSections.None` in `AnalyzeHandler.RunImportCriteriaAsync`.
- [X] T012 [US2] Pass `ConfigSections.Source | ConfigSections.Tests` to `LoadForReadAsync` in
  `CompileCriticPromptCommand.cs`, `CompileRepairPromptCommand.cs` and `CompileRepairBatchCommand.cs` (in
  `src/Spectra.CLI/Commands/Generate/`). Print each `loaded.Warnings` entry to stderr as `Warning: {w}` right after
  loading. `CompileExtractionPromptCommand.cs` keeps `ConfigSections.None`.
- [X] T013 [US2] In `src/Spectra.CLI/Commands/Docs/DocsChangedHandler.cs`, pass `ConfigSections.Source`. Add
  `loaded.Warnings` to the result's `warnings` list, which appears in JSON output and in the human warnings, and print
  each one to stderr as `Warning: {w}`. Make T009 pass.

**Checkpoint**: US2 is complete.

---

## Phase 4: User Story 3 — Markup-only edits are not changes (P2)

**Goal**: FR-007–FR-009 / SC-003 (C3, R3).
**Independent Test**: Every added element and every bare boolean attribute strips cleanly. Every B9 case and
placeholder word stays text.

- [X] T014 [P] [US3] Extend `tests/Spectra.Core.Tests/Text/TextNormalizerTests.cs`:
  - Theory `AddedElements_AreStripped`: one row per element from FR-007 (`<figure>x</figure>`, `<col span=2>`,
    `<wbr>`, `<source src="a.mp4">`, …), each expecting `x` or empty, whitespace-normalised.
  - Theory `BooleanAttributes_AreAccepted`: `<td nowrap>cell</td>` → `cell`, `<details open>` + summary,
    `<input disabled>`, `<video controls autoplay muted src="x">v</video>` → `v`, `<td align=center nowrap>c</td>` →
    `c`.
  - Theory `StillText`: `<b openx>` (not a boolean name), `if a<b and c>d then`, `send <data> now`, `the <time>
    field`, `the <label> text`, `the <header> row` — each normalises to its lower-cased text unchanged.
  - Keep every 080 row.

  Confirm the added-element and boolean-attribute rows fail.
- [X] T015 [US3] In `src/Spectra.Core/Text/TextNormalizer.cs`, extend the `HtmlTagRegex` element alternation with
  FR-007's list. Change the attribute group to `(?:\s+(?:NAME\s*=\s*VALUE|(?:BOOLEAN_LIST)(?=[\s/>])))*`, where
  `BOOLEAN_LIST` is the R3 list. Update the XML summary. Make T014 pass. `CriteriaMatcherTests` and
  `CriteriaChangesetTests` stay green.

**Checkpoint**: US3 is complete.

---

## Phase 5: User Story 4 — One failure shape for every criteria command (P2)

**Goal**: FR-010–FR-013 / SC-004 (C4, R4).
**Independent Test**: A JSON-mode failure from each of the five commands parses as one document with `command`,
`success`, `error_code`, `errors` and `warnings`. The skill reads `errors[0]` everywhere.

- [X] T016 [P] [US4] Write `tests/Spectra.CLI.Tests/Commands/Criteria/CriteriaFailureShapeTests.cs` (collection
  `CriteriaCommands`). Theory over one failure per command, in JSON mode:
  - `ai compile-extraction-prompt --doc docs/missing.md` (`DOCUMENT_NOT_FOUND`);
  - `ai compile-extraction-prompt` with no `--doc` (exit 4, `MISSING_INPUT` plus `missing_input`);
  - `ai ingest-criteria --doc docs/a.md --from missing.json` (`error_code` null);
  - `ai ingest-criteria` with a foreign ID (exit 7, `INVALID_CRITERION_ID` plus `outcome: "Rejected"`);
  - `docs retire --doc docs/none.md` (exit 4, `NOT_INDEXED`);
  - `docs apply-move --from docs/none.md --to docs/a.md` (exit 4);
  - `docs migrate-criteria` with an unreadable index (exit 1): write garbage into `_criteria_index.yaml`.

  For each, assert:
  - stderr parses as one document;
  - `command` is the expected name;
  - `success` is false;
  - `error_code` is as listed;
  - `errors` is a non-empty array;
  - `warnings` is an array;
  - the extras (`outcome`, `missing_input`) are present only where listed;
  - neither `refused` nor `message` exists.

  Confirm the rows fail on the current code.
- [X] T017 [P] [US4] Extend `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`: the `spectra-criteria` Step 2a
  section (between `**2a.` and `**2b.`) mentions `errors[0]` and never `` `message` ``. Confirm it fails.
- [X] T018 [US4] In `src/Spectra.CLI/Criteria/CriteriaCommandIO.cs`, change `WriteFailure(bool json, string command,
  string? errorCode, string message, IReadOnlyList<string>? warnings = null, IReadOnlyDictionary<string, object?>?
  extras = null)`:
  - **JSON mode**: serialise the five fields with `warnings` defaulting to `[]`, with `extras` merged after them.
    Build the object with an ordered `Dictionary<string, object?>`, so the field order is deterministic.
  - **Human mode**: print `Warning: {w}` lines first, then the existing line.
- [X] T019 [US4] In `src/Spectra.CLI/Commands/Generate/IngestCriteriaCommand.cs`, route the classified-failure JSON
  (~line 158) through `CriteriaCommandIO.WriteFailure(json, "ingest-criteria", result.ErrorCode,
  result.Errors.FirstOrDefault() ?? …, warnings: null, extras: { ["outcome"] = result.Outcome.ToString() })`.
  - Keep all messages: pass `errors` as the full list. Extend `WriteFailure` with an `IReadOnlyList<string>` overload
    if needed.
  - Human mode stays `Ingest failed [...] — nothing persisted:` plus the error lines, unchanged (FR-012).
- [X] T020 [US4] In `src/Spectra.CLI/Commands/Generate/CompileExtractionPromptCommand.cs`, make `WriteFailure` and
  `WriteRefusal` delegate to `CriteriaCommandIO.WriteFailure(json, "compile-extraction-prompt", …, warnings, extras)`:
  - a refusal uses `error_code: "MISSING_INPUT"` and `extras: { ["missing_input"] = missingInput }`;
  - human mode stays warnings-then-error, with the same refusal wording.
- [X] T021 [US4] Update the 080 assertions that read the replaced fields. Make T016 pass.
  - `tests/Spectra.CLI.Tests/Commands/Criteria/CompileExtractionPromptExistingTests.cs`: `message` → `errors[0]`;
    `refused` → `success == false` and `error_code == "MISSING_INPUT"`. The FailureHelper tests read
    `warnings` as before.
  - `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs` line ~114: `message` → `errors[0]`.
- [X] T022 [US4] In `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md` Step 2a, change "Exit 4 (refused) →
  show the `message` and skip that doc." to "show `errors[0]`". Add an exit-1 line for `DOCUMENT_NOT_FOUND` /
  `DOCUMENT_UNREADABLE`: report `error_code` and `errors[0]`, and skip that doc. Make T017 pass, and run
  `dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Skills"`.

**Checkpoint**: US4 is complete.

---

## Phase 6: User Story 5 — Rewrites keep trailing lines of a keep-chomped value (P2)

**Goal**: FR-014–FR-015 / SC-005 (C5, R5).
**Independent Test**: Removing the key after a `|+` value (top-level or nested) leaves the re-parsed value
identical, and a comment after the value still moves with the key below.

- [X] T023 [P] [US5] Add a pin test to `tests/Spectra.Core.Tests/Parsing/TestCaseLayoutTests.cs`: parse YAML
  `notes: |+\n  a\n\n\nnext: 1\n` with `YamlDotNet.Core.Parser` and record the `Scalar` event's `Start.Index` and
  `Style` for `a`. Assert the style is `Literal`, and assert what the index points at (the `|` indicator, or the first
  content line), so the R5 scan is pinned. The test documents the observed behaviour and fails if YamlDotNet changes
  it.
- [X] T024 [P] [US5] Extend `tests/Spectra.CLI.Tests/IO/FrontmatterCommentPlacementTests.cs` with its own fixtures:
  - (a) `description: |+` with two lines of content and two trailing blank lines, followed by `depends_on: TC-001`.
    After `t with { DependsOn = null }`, rewrite and re-parse: `Description` is equal to the original parse's,
    including trailing `\n\n`.
  - (b) `custom:` / `  notes: |+` / content / two trailing blank lines, followed by `status: active`. After
    `t with { Status = null }`, `Custom["notes"]` is unchanged after re-parsing.
  - (c) `description: >+` with an indentation indicator (`>2+`), same check as (a).
  - (d) A `|+` value, one blank line, `# above next`, `depends_on: TC-001`. Rewrite with `depends_on` changed: `#
    above next` is still directly above `depends_on`.
  - (e) Clip (`|`) and strip (`|-`) values followed by a removed key behave as in 080 (the existing
    comment-placement tests stay green unchanged).
  - (f) Byte identity for an unchanged file with a `|+` value.

  Confirm (a)–(c) fail on the current code.
- [X] T025 [US5] In `src/Spectra.Core/Parsing/TestCaseParser.cs` `CaptureFrontmatterEntries`:
  - While walking events, track for each top-level entry (the one opened by the last depth-1 key) the last `Scalar`
    event at any depth. On a `Literal` or `Folded` style, determine keep-chomping from the header, per R5 and the T023
    pin: find the `|`/`>` indicator on the header line and read the following `+`/`-`/digit characters.
  - Pass a `keepTrailing` flag per entry to `TrimTrailingDetachedLines(span, keepTrailing)`. When set, only lines from
    the first column-0 `#` line in the trailing run onwards detach; the blank lines before it stay in `RawText`.
  - Keep the invariant `Debug.Assert`.

  Make T024 pass. `TestCaseLayoutTests`, `TestFileWriterRoundTripTests`, `WriterPathPreservationTests` and
  `FrontmatterCommentPlacementTests` (080 rows) stay green unchanged.

**Checkpoint**: US5 is complete, and all five items are fixed.

---

## Phase 7: Polish & Cross-Cutting

- [X] T026 [P] `CHANGELOG.md`: add a "Changed/Fixed (review fixes, Spec 081)" subsection under the unreleased
  `[2.6.0]` entry, covering C1–C5. Call out:
  - a config without `ai` is accepted;
  - the missing-section rules, read and write;
  - the unified criteria failure JSON (`compile-extraction-prompt` loses `refused`/`message`);
  - the markup elements and boolean attributes that are now stripped;
  - keep-chomped values are preserved.
- [X] T027 [P] `CLAUDE.md` "Recent Changes": add a `081-review-fixes-080` entry above 080, listing `ConfigSections`,
  `ConfigLoadResult.Warnings`, `OwnedBy(…, repoRoot)`, `SameDocument`, the `WriteFailure` shape and the keep-chomp
  split, plus the final counts.
- [X] T028 [P] `docs/cli-reference.md`, per `contracts/cli-behaviour-changes.md`:
  - replace the 080 Configuration table with the section-aware one;
  - update the compile-extraction-prompt failure bullets and the ingest-criteria failure bullet to the unified shape;
  - add a note on normaliser markup under ingest-criteria.
- [X] T029 Run the full `dotnet build` and `dotnet test`. The only allowed failure is the known
  `ProfileFormatLoaderTests` one. Record the counts in the Notes and in `CLAUDE.md`.
- [X] T030 Fails-before check (FR-016 / SC-006):
  1. `git worktree add --detach <scratchpad>/wt-1c8d8de 1c8d8de`.
  2. Copy in the new and changed test files. Adapt only references to new APIs so they compile, as in 080 T050:
     brand-new API tests are dropped as compile-fail by design, and the `OwnedBy` signature is mapped back where
     possible.
  3. Build, then run the tests of each item and record the failing names in a table in the Notes.
  4. Remove the worktree.
- [X] T031 Run the quickstart spot checks (`specs/081-review-fixes-080/quickstart.md` steps 1–2) with the built
  `Spectra.CLI.exe` in a scratch workspace. Record the results in the Notes.

---

## Dependencies & Execution Order

- **T001 first.** After that the stories are independent in behaviour, but some share files:
  - **`CompileExtractionPromptCommand.cs`**: T006/T007 (US1) → T020 (US4).
  - **`CriteriaCommandIO.cs`**: T011 (US2) → T018 (US4).
  - **`DocsRetireCommand.cs`**: T006 (US1) → T011 (US2).
  - **`IngestCriteriaCommand.cs`**: T011 (US2) → T019 (US4).
  - **`DocsChangedHandler.cs`**: T007 (US1) → T013 (US2).
  - **`ConfigLoadingConsistencyTests.cs`**: T009 (US2) → T021 (US4).
  - **`CriteriaProvenanceTests.cs`**: T002 and T004 edit the same file — do them together.
- **US3** (normaliser) and **US5** (parser) touch no shared file and can go at any time.
- **Recommended order**: T001 → US1 → US2 → US4 → US3 → US5 → Polish.

## Parallel Opportunities

- Test writing within a story: T002/T003; T008 alongside T009's fixture work; T016/T017; T023/T024.
- US3 and US5 can run alongside US1/US2/US4, since they touch different files.
- T026/T027/T028.

```text
# Example: start US3 and US5 while US1 is in progress
T014 TextNormalizerTests.cs | T023 TestCaseLayoutTests.cs | T024 FrontmatterCommentPlacementTests.cs
```

## Implementation Strategy

- **MVP**: US1 (C1). It is the one item that silently changes criterion IDs and breaks tests' links.
- **Increments**: US2 (configs rejected for no reason), then US4 (output contract), then US3 and US5
  (comparison noise and a narrow data-loss path).
- After each story, run `dotnet test` for the touched projects.
- Commit once at the end as `fix(081): …`. Merge only with the user's approval, and never push.

## Notes

- **Baseline (T001)**, at `1c8d8de`: Core 735 / CLI 1498 (1 known failure:
  `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown`) / Execution 228 / Integration 8.
- **Final counts (T029)**: Core 785 / CLI 1535 (the same 1 known failure) / Execution 228 / Integration 8. Build has 0
  warnings.
- **Fails-before / passes-after (T030)**. Run in a detached worktree at `1c8d8de` with the new test files copied in.
  Tests of brand-new APIs were dropped there because they fail to compile by design: `CriteriaProvenanceTests` (the
  3-argument `OwnedBy` and `SameDocument`), and the `ConfigSections`/`Warnings` cases in `ConfigLoaderEntryPointTests`.

  | Item | Fails on `1c8d8de` | Passes now |
  |---|---|---|
  | C1 | `AbsoluteSourceDocTests` ×6 (shared-file compile / ingest / retire-absolute; index lookup in `docs changed`, `--dry-run` ingest, compile); `CriteriaProvenanceTests` (compile) | ✓ |
  | C2 | `ConfigLoaderEntryPointTests.MissingAiSection_IsAccepted` ×2; `ConfigLoadingConsistencyTests` `NoAiSection` ×10, `ReadCommand_MissingTests`, `WriteCommand_MissingTests` ×2, `WriteCommand_ThatReadsNeither` ×3, `DocsChanged_MissingSource` | ✓ |
  | C3 | `TextNormalizerTests.AddedElements_AreStripped` ×21, `BooleanAttributes_AreAccepted` ×8 (the `StillText` guards pass before and after) | ✓ |
  | C4 | `CriteriaFailureShapeTests` ×7, `SkillsManifestTests.CriteriaSkill_CompileStep_ReadsErrorsNotMessage`, plus the updated 080 assertions in `CompileExtractionPromptExistingTests` ×2 and `ConfigLoadingConsistencyTests.MalformedConfig_JsonMode_CompileExtraction` | ✓ |
  | C5 | `FrontmatterCommentPlacementTests` KeepChompedValue / NestedKeepChompedValue / FoldedKeepValue (the comment-placement and byte-identity cases pass before and after) | ✓ |

- **Deviations found while implementing**:
  - **T003(c) retire**: `./docs\b\login.md` already matched lexically in 080, so it is kept as a guard row. An
    absolute-path row for B was added as the fails-before case.
  - **T004**: folded into T002. The file was rewritten for the 3-argument signature.
  - **T019**: ingest's human-mode failure text is kept as is (FR-012). Only its JSON goes through the shared
    `CriteriaCommandIO.WriteFailureJson`. The same applies to compile-extraction-prompt's human text (warnings, then
    the error or refusal line). `WriteFailure` keeps its `message [code]` human form for the three docs commands.
  - **T023**: YamlDotNet puts a block scalar's start mark at the `|`/`>` indicator, which the test now pins.
    `IsKeepChompedBlock` reads the header directly from there.
- **Quickstart spot checks (T031)**, with the built `Spectra.CLI.exe` in a scratch workspace:
  - No `ai` section: `docs changed` and `ingest-criteria` both exit 0.
  - No `tests` section: `ingest-criteria` exits 1 with `spectra.config.json is invalid: missing 'tests' section`, and
    `compile-critic-prompt` prints `Warning: spectra.config.json has no 'tests' section; using the built-in defaults.`
  - C4 in JSON mode:
    - `compile-extraction-prompt` without `--doc` gives `{"command":"compile-extraction-prompt","success":false,
      "error_code":"MISSING_INPUT","errors":[…],"warnings":[],"missing_input":"document_path"}`.
    - `docs retire --doc docs/none.md` gives `{"command":"docs-retire","success":false,"error_code":"NOT_INDEXED",
      "errors":[…],"warnings":[]}` with exit 4.
