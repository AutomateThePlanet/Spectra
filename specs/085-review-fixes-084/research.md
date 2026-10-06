# Research: Spec 085 — Review Fixes for 084

## R0 — What the code does at `2a43298` (checked)

| Fact | Where | Consequence |
|---|---|---|
| `SectionExtractor.Extract` and `CoveredHeadings` each compare `AnchorKey.FromAnchor(anchor)` with `AnchorKey.Make(heading.Text)`, where `Text` is the **raw** heading text (closing hashes stripped) | `Core/Baseline/SectionExtractor.cs` | F1: `<a … id>`, `{#id}`, `[Link](x.md)` add letters to the raw key, so they never match. There are two copies of the comparison, which F1 merges into one |
| `TextNormalizer.Normalize` removes autolinks, images and links (keeping their text), comments, well-formed tags of known elements, entities, inline code and emphasis, collapses whitespace, and **lowercases last** | `Core/Text/TextNormalizer.cs` | A case-keeping `Clean` is `Normalize` without the final `ToLowerInvariant`. `Normalize` itself is unchanged, so every comparison key and fingerprint stays the same |
| `BaselineStore.SaveSuiteAsync(testsPath, suite, baseline, liveIds, ct)` prunes every key not in `liveIds`. Its five callers build `liveIds` from tests they parsed (`ingest-*` silently skip unparseable files) | `CLI/Baseline/BaselineStore.cs`, 5 callers | F2: an unparseable file's entry is pruned. The fix belongs in the store, which must see the folder itself |
| `BaselineRecorder.Replace` overwrites the entry with `ComputeAsync`'s result, which omits unresolved refs and unknown criteria | `CLI/Baseline/BaselineRecorder.cs` | F3: a dead ref's fingerprint is lost on rebuild. `Replace` needs the previous entry and the test's current ref/criterion lists |
| `ChangeClassifier.ModalWords` = must, should, may, cannot, required, only, not; tokens are compared exactly | `Core/Baseline/ChangeClassifier.cs` | F4: extend the set; fold `’`/`‘` to `'` only when testing membership |
| `RecordPersistedAsync` always uses `Replace` | `CLI/Baseline/BaselineRecorder.cs` | F5: add a mode; `--keep-baseline` selects `Complete` |
| Repair callers: `spectra-generate.md` line 283 `ingest-update {suite} --all --output-format json` (and the flags-table row at line 68); `spectra-review-flagged.md` line 40 `ingest-update {suite} --test-id {id} --from .spectra/repaired.json --output-format json` | skills | F5: the two call sites gain `--keep-baseline`; `spectra-update` stays without it |
| `Program.Main` sets `Console.OutputEncoding` | `CLI/Program.cs` | F6: on Windows this calls `SetConsoleOutputCP`, which changes the attached console's code page for the rest of the session |
| `MarkdownHeading(Level, Text, StartLine)` is a positional record compared by value in `SectionExtractorTests` | Core | New match data are computed in the record body from `Text`, so a record built with the same values is still equal |
| `TriageEngine.NoTestsAsync` lists `h.Text` (raw) | CLI | F1 / FR-005: list the cleaned text |

## R1 — One heading matcher (F1, FR-001–FR-005)

**Decision.**
- **`MarkdownHeading` gains three properties computed from `Text`:**
  - `CleanText`: remove a trailing attribute block, then `TextNormalizer.Clean`, which is `Normalize` without the
    final lowercasing;
  - `TextKey`: `AnchorKey.Make(CleanText)`;
  - `ExplicitIds`: the keys of every `id="…"` / `id='…'` in `Text` (regex `\bid\s*=\s*(?:"([^"]*)"|'([^']*)')`,
    case-insensitive), plus the `#id` of a trailing attribute block.
- **Attribute block** = `\s*\{:?\s*([#.][^}]*)\}\s*$` (Pandoc/kramdown style: `{#id}`, `{#id .cls}`, `{: #id}`,
  `{.cls}`). Its `#…` token, if any, is the ID. A trailing `{…}` that doesn't start with `#` or `.`
  (`## Config {key}`) is ordinary text.
- **`SectionExtractor.Match(headings, anchor)`** returns `(Index, Rule, Ambiguous)` or none:
  1. the first heading with `TextKey == key`; `Ambiguous` when more than one has it;
  2. the first heading whose `ExplicitIds` contains `key`;
  3. if the decoded anchor matches `^(?<base>.+)-(?<n>[1-9]\d*)$`, and the headings with `TextKey ==
     AnchorKey.Make(base)` number at least n+1, the (n+1)th.
- **`Extract`** and **`CoveredHeadings`** both call `Match`; nothing else compares keys. The `no_tests` list uses
  `CleanText`.
- **FR-001a (added in analysis, I1):**
  - `SectionExtractor.ComparisonText(raw)` is the normalised section text after removing the trailing attribute
    block from every ATX heading line outside fences. `BaselineContext` uses it for the current comparison text,
    which is also what snapshots store, so adding a `{#id}` is COSMETIC.
  - Anchor tags and links need nothing extra, because `Normalize` already strips them.
  - Fingerprints (raw) are untouched.
  - The one-time effect on existing snapshots of sections with `{#…}` headings is a spec edge case.

**What 084 actually reported (I1).** The spec first said these refs were REMOVED under 084. The code shows two
different 084 outcomes:
- a ref that never resolved is omitted by `BaselineRecorder.ComputeAsync`, so it is partial and `--init` can't add it;
- a ref baselined on a plain heading that later gained markup is REMOVED.

T9 tests both, each failing on `2a43298`.

**Rationale.**
- Computing the match data on the heading gives one definition. The two 084 copies are what let extraction and
  coverage diverge.
- `Clean` reuses the normaliser's exact stripping, so "HTML and links removed" means the same thing everywhere.
- The attribute-block rule follows the two Markdown dialects that use it, and avoids eating braces that are part of
  a title.

**The guarantee (FR-006), checked.**
- For a plain heading, `Clean` collapses whitespace only, so its letters and digits equal the raw text's: same key.
- For `**x**`, `*x*`, `_x_` and `` `x` ``, `Clean` removes only the markers, which are not letters or digits, so the
  key is the same.
- **Residuals** (documented in the spec): an HTML entity (`&amp;` loses `amp`), and HTML-like text inside inline code
  (`` `<br>` `` loses `br`), because tags are stripped before inline code. Neither shape is produced by the slug
  styles the spec supports.

**Alternatives.**
- Markdig's heading AST and its auto-identifiers extension: a different slug algorithm (GitHub's), and switching the
  scanner now would risk the T2 guarantee. Rejected.
- Keep the raw-key comparison as a rule-0 fallback: it would keep the residuals working, but it adds a fourth rule
  the user didn't specify. Rejected.

## R2 — Pruning only when the whole suite parsed (F2, FR-007–FR-009)

**Decision.**
- **New `BaselineStore.ScanSuiteAsync(repoRoot, testsPath, suite, ct) → SuiteScan`** with
  `(IReadOnlyList<TestCase> Tests, IReadOnlySet<string> Ids, IReadOnlyList<string> UnparseableFiles)`. It parses
  every `*.md` file without a leading `_`, in ordinal order, and `UnparseableFiles` holds repo-relative paths.
- **`SaveSuiteAsync(testsPath, suite, baseline, SuiteScan scan, List<string> warnings, ct)`:**
  - prune to `scan.Ids` only when `scan.UnparseableFiles` is empty;
  - otherwise skip pruning and add one warning per suite: `"<files> could not be parsed; no baseline entries were
    pruned in suite '<s>'."`
  The `liveIds` parameter is removed, so a caller can't bypass the rule.
- **Callers:**
  - `BaselineCommandIO.LoadSuiteTestsAsync` becomes a thin wrapper over `ScanSuiteAsync`, keeping its existing parse
    warning. Triage, ack and init pass the scan they already have.
  - `RecordPersistedAsync` (ingest-tests and ingest-update) scans after persisting, so the new or edited files are
    included, and passes the scan.
  - The ingest commands' success JSON gains `warnings` (always an array).
- `--dry-run` paths don't save, so they are unaffected.

**Rationale.** The store decides from what's on disk, so all five writers follow the rule by construction. Scanning
after persist in the ingest path costs one extra parse of one suite.

**Alternative.** Passing a "parse failed" flag from each caller: five places to get right, which F2 rules out.

## R3 — Rebuild keeps dead fingerprints (F3, FR-010, FR-011)

**Decision.**
- **`BaselineRecorder.Replace(baseline, test, computed, summary)`** now takes the `TestCase`.
- Before storing, for each ref in `test.SourceRefs`: if it's absent from `computed.Entry.Refs` (it didn't resolve)
  and the previous entry has it, copy the previous fingerprint. The same applies to `test.Criteria` against
  `computed.Entry.Criteria`.
- Keys the test no longer lists are not copied, so they are still dropped.
- Used by every full rebuild: `ingest-update`, `source-baseline --init --force` and `update-ack`.

**Rationale.** The dead ref's old fingerprint is exactly what makes triage report REMOVED (current section missing +
baseline fingerprint present).

## R4 — Meaning-flip words (F4, FR-012–FR-014)

**Decision.**
- **`ChangeClassifier.ModalWords`** becomes: must, should, may, required, no, not, never, none, nothing, cannot,
  can't, don't, doesn't, won't, isn't, aren't, shouldn't, mustn't, without, unless, except, only, always, all, any,
  every.
- **Membership** is tested on `Fold(word) = word.Replace('’', '\'').Replace('‘', '\'')`; `why` shows the folded
  form.
- `WordDiff.Tokenize` already keeps inner apostrophes (it trims only the edges), so `can't` and `can’t` are single
  tokens.

**Checked.** In the 084 state table every existing row keeps its state. "click the button" → "click the link"
contains none of the new words. The 13-word and 12-word size rows use alphabet words.

## R5 — `--keep-baseline` (F5, FR-015–FR-017)

**Decision.**
- **`IngestUpdateCommand`** gains `--keep-baseline` (bool) for both forms, passed to `RecordPersistedAsync(…,
  RecordMode mode)`. The modes are `RecordMode.Rebuild` (`Replace` + R3) and `RecordMode.KeepBaseline`
  (`Complete`: add missing keys only; no entry → create).
- `ingest-tests` always uses `Rebuild`; its tests are new.
- **The success JSON's `baseline` object gains `"mode": "rebuild" | "keep"`**, so a skill can confirm the repair path
  was taken.
- **Skills:**
  - `spectra-generate`: the repair batch command becomes `spectra ai ingest-update {suite} --all --keep-baseline
    --output-format json`, and the flags-table row mentions the flag and why;
  - `spectra-review-flagged` Step 3: `… --from .spectra/repaired.json --keep-baseline --output-format json`;
  - `spectra-update` unchanged.
- **Tests** in `SkillsManifestTests`: every `ingest-update` line in `spectra-generate` and `spectra-review-flagged`
  contains `--keep-baseline`, and no line in `spectra-update` does.

## R6 — UTF-8 only for redirected streams (F6, FR-018–FR-020)

**Decision.**
- **`Program.Main`** calls `ConsoleEncoding.UseUtf8ForRedirectedStreams()`, a new `Spectra.CLI.Infrastructure`
  helper:
  - if `Console.IsOutputRedirected`: `Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new
    UTF8Encoding(false)) { AutoFlush = true })`;
  - the same for stderr with `IsErrorRedirected` / `OpenStandardError` / `SetError`.
- Nothing touches `Console.OutputEncoding`, so an interactive console keeps its code page and behaves exactly as
  before 084.
- **Tests:**
  - `Utf8StdoutTests` (child process) gains a stderr case, using a failure document that contains a non-ASCII path;
  - a source guard asserts that `Program.cs` contains no `Console.OutputEncoding` assignment. In-process, an
    assignment's effect can't be observed without changing the test host's console. The guard fails on `2a43298`.

**Rationale.** `SetOut` / `SetError` replace only this process's writers; the console's code page is never touched.
`AutoFlush` keeps the ordering of interleaved stdout and stderr the same as the runtime's default writers.

## R7 — Compatibility (FR-021, FR-022)

- **The serializer and file shape are not touched.**
- **T9** builds a workspace, writes its baseline with today's code (equal to 084's, since the serializer is
  unchanged), then:
  - re-saves and asserts byte equality;
  - triages, and compares with a recorded 084 triage of the same fixture, captured as an expected JSON string in the
    test.
- **The FR-022 cases** are tested separately:
  - never-resolved refs: partial, then UNCHANGED after `--init`;
  - plain heading → `<a id>` / link / `{#id}`: REMOVED on 084, COSMETIC on 085.
- **The expected 084 triage output** is captured by running the fixture on the `2a43298` build (the T027 worktree) and
  pasted verbatim, never hand-written.
- **T2** keeps a private copy of the 084 matching rule (raw-text key, first match) in the test and asserts the same
  section text for every plain, emphasised and inline-code ref.

**Fails-before.** T2, T9 and the "all parse" half of T4 are guards and pass on `2a43298` as well. Every fix has at
least one test that fails there:
- F1: T1, T3;
- F2: T4 (unparseable case);
- F3: T5;
- F4: T6;
- F5: T7 (the `--keep-baseline` option doesn't exist there, and the skill assertions fail);
- F6: the T8 source guard.
