---
description: "Task list for Spec 084 — Source Change Baseline (Phase 2a)"
---

# Tasks: Source Change Baseline — Deterministic Change Detection Without Git (Phase 2a)

**Input**: Design documents from `/specs/084-source-change-baseline/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required. The spec lists T1–T9 as tables that mirror its rules, plus the performance acceptance check.
Write each table test with or before the code it covers.

**Organization**: by user story. Order note: US1 and US2 are both P1. US2 (recording and bootstrap) is implemented
first, because every triage scenario needs a baseline to compare against. US1's tests create baselines through
`source-baseline --init`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md
- Paths are repo-relative. The suite names in the tests (`storage`, `billing`) are fixture names.

---

## Phase 1: Setup

- [X] T001 Measure the baseline: run `dotnet build` and `dotnet test` on `084-source-change-baseline`. Record the counts (expected Core 839 / CLI 1592 with 1 known `ProfileFormatLoaderTests` failure / Execution 228 / Integration 8) and the pre-existing compiler warnings in the Notes section of specs/084-source-change-baseline/tasks.md.
- [X] T002 Create the fixture tests/Spectra.CLI.Tests/TestFixtures/BaselineTestWorkspace.cs, modelled on `CriteriaTestWorkspace`:
  - a temp root holding `spectra.config.json` (the `CriteriaTestWorkspace` default plus `tests.dir = test-cases/`), `docs/`, `docs/criteria/`, `test-cases/` and `.spectra/`;
  - `PathOf`, `WriteDoc(rel, text)` (LF);
  - `WriteTest(suite, id, string[] refs, string[] criteria, string? body = null)`, which writes a valid test `.md` with `source_refs` / `criteria` frontmatter and regenerates the suite `_index.json` through `IndexGenerator` / `IndexWriter`;
  - `WriteCriteriaFile(relativeToCriteriaDir, yaml)`;
  - `ReadBaseline(suite)` as a raw string;
  - `Snapshot()`, a SHA-256 of every file under the root **including `.spectra/`**;
  - `RunAsync(params string[] args) -> (Exit, Out, Err)`. It sets the current directory, captures the console, and builds a `RootCommand` with `GlobalOptions.AddTo` plus `AiCommand.Create()`, `DocsCommand`, `SuiteCommand`, `ListCommand`, `ValidateCommand`, `IndexCommand`, `ShowCommand` and `RunCommand`. Check each type's real name in `src/Spectra.CLI/Commands/*`.
  Tag the test classes with `[Collection(CriteriaCommandsCollection.Name)]`.

---

## Phase 2: Foundational (pure rules, storage, config — blocks every story)

**Purpose**: the model-free rules in `Spectra.Core/Baseline/` and the I/O store in `Spectra.CLI/Baseline/` that both
writers and triage use.

- [X] T003 [P] Write tests/Spectra.Core.Tests/Baseline/AnchorKeyTests.cs (T2) with a `[Theory]` table:
  - for the heading "Create customer records", each of `Create-customer-records`, `create-customer-records`, `Create-Customer-Records`, `create_customer_records`, `Create%20customer%20records` and `create-customer-records!` gives `createcustomerrecords`;
  - `create-customer-record` does not match;
  - "Über Uns" gives `überuns`;
  - a malformed escape `%zz` gives the raw key without throwing.
- [X] T004 [P] Implement src/Spectra.Core/Baseline/AnchorKey.cs: `static string Make(string text)` (`ToLowerInvariant`, keep `char.IsLetterOrDigit`) and `static string FromAnchor(string anchor)` (`Uri.UnescapeDataString` in a try, falling back to the raw anchor, then `Make`).
- [X] T005 [P] Write tests/Spectra.Core.Tests/Baseline/SectionExtractorTests.cs (T1) as a table. Each row is (document text, anchor, expected outcome, expected raw section text, ambiguous):
  - levels 1–6;
  - a level-2 section that includes its level-3/4 children and ends at the next level-2, and a level-2 that ends at the next level-1;
  - the last section, which runs to end of file;
  - a `#` heading inside a ```` ``` ```` fence and inside a `~~~` fence, ignored both as a boundary and as a target, including a 4-backtick fence containing ```` ``` ````;
  - no anchor, and an empty anchor (`file.md#`), giving the whole file;
  - a duplicate heading, where the first wins and `Ambiguous = true`;
  - a heading with closing hashes (`## Quotas ##`);
  - up to 3 leading spaces, while 4 spaces is not a heading;
  - `#NoSpace`, which is not a heading;
  - an anchor that matches nothing, giving `AnchorNotFound`.
  Also cover `AnchorOf("docs/a.md#X#Y")`, which returns `X#Y` (the text after the first `#`).
- [X] T006 Implement src/Spectra.Core/Baseline/SectionExtractor.cs per research R1: the `MarkdownHeading` and `SectionLookup` records, the `SectionOutcome` enum, and `Headings(text)`, `Extract(text, anchor)` and `AnchorOf(sourceRef)`. The text is LF-normalised on entry, and the section keeps the file's bytes after that normalisation. Depends on T004.
- [X] T007 [P] Write tests/Spectra.Core.Tests/Baseline/SourceFingerprintTests.cs:
  - the known SHA-256 of `"a\n"` in the `sha256:<64 hex>` format;
  - CRLF, lone CR and a leading BOM each give the same fingerprint as LF without a BOM;
  - a trailing-space change gives a different fingerprint;
  - `OfCriterion` is equal for `"Users **MUST** log in"` and `"users must  log in"`;
  - `IsValid` accepts the format and rejects uppercase hex, a missing prefix and a wrong length;
  - `Hex` strips the prefix.
- [X] T008 [P] Implement src/Spectra.Core/Baseline/SourceFingerprint.cs: `OfSection(raw)` (strip U+FEFF, `\r\n` and `\r` to `\n`, UTF-8 SHA-256, lowercase hex), `OfCriterion(text)` (over `TextNormalizer.Normalize(text)`), `Hex(fp)` and `IsValid(fp)`.
- [X] T009 [P] Write tests/Spectra.Core.Tests/Baseline/SourceBaselineSerializerTests.cs:
  - round-trip;
  - output keys sorted ordinally at all three levels, whatever the insertion order;
  - LF only (no `\r` byte), 2-space indent, one trailing `\n`, no BOM;
  - `#`, `→` and `ü` written unescaped;
  - empty maps written as `{}`;
  - a change to one test's hash changes only that test's lines (diff the line arrays);
  - each `Parse` failure throws `BaselineFormatException` naming the file: invalid JSON, a root array, `version: 2`, `tests` as an array, an entry without `refs`, a value `sha1:…`, an uppercase-hex value.
- [X] T010 [P] Implement src/Spectra.Core/Baseline/SourceBaseline.cs: `SourceBaseline` (`Version`, `SortedDictionary<string, BaselineEntry>(StringComparer.Ordinal) Tests`), `BaselineEntry` (`Refs`, `Criteria` as ordinal `SortedDictionary`), `BaselineFormatException`, and `SourceBaselineSerializer.Serialize` / `Parse` per research R5. `Serialize` uses `Utf8JsonWriter` with `Indented = true` and `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, then replaces CRLF with LF and appends `\n`.
- [X] T011 [P] Extend src/Spectra.Core/Models/Config/UpdateConfig.cs with `[JsonPropertyName("group_size")] public int? GroupSize`, `[JsonPropertyName("light_threshold_words")] public int? LightThresholdWords`, `EffectiveGroupSize => GroupSize ?? 10` and `EffectiveLightThresholdWords => LightThresholdWords ?? 12` (the latter two `[JsonIgnore]`).
- [X] T012 Update src/Spectra.Core/Config/ConfigLoader.cs:
  - add `("update", ConfigSections.None)` to `CheckedSections`;
  - in `Validate`, add `INVALID_UPDATE_GROUP_SIZE` ("update.group_size must be an integer ≥ 1") when `Update.GroupSize < 1`, and `INVALID_UPDATE_LIGHT_THRESHOLD` ("update.light_threshold_words must be an integer ≥ 0") when `< 0`. Both surface as `CONFIG_INVALID`;
  - extend the rule comment block to mention `update`.
  Depends on T011.
- [X] T013 Add config tests:
  - in tests/Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs, a `[Theory]` with these rows: `update` missing, `"update": null`, `"update": {"group_size": null}`, and `{"light_threshold_words": null}`, each giving the defaults 10/12 with no warning; `group_size: 0`, `group_size: "x"`, `group_size: 1.5` and `light_threshold_words: -1`, each giving `CONFIG_INVALID` with the key named;
  - in tests/Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs, rows `update null` and `update.group_size 0`, run through the existing read/write command pair (expected 0 warnings / `CONFIG_INVALID`).
  Depends on T012.
- [X] T014 Implement src/Spectra.CLI/Baseline/BaselineStore.cs per research R6:
  - constants `ErrorLockTimeout = "LOCK_TIMEOUT"` and `ErrorBaselineUnreadable = "BASELINE_UNREADABLE"`, and `BaselineStoreException(code, message)`;
  - `WithLockAsync<T>(repoRoot, body, ct, timeout?)` on `.spectra/baseline.lock` through `FileLockHandle`, 10 s by default;
  - `LoadSuiteAsync(testsPath, suite, ct)`: a missing file is empty; a `BaselineFormatException` or an IO error becomes `BASELINE_UNREADABLE` with the repo-relative path;
  - `SaveSuiteAsync(testsPath, suite, baseline, IReadOnlySet<string> liveIds, ct) -> bool written`: prune to the live IDs, serialise, skip if byte-identical, otherwise write a temp file `{name}.{guid}.tmp` and move it with overwrite. Don't create a file for an empty baseline when none exists;
  - `EnsureSnapshotAsync(repoRoot, fingerprint, normalised, ct) -> bool written`: skip if present, atomic temp-then-move, and tolerate a concurrent create;
  - `ReadSnapshotAsync(repoRoot, fingerprint, ct) -> string?`.
  Depends on T010.
- [X] T015 Implement src/Spectra.CLI/Baseline/BaselineContext.cs:
  - built from `(repoRoot, SpectraConfig)`. It constructs `SourceRefResolver(repoRoot, config.Source?.LocalDir ?? "docs/")` and loads criteria once via `CriteriaStore.ScanFilesAsync(full(config.Coverage?.CriteriaDir ?? "docs/criteria"))` into an ID→first-criterion map, adding a note for each duplicate ID;
  - `GetSectionAsync(sourceRef) -> CurrentSection` (`Missing`, `MissingReason`, `Fingerprint`, `Normalised`, `Ambiguous`). It caches file text by full path and sections by (full path, anchor key). An unresolved file gives the reason from `MissingReason` (`not found` / `outside allowed roots` / `empty ref`), and an unmatched anchor gives `anchor not found`;
  - `GetCriterion(id) -> (exists, fingerprint)`;
  - a snapshot text cache;
  - `Notes` (ordered, de-duplicated): `"{ref}: {n} headings share the anchor key '{key}'; the first was used"` and `"criterion {id} is defined more than once; the first was used"`.
  Depends on T006, T008, T014.
- [X] T016 Implement src/Spectra.CLI/Baseline/BaselineRecorder.cs:
  - `ComputeEntryAsync(TestCase test, BaselineContext ctx) -> (BaselineEntry Entry, IReadOnlyList<(string Fingerprint, string Normalised)> Snapshots)`. For each ref in `test.SourceRefs` that resolves and matches, record its fingerprint and queue the snapshot; refs that don't resolve, and unknown criterion IDs, are omitted (research R9). For each `test.Criteria` ID that exists, record its fingerprint;
  - `BaselineWriteSummary` (`Created`, `Updated`, `Unchanged`, `AutoAcked`, `SnapshotsWritten`) with a helper `Apply(SourceBaseline, id, entry)` that counts created / updated / unchanged by comparing with the existing entry.
  Depends on T015.

**Checkpoint**: Core tables T1–T2 and the fingerprint and serializer tests are green. The store and recorder build.

---

## Phase 3: User Story 2 — Baselines are recorded automatically, and existing workspaces can adopt them (Priority: P1)

**Goal**: `source-baseline --init`, `ingest-tests` and `ingest-update` write baseline entries and snapshots, and
nothing is written on `--dry-run`.

**Independent test**: ingest a test, then check `_source-baseline.json` and the snapshot. Remove the baseline,
`source-baseline --init --all-suites`, and check every test has an entry.

- [X] T017 [P] [US2] Write tests/Spectra.CLI.Tests/Commands/Baseline/SourceBaselineCommandTests.cs (part of T5):
  - `--init --suite storage` creates entries for all tests, with sorted keys, and writes one snapshot per distinct section;
  - a second run reports `unchanged` and leaves the file byte-identical;
  - a partial entry (a ref added to the test by hand) is completed without changing existing hashes;
  - an existing entry with a stale hash is kept without `--force` and recomputed with `--force`;
  - `--all-suites` covers two suites;
  - `--dry-run` leaves `Snapshot()` identical and reports the would-be counts;
  - test files stay byte-identical in every case;
  - an unresolved ref and an unknown criterion ID are omitted from the entry;
  - a suite folder with no test files: `--init` reports `created 0` and creates no `_source-baseline.json`;
  - a stale ref key (removed from the test by hand) is kept by `--init` and dropped by `--init --force` (FR-026a);
  - the JSON shape is as in contracts/cli-contract.md;
  - the human summary is on stderr in JSON mode.
- [X] T018 [US2] Implement src/Spectra.CLI/Commands/Baseline/SourceBaselineCommand.cs and SourceBaselineHandler.cs:
  - options `--init`, `--suite`, `--all-suites`, `--force`, `--dry-run`;
  - argument checks → `INVALID_ARGUMENTS` (exit 4);
  - `ConfigLoader.LoadForWriteAsync(root, Source | Tests)`, with failures through `CriteriaCommandIO.WriteFailure` (`command = "source-baseline"`, exit 1) and warnings printed and included;
  - suites = tests-dir subdirectories without a leading `_`, in ordinal order; a missing `--suite` → `SUITE_NOT_FOUND` (exit 4);
  - tests parsed from `*.md` minus `_*` with `TestCaseParser`, with unparseable files warned and skipped;
  - non-dry-run: within `BaselineStore.WithLockAsync`, load each suite strictly, compute entries (create, complete, or recompute with `--force`), ensure snapshots, save;
  - dry-run: compute and report only, with no lock;
  - `BaselineStoreException` → the failure document with its code (exit 1);
  - JSON on stdout, human summary per FR-022 (stderr in JSON mode).
  Register the command in src/Spectra.CLI/Commands/Ai/AiCommand.cs. Depends on T016.
- [X] T019 [P] [US2] Write tests/Spectra.CLI.Tests/Commands/Baseline/IngestBaselineRecordingTests.cs (part of T5):
  - `ai ingest-tests storage --from` gives entries for every persisted test and a `baseline` object in the success JSON;
  - `ai ingest-update storage --test-id X --from` replaces X's entry with the edited refs and criteria (an added ref appears, a removed one is gone) and leaves other entries untouched;
  - `ai ingest-update storage --all` records each written test;
  - an ingest failure (exit 5) writes no baseline;
  - a malformed `_source-baseline.json` gives exit 1 `BASELINE_UNREADABLE` **and no test file is written** (snapshot of `test-cases/` unchanged);
  - pruning: a deleted test's entry is dropped on the next ingest into that suite.
- [X] T020 [US2] Wire baseline recording into src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs per research R9:
  - after content is read, run the persist inside `BaselineStore.WithLockAsync`: strictly load the suite baseline first, run `ingestor.IngestAsync`, and only on success compute and apply entries for `result.PersistedTests` with a `BaselineContext`;
  - ensure snapshots, then save with live IDs = existing plus persisted;
  - add the `baseline` object to the JSON success output and `Baseline: created n, updated n, unchanged n` to the human line;
  - a `BaselineStoreException` before persisting → failure JSON (`command`, `error_code`, `errors`, `warnings`) on stderr, exit 1, nothing persisted;
  - an exception after persisting → the same, with an extra `persisted: [ids]` and the message naming `spectra ai source-baseline --init --suite <s>`;
  - the existing exit 5/6 failure output is unchanged (FR-027). Add a test to T019 asserting it is byte-identical to the pre-084 output for a malformed-content input.
  Keep the existing hand-rolled config loading. Depends on T016.
- [X] T021 [US2] Wire the same recording into src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs, for both the single `RunAsync` path (replace the edited test's entry) and `RunBatchAsync` (one lock around the batch, recording each successful ingest). Add `baseline` to both JSON outputs. Depends on T020.
- [X] T022 [P] [US2] Add `.spectra/baseline.lock` and `.spectra/update-triage.json` to the gitignore patterns in src/Spectra.CLI/Commands/Init/InitHandler.cs, each with a `// Spec 084` comment. Assert both in tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs, and assert that `.spectra/source-snapshots` and `_source-baseline.json` are **not** in the SPECTRA section.

**Checkpoint**: US2 is complete. Baselines exist for new, edited and bootstrapped tests.

---

## Phase 4: User Story 1 — See which tests are affected by documentation changes since they were written (Priority: P1) 🎯 MVP (with US2)

**Goal**: `update-triage` reports states, buckets, groups, `why`, `removed`, `unbaselined` and `no_tests`;
auto-acks COSMETIC; and writes the detail file and the human summary.

**Independent test**: a baselined test on `docs/storage.md#Quotas`. Change `10GB` → `50GB`; triage reports LOAD with
`why` `numbers: 10gb→50gb`. A paraphrased test on an unchanged section is UNCHANGED.

- [X] T023 [P] [US1] Write tests/Spectra.Core.Tests/Baseline/WordDiffTests.cs:
  - tokenisation table: `10gb.` → `10gb`, `(must)` → `must`, `$5` → `5`, `30%` → `30`, and punctuation-only tokens dropped;
  - a common prefix and suffix are trimmed;
  - insertions, deletions and replacements give the expected `DiffRun`s and Removed/Added counts;
  - deterministic tie-breaking: identical input twice gives identical runs, with delete before insert;
  - the cap: 5,001 words on one side → `Computed = false`, `Reason = "over 5000 words"`;
  - the cell-budget fallback (two 2,100-word middles with nothing in common) gives the whole middle removed and added.
- [X] T024 [US1] Implement src/Spectra.Core/Baseline/WordDiff.cs per research R3: `Tokenize`, `Compute(oldNormalised, newNormalised, cap = 5000)`, prefix/suffix trimming, an LCS with a `ushort` table when `n*m ≤ 4_000_000`, otherwise the whole middle, and the `WordDiffResult` / `DiffRun` records.
- [X] T025 [P] [US1] Write tests/Spectra.Core.Tests/Baseline/ChangeClassifierTests.cs (T3) as a state table. Each row is (baseline raw, snapshot present?, current raw or missing reason, light threshold, expected state, expected `why`):
  - UNCHANGED, including a CRLF-only change;
  - COSMETIC: markup only (`**Each** user`), whitespace only, and the case-only "Save" → "SAVE" (the documented residual);
  - LOAD numbers `10GB` → `50GB` with `why` `numbers: 10gb→50gb`;
  - LOAD for each modal word: must, should, may, cannot, required, only, not, and "must" → "must not";
  - LOAD size: 13 words changed against threshold 12;
  - LIGHT at exactly 12 words, with `why` `words: 12`;
  - LIGHT for a one-word non-modal, non-number change;
  - LOAD for a missing snapshot, with `why` `no diff: snapshot missing`;
  - LOAD for a 5,001-word side, with `why` `no diff: over 5000 words`;
  - REMOVED for a file not found, outside the allowed roots, and an anchor not found;
  - criterion rows: CRITERIA_CHANGED, CRITERIA_REMOVED, and unchanged;
  - the `why` formatting rows: more than 4 numbers gives `…`, an empty side gives `∅`.
- [X] T026 [US1] Implement src/Spectra.Core/Baseline/ChangeClassifier.cs per research R4: the `RefState` and `CriterionState` enums, the `CurrentSection` and `RefVerdict` records, `ClassifyRef(...)` in the fixed check order, `ClassifyCriterion(baselineFp, exists, currentFp)`, the modal word set, and the `why` builders. Depends on T024.
- [X] T027 [P] [US1] Write tests/Spectra.Core.Tests/Baseline/BucketAndGroupingTests.cs (T4):
  - a precedence matrix over every pair of states (the worst wins: REMOVED > CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED > LIGHT > COSMETIC > UNCHANGED);
  - no items → UNCHANGED;
  - grouping: the same changed-item set and bucket → one group; the same refs but a different bucket → two groups; ten tests with `groupSize` 4 → 4/4/2 in ID order; the order is suite, then bucket precedence, then signature; IDs `G1…` across suites; COSMETIC and UNCHANGED tests are not grouped; `why` is the union of the members' `why`, in order.
- [X] T028 [US1] Implement src/Spectra.Core/Baseline/BucketRules.cs (the `Bucket` enum in precedence order, `Of(refStates, criterionStates)`) and src/Spectra.Core/Baseline/TriageGrouper.cs (the `TestVerdict` input record, `TriageGroup`, and `Group(verdicts, groupSize)` per research R8). Depends on T026.
- [X] T029 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Baseline/UpdateTriageCommandTests.cs, covering the acceptance scenarios of User Story 1. Baselines come from `source-baseline --init`.
  - **SC-001 contrast**: on the `10GB` → `50GB` fixture, run `TestClassifier` as `ai update --dry-run` does (or the command itself, reading its result) and assert UP_TO_DATE; on the same workspace, triage reports LOAD. `TestClassifier` itself is not changed;
  - the number change gives LOAD with that `why`;
  - a paraphrased test on an unchanged section gives UNCHANGED;
  - a markup-only change gives COSMETIC, auto-acked: the baseline is refreshed for that ref only, and the next triage is UNCHANGED;
  - with `--no-auto-ack`, the COSMETIC state is reported and the baseline is untouched;
  - a reworded sentence gives LIGHT;
  - must → should gives LOAD;
  - a renamed heading or a deleted file gives REMOVED in `removed`;
  - a changed criterion gives CRITERIA_CHANGED, and a deleted one gives CRITERIA_REMOVED;
  - ten tests sharing two changed refs with `update.group_size` 4 give three groups;
  - `no_tests` lists an unreferenced heading of a changed doc, a whole-file ref suppresses it, and so does a parent-section ref;
  - a partial entry is counted as `partial`;
  - an empty suite folder (only `_index.json`, or nothing) is listed with every count 0, gets no baseline file, and has no tests in the detail file;
  - auto-ack keeps a stale key in the entry (FR-026a);
  - the detail file has diff runs and current fingerprints;
  - `--dry-run` leaves `Snapshot()` unchanged, with `would_auto_ack` reported;
  - the JSON shape is as in the contract;
  - the human summary is on stderr in JSON mode and on stdout in human mode;
  - exit 0 even when changes are found.
- [X] T030 [US1] Implement src/Spectra.CLI/Baseline/TriageEngine.cs:
  - load suites and tests (as in T018);
  - for each test, load its baseline entry (none → UNBASELINED full); classify each current ref that has a baseline hash (`BaselineContext` plus `ReadSnapshotAsync`) and each current criterion; record unbaselined refs and criteria (→ partial); ignore baseline-only keys;
  - compute the bucket with `BucketRules`;
  - collect COSMETIC refs for auto-ack;
  - group with `TriageGrouper`, using `update.EffectiveGroupSize` and `EffectiveLightThresholdWords`;
  - compute `no_tests` per research R7 with `SectionExtractor.Headings`;
  - build the stdout document and the detail document (data-model) as ordered dictionaries, so the output is deterministic.
  Depends on T028, T016.
- [X] T031 [US1] Implement src/Spectra.CLI/Baseline/TriagePresenter.cs: the human summary per contracts/cli-contract.md (per-suite counts line, unbaselined line with the recovery command, one line per group, `no_tests` lines, notes). It writes to a given `TextWriter`.
- [X] T032 [US1] Implement src/Spectra.CLI/Commands/Baseline/UpdateTriageCommand.cs and UpdateTriageHandler.cs:
  - options `--suite`, `--all-suites`, `--no-auto-ack`, `--dry-run`;
  - argument, config and suite errors as in T018 (`command = "update-triage"`);
  - non-dry-run with auto-ack: run the engine inside `WithLockAsync`, apply the COSMETIC refreshes (only those refs' hashes change), ensure their snapshots, save the affected suites with live IDs, and count `auto_acked`;
  - write `.spectra/update-triage.json` atomically, but not on `--dry-run`;
  - stdout gets compact JSON (`CriteriaCommandIO.Json` options) in JSON mode;
  - the presenter writes to stderr in JSON mode and to stdout otherwise;
  - exit 0.
  Register the command in AiCommand.cs. Depends on T030, T031.

**Checkpoint**: US1 and US2 make the MVP. Doc changes are detected, ranked and grouped deterministically.

---

## Phase 5: User Story 3 — Acknowledge tests that were reviewed and need no edit (Priority: P2)

**Goal**: `update-ack --ids` and `update-ack --group`, with the stale check. Unacknowledged changes persist (D5).

**Independent test**: change a section, triage, `update-ack --suite s --ids TC-1`, triage again → UNCHANGED, with the
test file byte-identical.

- [X] T033 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Baseline/UpdateAckCommandTests.cs (part of T5):
  - `--ids` refreshes refs and criteria to current content, the next triage is UNCHANGED, and test files stay byte-identical;
  - `--group G2` from the last triage acks all its members;
  - editing a member's section after triage → `TRIAGE_STALE` (exit 4), with the baseline untouched;
  - no detail file → `TRIAGE_MISSING`;
  - a detail file from `--suite other` → `TRIAGE_MISSING`;
  - an unknown group → `GROUP_NOT_FOUND`, and so does a group from another suite;
  - an unknown ID → `TEST_NOT_FOUND`, with nothing written;
  - both or neither of `--ids`/`--group` → `INVALID_ARGUMENTS`;
  - acking a test with a REMOVED ref → `REF_REMOVED` (exit 4), with the baseline untouched; the same for a CRITERIA_REMOVED criterion → `CRITERION_REMOVED`; `--group` on a REMOVED or CRITERIA_REMOVED group is refused; the next triage still reports the test as REMOVED;
  - `--ids` drops a stale key from the entry (FR-026a);
  - after editing the test file to drop the REMOVED ref, or replace it with a ref that resolves, the next triage no longer reports REMOVED. With a replacement ref the test shows as partial; `update-ack --ids` then succeeds, drops the stale key and records the new ref, and the next triage is UNCHANGED;
  - `--dry-run` leaves `Snapshot()` unchanged;
  - an unacknowledged change is reported again with the same state on the next triage.
- [X] T034 [US3] Implement src/Spectra.CLI/Commands/Baseline/UpdateAckCommand.cs and UpdateAckHandler.cs:
  - options `--suite` (required), `--ids` (comma list), `--group`, `--dry-run`;
  - argument, config and suite errors as in T018 (`command = "update-ack"`);
  - `--ids`: resolve every ID among the suite's tests or refuse with `TEST_NOT_FOUND`;
  - `--group`: resolve and stale-check against `.spectra/update-triage.json` per research R8;
  - before writing, compute each named test's current verdict and refuse any test with a REMOVED ref (`REF_REMOVED`) or a CRITERIA_REMOVED criterion (`CRITERION_REMOVED`), naming the test and items, with nothing written;
  - then, under the lock (none on `--dry-run`), recompute the entries with `BaselineRecorder`, ensure snapshots and save;
  - output per the contract.
  Register the command in AiCommand.cs. Depends on T032.

**Checkpoint**: the review loop closes. Reviewed tests leave triage, and nothing is deferred silently.

---

## Phase 6: User Story 4 — The update skill drives triage instead of word overlap (Priority: P2)

**Goal**: `spectra-update` runs the triage → review → edit → ack flow. The other skills point at triage.

**Independent test**: the skill content assertions in `SkillsManifestTests`.

- [X] T035 [P] [US4] Update tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs per research R12:
  - `AiUpdateCallSites_PassDryRun` matches the legacy command only (regex `spectra ai update(\s|$)`), drops the `spectra-update` row, and keeps the `spectra-generate`/`spectra-execute` rows but asserts they now use `update-triage … --dry-run`;
  - drop `spectra-update` from `AllSkills_WithCommands_IncludeResultFileRead`;
  - add `UpdateSkill_DrivesTriage`: the skill contains `spectra ai update-triage`, `spectra ai update-ack`, `compile-update-prompt`, `ingest-update`, "LOAD only" marked recommended, "LOAD + LIGHT", "report only", `REMOVED`, `CRITERIA_REMOVED`, `CONFIG_MISSING`, `CONFIG_INVALID`, `LOCK_TIMEOUT` and `NON-STOP`, and doesn't match `spectra ai update(\s|$)`;
  - in `UpdateSkill_DrivesTriage`, also assert on the text between the REMOVED step heading and the next step: it contains `update-ack`, `source_refs`, `spectra-delete` and `spectra delete`, and does not contain `compile-update-prompt` or `--force`;
  - add `UpdateSkill_BucketOptions`: the bucket step says LOAD only covers `CRITERIA_CHANGED`, LOAD + LIGHT adds `LIGHT`, choose groups is limited to those three buckets, and REMOVED / CRITERIA_REMOVED never enter the edit loop;
  - add `UpdateSkill_AllSuitesIsOneTriageCall`: it contains `update-triage --all-suites`.
  Keep `UpdateSkill_UsesStepFormat`, `ContainsDoNothingInstruction` and the progress-page assertion green.
- [X] T036 [US4] Rewrite src/Spectra.CLI/Skills/Content/Skills/spectra-update.md per FR-032 and research R12:
  - Step 0: the progress page (`init-seam-progress`, `.spectra-progress.html`, progress.json phases);
  - Step 1: `spectra ai update-triage --suite <suite> --output-format json --verbosity quiet` ("do NOTHING while it runs"), reading stdout;
  - Step 2: present the summary;
  - Step 3: list REMOVED / CRITERIA_REMOVED for review only. Do not run `compile-update-prompt` for them, and never edit the `.md` file yourself. For each, tell the user which ref or criterion is gone (with the triage `reason`), that they can fix or drop it in the test's `source_refs` / `criteria`, and that afterwards `spectra ai update-ack --suite <suite> --ids <id>` records the new baseline. These tests can't be acknowledged while the ref is still listed (`REF_REMOVED`);
  - in Step 3, also: if the feature itself was removed, tell the user the test can be deleted with the `spectra-delete` skill (`spectra delete <id> --dry-run`, then confirm). Never run the delete from this skill;
  - Step 4: ask which buckets to process: LOAD only (recommended; the LOAD and CRITERIA_CHANGED buckets) / LOAD + LIGHT (adds LIGHT) / choose groups (only LOAD, CRITERIA_CHANGED and LIGHT groups are offered) / report only. REMOVED and CRITERIA_REMOVED groups never enter the edit loop;
  - Step 5: per selected group and test, `compile-update-prompt` → edit → `ingest-update`, with the existing bounded retry;
  - Step 6: `spectra ai update-ack --suite <suite> --ids …` for the tests the user reviewed and kept;
  - Step 7: report counts;
  - an exit-code section mirroring `spectra-criteria`: exit 1 → report `error_code` (`CONFIG_MISSING`, `CONFIG_INVALID`, `BASELINE_UNREADABLE`, `LOCK_TIMEOUT`, which is retried once) and stop for that suite; exit 4 → report and stop;
  - an UNBASELINED hint (`source-baseline --init`);
  - the NON-STOP contract;
  - "Update all" = one `update-triage --all-suites`;
  - the bucket meanings replace the classification meanings;
  - the cancel section is kept.
  Depends on T035.
- [X] T037 [P] [US4] Re-point the update delegation rows:
  - src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:411 and spectra-execute.md:138 → `spectra ai update-triage --suite {suite} --dry-run --output-format json --verbosity quiet`;
  - replace the `spectra ai update --suite notification --diff` example in spectra-help.md with `spectra ai update-triage --suite notification`.
  If the bundled skill hash manifest or the agent content embeds these skills, regenerate it as `spectra update-skills` expects (check `SkillsManifest` for a hash list).

**Checkpoint**: the skill drives the new flow. `spectra ai update` is no longer called by any skill.

---

## Phase 7: User Story 5 — The baseline lives safely next to tests, and its output is deterministic (Priority: P3)

**Goal**: guard and determinism properties (T8, T9), and rename/delete behaviour.

**Independent test**: the guard and determinism classes below.

- [X] T038 [P] [US5] Write tests/Spectra.CLI.Tests/Commands/Baseline/BaselineFileGuardTests.cs (T8):
  - build a workspace with two suites, then run `validate`, `index --rebuild`, `list`, `show <id>`, `ai analyze --coverage --output-format json`, `ai review-flagged` (or its handler), `suite list`, `doctor ids` (handler) and `run list-suites` (and `run start` plus `run status` if practical), plus `new DataCollector(root)` for the dashboard;
  - run each once without and once with `_source-baseline.json` (and a snapshot) present;
  - assert exit codes are equal, outputs are equal after normalising temp paths and run IDs or timestamps, and no output mentions `_source-baseline`;
  - `suite rename storage archive` → `test-cases/archive/_source-baseline.json` exists, byte-identical;
  - `suite delete archive` → the file is gone.
  If a command needs a fixture this workspace lacks, use the existing per-command test pattern (handler construction) the audit listed, in the same class.
- [X] T039 [P] [US5] Write tests/Spectra.CLI.Tests/Commands/Baseline/BaselineDeterminismTests.cs (T9):
  - the same workspace triaged three times (`--no-auto-ack`, and after auto-ack settles) gives byte-identical stdout and `.spectra/update-triage.json`;
  - `source-baseline --init` run in two fresh copies of the same workspace (different temp roots) gives byte-identical baseline files and snapshot sets, and no absolute path appears in any output or file;
  - no `\r` byte in any written file;
  - changing one test's section and acking changes only that test's lines in the baseline (line diff).
- [X] T040 [US5] Fix whatever T038/T039 expose. Expected: none for T038 (research R0 audit); possibly ordering or path leaks for T039. Fix them in the Baseline/ sources, never by loosening the tests.

---

## Phase 8: Polish, guards against real workspaces, performance and docs

- [X] T041 Write tests/Spectra.CLI.Tests/Commands/Baseline/BaselineInitTemplateTests.cs (T6, T7):
  - T6: delete the fixture config, run the real `InitHandler(logger, root).HandleAsync(force: false, skipSkills: true)` (as `InitTemplateWorkspaceTests` does); write a doc; `ai ingest-tests` a test referencing a section; edit that section (a number change); `update-triage --suite` reports it as LOAD; zero `spectra.config.json has no` occurrences across all outputs; `update-ack --ids` → the next triage is UNCHANGED;
  - T7: a workspace with tests and no baseline files → triage reports every test UNBASELINED and no other bucket; `Snapshot()` is unchanged except `.spectra/update-triage.json`; then `source-baseline --init --all-suites` → triage is all UNCHANGED, with no config edit at any point.
- [X] T042 Write tests/Spectra.CLI.Tests/Commands/Baseline/UpdateTriageErrorTests.cs:
  - each code in the contract's error table, from each command it applies to, is one JSON document on stderr with `command`, `success:false`, `error_code`, `errors` and `warnings`, and the exit code from the table;
  - human mode ends with `[CODE]`;
  - `LOCK_TIMEOUT` is produced by holding `.spectra/baseline.lock` with `FileLockHandle` in the test and a short timeout hook (add an internal timeout override if needed);
  - `BASELINE_UNREADABLE` names the file.
- [X] T043 Write tests/Spectra.CLI.Tests/Commands/Baseline/UpdateTriagePerformanceTests.cs (SC-003, SC-004) per research R13:
  - generate 10 suites × 250 tests and 50 docs × 10 sections, each test with 2 section refs and 1 criterion (one criteria file);
  - `source-baseline --init --all-suites`, then edit 5 sections (number changes) so that about 10% of tests are affected;
  - `update-triage --all-suites --output-format json` takes under 30 s (Stopwatch), and stdout is ≤ 8,000 characters;
  - also assert the generation itself took under 30 s, so a slow machine fails clearly.
  If the numbers are missed, profile and fix caching in `BaselineContext` / `TriageEngine`.
- [X] T044 [P] Update docs/cli-reference.md:
  - new sections for `spectra ai update-triage`, `update-ack` and `source-baseline`: flags, the JSON shape, the human summary, the state rules table, the bucket precedence, the `why` features, grouping, auto-ack, the detail file, and the error codes with exits;
  - the storage files and what to commit (baseline and snapshots) versus what is gitignored (lock, triage);
  - the `update.group_size` and `update.light_threshold_words` keys with their defaults (not in the init template);
  - the documented residuals (case-only is cosmetic; setext headings aren't boundaries; non-ASCII anchors keep diacritics);
  - mark `spectra ai update` as legacy, replaced by `update-triage`;
  - note `baseline` in the `ingest-tests` / `ingest-update` output;
  - in the `ingest-tests` / `ingest-update` sections, note the two failure shapes: baseline failures use the shared shape, and content and schema failures (exit 5/6) are unchanged.
- [X] T045 [P] Update docs/usage.md: rewrite the "update tests after doc changes" workflow around triage → review → edit → ack. Include bootstrapping an existing workspace (`source-baseline --init`), what the buckets mean, how to commit the baseline and snapshots, and that unacknowledged changes reappear.
- [X] T046 [P] Update CHANGELOG.md with a "Spec 084 — source change baseline (Phase 2a)" subsection under the unreleased `[2.6.0]`, before `### Notes`: Added (three commands, storage, config keys), Changed (ingest-tests/ingest-update record baselines; the spectra-update skill; init gitignore), and Legacy (`ai update`).
- [X] T047 [P] Update CLAUDE.md: add a "Recent Changes" entry for 084 at the top, in the 083 style (the rule summary, new types and commands, and the test counts from T048); add the three commands to the Commands block; mark `spectra ai update` as legacy there.
- [X] T048 Run the full `dotnet build` (expecting no new warnings beyond T001's) and `dotnet test`. Record the counts in the Notes; only the known `ProfileFormatLoaderTests` failure is acceptable.
- [X] T049 Run the quickstart.md manual check with the built CLI in a scratch directory (`dotnet run --project src/Spectra.CLI -- …`), steps 1–8, and record the results in the Notes.
- [X] T050 Fill in the Notes section (deviations, fails-before evidence where relevant, counts), mark every task [X], and stop. Ask the user before committing as `fix(084): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → **Foundational (T003–T016)** → stories.
- **US2 (T017–T022)** needs Foundational.
- **US1 (T023–T032)** needs Foundational. Its CLI tests (T029) use `source-baseline --init` from US2 (T018). Core tasks T023–T028 can run in parallel with US2.
- **US3 (T033–T034)** needs US1 (the detail file and the triage engine for `--group`) and US2 (the recorder).
- **US4 (T035–T037)** needs the command names only. The skill text can be written in parallel with US1/US3, but its tests are verified after T034.
- **US5 (T038–T040)** needs US1–US3.
- **Polish (T041–T050)** needs everything. T044–T047 can run in parallel.

Within Foundational:
- T003∥T005∥T007∥T009∥T011 (tests and models in different files);
- T004 → T006;
- T008 and T010 are independent;
- T012 → T013;
- T014 → T015 → T016.

## Parallel Examples

```text
# Foundational, first wave:
T003 AnchorKeyTests   T005 SectionExtractorTests   T007 SourceFingerprintTests   T009 SerializerTests   T011 UpdateConfig
# US1 core while US2 writers proceed:
T023 WordDiffTests → T024   T025 ChangeClassifierTests → T026   T027 Bucket/Grouping tests → T028
# Docs at the end:
T044 cli-reference   T045 usage   T046 CHANGELOG   T047 CLAUDE.md
```

## Implementation Strategy

1. **MVP = Foundational + US2 + US1**: baselines are recorded and triage reports changes. This already replaces
   word-overlap detection for anyone running the command.
2. **US3**: closes the review loop (ack).
3. **US4**: switches the skill users actually reach.
4. **US5 + Polish**: guards, determinism, the real-template and pre-existing-workspace checks, performance, docs.

## Notes

**Baseline (T001, `63531e3`):** Core 839 / CLI 1592 (1 known failure: `ProfileFormatLoaderTests`) / Execution 228 /
Integration 8. Four pre-existing compiler warnings: CS0108 ×2 (`Name` in DocsRetire/DocsApplyMove), CS8714/CS8621 in
`CriteriaLayoutMigrator`.

**Final (T048):** Core 1025 / CLI 1699 (same 1 known failure) / Execution 228 / Integration 8. No new compiler warnings.

**Performance (T043):** `update-triage --all-suites` on 2,500 tests takes about 2 s (target 30 s); stdout is 7,059
characters (budget 8,000).

**Quickstart (T049), built CLI in a scratch git repo:** every step behaved as specified.
- `init` gitignores the lock and the triage file.
- Ingest records 1 entry and 1 snapshot.
- Triage reports UNCHANGED with no `Warning:` line.
- A markup-only edit is COSMETIC and auto-acked; the next run is UNCHANGED.
- 10GB → 50GB gives G1 LOAD `numbers: 10gb→50gb`, and `no_tests` lists "Archive tier".
- `update-ack --group G1` clears it; `git status` shows only the doc, the baseline and new snapshots.
- With the baseline deleted, the test is UNBASELINED; `--init` then gives a clean triage.
- The three `--dry-run`s leave `git status` and `.spectra/` unchanged.

**Deviations and findings**

1. **UTF-8 output (found by the quickstart, fixed).** The human summary printed `10gb50gb`, and the redirected JSON
   held byte 0x1A instead of `→`.
   - Cause: on Windows, .NET writes a redirected stdout in the legacy console code page.
   - Scope: this affects every command, including the prompts the compile commands print (an em dash or `Ü` in a
     doc reached the model as `?`). It is not specific to 084.
   - Fix: one line in `Program.Main` sets `Console.OutputEncoding` to UTF-8 without a BOM.
   - Test: `Utf8StdoutTests` runs the built CLI as a child process; it fails with the line commented out and passes
     with it.
   - Not changed: stdin decoding (`ingest-* ` without `--from`); the skills always pass `--from`.
2. **`spectra show` cannot open bare-filename tests (found, not fixed).** `ShowHandler` joins the index's `file` to
   the tests dir instead of the suite folder, which is the same bug class as 073. It is out of scope, so it was flagged
   as a separate task. The T8 guard still compares `show` with and without the baseline, and it fails the same way
   both times.
3. **Research R1 was wrong about Markdig.** It is already a Core dependency, not a new one. The own line scanner was
   kept anyway: the ATX-only rule and line-exact section slicing are simpler that way, and T1 pins the behaviour.
4. **`no_tests`:** a heading whose section contains a referenced heading also counts as covered. Without this, a
   document's `# Title` would always be listed.
5. **Compact stdout (SC-004).** The first measurement was 8,504 characters. Two changes brought it to 7,059:
   `counts` lists non-zero buckets only (a missing bucket is 0), and a group's `refs` / `criteria` appear only when
   non-empty. Both are noted in the contract.
6. **New error code `BASELINE_WRITE_FAILED`** (exit 1). An I/O failure while recording the baseline after an
   ingest has already persisted gets this code, with `persisted:[ids]`. The recovery command differs:
   - for `ingest-tests`: `source-baseline --init`;
   - for `ingest-update`: `update-ack --ids`, because `--init` without `--force` keeps a stale existing entry.
7. **Skill tests:**
   - `spectra-update` was dropped from the progress-page list. Its edit loop uses the seam page
     (`.spectra/seam-progress.html`), not `.spectra-progress.html`; research R12 had said it would stay.
     `UpdateSkill_UsesTheSeamProgressPage` replaces that check.
   - It was also dropped from the result-file list.
   - The Cancel section became a "Stop" section: no long-running command is left to cancel.
8. **Detail file:** each test carries a `current` map (refs and criteria → fingerprint or null). `update-ack --group`
   uses it for the stale check.
9. **Locking:** triage takes the baseline lock whenever it may auto-ack (not `--dry-run`, not `--no-auto-ack`), even
   when nothing turns out to be COSMETIC.
10. **Performance fixture:** the shape differs from research R13 to model features. Each suite owns 5 docs, each an
    Overview plus 5 parts; each test references one part and its doc's Overview.
11. **LOW A2:** there is no bundled skill hash manifest, so nothing needed regenerating.
12. **SC-001:** the legacy classifier only says UP_TO_DATE for a test whose words (longer than 3 characters) all
    appear in the doc. The fixture is written that way, and it shows the legacy classifier missing the 10GB → 50GB
    change.
