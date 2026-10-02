# Research: Spec 084 — Source Change Baseline (Phase 2a)

## R0 — What the code already has (checked, not assumed)

| Fact | Where | Consequence |
|---|---|---|
| Every command that enumerates the tests dir uses `*.md` (usually skipping `_` files), the exact name `_index.json`, or directories only. None uses `*.json`, `*`, `_*.json` or `*index*` there. | Full audit of `Directory.Get*/Enumerate*` and `Matcher` in `src/` (Validate, Index, List, Show, RunServices, DataCollector, AnalyzeHandler, BatchReadTestsTool, ReviewFlagged, SuiteScanner, SuiteList/Delete, Delete, DoctorIds, GlobalIdScanner, CriteriaReferenceCounter, TestCaseFrontmatterScanner, every compile-*/ingest-*) | `_source-baseline.json` is invisible to every existing command **today**. T8 needs **guard tests only, no source change**. |
| `spectra suite rename` runs `Directory.Move(oldDir, newDir)` and rewrites only `_index.json`'s `suite` field; `spectra suite delete` runs `Directory.Delete(suiteDir, true)`. | `SuiteRenameHandler.cs:120`, `SuiteDeleteHandler.cs:103` | The baseline moves and goes with its suite for free. The baseline file must **not** store the suite name, or a rename would leave it stale (data-model). |
| The `update` config section exists: `UpdateConfig { chunk_size = 30, require_review = true }`, non-required, default `new()`. | `Models/Config/UpdateConfig.cs`, `SpectraConfig.cs:22` | The new keys go there. `"update": null` would set `Update = null` today, so it must be added to the loader's filled sections (R10). |
| The init template has no `update` section. | `Templates/spectra.config.json` | Defaults apply silently (D8). T6 guards this. |
| `ingest-tests` and `ingest-update` load config by hand (`JsonSerializer.Deserialize<SpectraConfig>`), not through `ConfigLoader`. | `IngestTestsCommand.cs:47`, `IngestUpdateCommand.cs:77/178` | Out of 083's scope and kept. Baseline recording must be null-safe on `Coverage` and `Source` (R9). |
| `TextNormalizer.Normalize` lowercases, strips markup and collapses whitespace to single spaces. It keeps punctuation and Markdown `#`. | `Core/Text/TextNormalizer.cs` | Words = whitespace tokens of the normalised text with leading and trailing punctuation trimmed (R3). Line structure is lost, so headings must be found in the **raw** text (R1). |
| `SourceRefResolver.Resolve(refs)` strips the fragment, resolves against the repo root then `source.local_dir` inside the allowed roots, and returns `ResolvedSourceRef(Ref, RelativePath, FullPath)` or `MissingSourceRef(Ref, Reason)`. It never throws. | `Core/Coverage/SourceRefResolver.cs` | Reused as is for the file part of D1. The anchor is taken from the ref text (after the first `#`). |
| `FileLockHandle.AcquireAsync(path, timeout, ct)` is the cross-process lock behind `CriteriaStore.WithLockAsync` (10 s, `LOCK_TIMEOUT`). | `Core/IdAllocation/FileLockHandle.cs`, `CLI/Criteria/CriteriaStore.cs` | `BaselineStore.WithLockAsync` mirrors it on `.spectra/baseline.lock`. |
| `CriteriaStore.ScanFilesAsync(criteriaDir)` is the one criteria scan. It is recursive, in ordinal order, and lenient. | `CLI/Criteria/CriteriaStore.cs` | The criterion lookup for FR-006/FR-014 uses it, first occurrence wins. |
| `CriteriaCommandIO.WriteFailureJson` is the shared failure shape `{command, success, error_code, errors, warnings, …extras}`. | `CLI/Criteria/CriteriaCommandIO.cs` | Reused by the three new commands. |
| `compile-update-prompt` refuses only when the test is missing or no source/criteria context resolves. It does **not** use `TestClassifier`. | `Generation/UpdatePromptCompiler.cs:26` | The skill can feed it triaged tests unchanged. |
| `docs changed` marks a doc changed when its file hash differs from the criteria index's `DocHash`. | `DocsChangedHandler.cs` | That signal is refreshed by the criteria workflow, which runs **before** update triage, so it is wrong for `no_tests` (R7). |
| Skill tests constrain `spectra-update`: `AiUpdateCallSites_PassDryRun` (any line containing `spectra ai update` must have `--dry-run`; it matches `update-triage` too), `LongRunningSkills_IncludeProgressPageStep` and `AllSkills_WithCommands_IncludeResultFileRead` (`.spectra-result.json`). | `SkillsManifestTests.cs:260–390` | These tests change with the skill (R12). |
| The `spectra-generate` and `spectra-execute` delegation rows call `spectra ai update --suite {suite} --dry-run …`. | `spectra-generate.md:411`, `spectra-execute.md:138` | They are re-pointed to `update-triage --dry-run` (R12). |

## R1 — Sections and anchors (FR-001–FR-003)

**Decision.** A pure `SectionExtractor` in `Spectra.Core/Baseline/` works on raw document text with line endings
already normalised to LF.

- **Scan.** Go line by line, tracking fenced code.
  - A fence opens on a line whose first non-space characters (up to 3 leading spaces) are at least three backticks
    or at least three tildes.
  - It closes on a line of the same character with at least as many.
  - While inside a fence, no heading is recognised.
- **Headings.** A line outside a fence matching `^ {0,3}(#{1,6})[ \t]+(.*?)[ \t]*#*[ \t]*$`, or `^ {0,3}#{1,6}$`.
  The heading text has trailing closing hashes stripped. The result is a list of `(Level, Text, StartLine)`.
- **Section.** The section for heading *i* is lines `StartLine(i)` up to, but not including, the next heading with
  `Level ≤ Level(i)`, or the end of the text. The heading line is included, and the trailing newline of the last line
  is kept as it is in the file.
- **Anchor.** The anchor is the text after the first `#` in the ref.
  - Empty or absent → the whole text.
  - Otherwise `AnchorKey.Make(Uri.UnescapeDataString(anchor))`. A malformed escape falls back to the raw anchor.
  - It is compared with `AnchorKey.Make(heading.Text)`, where `Make` = `ToLowerInvariant` and then keep only
    `char.IsLetterOrDigit`.
  - The first match wins. A second match sets `Ambiguous = true`.
  - No match → `NotFound` → REMOVED, reason `anchor not found`.
- **The key.** `IsLetterOrDigit` is Unicode-aware, so non-ASCII headings work. Diacritics are kept, because the key is
  for matching, not slugging. So `#über` matches "Über", and `#uber` does not; this is documented.

**Rationale**
- Headings must come from the raw text, because the normaliser collapses newlines.
- One pass gives both boundaries and anchors.
- The per-ref key is computed once per (file, anchor), with a per-run cache.

**Alternatives**
- A Markdown library (Markdig): a new dependency for one feature, with differences in slugging, and YAGNI
  (Principle V).
- GitHub's slug algorithm: it doesn't match GitBook's capitalised anchors, which D2 rules out.

## R2 — Fingerprints and snapshots (FR-004, FR-005, FR-008)

**Decision.**
- **Ref fingerprint.** `"sha256:" + lowercase hex(SHA256(UTF8(raw)))`, where `raw` is the section text after removing
  a leading U+FEFF and replacing `\r\n` and lone `\r` with `\n`.
- **Snapshot.** `.spectra/source-snapshots/{hex}.txt`, containing `TextNormalizer.Normalize(raw)` as UTF-8 without a
  BOM and with no trailing newline.
  - It is written only if absent. It is content-addressed by its source's fingerprint, so an existing file is never
    rewritten.
  - The write is atomic: temp then move. If another writer created it in the meantime, that is fine.
- **Criterion fingerprint.** `"sha256:" + hex(SHA256(UTF8(Normalize(criterion.Text))))`. No snapshot is kept for
  criteria.

**Why the raw-text hash.**
- The COSMETIC rule needs "raw differs, normalised equal". So the raw fingerprint detects that something changed, and
  the snapshot (normalised) tells cosmetic from real.
- With a missing snapshot the two can't be told apart, so the ref counts as LOAD / no diff, as the user specified.

**Why LF and BOM normalisation.**
- With `core.autocrlf=true`, a Windows checkout has CRLF and Linux CI has LF.
- Without normalisation, every ref would be COSMETIC on the other OS, and auto-ack would rewrite the committed
  baseline on every run.

**Alternatives**
- Store two hashes per ref (raw and normalised): this changes D4's shape and doubles the diff size. Rejected.
- Hash the normalised text only: COSMETIC becomes unobservable, and D3's "auto-acknowledged" step could never refresh
  anything. Rejected.

## R3 — Word diff (FR-013)

**Decision.**
- **Tokens.** Split the normalised text on spaces (it is already collapsed). Trim leading and trailing characters
  where `char.IsPunctuation || char.IsSymbol`, and drop empty tokens. So `10gb.` → `10gb`, `(must)` → `must`,
  `$5` → `5`, `30%` → `30`.
- **Cap.** If either side has more than 5,000 tokens → no diff, LOAD, `why` `no diff: over 5000 words`.
- **Diff.**
  1. Trim the common prefix and the common suffix.
  2. For the middle (n × m), run a classic LCS dynamic programme when `n × m ≤ 4,000,000`. It uses a `ushort` length
     table of at most 8 MB, then backtracks with a fixed tie-break: prefer delete before insert. The result is
     deterministic.
  3. Over 4,000,000 cells, the whole middle counts as removed and added, which is deterministic and conservative.
  The output is ordered runs `(Kind: Removed|Added, Words[])`.
- **Features.**
  - `numbers`: a removed or added token contains a digit (`char.IsDigit`).
  - `modal`: a removed or added token ∈ {must, should, may, cannot, required, only, not}.
  - `size`: `max(removedCount, addedCount) > light_threshold_words`.
- **`why` strings.** Fixed order: numbers, modal, size, then no-diff reason.
  - `numbers: {removed numbers joined ","}→{added numbers joined ","}`, at most 4 per side, then `…`. An empty side
    is `∅`.
  - `modal: {removed}→{added}`, in the same form.
  - `words: {max count}`, only when the size feature holds.
  - `no diff: snapshot missing` / `no diff: over 5000 words`.
  - LIGHT refs carry `words: {max count}`, so the skill can show how small the change is.

**Rationale**
- Prefix and suffix trimming makes the common case (a local edit) linear.
- The cell budget bounds memory at 8 MB.
- Classification only needs the features, so the conservative fallback can only push a ref towards LOAD, never hide a
  change.

**Alternatives**
- Myers O(ND): it's minimal and fast, but its backtracking memory is O(D²) in the worst case and the code is more
  intricate. The trimmed DP is enough at 5,000 words.
- A sum-of-changes count: it would double-count a replacement, which the spec rejects (Assumptions).

## R4 — States, buckets and the `why` per test (FR-012, FR-014, FR-015)

**Decision.** A pure `ChangeClassifier` handles refs. The order of checks is fixed:

1. The file is unresolved or the anchor is not found → REMOVED, with the resolver's reason or `anchor not found`.
2. The fingerprint equals the baseline fingerprint → UNCHANGED.
3. The snapshot is missing → CHANGED / LOAD, `no diff: snapshot missing`.
4. The snapshot text equals the current normalised text → COSMETIC.
5. Either side has more than 5,000 words → LOAD, no diff.
6. Otherwise diff, then LOAD if any feature holds, else LIGHT.

Criteria:
- the ID is missing from every criteria file → CRITERIA_REMOVED;
- the fingerprint differs → CRITERIA_CHANGED;
- else unchanged.

`BucketRules.Worst(states)` uses the precedence list. Ref keys and criterion IDs present now but absent from the
baseline entry are *unbaselined items*: they don't affect the bucket, but they make the test `partial`. Baseline keys
absent now are ignored.

## R5 — Baseline file format and writer (FR-007, FR-009, FR-023)

**Decision.**
- **Model.** `SourceBaseline { int Version = 1; SortedDictionary<string, BaselineEntry> Tests (ordinal) }` and
  `BaselineEntry { SortedDictionary<string,string> Refs; SortedDictionary<string,string> Criteria }`.
- **Writer.** `Utf8JsonWriter` with `Indented = true` and relaxed escaping (`UnsafeRelaxedJsonEscaping`, so `#`, `→`
  and non-ASCII stay readable). Keys are written in ordinal order, then `\r\n` is replaced with `\n` (on .NET 8,
  `Indented` uses `Environment.NewLine`), and a single trailing `\n` is added.
  - Each test is a nested object, so a change to one test touches only its own lines.
  - An empty `refs` / `criteria` is written as `{}`.
- **Reader.** It is strict.
  - These are `BASELINE_UNREADABLE`, naming the file and the problem: invalid JSON, a root that isn't an object,
    `version` ≠ 1, `tests` that isn't an object, an entry missing `refs` or `criteria`, or a value that doesn't match
    `^sha256:[0-9a-f]{64}$`.
  - A missing file is an empty baseline.
- **Write.**
  1. Serialise.
  2. If the bytes equal the file on disk, skip.
  3. Otherwise write `{name}.{guid}.tmp` in the same folder and `File.Move(tmp, path, overwrite: true)`.
- **Pruning.** Writing a suite's file drops entries whose IDs are not among the suite's current tests (FR-009).

**Alternatives**
- `JsonSerializer` with a `SortedDictionary` model: key order is fine, but the indentation and newline behaviour
  depend on the OS/runtime, and escaping takes more options. The explicit writer is fewer lines to make certain.
- One line per test: smaller diffs, but unreadable for long refs. Rejected.

## R6 — Lock and store (FR-009, FR-028)

**Decision.** `Spectra.CLI.Baseline.BaselineStore`:
- `WithLockAsync(repoRoot, body, ct)` on `.spectra/baseline.lock`: 10 s, then `BaselineStoreException(LOCK_TIMEOUT)`.
- `LoadSuiteAsync(testsPath, suite)`, strict.
- `SaveSuiteAsync(testsPath, suite, baseline, liveIds)`: prunes, skips identical content, writes atomically.
- `EnsureSnapshotAsync(repoRoot, fingerprint, normalisedText)`.

Writers take the lock **before** they read the baseline, and hold it until the last write. On `--dry-run` no lock
is taken, so no lock file is created. Writers only read, compute and report.

## R7 — `no_tests` (FR-020, amended in the spec)

**Decision.**
1. A document is "changed in this triage" when at least one triaged ref into it is CHANGED (LOAD or LIGHT).
2. For each such document, list the headings it has now (R1 scan) that no triaged test's ref covers.
3. A whole-file ref covers all of its document's headings. A section ref covers its heading and all headings inside
   its section.
4. Order the entries by doc path (ordinal), then heading position.

**Why not `docs changed`.** The criteria workflow runs before update triage and refreshes the criteria index's
`DocHash`. So by the time triage runs, `docs changed` reports the doc as unchanged, and `no_tests` would be empty in
the normal flow. The baseline is the only record that survives that.

## R8 — Grouping, group IDs and the detail file (FR-017, FR-019, FR-021, FR-025)

**Decision.**
- **Group key.** `(suite, bucket, changed-item signature)`. The signature is the sorted list of `ref:<ref>=<STATE>` and
  `criterion:<id>=<STATE>` for every non-UNCHANGED item; COSMETIC refs that were auto-acked are excluded.
- **Order.** Suite ordinal, then bucket precedence, then signature ordinal. Each key's tests are sorted by ID
  (ordinal) and chunked at `group_size`. IDs are `G1…Gn` across the run.
- **Detail file.** `.spectra/update-triage.json` (compact JSON, deterministic), with:
  - `version`, `scope` (`{"all_suites":true}` or `{"suite":"s"}`) and `config` (the `group_size` and
    `light_threshold_words` used);
  - per test, every item with its baseline and current fingerprints, state, `why` and diff runs;
  - `groups` (with members);
  - the unbaselined IDs (full and partial);
  - the notes.
  It is written atomically, without the baseline lock (scratch, single-writer by convention), and not on
  `--dry-run`.
- **`update-ack --group`.**
  1. Read the detail file. Missing, or not covering the `--suite` → `TRIAGE_MISSING`.
  2. Find the group. Absent, or in another suite → `GROUP_NOT_FOUND`.
  3. Recompute the members' current fingerprints, refs and criteria. Any difference from what the detail file
     recorded as *current* → `TRIAGE_STALE`.
  4. Otherwise refresh the members' entries.
  All of this runs under the lock, with nothing written on refusal.

**Rationale.** Sequential IDs are short (good for the token budget) and readable. The staleness check is what makes
run-scoped IDs safe.

## R9 — Recording from `ingest-tests` and `ingest-update` (FR-027)

**Decision.**
- **`BaselineRecorder.ComputeEntryAsync(test, context)`.** For each ref in `test.SourceRefs`, resolve it, extract the
  section, then fingerprint it and record a snapshot. For each ID in `test.Criteria`, look it up and fingerprint it.
  - A ref that doesn't resolve, or whose anchor isn't found, is **omitted** from the entry, so it shows as an
    unbaselined ref at triage. This is not an error: a generator can write a ref to a doc that isn't there yet.
    Recording a fake hash would make it look REMOVED for ever.
  - The same rule applies to unknown criterion IDs.
  - The context caches file text, sections and criteria per run.
- **`ingest-tests`.** After config and content are read, it opens the lock.
  1. Strictly load the suite baseline. Unreadable → exit 1 `BASELINE_UNREADABLE`; nothing is persisted.
  2. Persist the tests (unchanged code).
  3. Compute an entry for each persisted test and save, pruning to the suite's current IDs (existing plus persisted).
  On an ingest failure (exit 5/6) no baseline is written.
- **`ingest-update` (single and `--all`).** The same flow, under one lock for the whole batch. Each successfully
  ingested test's entry is replaced.
- **JSON success output** gains `"baseline": {"created":n,"updated":n,"unchanged":n,"snapshots_written":n}`; the
  human line gains `Baseline: …`.
- **Failure after persist.** If the baseline save fails after the tests were persisted (an I/O error), this is one
  JSON failure document on stderr, `error_code` = the store's code, `extras.persisted` = the IDs, and exit 1. The
  message names the recovery: `spectra ai source-baseline --init --suite <s>`.
- **Config.** The hand-rolled config loading is kept; changing it is out of scope. The recorder uses
  `config.Source?.LocalDir ?? "docs/"` and `config.Coverage?.CriteriaDir ?? "docs/criteria"`, so a `null` section
  can't crash it.

**Alternatives**
- Record after persisting without taking the lock first: an unreadable baseline would then be discovered only after
  the tests were written. Rejected.
- Fail ingest when a ref doesn't resolve: that's a behaviour change to generation, out of scope.

## R10 — Config keys (FR-029)

**Decision.**
- **`UpdateConfig`** gains `[JsonPropertyName("group_size")] int? GroupSize` and
  `[JsonPropertyName("light_threshold_words")] int? LightThresholdWords`, plus `EffectiveGroupSize => GroupSize ?? 10`
  and `EffectiveLightThresholdWords => LightThresholdWords ?? 12`.
  - A JSON `null` key becomes `null`, which means the default.
  - A string or a fraction → `JsonException` → `CONFIG_INVALID`. The STJ message names the path
    `$.update.group_size`.
- **`ConfigLoader.CheckedSections`** gains `("update", ConfigSections.None)`, so a missing or `null` `update` is
  filled silently.
- **`ConfigLoader.Validate`** adds `INVALID_UPDATE_GROUP_SIZE` ("update.group_size must be an integer ≥ 1") and
  `INVALID_UPDATE_LIGHT_THRESHOLD` ("update.light_threshold_words must be an integer ≥ 0"). Both surface as
  `CONFIG_INVALID` through the entry points.
- **Guards.**
  - T6: the real init template has no `update` section, so there must be 0 warnings.
  - T7: a pre-existing workspace runs with no config edit.
  - The 083 `ConfigRuleMatrixTests` stays green, with added rows for `update` null, `update.group_size` null, and an
    out-of-range value.

## R11 — Command surface and errors (FR-016, FR-024, FR-026, FR-030, FR-031)

**Decision.**
- **Placement.** Three commands under `spectra ai` (registered in `AiCommand`), with code in
  `src/Spectra.CLI/Commands/Baseline/`. Each command class is a thin parser, and a handler holds the logic: the
  `UpdateTriageCommand`/`UpdateTriageHandler`, `UpdateAckCommand`/`UpdateAckHandler` and
  `SourceBaselineCommand`/`SourceBaselineHandler` pairs.
- **Config.** All three call `ConfigLoader.LoadForWriteAsync(root, ConfigSections.Source | ConfigSections.Tests, ct)`
  and print warnings (stderr in JSON mode, `Warning:` lines otherwise). The warnings are included in the success
  JSON `warnings` array.
- **Exit codes.** 0 success, 1 environment error, 4 refusal. They use the shared `ExitCodes` / criteria constants.
- **Failures.** All go through `CriteriaCommandIO.WriteFailure` / `WriteFailureJson`, with `command` set to
  `update-triage` / `update-ack` / `source-baseline`.
- **Argument checks** run before config is loaded and are refused with `INVALID_ARGUMENTS`:
  - exactly one of `--suite` and `--all-suites`;
  - for ack, exactly one of `--ids` and `--group`;
  - `--init` is required on `source-baseline`.
- **Scope.** Suites are the directories under the tests dir that don't start with `_`, in ordinal order, the same
  rule as `validate`/`index`. `--suite` naming a missing directory → `SUITE_NOT_FOUND`.
- **Loading tests.** `*.md` files minus `_*` in each suite folder, parsed with `TestCaseParser`. Unparseable files are
  skipped with a warning naming the file, as the ingestors do.

## R12 — Skill and its tests (FR-032, FR-033)

**Decision.**
- **`spectra-update.md` rewritten.**
  1. Step 0: the progress page.
  2. Step 1: `spectra ai update-triage --suite <s> --output-format json --verbosity quiet`, or `--all-suites`. Read
     stdout.
  3. Step 2: show the summary.
  4. Step 3: list REMOVED / CRITERIA_REMOVED for review only.
  5. Step 4: ask which buckets to process, with options LOAD only (recommended: the LOAD and CRITERIA_CHANGED
     buckets), LOAD + LIGHT (adds LIGHT), choose groups (only groups in those three buckets), report only. REMOVED and
     CRITERIA_REMOVED never enter the edit loop; Step 3 tells the user to fix the ref by hand and ack, or to delete the
     test with the `spectra-delete` skill when the feature itself was removed.
  6. Step 5: per selected group and test, the existing compile-update-prompt → edit → ingest-update loop, with
     bounded retry.
  7. Step 6: `update-ack --suite <s> --ids …` for the tests the user reviewed and kept.
  8. Step 7: report counts.
  - Exit-code handling as `spectra-criteria`: report `error_code`, stop for that suite, retry once on
    `LOCK_TIMEOUT`, and name `CONFIG_MISSING` / `CONFIG_INVALID`.
  - The NON-STOP contract is restated.
  - The legacy classification-meanings section becomes a buckets section.
- **`spectra-generate` / `spectra-execute` delegation rows** → `spectra ai update-triage --suite {suite} --dry-run
  --output-format json --verbosity quiet`.
- **`spectra-help`**: the `ai update` example is replaced with `update-triage`.
- **`SkillsManifestTests`.**
  - `AiUpdateCallSites_PassDryRun` matches the legacy command exactly (`spectra ai update` followed by a space or end
    of line). `spectra-update` is dropped from its rows, and a new test asserts the skill never calls the legacy
    command.
  - `spectra-update` is dropped from the `.spectra-result.json` list; it reads stdout, as `spectra-generate` does.
  - It stays in the progress-page list; Step 0 keeps `.spectra-progress.html` for the edit loop.
  - New: `UpdateSkill_DrivesTriage` covers the steps, the recommended option, the error codes and `update-ack`.

## R13 — Performance (SC-003, SC-004)

**Decision.**
- **Per run:** each test file is parsed once. Each doc file is read once (cached by full path), and its heading scan
  runs once. Each (file, anchor) section is extracted, fingerprinted and normalised once. The criteria scan runs
  once. Snapshot reads are cached by hex.
- **Perf test.** `UpdateTriagePerformanceTests` (CLI tests, non-parallel collection) generates 10 suites × 250 tests
  and 50 docs × 10 sections. Each test refs 2 sections and links 1 criterion. One baseline-init runs, then 5 sections
  are edited so about 10% of tests are affected.
  - It asserts that `update-triage --all-suites` takes under 30 s.
  - It asserts that stdout is ≤ 8,000 characters.
  - It also asserts that the workspace generation takes under 30 s, so a slow machine fails clearly.

**Rationale.** All the expected cost is file I/O plus one diff per changed section. A 2,500-test triage should take
seconds.

## R14 — Test strategy (T1–T9)

| ID | Class | Kind |
|---|---|---|
| T1 | `Core.Tests/Baseline/SectionExtractorTests` | Theory table |
| T2 | `Core.Tests/Baseline/AnchorKeyTests` | Theory table (incl. percent-escapes, `_`, punctuation, non-ASCII) |
| T3 | `Core.Tests/Baseline/ChangeClassifierTests` + `WordDiffTests` + `SourceFingerprintTests` | Theory tables (state per row; CRLF/BOM) |
| T4 | `Core.Tests/Baseline/BucketAndGroupingTests` | Precedence matrix; grouping/chunking/order |
| T5 | `CLI.Tests/Commands/Baseline/BaselineWritersTests` | Command level: ingest-tests, ingest-update (single and `--all`), ack, init, dry-run directory snapshots, auto-ack |
| T6 | `CLI.Tests/Commands/Baseline/BaselineInitTemplateTests` | Real `InitHandler` → ingest → edit → triage (0 config warnings) → ack → UNCHANGED |
| T7 | same class | Pre-existing workspace: all UNBASELINED, snapshot unchanged except the detail file; init → clean |
| T8 | `CLI.Tests/Commands/Baseline/BaselineFileGuardTests` | Each command's output with and without the file, byte-compared after normalising temp paths; rename and delete |
| T9 | `CLI.Tests/Commands/Baseline/BaselineDeterminismTests` | Repeat runs byte-identical; one-test change = local diff |
| — | `UpdateTriageErrorTests`, `SkillsManifestTests`, `ConfigRuleMatrixTests` and `ConfigLoaderEntryPointTests` rows | Error codes and shape, skill, config |

A new fixture, `BaselineTestWorkspace`, follows `CriteriaTestWorkspace`:
- a temp root with config, docs, test-cases and `.spectra`;
- `RunAsync` registers `ai`, `docs`, `suite`, `list`, `validate`, `index`, `show` and `run` on a `RootCommand`;
- `WriteTest(suite, id, refs, criteria)`, `Snapshot()` (every file, including `.spectra/`, so `--dry-run` is proven
  over the whole tree) and `ReadBaseline(suite)`.

All of these tests run in the non-parallel `CriteriaCommands` collection, because they capture the console and change
the current directory.
