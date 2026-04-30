# Phase 1 Data Model — Document Index Restructure

**Branch**: `045-doc-index-restructure`
**Date**: 2026-04-29

This document defines the C# models, validation rules, and state transitions for the new document index layout. It complements `contracts/` (file-format specs) and `quickstart.md` (developer walkthrough).

## Entities

### `DocIndexManifest` (Spectra.Core/Models/Index/)

Top-level record of the documentation index. The only artifact always loaded into AI prompts.

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `Version` | `int` | yes | `2` | Schema version. v1 = legacy single-file. v2 = this spec. |
| `GeneratedAt` | `DateTimeOffset` | yes | — | UTC timestamp of last write. |
| `TotalDocuments` | `int` | yes | — | Sum of all `DocSuiteEntry.DocumentCount`. |
| `TotalWords` | `int` | yes | — | Sum across all suites. |
| `TotalTokensEstimated` | `int` | yes | — | Sum across all suites. |
| `Groups` | `IReadOnlyList<DocSuiteEntry>` | yes | `[]` | Sorted by `Id` (ordinal). |

**Validation**:
- `Version == 2`. Reading a manifest with a different version throws `SpectraException("Unsupported manifest version")`.
- `Groups` MUST have unique `Id` values. Duplicate IDs throw on write.
- `TotalDocuments == Groups.Sum(g => g.DocumentCount)`. Enforced post-construction; mismatch throws on write (caller bug).

**YAML mapping** (via YamlDotNet `[YamlMember]` attributes):

```
version: 2
generated_at: 2026-04-29T15:00:00Z
total_documents: 541
total_words: 158516
total_tokens_estimated: 205825
groups:
  - id: SM_GSG_Topics
    ...
```

### `DocSuiteEntry` (Spectra.Core/Models/Index/)

One suite within the manifest.

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `Id` | `string` | yes | — | Stable suite identifier. Sanitized; no slashes/spaces. |
| `Title` | `string` | yes | — | Human-readable label. Derived by replacing `_`/`-` with spaces and title-casing if not overridden. |
| `Path` | `string` | yes | — | Repo-relative path with forward slashes (e.g., `docs/requirements/gitbook-repo/docs/SM_GSG_Topics`). |
| `DocumentCount` | `int` | yes | — | Number of docs assigned to this suite. |
| `TokensEstimated` | `int` | yes | — | Sum of per-doc estimates. |
| `SkipAnalysis` | `bool` | yes | `false` | If true, AI analyzers exclude this suite by default. |
| `ExcludedBy` | `string` | yes | `"none"` | One of `pattern`, `config`, `frontmatter`, `none`. |
| `ExcludedPattern` | `string?` | no | `null` | When `ExcludedBy == "pattern"`, the matched glob. |
| `IndexFile` | `string` | yes | — | Relative path within `_index/` (e.g., `groups/SM_GSG_Topics.index.md`). |
| `SpilloverFiles` | `IReadOnlyList<string>?` | no | `null` | When non-empty, lists the source-document repo-relative paths that have per-doc spillover index files. Phase 3 only. |

**Validation**:
- `Id` matches regex `^[A-Za-z0-9._-]+$` and does not start with `.` or `-`.
- `IndexFile` MUST start with `groups/` and end with `.index.md`.
- `ExcludedBy == "pattern"` ⇒ `ExcludedPattern != null`.
- `ExcludedBy != "none"` ⇒ `SkipAnalysis == true`.
- `SpilloverFiles`, when present, has at least one entry.

**State transitions**:
- A suite can transition from `SkipAnalysis: false` to `true` (and back) across runs as patterns/frontmatter change. The transition is captured in the JSON result's per-suite block on the run that flips it.

### `ChecksumStore` (Spectra.Core/Models/Index/)

Hash table keyed by document path. Never sent to AI prompts.

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `Version` | `int` | yes | `2` | Bumps with manifest version. |
| `GeneratedAt` | `DateTimeOffset` | yes | — | UTC. |
| `Checksums` | `Dictionary<string, string>` | yes | `{}` | Key: forward-slash repo-relative path. Value: hex-encoded SHA-256 (matches existing `DocumentIndexExtractor.ComputeHash`). |

**Validation**:
- All checksum values match `^[a-f0-9]{64}$`.
- All keys use forward slashes; any backslashes throw on write (programming error).
- The set of keys equals the set of all document paths across all suite index files (cross-checked at write time).

### `SuiteIndexFile` (Spectra.Core/Models/Index/)

In-memory model of one `groups/{id}.index.md` file. Same shape as today's `DocumentIndex` but scoped to one suite, no checksum block.

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `SuiteId` | `string` | yes | — | Matches `DocSuiteEntry.Id`. |
| `GeneratedAt` | `DateTimeOffset` | yes | — | UTC; per-suite timestamp. |
| `DocumentCount` | `int` | yes | — | Reuses count from manifest. |
| `TokensEstimated` | `int` | yes | — | Reuses from manifest. |
| `Entries` | `IReadOnlyList<DocumentIndexEntry>` | yes | `[]` | Reuses existing `DocumentIndexEntry` model verbatim. Sorted by `Path` (ordinal). |

**Validation**:
- `SuiteId` is non-empty and matches `DocSuiteEntry.Id` regex.
- `Entries[i].Path` is unique across the file.
- `DocumentCount == Entries.Count`. Enforced on write.

### `MigrationRecord` (Spectra.CLI/Index/)

Returned by `LegacyIndexMigrator.MigrateAsync`. Surfaced in the JSON result.

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `Performed` | `bool` | yes | — | False when no migration was needed; true on success. |
| `LegacyFile` | `string?` | no | `null` | Repo-relative path to `_index.md.bak` after success; null when nothing was migrated. |
| `SuitesCreated` | `int` | yes | `0` | Count of suite index files written during migration. |
| `DocumentsMigrated` | `int` | yes | `0` | Count of doc entries that survived migration. |
| `LargestSuiteId` | `string?` | no | `null` | Suite with the highest `TokensEstimated`. |
| `LargestSuiteTokens` | `int` | yes | `0` | The token count for that suite. |
| `Warnings` | `IReadOnlyList<string>` | yes | `[]` | E.g., "12 entries had no checksum and were re-hashed from disk." |

### `PreFlightTokenError` (Spectra.CLI/Index/)

A typed error model embedded in `SpectraException` when the pre-flight check fails.

| Field | Type | Required | Notes |
|---|---|---|---|
| `BudgetTokens` | `int` | yes | Configured `ai.analysis.max_prompt_tokens`. |
| `EstimatedTokens` | `int` | yes | What the prompt would have cost. |
| `OverflowingSuites` | `IReadOnlyList<(string Id, int Tokens)>` | yes | Sorted by tokens descending. |
| `SuggestedFlags` | `IReadOnlyList<string>` | yes | E.g., `["--suite SM_GSG_Topics", "--analyze-only"]`. |

The exception's `Message` is composed deterministically from these fields so it tests cleanly.

### Configuration extensions

#### `SourceConfig` (Spectra.Core/Models/Config/)

| New field | Type | Default | Notes |
|---|---|---|---|
| `DocIndexDir` | `string` | `"docs/_index"` | Replaces `DocIndex` (legacy alias kept; see R-008). |
| `GroupOverrides` | `Dictionary<string, string>` | `{}` | Keys = repo-relative document paths; values = suite IDs. |

#### `CoverageConfig` (Spectra.Core/Models/Config/)

| New field | Type | Default | Notes |
|---|---|---|---|
| `AnalysisExcludePatterns` | `IReadOnlyList<string>` | see R-012 | Defaults: `**/Old/**`, `**/old/**`, `**/legacy/**`, `**/archive/**`, `**/release-notes/**`, `**/CHANGELOG*`, `**/SUMMARY.md`. Replace-not-merge. |
| `MaxSuiteTokens` | `int` | `80000` | Spillover trigger. Phase 3. |

#### `AnalysisConfig` (Spectra.Core/Models/Config/)

| New field | Type | Default | Notes |
|---|---|---|---|
| `MaxPromptTokens` | `int` | `96000` | Pre-flight budget. Used by `PreFlightTokenChecker`. |

## Result schema extension

`DocsIndexResult` (Spectra.CLI/Results/) gains:

| New field | Type | Notes |
|---|---|---|
| `Suites` | `IReadOnlyList<SuiteResultEntry>?` | One per suite. |
| `Manifest` | `string?` | Repo-relative path to `_manifest.yaml`. |
| `Migration` | `MigrationRecord?` | Present only when migration ran on this invocation. |

`SuiteResultEntry`:

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | |
| `DocumentCount` | `int` | |
| `TokensEstimated` | `int` | |
| `SkipAnalysis` | `bool` | |
| `IndexFile` | `string` | Repo-relative. |

## Suite assignment algorithm (`SuiteResolver`)

Inputs:
- A list of discovered documents (relative path beneath `local_dir`, frontmatter dictionary).
- `SourceConfig.GroupOverrides`.

Algorithm (per document):
1. If frontmatter contains `suite: <id>` and `<id>` is valid (R-004 / FR-008): suite = `<id>`. Stop.
2. If `GroupOverrides[relative_path]` exists: suite = that value. Stop.
3. Else apply directory-based default (R-009): walk the relative path's directories from outermost to innermost; pick the first directory that contains more than one document; sanitize separators in the joined prefix. Stop.
4. Else: suite = `_root`.

Outputs:
- `Dictionary<string, string>` mapping document relative path → suite ID.
- A list of validation errors (frontmatter rejections from step 1) — surfaced as a `SpectraException` aggregating all offenders.

The algorithm is deterministic and runs entirely in memory after document discovery.

## Migration state machine

```
[start]
  └── needs_migration? (legacy file present, new layout absent)
        ├── no  ──► [normal incremental flow]
        └── yes
              └── --no-migrate?
                    ├── yes ──► [error: legacy file requires migration]
                    └── no
                          └── parse legacy file
                                ├── parse failure ──► [error: legacy file unreadable; original untouched]
                                └── ok
                                      └── group entries by suite (via SuiteResolver)
                                            └── apply default exclusion patterns
                                                  └── write _index.tmp/ (manifest + checksums + groups/)
                                                        ├── any write fails ──► [delete _index.tmp/; error]
                                                        └── all writes ok
                                                              └── rename _index.tmp/ → _index/
                                                                    └── rename _index.md → _index.md.bak
                                                                          └── [normal incremental flow with migration record]
```

All error transitions leave the legacy `_index.md` byte-identical to its pre-run state.
