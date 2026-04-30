# Contract — `groups/{suite}.index.md` file format (v2)

**Status**: Frozen for Spec 040 / branch 045.

## File location

`<source.doc_index_dir>/groups/<suite-id>.index.md` — default `docs/_index/groups/SM_GSG_Topics.index.md`.

## Encoding

UTF-8 without BOM. Unix line endings (`\n`). The reader accepts `\r\n`.

## Structure

```markdown
# SM_GSG_Topics

> Group: SM_GSG_Topics | 145 documents | ~10,877 tokens
> Last indexed: 2026-04-29T15:00:00Z

---

### docs/requirements/gitbook-repo/docs/SM_GSG_Topics/manage-items/standard-items/setting-up-standard-items.md
- **Title:** Setting up standard items
- **Size:** 4 KB | **Words:** 312 | **Tokens:** ~406
- **Last Modified:** 2026-04-15
- **Key Entities:** Standard Item, Item Wizard, Pricing

| Section | Summary |
|---------|---------|
| Overview | Standard items are the most common type of item in the catalog. |
| Procedure | To set up a standard item, open Items > New Item and ... |

---

### docs/requirements/gitbook-repo/docs/SM_GSG_Topics/manage-items/serialized-items/setting-up-serialized-items.md
- **Title:** Setting up serialized items
- **Size:** 5 KB | **Words:** 401 | **Tokens:** ~520
- **Last Modified:** 2026-04-12
...
```

## Header

The first heading uses the suite ID as-is. The blockquote that follows MUST contain:
- `Group: <id>`
- `<doc_count> documents`
- `~<tokens_estimated> tokens`
- `Last indexed: <RFC3339>`

separated by ` | ` and `\n>` for the second line. Format is identical to today's `# Documentation Index` header but scoped to one suite.

## Per-document entries

Format is identical to today's per-doc entry in `_index.md`:

- `### <repo-relative path>` (forward slashes).
- `- **Title:** <title>` — required.
- `- **Size:** <KB> KB | **Words:** <count> | **Tokens:** ~<estimate>` — required, all integers.
- `- **Last Modified:** YYYY-MM-DD` — required.
- `- **Key Entities:** <comma-separated list>` — optional, omitted when empty.
- A `| Section | Summary |` table — optional, omitted when no headings.

Entries are separated by a `---` line on its own. The file ends after the last entry — there is **no checksum block** (checksums live in `_checksums.json`).

## Reader behavior

Reuses today's `DocumentIndexReader.ParseFull(string)` parser logic, but ignores the legacy checksum block (it is never present in v2). The reader's regex set is shared between legacy and new — no duplication.

## Writer behavior

- Writes to a temporary file then atomically renames.
- Entries sorted by `Path` (ordinal).
- Generated header timestamp matches the manifest's `generated_at` for the same write batch — within a single `spectra docs index` run, all rewritten files share one timestamp.

## Cross-file consistency

For every entry path in the suite index file:
- The path appears as a key in `_checksums.json`.
- The path is associated with exactly one suite (this one) via `SuiteResolver`.
- The path's containing relative-directory matches the suite's `path` prefix in the manifest.
