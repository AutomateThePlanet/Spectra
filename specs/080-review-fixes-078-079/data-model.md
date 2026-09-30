# Data Model: Spec 080 (changes only)

## TestCaseLayout — frontmatter shape (A4)

| Field | Change |
|---|---|
| `FrontmatterLeading` (new, `string`) | Text before the first top-level key: a file-header comment and blank lines. Always emitted first. |
| `FrontmatterTrailing` (new, `string`) | Blank lines and column-0 comments after the last key's value. Always emitted last. |
| `FrontmatterEntries` | Each entry is now `FrontmatterEntry(Key, Prelude, RawText, IsKnown)`. `Prelude` is the blank lines and column-0 `#` lines directly above the key; `RawText` is the key line and its value only. |

**Invariant.** `Leading + Σ(entry.Prelude + entry.RawText) + Trailing == raw frontmatter text`.

**Writer rule, per emitted entry.** Emit `Prelude`, then either `RawText` (unchanged value) or the canonical rendering
(changed value). A key that is omitted after a change drops its `Prelude`.

## TestCaseFrontmatterKeys (new, A3)

`Spectra.Core.Models.TestCaseFrontmatterKeys.Ordered : IReadOnlyList<string>` holds the 21 keys in write order. It is
the only source for `TestCaseParser.KnownFrontmatterKeys` and `TestFileWriter.KeyOrder`. It must equal, as a set, the
`[YamlMember]` aliases of `TestCaseFrontmatter` (enforced by a test).

## ConfigLoader entry points (A5)

```text
ConfigLoadResult(SpectraConfig? Config, string? Error)
public static LoadForReadAsync(repoRoot)   missing → Config = SpectraConfig.Default
public static LoadForWriteAsync(repoRoot)  missing → Error = MissingConfigMessage
both                                       malformed / invalid → Error = InvalidConfigPrefix + first error
                                           (InvalidConfigPrefix = "spectra.config.json is invalid: ")
```

The entry points are `public static` on the existing `sealed class ConfigLoader` and construct a loader
internally, so call sites need no instance.

## SourceRefResolver (A2)

- **Allowed roots:** the repository root and the configured source directory. A candidate resolves only to an
  existing file under one of them.
- **`MissingReason`:** `Empty | NotFound | OutsideAllowedRoots`. `OutsideRepository` is renamed to
  `OutsideAllowedRoots`.

## ExtractionOutcome (B6)

`Extracted | EmptyResponse | ParseFailure | Rejected`. The new value `Rejected` means the response was classified but
refused before anything was persisted. `CriteriaIngestResult.ErrorCode` says why.

## CriteriaProvenance (new, B1–B3)

```text
IsShared(index, source)  : another document source has the same lexical `file`
OwnedBy(criteria, doc)   : criteria where CriteriaPaths.NormalizeLexical(source_doc) == doc (OrdinalIgnoreCase);
                           null source_doc → owned by none. `doc` is already a normalised repo-relative path.
```

`CriteriaWorkspace.LeaveSharedFileAsync(file, doc)` removes `OwnedBy(doc)` from the file. It then deletes the file if
it is empty and unreferenced; otherwise it rewrites the file if anything was removed.

`NormalizeLexical` is the rule the existing filters already use (`CriteriaIngestor`, `DocsRetireCommand`), so
moving them onto `OwnedBy` does not change matching.

## CriteriaChangeset tags key (B4)

`tags` comparison key = the tags trimmed, de-duplicated case-insensitively, sorted case-insensitively, lower-cased and
joined with `", "`. Reported `old`/`new` values keep the original display text.

## Criteria file scan (B8)

`CriteriaStore.ScanFilesAsync(criteriaDir)` returns `(File, Criteria)` for every `*.criteria.yaml` under the criteria
dir: recursive, ordinal order, lenient read. It is the only criteria-file scan, used by `CriteriaStore.LoadAsync` and
`docs changed`.
