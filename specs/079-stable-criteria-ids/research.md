# Research: Spec 079 — Stable Acceptance-Criteria Identity

These findings come from reading the code on `079-stable-criteria-ids`, which was branched from
`fix/doc-change-update-workflow` after 078 merged. No NEEDS CLARIFICATION items remain; both clarifications were answered
on 2026-09-28.

## Confirmed facts

| # | Fact | Where |
|---|---|---|
| F1 | The `existing_criteria` placeholder is passed as `""`. The template body never references it, and the response schema has no `id`. | `ExtractionPromptCompiler.cs:83`; `Prompts/Content/criteria-extraction.md` |
| F2 | ID reuse is keyed on case-insensitive text taken only from the doc's base-name file. New IDs are `AC-{COMPONENT}-{max+1:D3}`, with `max` coming from that one file. | `CriteriaIngestor.cs:98-123` |
| F3 | The criteria file is `{criteria_dir}/{basename}.criteria.yaml`. The index `file` is that base name, and `source_doc` is stored exactly as `--doc` was typed. | `CriteriaIngestor.cs:93-95,124,143,151` |
| F4 | `ExtractedCriterion.SourceSection` has no `[JsonPropertyName]`. Case-insensitive binding doesn't map `source_section`, so the field is dropped. | `CriteriaResponseClassifier.cs:95-104` |
| F5 | `CriteriaFileReader.ReadAsync` swallows every exception and returns `[]`. | `Parsing/CriteriaFileReader.cs` |
| F6 | Both writers stamp a timestamp (`# Generated at`, `# Last updated`) and use a fixed `{path}.tmp`. There is no lock anywhere. | `CriteriaFileWriter.cs`, `CriteriaIndexWriter.cs`, `AtomicFileWriter.cs` |
| F7 | `docs changed` only walks discovered docs. It matches `SourceDoc` against the (already `/`) doc path, case-insensitively, and always exits 0. It never reports deleted docs. | `DocsChangedHandler.cs:76-140` |
| F8 | Import numbers criteria **before** merging, using `AC-{SOURCE}-{n}` or `AC-{n}` from a scan of index files. Merge matches by ID, then by `Source`. It writes `imported/{name}.criteria.yaml` and upserts the index keyed on `file`. | `AnalyzeHandler.cs:260-627`, `CriteriaMerger.cs` |
| F9 | `TestClassifier` calls `criteria.ToDictionary(c => c.Id)`, which throws on duplicates. Coverage and the dashboard double-count duplicates. | `TestClassifier.cs:51`; `AcceptanceCriteriaCoverageAnalyzer` |
| F10 | Readers either go through the index `file` entry (the coverage analyzer, `GroundedPromptBuilder`, list-criteria, `docs changed`) or scan the criteria dir **recursively** (`CriteriaContextLoader`, `CoverageSnapshotBuilder`). None assume the top level only. | R7 (reads) |
| F11 | `FileLockHandle.AcquireAsync(path, timeout)` is a reusable cross-process lock. The test-ID high-water mark is a single integer in a gitignored `.spectra/id-allocator.json`. | `Core/IdAllocation/` |
| F12 | Ingest exit codes are 0 / 1 / 5 (empty) / 6 (parse). Success JSON goes to stdout, failure JSON to stderr, and there is no human text in JSON mode. A dry run returns before IDs are assigned. | `IngestCriteriaCommand.cs` |
| F13 | `TestIndexEntry.Criteria` (`_index.json` → `criteria`) exists, which is enough to count the tests that reference an ID. | `TestIndexEntry.cs:63-66` |

## R1 — Where the committed high-water mark lives

**Decision.** Add `id_high_water_marks` (per prefix, sorted) to `_criteria_index.yaml`. It is persisted in the same locked
write as the source upsert. The effective mark is `max(stored, highest number with that prefix in any criteria file under
the criteria dir)`.

**Rationale.**
- The index is already committed and already rewritten on every ingest, so no new file is needed.
- Scanning the files covers workspaces with no stored marks yet, and hand edits.
- A fresh clone sees the marks.
- Concurrent branches that both issue IDs show up as a merge conflict on the index.

**Alternatives considered.**
- A separate committed file (`docs/criteria/_id_high_water_marks.yaml`). Rejected: a second file to keep consistent, with
  no benefit.
- The gitignored `.spectra/` store used for test IDs. Rejected: it fails "never reused after a fresh clone" (spec US2
  scenario 5).

## R2 — Criteria file layout

**Decision.** `{criteria_dir}/{repo-relative doc path, trailing .md removed}.criteria.yaml`, so `docs/a/login.md` →
`docs/criteria/docs/a/login.criteria.yaml`. The index `file` is relative to the criteria dir. Before a write, a derived path
already used by a *different* index source fails loud with `CRITERIA_FILE_COLLISION`; only a repo-root `imported/` folder
can trigger this.

**Rationale.**
- Mirroring the repo-relative path is injective, so FR-015 holds by construction, including for docs outside
  `source.local_dir`.
- Every reader tolerates subdirectories (F10).

**Alternatives considered.**
- Path relative to the source dir (`docs/criteria/a/login.criteria.yaml`). Rejected: docs outside the source dir would
  collide with docs inside it.
- A flattened encoded name (`docs__a__login`). Rejected: ambiguous when names contain `__`.
- A hash-based name. Rejected: unreadable in reviews and diffs.

## R3 — Matching algorithm and "same requirement"

**Decision.** Match in this order:
1. Validate explicit IDs strictly: each must belong to this doc's previous criteria and appear only once.
2. Claim the explicit IDs.
3. For items without an ID, fall back one-to-one on the normalised text key, in response order.
4. Allocate new IDs for the rest.

When the normalised text is unchanged, the stored text is kept verbatim. The model decides semantic sameness; the CLI does
no fuzzy matching (spec Assumption).

**Rationale.** This is deterministic, auditable and model-free (Constitution III). The one-to-one claiming fixes the
duplicate-text bug (F2).

**Alternatives considered.** Similarity thresholds (Jaccard or edit distance). Rejected: non-obvious, not model-free in
spirit, and they produce false merges.

## R4 — The text normaliser

**Decision.** `Spectra.Core.Text.TextNormalizer.Normalize` returns a lowercase comparison key (data-model.md). It uses
`WebUtility.HtmlDecode` and precompiled regexes with non-word boundaries for emphasis markers.

**Rationale.** The previous exact match was case-insensitive, and keeping that avoids reporting `must` → `MUST` as a
modification. A comparison key (not display text) is also what section hashing needs in Phase 2a.

**Alternatives considered.** Markdig rendering to plain text. Rejected: heavier, and its output drifts with renderer
versions, while hashing needs stability.

## R5 — The no-op rule and byte stability

**Decision.** An empty changeset with an unchanged `doc_hash` writes nothing. An empty changeset with a new `doc_hash` updates
only the index entry (`doc_hash`, `last_extracted`, normalised `source_doc`) and never rewrites the criteria file. When a
file *is* written, criteria keep response order. Metadata for the changeset includes `technique_hint` besides the four
fields the spec names.

**Rationale.** This keeps the criteria file byte-identical on a no-op (FR-014), whatever order the model returns.
`technique_hint` is included because otherwise a changed hint would be persisted without being reported.

**Known one-off.** Because F4 is fixed, the first re-extraction after upgrading reports `source_section` metadata changes
(null → value) on criteria that were extracted before. This is documented in the CHANGELOG.

## R7 — Migration

**Decision.** `spectra docs migrate-criteria [--dry-run]`. It runs under the criteria lock and does the following:
1. Normalises every `source_doc`.
2. Groups document sources by `file`. A group of two or more is a **collision**: report it, recommend re-extraction, leave it
   untouched.
3. Moves every other document source's file to its derived path. Files are moved, never copied, because the recursive
   scanners would otherwise double-count (F10).
4. Rewrites the `source_doc` inside the moved criteria, and rewrites the index `file`.
5. Seeds `id_high_water_marks` from a scan.

It is idempotent because a second run finds every file already at its derived path. Ingest, retire, apply-move and import
call it before their first write when layout detection says "old". `docs changed` and `compile-extraction-prompt` only warn
(clarification Q1).

**Reads in the old layout.** All read paths already resolve through the index `file` entry, or scan recursively (F10). The
only one that needs changing is `compile-extraction-prompt`: it gets existing criteria via the index entry for the
normalised `source_doc` (FR-016d).

## R8 — Deleted and moved detection

**Decision.**
- **deleted**: a `source_type: document` index entry whose normalised `source_doc` isn't on disk.
- **moved**: exactly one `new` doc and exactly one `deleted` entry share the hash, compared case-insensitively with the same
  `FileHasher` used at ingest. Ambiguous matches stay as new plus deleted.
- `docs changed` stays read-only and gains `from`, `duplicate_ids`, `migration_needed` and `warnings`.

**Apply-move** (`spectra docs apply-move --from --to [--dry-run]`) runs these checks, all refusing with exit 4:
- `from` must be indexed and absent from disk
- `to` must exist, must not be indexed, and must have a hash equal to the recorded `doc_hash`

It then moves the file to the derived path of `to`, and rewrites `source_doc` in the index, the criteria and the header.

**Retire** (`spectra docs retire --doc <path> [--dry-run]`) requires an indexed document source whose file is gone, and
refuses with exit 4 otherwise. It removes the index entry and deletes the criteria file, and reports the changeset (every
criterion removed, with referencing tests). High-water marks are never lowered.

**Rationale.** Explicit, reviewable, model-free operations; `--dry-run` everywhere (Constitution IV).

**Alternatives considered.**
- `docs changed --apply`. Rejected: it mixes a read-only listing with writes.
- Automatic retire. Rejected by the spec.

## R9 — Exit codes and output

| Command | New / changed codes |
|---|---|
| `ai ingest-criteria` | **7 = `INVALID_CRITERION_ID`** (unknown, foreign or duplicated `id`). **1** with `error_code` for `CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`, `INVALID_DOC_PATH` or `LOCK_TIMEOUT`. 0 / 5 / 6 unchanged. |
| `docs retire`, `docs apply-move` | 0 ok (including dry run), 1 error, 4 refused. |
| `docs migrate-criteria` | 0 (collisions are reported, not an error), 1 error. |
| `ai analyze --import-criteria` | Unchanged codes. It gains the auto-migration note and the high-water-mark allocation. |

**Human changeset summary** (clarification Q2): on success it goes to stderr in JSON mode and to stdout in human mode. Failure
output is unchanged. The same rule applies to retire.

## R10 — `ai update` and duplicate IDs

**Decision.**
- `TestClassifier` builds its lookup with first-wins over `GroupBy`.
- `UpdateHandler` prints a warning naming each duplicated ID after it loads criteria.
- The duplicate scan used by ingest and `docs changed` lives in one Core helper, `CriteriaDuplicateScanner`, which returns
  the ID and its files.

**Rationale.** FR-010 and FR-011, with a minimal change.

## R11 — The prompt template and customised templates

**Decision.**
- The built-in `criteria-extraction.md` gains an `{{#if existing_criteria}}` block. The block lists the existing criteria as
  a deterministic JSON array (`id`, `text`, `source_section`, in file order) and states the reuse rule.
- The response schema documents an optional `id`.
- A user override in `.spectra/prompts/` that lacks `existing_criteria` still compiles. `compile-extraction-prompt` warns
  once on stderr, and `spectra prompts reset criteria-extraction` restores the new template. Ingest's text fallback keeps
  IDs stable for unchanged text.

## R12 — Test-reference counting

**Decision.** Read every `{tests.dir}/*/_index.json`, which is read-only, and count entries whose `criteria` contain the ID
(`OrdinalIgnoreCase`), including the test IDs. A missing or unreadable index is skipped with a warning; counts are best
effort and never block.

## Out-of-scope observations

- The shared `.spectra/criteria.json` staging file makes skill extraction serial (Phase 5).
- Docs that were never ingested (empty docs) stay `new` forever.
- `CoverageSnapshotBuilder` ignores its `criteriaIndexFile` parameter and scans recursively. This is harmless after the
  migration, which moves rather than copies.
- `InitHandler` writes a sample `sample.criteria.yaml` at the top level. The migration handles it like any other entry, and
  it is left alone if it has no index entry.
