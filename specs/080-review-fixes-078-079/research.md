# Research: Spec 080 — Review fixes for 078 and 079

Base: `080-review-fixes-078-079`, branched from `1ba172a` (079 committed on `079-stable-criteria-ids`, not merged).
Every item gets a regression test that fails on the pre-fix code (FR-015). Line numbers are as of `1ba172a`.

## R1 — A1: update writes to the test's own file

**Facts**
- `UpdateHandler.ApplyChangesAsync` builds the path as `Path.Combine(suitePath, $"{Id}.md")` in the update loop
  (≈661), the orphan loop (≈684) and the delete loop (≈694).
- `BatchReadTestsTool` enumerates `Directory.GetFiles(suitePath, "*.md")` (top level only) and passes the **full file
  path** as `TestCase.FilePath`.

**Decision.** Add one helper, `TestFilePathOf(test, suitePath)`:
- `Path.IsPathRooted(test.FilePath)` → use `test.FilePath`
- otherwise → `Path.Combine(suitePath, test.FilePath)`
- empty `FilePath` → fall back to `{Id}.md`, for in-memory tests that were never read from disk

All three loops use it.

**Rationale.** The file of record is where the test was read from. Deriving the path from the ID creates a duplicate
test and makes the delete a no-op.

**Test.** `UpdateHandlerFilePathTests`: `TC-100-login.md` with `id: TC-100` and a missing doc.
- `--no-review` marks that file and creates no `TC-100.md`.
- `--no-review --delete-orphaned` deletes it.

Both fail on `1ba172a`.

## R2 — A2: a source dir outside the repository

**Facts**
- `SourceRefResolver` accepts a candidate only if it is under the repo root (`IsUnderRoot`, `TryFile`).
- A repo-relative candidate that escapes the repo returns `OutsideRepository` immediately, so the source-dir fallback is
  never tried.
- `SourceRefDocumentLoader` turns every `Missing` ref into the same "Source ref not found: {ref}".

**Decision**
- The resolver gets **allowed roots** = {repo root, source dir}. A candidate resolves if it is an existing file under
  **either** root.
- The candidate order is unchanged (fully qualified → repo-relative → source-dir).
- A ref whose candidates all lie outside both roots → `Missing(OutsideAllowedRoots)`. The enum value
  `OutsideRepository` is renamed, because the meaning changed.
- `RelativePath` for a file outside the repo is `Path.GetRelativePath(repoRoot, full)` with `/` (e.g.
  `../product-docs/docs/checkout.md`).
- The loader's messages differ by reason:
  - `NotFound` → `Source ref not found: {ref}`
  - `OutsideAllowedRoots` → `Source ref is outside the repository and the configured source directory: {ref}`
  - `Empty` refs are ignored, as before.

**Rationale.** `source.local_dir` is user-configured. Honouring it doesn't widen access beyond what the user chose, and
escaping both roots is still refused.

**Not changed.** `CriteriaPaths.NormalizeSourceDoc` (criteria `--doc`) still requires the repo. Criteria storage for
out-of-repo docs isn't a review item.

**Tests**
- Extend `SourceRefResolverTests`: sibling source dir, with bare, `../`-relative and absolute refs → resolved; a ref
  escaping both roots → `OutsideAllowedRoots`.
- `UpdateOutsideSourceDirTests`: classification finds the docs, so there are no orphans.
- Extend `CriticVerificationCommandsTests`: an absolute ref into the sibling folder loads; an escaping ref shows the
  "outside…" message.

## R3 — A3: one list of known frontmatter keys

**Decision**
- Add `Spectra.Core.Models.TestCaseFrontmatterKeys.Ordered`, holding the 21 keys in write order (today's
  `TestFileWriter.KeyOrder`).
- `TestCaseParser.KnownFrontmatterKeys` becomes `new HashSet<string>(Ordered)`, and `TestFileWriter.KeyOrder` becomes
  `Ordered`.
- **Guard test:** the `[YamlMember]` aliases on `TestCaseFrontmatter` (via reflection) equal `Ordered` as a set. This
  also catches DTO drift.
- **Guard test:** writing a test that sets every key never throws. The writer's `switch` throws on an unhandled key.

**Rationale.** A single source makes divergence impossible. The reflection test covers the third copy, the DTO itself.

## R4 — A4: comments attach to the key below them

**Facts.** `TestCaseParser.CaptureFrontmatterEntries` slices each key from its start to the next key's start. So the
comment and blank lines before key N+1 end up in entry N. The first entry absorbs any text before the first key.

**Decision (the layout shape changes)**
- `TestCaseLayout` gains `FrontmatterLeading` (text before the first key) and `FrontmatterTrailing` (after the last
  entry's value).
- `FrontmatterEntry` gains a `Prelude`. This holds the run of blank lines and **column-0** `#` comment lines directly
  above the key line. `RawText` is then only the key and its value.
- **Split rule.** Scan the lines between one key's start and the next key's start from the bottom up. Trailing lines
  that are blank or start with `#` at column 0 move to the next entry's `Prelude`. For the last key they move to
  `FrontmatterTrailing`.
- Indented `#` lines stay with the value, because they may be block-scalar content.
- **Writer**
  - Emit `FrontmatterLeading` first. For each emitted key, emit `Prelude` then either the raw text (unchanged) or the
    canonical text (changed). Emit `FrontmatterTrailing` last.
  - A key that is removed (empty after a change) drops its prelude with it.
  - When unknown keys are reordered to follow the known ones, they carry their preludes.
- **Invariant (new layout tests):** `Leading + Σ(Prelude + RawText) + Trailing == frontmatter text`.

**Rationale.** Comments annotate the line below them, so attaching them there makes rewrites of the entry above
harmless. Keeping the leading block separate means a file header never travels with a key that gets reordered.

**Alternatives considered.** Attaching to the key above (today's behaviour) is the bug itself. Attaching all comments
to the leading block would move inline documentation away from its key.

**078 tests affected.** `TestCaseLayoutTests.Entries_CaptureFlowCollectionsBlockScalarsAndComments` asserted the buggy
attachment: the `id` entry started with the leading comment, and the `custom` entry ended with the between-keys comment.
It is updated to the new rule. The round-trip tests (`TestFileWriterRoundTripTests`) must stay green unchanged.

## R5 — A5: one config loader with two entry points

**Facts**
- `ConfigLoader` returns `ParseResult<SpectraConfig>`. It fails on a missing file, invalid JSON or missing required
  properties (a `JsonException`), and on validation errors (`source`, `tests` or `ai` missing).
- Hand-rolled loaders: `CriteriaCommandIO.LoadConfigAsync`, `SourceRefDocumentLoader.ReadSourceDirAsync` (which falls
  back to `docs` on a parse error), the lenient block in `CompileExtractionPromptCommand`,
  `CompileCriticPromptCommand.ResolveTestsDirAndConfigAsync` (defaults on a `JsonException`), the repair-prompt and
  repair-batch `ResolveTestsDirAsync`, `DocsChangedHandler` (errors on a missing file), the JSON block in
  `IngestCriteriaCommand`, and `AnalyzeHandler.RunImportCriteriaAsync`.

**Decision.** Add two methods to `Spectra.Core.Config.ConfigLoader`:

```text
static Task<ConfigLoadResult> LoadForReadAsync(string repoRoot, ct)   // missing → Default; malformed/invalid → error
static Task<ConfigLoadResult> LoadForWriteAsync(string repoRoot, ct)  // missing → MissingConfigMessage; malformed/invalid → error
record ConfigLoadResult(SpectraConfig? Config, string? Error)  // Error != null ⇒ exit 1
MissingConfigMessage = "No spectra.config.json found. Run 'spectra init' first."
InvalidConfigPrefix = "spectra.config.json is invalid: "   // message = InvalidConfigPrefix + firstError.Message
```

| Kind | Commands |
|---|---|
| Read | `compile-extraction-prompt`; `compile-critic-prompt`, `compile-repair-prompt` and `compile-repair-batch` (their config resolution **and** `SourceRefDocumentLoader`, which now receives the source dir instead of re-reading config); `docs changed` |
| Write | `ingest-criteria`; `docs retire`, `docs apply-move`, `docs migrate-criteria` (via `CriteriaCommandIO`, which now delegates); `ai analyze --import-criteria` |

- Output shape is unchanged: in JSON mode each command wraps the message in its existing error JSON; in human mode it
  writes the message to stderr.
- **Behaviour change:** `docs changed` with no config now runs on the defaults instead of exiting 1.

**Rationale.** This implements the user's clarification. Missing config means defaults only for read commands, and
nothing ever continues on a config it couldn't parse.

**Tests.** `ConfigLoaderEntryPointTests` (Core) covers missing, malformed and invalid for both methods. A CLI theory runs
every in-scope command against a malformed config and asserts exit 1 plus the shared message. Missing config → the read
commands succeed and the write commands show `MissingConfigMessage`.

## R6 — B1/B2/B3: shared-file provenance, one helper

**Decision.** Add `Spectra.Core.Criteria.CriteriaProvenance`:
- `IsShared(index, source)`: another document source has the same lexical `file`.
- `OwnedBy(criteria, doc)`: criteria whose lexically normalised `source_doc` equals `doc` (case-insensitive).
  Criteria with no `source_doc` belong to no document.

`CriteriaWorkspace` gains `LeaveSharedFileAsync(sharedFile, doc)`:
- Read the file strictly and remove `OwnedBy(doc)`.
- If nothing is left **and** no index source references the file → delete it.
- Otherwise, if anything was removed → rewrite it with the remaining criteria. The header's `source_doc` is the first
  remaining criterion's `source_doc`, and `doc_hash` is null.

**Callers**
- **Ingest** (shared → derived): replaces today's "delete only if unreferenced" branch.
- **Retire** (shared): removes the index entry first, then leaves the file. Today it leaves the file untouched.
- **Apply-move** (shared) — B1 plus B2. It copies only `OwnedBy(from)`, then leaves the shared file.
- **compile-extraction-prompt** — B3. If `IsShared`, it offers only `OwnedBy(doc)`.

**Rationale.** One provenance rule everywhere, so the model is never offered an ID that ingest would reject.

**Tests**
- Extend `IngestCriteriaStorageTests.PastCollision…`: after A leaves, the shared file still holds B's criteria and no
  longer A's.
- New `SharedCriteriaFileTests`: compile for A offers only A's IDs, and ingesting those IDs succeeds (exit 0, where
  `1ba172a` gives exit 7); apply-move B copies only B's criteria; retire removes the doc's criteria from the shared file
  and coverage drops; the last leaver deletes the file.

## R7 — B4: tags compared as a set

**Decision.** In `CriteriaChangeset`, the `tags` comparison key is the tags trimmed, de-duplicated case-insensitively,
sorted ordinally case-insensitively, lower-cased and joined. The reported `old`/`new` values stay the original joined
display text. Because the no-op rule uses `IsEmpty`, a tags-only reorder now writes nothing. The stored casing is kept,
since the previous criterion is not rewritten.

**Tests**
- A Core `CriteriaChangesetTests` case: reordered or case-changed tags → empty.
- A CLI identity test: a re-ingest with tags reversed leaves the file byte-identical.

## R8 — B5: skill instructions for exit 1

**Decision.** In `spectra-criteria.md`, steps 1a (apply-move), 1b (retire) and 2c (ingest) each gain the line:
"Exit 1 → report the `error_code` (e.g. `CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH`) and
stop for this document. On `LOCK_TIMEOUT`, retry the same command once." A `SkillsManifestTests` assertion covers each
step.

## R9 — B6: a distinct rejection outcome

**Decision**
- `ExtractionOutcome` gains `Rejected`. `CriteriaIngestResult.Rejected(...)` sets `Outcome = Rejected`.
- `IsCacheable` (`Outcome == Extracted`) is already false for it. `IsSuccess` becomes `Outcome == Extracted`.
- Every switch or comparison on the outcome is audited. `IngestCriteriaCommand` maps by `ErrorCode` first, so its exit
  codes don't change.
- The failure JSON reports `"outcome": "Rejected"`.

**Tests.** The exit-7 test asserts `outcome == "Rejected"`, which is `Extracted` on `1ba172a`.

## R10 — B7: compile-extraction-prompt failure output

**Decision**
- Warnings are collected into a list and are not printed immediately.
- **JSON mode.** Every failure path writes exactly one JSON object to stderr, always with a `warnings` array:
  - refusals, exit 4: `{refused, missing_input, message, warnings}`
  - document not found (`DOCUMENT_NOT_FOUND`), document unreadable (`DOCUMENT_UNREADABLE`) and invalid config
    (`error_code: null`), exit 1: `{error_code, message, warnings}`
- **Human mode.** On a failure, the warnings are printed first, then the error line.
- **On success,** the warnings go to stderr as text, as today.

**What the code actually does.** The command runs in this order: check `--doc` → document exists → read → empty-doc
short-circuit (a success, JSON on stdout) → collect warnings → compile. The compiler's only refusal (`MissingRequired`,
path or content missing) is already caught by the command's own checks. So the "warning printed before the refusal JSON"
sequence from the review is not reachable from the CLI today. The real defect on `1ba172a` is that the
document-not-found and document-unreadable failures print plain text in JSON mode. After T044, config loads first, so a
config error also comes before any warning.

**Tests.**
- Command level:
  - A missing document and an unreadable document, in JSON mode: stderr parses as one JSON document with the right
    `error_code` and `"warnings": []`. On `1ba172a` it is plain text, so this fails.
  - An invalid config gives the same result (shared with T041).
  - An empty document still short-circuits to stdout with exit 0.
- Shape: the command's failure-writing helper, called directly with a non-empty warnings list, produces one JSON
  document carrying them. This covers the compiler-refusal path defensively, since it cannot be reached through the CLI.

## R11 — B8: one criteria-file scan

**Decision.** Add `CriteriaStore.ScanFilesAsync(criteriaDir, ct)`, which returns `IReadOnlyList<(string File,
IReadOnlyList<AcceptanceCriterion>)>`: a recursive, ordinal-sorted, lenient read. `LoadAsync` and
`DocsChangedHandler` both use it, and `DocsChangedHandler.ScanDuplicatesAsync` is deleted.

**Test.** B8 is a pure refactor: today both scans are recursive, match `*.criteria.yaml` and read leniently, so they
behave identically and no behavioural test can fail before the fix. The regression test is therefore a structural
guard, and the plan says so openly:
- `DocsChangedHandler` no longer declares its own scan: reflection finds no `ScanDuplicatesAsync`. This fails on
  `1ba172a`.
- `CriteriaStore.ScanFilesAsync` exists and is public.

A behavioural check sits alongside it: a nested criteria file shows up in both the `docs changed` and the ingest
`duplicate_ids`. This locks in the equivalence the refactor must keep.

## R12 — B9: the normaliser strips only real HTML tags

**Decision**
- Replace the tag regex with: `</?(?:WHITELIST)(?:\s+[A-Za-z_:][-A-Za-z0-9_:.]*\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'=<>`]+))*\s*/?>`
  (case-insensitive), plus HTML comments `<!--[\s\S]*?-->`.
- `WHITELIST` = `a|abbr|b|blockquote|br|cite|code|dd|del|div|dl|dt|em|h[1-6]|hr|i|img|ins|kbd|li|mark|ol|p|pre|q|s|small|span|strike|strong|sub|sup|table|tbody|td|th|thead|tr|u|ul|var`.
- Attributes must have `=`.

With this, `total<limit and qty>5` and `x<b and y>z` are kept, while `<b>x</b>`, `<a href="u">t</a>`, `<br/>`, `<br>`
and `<!-- c -->` are stripped.

**Rationale.** In requirement prose, `<` and `>` are far more often comparisons than tags. A known-element whitelist
plus strict attribute syntax removes the false positives. HTML entity decoding still happens after stripping.

**Tests.** New `TextNormalizerTests` rows. `total<limit and qty>5` and `…6` must give different keys; on `1ba172a` they
are equal.

## Out-of-scope observations

- Criteria extraction for docs outside the repository (`NormalizeSourceDoc` still refuses them).
- `docs changed` path reporting for a source dir outside the repository. It uses `DocumentMapBuilder` paths as today.
