# Contract — `_checksums.json` schema (v2)

**Status**: Frozen for Spec 040 / branch 045.

## File location

`<source.doc_index_dir>/_checksums.json` — default `docs/_index/_checksums.json`.

## Encoding

UTF-8 without BOM. JSON pretty-printed with 2-space indent for readability in Git diffs.

## Shape

```json
{
  "version": 2,
  "generated_at": "2026-04-29T15:00:00Z",
  "checksums": {
    "docs/checkout.md": "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2",
    "docs/requirements/gitbook-repo/docs/SM_GSG_Topics/manage-items/standard-items/setting-up-standard-items.md": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
  }
}
```

## Field rules

- `version` (int, required) — must be `2`.
- `generated_at` (RFC3339 string, required) — UTC, second precision.
- `checksums` (object, required) — keys are repo-relative paths with forward slashes; values are 64-character lowercase hex SHA-256 digests.

## Hash algorithm

SHA-256 over the document's raw byte content. Implementation is the existing `DocumentIndexExtractor.ComputeHash(string content)` in `Spectra.Core/Parsing/`.

## Reader behavior

- Tolerates absent file (returns `null`/empty store).
- Rejects non-hex or wrong-length digests with a clear error.
- Treats backslashes in keys as a parse error (programming bug indicator).

## Writer behavior

- Writes to a temporary file then atomically renames.
- Sorts `checksums` keys alphabetically (ordinal) for deterministic Git diffs.
- Pretty-prints with `JsonSerializerOptions.WriteIndented = true`.

## Privacy invariant

This file MUST NEVER be sent as part of an AI prompt. The reader/writer both live in `Spectra.Core` and have no callers in `Spectra.CLI/Agent/Copilot/`. A unit test asserts no AI-prompt builder references `ChecksumStoreReader`.

## Cross-file consistency

- For every key in `checksums`, the document path must be referenced by exactly one suite index file (validated by `DocsIndexHandler` post-write).
- Counts in the manifest's per-group `document_count` must sum to `Count(checksums)`.
