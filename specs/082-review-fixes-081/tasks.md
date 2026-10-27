# Tasks: Review Fixes for Spec 081

**Input**: Design documents from `/specs/082-review-fixes-081/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R4), data-model.md, contracts/cli-behaviour-changes.md, quickstart.md

**Tests**: REQUIRED. FR-013: every item gets a regression test that fails on `7df3fef` and passes after the fix. In
every story, write the test first, run it, and confirm it fails for the stated reason before implementing. A test of
a brand-new API that fails to compile before the fix counts as failing.

**Conventions**:
- CLI command tests go in `[Collection(CriteriaCommandsCollection.Name)]`.
- Workspaces come from `CriteriaTestWorkspace`.
- `ConfigLoadingConsistencyTests` already has a `WriteConfig(params string[] sections)` helper and the `SourceJson`,
  `TestsJson` and `CoverageJson` constants (from 081).
- For edits that need `\` escapes, write the script to the scratchpad with the Write tool. Bash heredocs strip
  backslashes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: US1–US4 from spec.md.

---

## Phase 1: Setup

- [X] T001 Record the baseline on `082-review-fixes-081` (at `7df3fef`). Run `dotnet build` and `dotnet test`.
  Expected: Core 785 / CLI 1535 with 1 known `ProfileFormatLoaderTests` failure / Execution 228 / Integration 8. Paste
  the numbers into the Notes section of `specs/082-review-fixes-081/tasks.md`.

No foundational phase. US1, US3 and US4 share `ConfigLoader.cs` and `CriteriaCommandIO.cs` and run in that order.
US2 is independent.

---

## Phase 2: User Story 1 — Criteria are never written to an unconfigured location (P1) 🎯 MVP

**Goal**: FR-001–FR-004 / SC-001, SC-002 (A1, R1).
**Independent Test**:
- Every criteria write command without `coverage` exits 1 naming it and writes nothing, including under `--dry-run`.
- `ingest-criteria` and `retire` without `tests` succeed with one warning.
- The read commands that use `coverage` warn and run on the default location.

- [X] T002 [P] [US1] In `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`:
  - **Replace** `Write_MissingSectionItReads_IsAnError_NamingTheSection` (it encodes 081's `tests`-on-write rule) with
    `Write_MissingRequiredSection_IsAnError_NamingTheSection`: `LoadForWriteAsync(root, requires:
    ConfigSections.Coverage)` on a config without `coverage` gives `Config == null` and
    `Error == "spectra.config.json is invalid: missing 'coverage' section"`.
  - Add `Write_MissingSectionItOnlyReads_Warns`: `LoadForWriteAsync(root, requires: ConfigSections.Coverage, reads:
    ConfigSections.Tests)` on a config with `coverage` but no `tests` succeeds, with `Tests.Dir == "test-cases/"` and
    exactly one warning containing `'tests'`.
  - Add `Read_MissingCoverage_UsesDefaults_AndWarns`: `LoadForReadAsync(root, ConfigSections.Coverage)` without
    `coverage` gives `Coverage.CriteriaDir == "docs/criteria"` and one warning containing `'coverage'`.

  These fail to compile before T004, since `ConfigSections.Coverage` and the `requires` parameter are new.
- [X] T003 [US1] In `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs`:
  - **Replace** `WriteCommand_MissingTests_Exits1_NamingTheSection_AndWritesNothing` with
    `WriteCommand_MissingTests_Warns_AndSucceeds`, over `ai ingest-criteria --doc docs/a.md --from
    .spectra/criteria.json` and `docs retire`. For retire, seed an indexed, deleted doc by ingesting `docs/gone.md`
    first with full config, then deleting the doc. With `WriteConfig(SourceJson, CoverageJson)`: exit 0, and exactly
    one stderr `Warning:` line containing `'tests'`.
  - Add theory `WriteCommand_MissingCoverage_Exits1_AndWritesNothing(string command, bool dryRun)` over all 5 write
    commands × {false, true} (append `--dry-run` where the command supports it; `--import-criteria` supports it).
    With `WriteConfig(SourceJson, TestsJson)`: exit 1, output contains `missing 'coverage' section`, and
    `_ws.Snapshot()` is unchanged.
  - Add theory `ReadCommand_MissingCoverage_UsesDefaults_AndWarns` over `ai compile-extraction-prompt --doc docs/a.md`,
    `ai compile-critic-prompt --suite s --test TC-1` and `docs changed`. With `WriteConfig(SourceJson, TestsJson)`:
    exit 0, and a `Warning:` line or a JSON `warnings` entry containing `'coverage'`.
  - Check that `WriteCommand_ThatReadsNeither_IgnoresMissingSourceAndTests` still passes (its config keeps
    `coverage`).

  Confirm the new and replaced rows fail on the current code.
- [X] T004 [US1] In `src/Spectra.Core/Config/ConfigLoader.cs`:
  - Add `Coverage = 4` to `ConfigSections`.
  - Add `("coverage", ConfigSections.Coverage)` to the checked-section list.
  - Change `LoadForWriteAsync(string repoRoot, ConfigSections requires = ConfigSections.None, ConfigSections reads =
    ConfigSections.None, CancellationToken ct = default)`:
    - a missing section in `requires` → the first one gives `InvalidConfigPrefix + $"missing '{name}' section"`;
    - a missing section in `reads` (and not in `requires`) → a warning, as for read loads.
  - Keep `LoadForReadAsync(repoRoot, reads, ct)`.
  - Update the XML docs. `coverage` is no longer "always optional"; it is optional unless a command declares it.

  Make T002 pass. The rest of `ConfigLoaderEntryPointTests` and `ConfigLoaderTests` stay green.
- [X] T005 [US1] In `src/Spectra.CLI/Criteria/CriteriaCommandIO.cs`:
  - Change `LoadConfigAsync(repoRoot, ConfigSections requires, ConfigSections reads, ct)` to return `(Config, Error,
    Warnings)`. T016 adds `ErrorCode`.
  - Update the callers:
    - `IngestCriteriaCommand.cs` and `DocsRetireCommand.cs`: `requires: Coverage, reads: Tests`;
    - `DocsApplyMoveCommand.cs` and `DocsMigrateCriteriaCommand.cs`: `requires: Coverage`;
    - `AnalyzeHandler.RunImportCriteriaAsync`: `LoadForWriteAsync(currentDir, requires: ConfigSections.Coverage)`.
- [X] T006 [US1] Surface the write-command config warnings:
  - `IngestCriteriaCommand.cs`: pass them to the success output. They go first in the JSON `warnings` array and as
    `Warning:` lines where the command prints its other warnings, and into `WriteFailure(..., warnings)` on failure.
  - `DocsRetireCommand.cs`: print each as `Warning: {w}` on stderr right after loading, and pass them to its
    `WriteFailure` calls.
- [X] T007 [US1] Declare `coverage` on the read commands that use it:
  - `CompileExtractionPromptCommand.cs`: `ConfigSections.Coverage`, and add `loaded.Warnings` to its `warnings` list
    right after loading. They print on success and go inside the failure JSON.
  - `CompileCriticPromptCommand.cs`: `Source | Tests | Coverage`.
  - `DocsChangedHandler.cs`: `Source | Coverage`.

  Make T003 pass. `CriteriaFailureShapeTests`, `SharedCriteriaFileTests`, `AbsoluteSourceDocTests` and the
  `Docs*Tests` stay green. Their fixtures write `SpectraConfig.Default`, which includes `coverage`.

**Checkpoint**: US1 is complete.

---

## Phase 3: User Story 2 — Placeholders in angle brackets are compared as text (P1)

Independent of the config work (US1/US3/US4), so it can run at any time.

**Goal**: FR-005–FR-006 / SC-003 (A2, R2).
**Independent Test**: A change from one capitalised placeholder to another is a change. Real HTML in HTML form is
still ignored. Every 080 and 081 row keeps its result.

- [X] T008 [P] [US2] Extend `tests/Spectra.Core.Tests/Text/TextNormalizerTests.cs`:
  - Theory `CapitalisedPlaceholders_AreText` with rows `<Source>`, `<Summary>`, `<Section>`, `<Input>`,
    `<Details>`, `<Caption>`, `<LIMIT>`, `<BR>`: `Normalize($"Enter {p} now")` equals `$"enter {p.ToLowerInvariant()}
    now"`.
  - Fact `PlaceholderSwaps_AreChanges`:
    - `Enter <Summary> and press Save` vs `Enter <Details> and press Save` give different keys;
    - `Select <Source> from the list` vs `Select <Target> from the list` give different keys.
  - Theory `HtmlForm_IsStillMarkup`:
    - `<details open>x</details>` → `x`;
    - `x</Summary>` → `x`;
    - `<Details open>x` → `x`;
    - `<P>Para</P>` → `para`;
    - `<Summary>x</Summary>` → `x`;
    - `<figure><figcaption>c</figcaption></figure>` → `c`;
    - `<INPUT type=checkbox>x` → `x`.
  - Every existing row stays unchanged.

  Confirm the placeholder rows fail.
- [X] T009 [US2] Extend `tests/Spectra.Core.Tests/Criteria/CriteriaChangesetTests.cs` with
  `PlaceholderChange_IsAModification`: previous `Enter <Summary> and press Save`, next `Enter <Details> and press
  Save`, same ID → one `modified` entry. Confirm it fails.
- [X] T010 [US2] In `src/Spectra.Core/Text/TextNormalizer.cs`, rewrite `HtmlTagRegex` per R2 as four alternatives
  over the unchanged element and boolean-attribute lists:
  - (a) `</(?:NAMES)\s*>`;
  - (b) `<(?:NAMES)(?:ATTR)+\s*/?>`;
  - (c) `<(?-i:lowercase-NAMES)\s*/?>`;
  - (d) `<(?<n>NAMES)\s*>(?=[\s\S]*?</\k<n>\s*>)`.

  Keep `RegexOptions.IgnoreCase | CultureInvariant` for the rest, and update the XML summary. Make T008 and T009 pass.
  All 080 and 081 `TextNormalizerTests`, `CriteriaMatcherTests` and `CriteriaChangesetTests` rows stay green.

**Checkpoint**: US2 is complete.

---

## Phase 4: User Story 3 — A section set to null behaves like a missing section (P2)

Done before US4 because it edits the same loader code.

**Goal**: FR-007 / SC-004 (A3, R3).
**Independent Test**: For each of `source`, `tests`, `coverage` and `ai`, a `null` value gives exactly the result of a
missing key.

- [X] T011 [P] [US3] Add a theory `NullSection_BehavesLikeAMissingSection(string section)` to
  `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`, with rows `source`, `tests`, `coverage`, `ai`.
  - Build a full config, then two variants: one without the key, and one with `"<section>": null`.
  - Load each variant with `LoadForReadAsync(root, Source | Tests | Coverage)`, and with
    `LoadForWriteAsync(root, requires: Coverage, reads: Tests)`.
  - Assert equal `Error` and `Warnings`, and, when a config loads, equal `Source.LocalDir`, `Tests.Dir` and
    `Coverage.CriteriaDir`.

  Add `TopLevelNull_And_EmptyFile_AreInvalid`: the file content `null`, or empty, gives the `InvalidConfigPrefix` error
  from both entry points.

  Confirm the `null` rows fail. For `coverage: null` the old code either errors or yields a null `Coverage`, so assert
  `result.Config!.Coverage` is not null.
- [X] T012 [P] [US3] In `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs`:
  - `"ai": null` (with source, tests, coverage) → `docs changed --output-format json` exits 0;
  - `"coverage": null` → `ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json` exits 1 with
    `missing 'coverage' section`, and the snapshot is unchanged;
  - `"source": null` → `docs changed` exits 0 and warns about `'source'`.

  Confirm they fail.
- [X] T013 [US3] In the `ConfigLoader.cs` pre-scan, treat a key whose value is `null` (a `JsonNode` of `null`) as
  missing: replace it with a new `JsonObject` and record it. Match the key case-insensitively, as today. When several
  keys match case-insensitively, a present non-null match wins. Make T011 and T012 pass.

**Checkpoint**: US3 is complete.

---

## Phase 5: User Story 4 — Config problems carry a named error code (P3)

**Goal**: FR-008–FR-011 / SC-005 (A4, R4).
**Independent Test**: In JSON mode, every config failure of every in-scope command with a JSON mode carries
`CONFIG_MISSING`, `CONFIG_INVALID` or `CONFIG_SECTION_MISSING`. The skill names them.

- [X] T014 [P] [US4] In `tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs`, add assertions on
  `result.ErrorCode`:
  - no file on a write load → `CONFIG_MISSING`;
  - malformed, wrong type, top-level `null`, empty file → `CONFIG_INVALID`;
  - a missing required section → `CONFIG_SECTION_MISSING`;
  - success → `null`.

  Fails to compile before T016.
- [X] T015 [US4] In `tests/Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs`:
  - `ConfigErrorCodes_JsonMode`:
    - no file: the 4 criteria write commands on stderr, plus `--import-criteria` on stdout → `CONFIG_MISSING`;
    - malformed: the 4 criteria write commands, `compile-extraction-prompt`, `compile-critic-prompt --suite s --test
      TC-1` and `docs changed` (stderr, one JSON document with `command` and `error_code`), plus `--import-criteria`
      (stdout `ErrorResult` with `error_code`) → `CONFIG_INVALID`;
    - no `coverage`: the 5 write commands → `CONFIG_SECTION_MISSING`.
  - **Update** `MalformedConfig_JsonMode_OtherReadCommands_ReportThePlainMessage`:
    - keep only `compile-repair-prompt` and `compile-repair-batch`, which have no JSON mode;
    - move `compile-critic-prompt` and `docs changed` into a new assertion: one JSON failure document, with
      `"command"` `compile-critic-prompt` / `docs-changed` and `error_code` `CONFIG_INVALID`.

  Confirm the code rows fail.
- [X] T016 [US4] In `src/Spectra.Core/Config/ConfigLoader.cs`:
  - Add `public string? ErrorCode { get; init; }` to `ConfigLoadResult`.
  - Add the constants `ErrorConfigMissing = "CONFIG_MISSING"`, `ErrorConfigInvalid = "CONFIG_INVALID"` and
    `ErrorConfigSectionMissing = "CONFIG_SECTION_MISSING"`.
  - Set the code on every error return in `LoadEntryAsync`.

  Make T014 pass.
- [X] T017 [US4] Pass the code through to every in-scope failure writer:
  - **`CriteriaCommandIO.cs`**: `LoadConfigAsync` also returns `ErrorCode`.
  - **The four write commands**: `IngestCriteriaCommand.cs`, `DocsRetireCommand.cs`, `DocsApplyMoveCommand.cs`,
    `DocsMigrateCriteriaCommand.cs` pass it to `WriteFailure` instead of `null`.
  - **`CompileExtractionPromptCommand.cs`**: passes `loaded.ErrorCode` to its `WriteFailure`.
  - **Import**: `src/Spectra.CLI/Results/CommandResult.cs` `ErrorResult` gains
    `[JsonPropertyName("error_code")] [JsonIgnore(Condition = WhenWritingNull)] public string? ErrorCode { get; init; }`,
    and `AnalyzeHandler.RunImportCriteriaAsync` sets it on the config failure.
  - **`CompileCriticPromptCommand.cs` and `DocsChangedHandler.cs`**: in JSON mode, a config failure goes through
    `CriteriaCommandIO.WriteFailureJson("compile-critic-prompt" / "docs-changed", loaded.ErrorCode, [loaded.Error!])`.
    In human mode, keep the current text line.

  Make T015 pass.
- [X] T018 [P] [US4] Add `CriteriaSkill_NamesConfigErrorCodes` to `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`:
  - the `spectra-criteria` content contains `CONFIG_MISSING`, `CONFIG_INVALID` and `CONFIG_SECTION_MISSING`;
  - it contains neither `` `null` for a config `` nor `` with `error_code` `null` ``.

  Confirm it fails.
- [X] T019 [US4] In `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md`:
  - Replace the four "`null` for a config problem" / "a config error with `error_code` `null`" phrases (Steps 1a, 1b,
    2a, 2c) with the named codes. Suggested wording: "`CONFIG_MISSING` / `CONFIG_INVALID` / `CONFIG_SECTION_MISSING`
    for a config problem: report it and stop — fix `spectra.config.json` first".
  - Keep the other exit-1 wording, which `CriteriaSkill_WriteSteps_HandleExit1` from 080 asserts.

  Make T018 pass, and run `dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Skills"`.

**Checkpoint**: US4 is complete, and all four items are fixed.

---

## Phase 6: Polish & Cross-Cutting

- [X] T020 [P] `CHANGELOG.md`: add "Changed/Fixed (review fixes, Spec 082)" under the unreleased `[2.6.0]`, covering:
  - criteria writes require `coverage` (`tests` only warns);
  - `null` sections are treated as missing;
  - the named config error codes, including the new JSON failure output of `compile-critic-prompt` and `docs changed`
    in JSON mode and `ErrorResult.error_code`;
  - the placeholder rule.
- [X] T021 [P] `CLAUDE.md` "Recent Changes": add an `082-review-fixes-081` entry above 081, with the final counts.
- [X] T022 [P] `docs/cli-reference.md`, per `contracts/cli-behaviour-changes.md`:
  - update the Configuration table (`coverage` required on writes, `tests` warns, `null` = missing, the error-codes
    table);
  - the note on normaliser markup (HTML form only).
- [X] T023 Run the full `dotnet build` and `dotnet test`. The only allowed failure is the known
  `ProfileFormatLoaderTests` one. Record the counts in the Notes and in `CLAUDE.md`.
- [X] T024 Fails-before check (FR-013):
  1. `git worktree add --detach <scratchpad>/wt-7df3fef 7df3fef`.
  2. Copy in the new and changed test files. Drop the cases that use brand-new APIs (`ConfigSections.Coverage`,
     `requires`, `ErrorCode`) as compile-fail by design.
  3. Build, run, and record the failing names per item in a table in the Notes.
  4. Remove the worktree.
- [X] T025 Run the quickstart spot checks (`specs/082-review-fixes-081/quickstart.md` steps 1–3) with the built
  `Spectra.CLI.exe` in a scratch workspace. Record the results in the Notes.

---

## Dependencies & Execution Order

- **T001 first.**
- **US1 → US3 → US4**, in that order, since they share files:
  - `ConfigLoader.cs`: T004 → T013 → T016;
  - `CriteriaCommandIO.cs` and the write commands: T005/T006 → T017;
  - `ConfigLoadingConsistencyTests.cs`: T003 → T012 → T015;
  - `ConfigLoaderEntryPointTests.cs`: T002 → T011 → T014.
- **US2** (`TextNormalizer.cs`, `TextNormalizerTests.cs`, `CriteriaChangesetTests.cs`) shares no file and can run
  alongside any of them.
- **Polish** comes after all stories.

## Parallel Opportunities

- US2 (T008–T010) alongside US1/US3/US4.
- T018 (skill test) alongside T014–T017.
- T020/T021/T022.

```text
# Example: US2 while US1 is in progress
T008 TextNormalizerTests.cs | T009 CriteriaChangesetTests.cs  (then T010)
```

## Implementation Strategy

- **MVP**: US1 (A1). It is the only item that writes files to a location nobody configured.
- **Next**: US2 (A2, hidden doc changes), then US3 and US4.
- After each story, run `dotnet test` for the touched projects.
- Commit once at the end as `fix(082): …`. Merge only with the user's approval, and never push.

## Notes

- **Baseline (T001)**, at `7df3fef`: Core 785 / CLI 1535 (1 known failure:
  `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown`) / Execution 228 / Integration 8.
- **Final counts (T023)**: Core 812 / CLI 1568 (the same 1 known failure) / Execution 228 / Integration 8. Build has 0
  warnings.
- **Fails-before / passes-after (T024)**. Run in a detached worktree at `7df3fef` with the changed test files copied in.
  `ConfigLoaderEntryPointTests` was left at its `7df3fef` version: its new cases use brand-new APIs
  (`ConfigSections.Coverage`, `requires`, `ErrorCode`), so they fail to compile by design.

  | Item | Fails on `7df3fef` | Passes now |
  |---|---|---|
  | A1 | `ConfigLoadingConsistencyTests` `WriteCommand_MissingCoverage_Exits1_AndWritesNothing` ×10 (5 commands × dry-run), `WriteCommand_MissingTests_Warns_AndSucceeds` ×2, `ReadCommand_MissingCoverage_UsesDefaults_AndWarns` ×3; the Core cases (compile) | ✓ |
  | A2 | `TextNormalizerTests.CapitalisedPlaceholders_AreText` ×7 (`<LIMIT>` passes before and after, since it is not an element name), `PlaceholderSwaps_AreChanges`, `CriteriaChangesetTests.PlaceholderChange_IsAModification`; the `HtmlForm_IsStillMarkup` guards pass before and after | ✓ |
  | A3 | `ConfigLoadingConsistencyTests` `NullAi_IsAccepted`, `NullCoverage_RefusesAWrite_NamingTheSection`, `NullSource_OnARead_UsesDefaults_AndWarns`; the Core `NullSection_BehavesLikeAMissingSection` ×4 (compile) | ✓ |
  | A4 | `ConfigLoadingConsistencyTests.ConfigErrorCodes_JsonMode` ×18, `SkillsManifestTests.CriteriaSkill_NamesConfigErrorCodes`; the Core `ErrorCodes_NameEachConfigProblem` (compile) | ✓ |

- **Deviations found while implementing**:
  - **`DocsChangedTests` fixture**: the 079 fixture config had no `coverage` section. A1 correctly makes
    `docs changed` warn about it first, which broke that test's `warnings[0]` assertion. The fixture now includes
    `"coverage": {}`, because the test is about the migration warning.
  - **Warnings on write commands in JSON mode**: `docs retire` prints its config warnings to stderr only on success,
    so a JSON-mode failure stays one document. On failure they go inside the failure JSON's `warnings`.
    `ingest-criteria` puts them first in its result `warnings`.
  - **`MalformedConfig_JsonMode_OtherReadCommands_ReportThePlainMessage`** (080): narrowed to the two repair
    compilers, which have no JSON mode. `compile-critic-prompt` and `docs changed` are covered by
    `ConfigErrorCodes_JsonMode`.
- **Quickstart spot checks (T025)**, with the built `Spectra.CLI.exe`:
  - **A1, no `coverage`**: `ingest-criteria --output-format json` gives `{"command":"ingest-criteria",
    "success":false,"error_code":"CONFIG_SECTION_MISSING","errors":["spectra.config.json is invalid: missing
    'coverage' section"],"warnings":[]}` with exit 1, and 0 files are written.
  - **A1, `coverage` but no `tests`**: `Persisted 1 criterion(s)…` plus `Warning: spectra.config.json has no 'tests'
    section; using the built-in defaults.`, exit 0.
  - **A3**: with `"ai": null`, `docs changed` exits 0.
  - **A4**: with malformed JSON, `docs changed --output-format json` gives `{"command":"docs-changed",
    "success":false,"error_code":"CONFIG_INVALID",…}` with exit 1.
