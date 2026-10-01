# Tasks: Review Fixes for Spec 082 — One Config Rule

**Input**: Design documents from `/specs/083-review-fixes-082/`
**Prerequisites**: plan.md, spec.md, research.md (R0–R5), data-model.md, contracts/cli-behaviour-changes.md, quickstart.md

**Tests**: REQUIRED. FR-019: every fix has a regression test that fails on `3594d62` and passes after. Write the test
first, run it, and confirm it fails for the stated reason before implementing. A test of a changed or new API that
fails to compile before the fix counts as failing.

**Conventions**:
- CLI command tests go in `[Collection(CriteriaCommandsCollection.Name)]`.
- Workspaces come from `CriteriaTestWorkspace`, which runs commands from its root.
- `ConfigLoadingConsistencyTests` already has `WriteConfig(params string[])` and the constants `SourceJson`,
  `TestsJson` and `CoverageJson`.
- `InitHandler` is constructed as `new InitHandler(NullLogger<InitHandler>.Instance, root)` and run with
  `HandleAsync(force: false, skipSkills: true)`. It refuses if a `spectra.config.json` exists, so delete the
  fixture's config first.
- For edits that need `\` escapes, write the script to the scratchpad with the Write tool. Bash heredocs strip
  backslashes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: US1–US5 from spec.md.

Phase order. US1, US2 and US3 are all P1. US3 (the rule itself) goes first, because US2's guard test only passes once
the rule is in, and US1's end-to-end test exercises it.

---

## Phase 1: Setup

- [X] T001 Record the baseline on `083-review-fixes-082` (at `3594d62`). Run `dotnet build` and `dotnet test`.
  Expected: Core 812 / CLI 1568 with 1 known `ProfileFormatLoaderTests` failure / Execution 228 / Integration 8. Paste
  the numbers into the Notes section of `specs/083-review-fixes-082/tasks.md`.

No foundational phase: the rule change lives in US3.

---

## Phase 2: User Story 3 — One predictable config rule (P1)

**Goal**: FR-001–FR-005 (R1).
**Independent Test**: The T1 matrix. Every FR-001 row except the duplicate-key rows (US4) is checked for one read
command and one write command: the exit code, `error_code`, exact warning count, and the criteria location for
successful writes.

- [X] T002 [P] [US3] Replace the 082 tests that assert the removed rule, in
  `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`:
  - delete `Write_MissingRequiredSection_IsAnError_NamingTheSection`;
  - rewrite `Write_MissingSectionItOnlyReads_Warns` as `LoadForWriteAsync(root, reads: ConfigSections.Tests)` → one
    `tests` warning. Always pass `reads:` by name; see T022;
  - change `Read_MissingCoverage_UsesDefaults_AndWarns` into `MissingCoverage_UsesDefaults_WithoutWarning`, over both
    entry points and `reads: Source | Tests`: `Coverage.CriteriaDir == "docs/criteria"` and no warnings;
  - rewrite `NullSection_BehavesLikeAMissingSection` without `requires`. Load with `reads: Source | Tests`, and assert
    the `coverage` and `ai` rows give zero warnings;
  - remove the `CONFIG_SECTION_MISSING` assertions from `ErrorCodes_NameEachConfigProblem`.

  Fails to compile on `3594d62`?
  - No: `ConfigSections.Coverage` and `requires` still exist there, so the rewritten tests compile.
  - The fails-before evidence is the new "no warning for coverage" assertion, which fails at `3594d62`.
- [X] T003 [US3] Write `tests/Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs` (T1).
  - Use a `TheoryData` with one row per FR-001 situation, with columns `(label, configJson or null for no file,
    criticExit, criticCode, criticWarnings, ingestExit, ingestCode, ingestWarnings)`.
  - Rows:
    - no file;
    - malformed `{"source": {`;
    - root not an object (`[]`, `null`);
    - `source` missing, and `source: null`;
    - `tests` missing, and `tests: null`;
    - `coverage` missing, and `coverage: null`;
    - `ai: null`, and `generation` missing.

    Base every non-error row on the real template content: read `Spectra.CLI.Templates.spectra.config.json` from the
    CLI assembly's manifest resources, then remove or null the key with `JsonNode`.
  - For each row:
    - run `ai compile-critic-prompt --suite s --test TC-1 --output-format json` and `ai ingest-criteria --doc
      docs/a.md --from .spectra/criteria.json --output-format json`;
    - assert the exit codes;
    - assert `error_code` from the failure JSON on stderr;
    - count the config warnings:
      - critic: stderr lines containing `spectra.config.json has no`;
      - ingest: `warnings` entries containing it;
    - for successful ingest rows, assert `docs/criteria/docs/a.criteria.yaml` exists.
  - Fixtures: `docs/a.md`, `test-cases/s/TC-1.md`, `test-cases/s/_index.json` and `.spectra/criteria.json`, as in
    `ConfigLoadingConsistencyTests`.
  - Add a Windows-only row for an unreadable file (exclusive lock) → both commands `CONFIG_INVALID`.

  Confirm that the `coverage` rows and the `tests`-on-ingest rows fail.
- [X] T004 [US3] Replace the 082 tests that assert the removed rule, in
  `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs`:
  - `WriteCommand_MissingCoverage_Exits1_AndWritesNothing` → `WriteCommand_MissingCoverage_Succeeds_WithoutWarning`.
    Over the 5 write commands, and for each with `--dry-run`: no `missing 'coverage'` text and no `coverage` warning.
    - Seed the retire and apply-move rows as the other retire tests in this file do (ingest first with the full
      config, delete or move the doc, then `WriteConfig(SourceJson, TestsJson)`).
    - The non-dry-run ingest row asserts `docs/criteria/docs/a.criteria.yaml` exists.
    - Dry-run rows assert an unchanged snapshot.
  - `ReadCommand_MissingCoverage_UsesDefaults_AndWarns` → `ReadCommand_MissingCoverage_NoWarning`: exit 0 and no
    `'coverage'` warning.
  - `NullCoverage_RefusesAWrite_NamingTheSection` → `NullCoverage_WriteSucceeds`.
  - Delete the three `nocoverage` rows of `ConfigErrorCodes_JsonMode`. The `none` and `malformed` rows stay.
- [X] T005 [P] [US3] In `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`, change
  `CriteriaSkill_NamesConfigErrorCodes`: it must contain `CONFIG_MISSING` and `CONFIG_INVALID`, and must NOT contain
  `CONFIG_SECTION_MISSING`. Confirm it fails.
- [X] T006 [US3] Implement the rule in `src/Spectra.Core/Config/ConfigLoader.cs` (R1):
  - remove `Coverage` from `ConfigSections`;
  - remove the `requires` parameter from `LoadForWriteAsync` and `LoadEntryAsync`;
  - remove `ErrorConfigSectionMissing` and its branch;
  - keep `coverage` in `CheckedSections` with flag `None`, so it is filled silently;
  - warnings go only to missing sections in `reads`;
  - update the XML docs. They must state that no section is required, and that any new rule must be checked against
    the embedded init template and a pre-existing workspace (FR-021).
- [X] T007 [US3] Update the call sites to the FR-003 declarations:
  - `src/Spectra.CLI/Criteria/CriteriaCommandIO.cs`: `LoadConfigAsync(repoRoot, ConfigSections reads, ct)`, with no
    `requires`.
  - `IngestCriteriaCommand.cs`, `DocsRetireCommand.cs`: `Tests`.
  - `DocsApplyMoveCommand.cs`, `DocsMigrateCriteriaCommand.cs`, `AnalyzeHandler.RunImportCriteriaAsync`: `None`.
  - `CompileExtractionPromptCommand.cs`: `None`.
  - `CompileCriticPromptCommand.cs`: `Source | Tests`.
  - `DocsChangedHandler.cs`: `Source`.

  Remove the 082 comments that say `coverage` is required or read.
- [X] T008 [US3] In `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md`, replace the four
  "`CONFIG_MISSING` / `CONFIG_INVALID` / `CONFIG_SECTION_MISSING`" mentions with "`CONFIG_MISSING` /
  `CONFIG_INVALID`".
  - Make T002–T005 pass.
  - `dotnet test --filter "FullyQualifiedName~Config|FullyQualifiedName~Criteria|FullyQualifiedName~Docs|FullyQualifiedName~Skills"`
    is green.

**Checkpoint**: US3 is complete.

---

## Phase 3: User Story 1 — Criteria commands work in an init-created workspace (P1) 🎯 MVP

**Goal**: FR-006, FR-015 (F1, T2).
**Independent Test**: The real `spectra init`, then `docs changed`, `compile-extraction-prompt`, `ingest-criteria`,
`docs retire` and `docs apply-move`, all succeeding with 0 config warnings.

- [X] T009 [P] [US1] Add `Init_WritesCoverageSection` to `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs`: after
  `HandleAsync`, the written config has `coverage.criteria_dir == "docs/criteria"` and `coverage.criteria_file ==
  "docs/criteria/_criteria_index.yaml"`. Confirm it fails.
- [X] T010 [US1] Write `tests/Spectra.CLI.Tests/Commands/Criteria/InitTemplateWorkspaceTests.cs`, class
  `InitTemplateWorkspaceTests` (collection `CriteriaCommands`), with a shared helper `RunWorkflowAsync(CriteriaTestWorkspace ws)`:
  1. Write `docs/a.md`, `docs/gone.md` and `docs/move.md`.
  2. Run `docs changed --output-format json`: exit 0.
  3. Run `ai compile-extraction-prompt --doc docs/a.md`: exit 0.
  4. Ingest each doc with a fixture response, in JSON mode: exit 0.
  5. Delete `docs/gone.md` and run `docs retire --doc docs/gone.md --output-format json`: exit 0.
  6. Move `docs/move.md` to `docs/moved.md` (same content) and run `docs apply-move --from docs/move.md --to
     docs/moved.md --output-format json`: exit 0.
  7. Collect every stdout and stderr, and return the count of `spectra.config.json has no` occurrences, plus whether
     `docs/criteria/docs/a.criteria.yaml` exists.

  Test `RealTemplate_WorkflowSucceeds_WithoutConfigWarnings`:
  - delete the fixture config;
  - run `new InitHandler(NullLogger<InitHandler>.Instance, ws.Root).HandleAsync(force: false, skipSkills: true)`,
    which must return 0;
  - run the workflow;
  - assert 0 warnings and that the criteria file exists.

  Confirm it fails on the current code: `ingest-criteria` exits 1 with `CONFIG_SECTION_MISSING`.
- [X] T011 [US1] In `src/Spectra.CLI/Templates/spectra.config.json`, add after the `tests` section:

  ```json
  "coverage": {
    "criteria_dir": "docs/criteria",
    "criteria_file": "docs/criteria/_criteria_index.yaml"
  }
  ```

  Make T009 and T010 pass. `InitCommandTests` and `QuickstartWorkflowTests` stay green.

**Checkpoint**: US1 is complete.

---

## Phase 4: User Story 2 — Existing workspaces keep working (P1)

**Goal**: FR-007, FR-016 (T3).
**Independent Test**: The real template with `coverage` removed, then the same workflow: 0 warnings, and criteria in
`docs/criteria`.

- [X] T012 [US2] Add `PreExistingWorkspace_WithoutCoverage_WorkflowSucceeds_WithoutConfigWarnings` to
  `tests/Spectra.CLI.Tests/Commands/Criteria/InitTemplateWorkspaceTests.cs`:
  - run `init` as in T010;
  - parse the written `spectra.config.json` with `JsonNode`, remove `coverage`, and write it back;
  - run `RunWorkflowAsync`;
  - assert 0 config warnings and that `docs/criteria/docs/a.criteria.yaml` exists.

  It fails on `3594d62` (ingest refuses), and passes once T006–T007 are in, with no further code.

**Checkpoint**: US2 is complete.

---

## Phase 5: User Story 4 — Case-variant duplicate keys are rejected (P2)

**Goal**: FR-008, FR-018 (T5, R2).
**Independent Test**:
- `tests`/`Tests` and `generation`/`Generation` → `CONFIG_INVALID` naming the key, for one read and one write command;
- an exact duplicate key → `CONFIG_INVALID`, not a crash.

- [X] T013 [P] [US4] In `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`:
  - Theory `CaseVariantDuplicateKeys_AreInvalid_NamingTheKey`, with rows `"tests": {}, "Tests": null` and
    `"generation": {}, "Generation": {}`. For both entry points: `ErrorCode == "CONFIG_INVALID"`, and `Error` contains
    both spellings.
  - Fact `ExactDuplicateKey_IsInvalid_NotAnException`: `{"source": {}, "source": {}}` gives `CONFIG_INVALID` from both
    entry points, with no exception thrown.

  Confirm both fail. The exact duplicate currently throws `ArgumentException`.
- [X] T014 [US4] Add rows to `tests/Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs`, based on the template: a
  case-variant `tests`/`Tests` row, and a `generation`/`Generation` row. Both commands exit 1 with `CONFIG_INVALID`,
  and the message names the key. Confirm they fail.
- [X] T015 [US4] In `src/Spectra.Core/Config/ConfigLoader.cs` `LoadEntryAsync`:
  - materialise `root.Select(kv => kv.Key).ToList()` inside the parse `try`;
  - catch `ArgumentException` alongside `JsonException` → `CONFIG_INVALID` with the parser's message;
  - then group the keys `OrdinalIgnoreCase`. The first group with more than one key → `CONFIG_INVALID`,
    `InvalidConfigPrefix + $"duplicate top-level key {string.Join(" / ", group.Select(k => $"'{k}'"))} (keys may not
    differ only in case)"`;
  - remove the 082 A3 "a present non-null match wins" branch, which is now unreachable. Missing or null filling works
    on the single key.

  Make T013 and T014 pass.

**Checkpoint**: US4 is complete.

---

## Phase 6: User Story 5 — A real wording change is never hidden (P2)

**Goal**: FR-009–FR-013, FR-017 (T4, R4).
**Independent Test**: The FR-012 decision table plus every existing row, unchanged.

- [X] T016 [P] [US5] Add a theory `HtmlForms_DecisionTable` to `tests/Spectra.Core.Tests/Text/TextNormalizerTests.cs`,
  with one row per FR-012 example:
  - **Markup**:
    - `</Summary>`;
    - `<details open>x`, `<Details open>x`, `<source srcset="a.webp">x`, `<col span=2>x`;
    - `a<br/>b`, `a<hr />b`, `a<BR/>b`;
    - `a<br>b`, `a<hr>b`, `a<wbr>b`;
    - `<p>x</p>`, `<P>x</P>`, `<details><summary>x</summary></details>`;
    - the residual `Enter <summary> x </summary>` → `enter x`.
  - **Text**:
    - `send <source> now`, `<input> field`, `the <summary> text`, `the <section> box`;
    - `<Summary>`, `<Source>`, `<LIMIT>`, `<BR>`;
    - `<Summary> and </summary>` → `<summary> and`.

  Keep every existing row. Confirm the four lowercase-placeholder rows and the `<Summary> and </summary>` row fail
  (R4, spec "Normaliser changes").
- [X] T017 [US5] In `src/Spectra.Core/Text/TextNormalizer.cs`, rewrite `HtmlTagRegex` as the five R4 alternatives:
  - closing `</(?:NAMES)\s*>`;
  - with attributes `<(?:NAMES)(?:ATTR)+\s*/?>`;
  - self-closing `<(?:NAMES)\s*/>`;
  - lowercase void `<(?-i:br|hr|wbr)\s*>`;
  - bare candidate `<(?<bare>NAMES)\s*>`.

  Replace `HtmlTagRegex().Replace(s, " ")` with a match evaluator. It returns `" "` for the first four. For a `bare`
  match, it returns `" "` only if `Regex.IsMatch(s[(m.Index + m.Length)..], $"</{Regex.Escape(name)}\s*>")`, matched
  case-sensitively with `CultureInvariant`. Otherwise it returns `m.Value`.

  Document the residual in the XML summary. Make T016 pass. All existing `TextNormalizerTests`, `CriteriaMatcherTests`
  and `CriteriaChangesetTests` rows stay green.

**Checkpoint**: US5 is complete, and all fixes are in.

---

## Phase 7: Polish & Cross-Cutting

- [X] T018 [P] `CHANGELOG.md`: add "Changed/Fixed (review fixes, Spec 083)" under the unreleased `[2.6.0]`, covering:
  - the one config rule, stated as a table or bullets;
  - `CONFIG_SECTION_MISSING` removed;
  - criteria writes no longer refuse, nor warn, without `coverage`, which fixes init-created and existing
    workspaces;
  - the init template gains `coverage`;
  - duplicate keys → `CONFIG_INVALID` (an exact duplicate used to crash);
  - the HTML forms and the documented residual.
- [X] T019 [P] `CLAUDE.md` "Recent Changes": add an `083-review-fixes-082` entry above 082, including the root cause
  (rules checked against defaults, not the embedded template), the removed pieces, T2 and T3 as the standing guard,
  and the final counts.
- [X] T020 [P] `docs/cli-reference.md`:
  - replace the 082 Configuration tables with the FR-001 table and the FR-003 declared-reads table;
  - remove `CONFIG_SECTION_MISSING`;
  - add the template's `coverage` section under `spectra init`;
  - update the markup note to the five HTML forms plus the residual;
  - add a sentence on FR-021: config rules are checked against the init template and a pre-existing workspace.
- [X] T021 Run the full `dotnet build` and `dotnet test`. The only allowed failure is the known
  `ProfileFormatLoaderTests` one. Record the counts in the Notes and in `CLAUDE.md`.
- [X] T022 Fails-before check (FR-019):
  1. `git worktree add --detach <scratchpad>/wt-3594d62 3594d62`.
  2. Copy in the new and changed test files.
  3. Tests that reference APIs removed by this spec compile fine there, because `3594d62` still has them. A positional
     second argument to `LoadForWriteAsync` would mean `requires` at `3594d62` but `reads` after 083. So every
     rewritten `ConfigLoaderEntryPointTests` call uses the named argument `reads:`, which compiles and means the same
     at both commits. The tests then fail there on behaviour, not on a silent change of meaning.
  4. Build, run, and record the failing names per fix in a table in the Notes.
  5. Remove the worktree.
- [X] T023 Run the quickstart with the built `Spectra.CLI.exe` and the real `spectra init`
  (`specs/083-review-fixes-082/quickstart.md` steps 1–6), in a scratch directory. Record the results in the Notes.

---

## Dependencies & Execution Order

- **T001 first.**
- **US3 → US1 → US2**:
  - US2's T012 passes only after T006/T007;
  - T010 and T012 share `InitTemplateWorkspaceTests.cs`.
- **`ConfigLoader.cs`**: T006 → T015.
- **`ConfigRuleMatrixTests.cs`**: T003 → T014.
- **`ConfigLoaderEntryPointTests.cs`**: T002 → T013.
- **US5** (`TextNormalizer.cs`, `TextNormalizerTests.cs`) shares no file. It can run at any time after T001.
- **Polish** comes after all stories.

## Parallel Opportunities

- T002, T005 and T009 can be written in parallel (different files).
- US5 (T016 → T017) runs alongside US3, US1, US2 and US4.
- T018, T019 and T020 can run in parallel.

```text
# Example: while US3's rule lands, start US5
T016 TextNormalizerTests.cs  →  T017 TextNormalizer.cs
```

## Implementation Strategy

- **MVP**: US3 + US1. The rule plus the template makes every init-created workspace work again, which is the reported
  breakage. US2 then proves pre-existing workspaces, at no extra code cost.
- **Then** US4 (duplicates, including the exact-duplicate crash) and US5 (normaliser).
- After each story, run `dotnet test` for the touched projects.
- Commit once at the end as `fix(083): …`. Merge only with the user's approval, and never push.

## Notes

- **Baseline (T001)**, at `3594d62`: Core 812 / CLI 1568 (1 known failure:
  `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown`) / Execution 228 / Integration 8.
- **Final counts (T021)**: Core 839 / CLI 1592 (the same 1 known failure) / Execution 228 / Integration 8. A full
  rebuild reports 4 compiler warnings (CS8714/CS8621 in `CriteriaLayoutMigrator`, CS0108 `Name` in
  `DocsRetireCommand`/`DocsApplyMoveCommand`). They are **pre-existing**: the same 4 appear on a no-incremental build of
  `3594d62`. Incremental builds had hidden them.
- **Fails-before / passes-after (T022)**. Run in a detached worktree at `3594d62` with all new and changed test files
  copied in. Everything compiled, because `reads:` is passed by name. 9 Core and 38 CLI tests fail there, and all pass
  now:

  | Fix | Fails on `3594d62` |
  |---|---|
  | Rule (US3) | `ConfigRuleMatrixTests` rows: the init template, source/tests/coverage missing or null, ai null, generation missing; `WriteCommand_MissingCoverage_Succeeds_WithoutWarning` ×10; `ReadCommand_MissingCoverage_NoWarning` ×3; `NullCoverage_WriteSucceeds_WithoutWarning`; `CriteriaSkill_NamesConfigErrorCodes` |
  | F1 + T2 (US1) | `Init_WritesCoverageSection`; `InitTemplateWorkspaceTests.RealTemplate_WorkflowSucceeds_WithoutConfigWarnings` |
  | T3 (US2) | `InitTemplateWorkspaceTests.PreExistingWorkspace_WithoutCoverage_WorkflowSucceeds_WithoutConfigWarnings` |
  | F3 (US4) | `CaseVariantDuplicateKeys_AreInvalid_NamingTheKey` ×2, `ExactDuplicateKey_IsInvalid_NotAnException`, `CaseVariantDuplicateKey_IsRejected_ByEveryCommand` ×10, the matrix `generation / Generation` row |
  | F4 (US5) | `HtmlForms_DecisionTable` ×6 (the four lowercase placeholders, `<Summary> and </summary>`, `a<BR/>b`) |

- **Deviations and findings while implementing**:
  - **T002**: the rewritten Core loader tests pass on `3594d62` too. That is expected: 082 only warned about
    `coverage` when a caller declared it. The defect lived in the command declarations, so the command-level tests
    are the fails-before evidence.
  - **T003**: the `tests / Tests` duplicate row passes on `3594d62` by accident. The deserializer kept `"Tests": null`
    and then validation failed on the missing `tests`. The `generation / Generation` row and the ten-command theory
    are the real duplicate evidence.
  - **T003 warning count**: in JSON mode, ingest shows each warning in its JSON `warnings` and also as a stderr line
    (human output is never suppressed). The matrix counts ingest's JSON entries; summing both channels double-counted.
  - **Analysis C1**: added `CaseVariantDuplicateKey_IsRejected_ByEveryCommand`, run over all 10 commands (SC-004).
  - **Analysis U1**: the missing-`coverage` write test asserts exit 0 for every row, with retire and apply-move seeded
    to succeed.
  - **Analysis U2/U3**: T2/T3 use `skipSkills: true` (documented in the test; the config is written the same way) and
    the `LoggingSetup` logger, as the existing init tests do.
  - **Spec "Normaliser changes"** gained the `a<BR/>b` row. A self-closing bare tag is markup in any case (spec
    Assumptions); 082 kept uppercase `<BR/>` as text. It was found while writing T4.
- **Quickstart with the real `spectra init` (T023)**, using the built `Spectra.CLI.exe`:
  - `init` writes `coverage` (`docs/criteria`, `docs/criteria/_criteria_index.yaml`);
  - `docs changed` gives 0 warnings, and `ingest-criteria` gives `Persisted 1 criterion(s)` with exit 0 and the file
    in `docs/criteria/docs/`;
  - with `coverage` removed: 0 warnings, and ingest exits 0;
  - with a `Tests` duplicate added: `{"command":"docs-changed","success":false,"error_code":"CONFIG_INVALID",
    "errors":["… duplicate top-level key 'tests' / 'Tests' (keys may not differ only in case)"],"warnings":[]}` with
    exit 1.
