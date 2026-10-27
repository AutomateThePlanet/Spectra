# Data Model: Spec 079

## Criteria index (`docs/criteria/_criteria_index.yaml`), changed

`CriteriaIndex` gains one committed field:

| Field | YAML | Type | Notes |
|---|---|---|---|
| `IdHighWaterMarks` | `id_high_water_marks` | `SortedDictionary<string,int>` (ordinal) | Per ID prefix (e.g. `AC-CHECKOUT`), the highest number ever issued. It only increases. It is written sorted so the output is deterministic. |

`CriteriaSource` (one entry per source):

| Field | Change |
|---|---|
| `source_doc` | Always normalised: repository-relative, `/` separators, no leading `./` or `/`. |
| `file` | Path relative to the criteria dir. For document sources it is the derived per-doc path (below); for imports it stays `imported/{name}.criteria.yaml`. |
| `doc_hash` | Unchanged meaning. It is the move-detection key. |
| others | Unchanged. |

**Layout detection.** A workspace is in the **old layout** when any `source_type: document` entry has a `file` that differs
from its derived path.

## Derived criteria file path (per document)

```text
CriteriaPath(doc) = {criteria_dir}/{repoRelative(doc) with a trailing ".md" removed}.criteria.yaml
```

| Doc | Criteria file (criteria dir `docs/criteria`) | Index `file` |
|---|---|---|
| `docs/login.md` | `docs/criteria/docs/login.criteria.yaml` | `docs/login.criteria.yaml` |
| `docs/a/login.md` | `docs/criteria/docs/a/login.criteria.yaml` | `docs/a/login.criteria.yaml` |
| `docs/b/login.md` | `docs/criteria/docs/b/login.criteria.yaml` | `docs/b/login.criteria.yaml` |
| `specs/x.txt` | `docs/criteria/specs/x.txt.criteria.yaml` | `specs/x.txt.criteria.yaml` |

- The mapping is injective over repository paths. Only `.md` is stripped, and a doc can't be both `x.md` and `x`, since
  that would be one file.
- The only names that aren't derived are imports under `imported/`. Before writing, a derived path that another index
  source already uses fails loud with `CRITERIA_FILE_COLLISION`. That can only happen for a repo-root folder named
  `imported/`.

## Source-doc normalisation

```text
NormalizeSourceDoc(input, repoRoot):
  1. resolve to a full path (relative to repoRoot when not rooted)
  2. must lie under repoRoot, else refuse (INVALID_DOC_PATH)
  3. repo-relative, '\' → '/', no leading './' or '/'
```

The same function is used by ingest, compile-extraction-prompt, `docs changed`, retire, apply-move and the migration.

## ID allocation

```text
ID     := PREFIX "-" NUMBER            e.g. AC-CHECKOUT-013, AC-042 (import without key)
PREFIX := everything before the last "-" ; NUMBER := trailing digits (parsed as int)
Allocate(prefix, count):
  effective = max(index.id_high_water_marks[prefix] ?? 0,
                  max NUMBER of every ID with that prefix found in any criteria file under the criteria dir)
  issue effective+1 .. effective+count  (formatted D3, wider if needed)
  index.id_high_water_marks[prefix] = effective+count
```

It always runs inside the criteria lock (below), and the new high-water mark is persisted in the same index write as the
source upsert.

## Criteria lock

- **Lock file**: `.spectra/criteria.lock`, acquired with `FileLockHandle` (the existing cross-process lock, 10 s timeout).
  It is gitignored.
- **What runs under it**: every read-modify-write of `_criteria_index.yaml`, criteria files, and the high-water marks.
  That covers ingest, import, retire, apply-move and migrate.
- **Temp files**: `{target}.{guid}.tmp`, then move.

## Text normaliser (`Spectra.Core.Text.TextNormalizer`)

`Normalize(string) → string` returns a **comparison key**, not display text. Steps, in order, all culture-invariant:

1. Decode HTML entities.
2. Strip HTML tags.
3. Images: `![alt](url)` → `alt`. Links: `[text](url)` and `[text][ref]` → `text`. Autolinks: `<https://…>` → the URL.
4. Inline code: `` `x` `` → `x`.
5. Emphasis and strike: `**x**`, `__x__`, `*x*`, `_x_`, `~~x~~` → `x`. Markers count only when they are not inside a word,
   so `snake_case_name` and `2 * 3` are untouched.
6. Collapse all whitespace runs to one space, trim, and lowercase (invariant).

## Criterion matching (ingest)

Input: the parsed response items (each may carry `id`) and the doc's **previous** criteria (P).

1. **Validate explicit IDs.** Every `id` must be in P, and must not repeat within the response. Any violation →
   `INVALID_CRITERION_ID`: nothing is written, and the offending IDs are listed.
2. **Claim explicit IDs.** Each item with a valid `id` claims that previous criterion.
3. **Text fallback.** Each item without an `id`, in response order, claims the first unclaimed previous criterion with an
   equal normalised text.
4. **Allocate.** Items still unmatched get new IDs from the allocator, with prefix `AC-{COMPONENT}`.
5. **Kept text.** For a kept ID with equal normalised text, the stored text is the previous text, verbatim.

## Changeset

```text
CriteriaChangeset {
  doc: string                                   // normalised source_doc
  added:    [ { id, text } ]
  modified: [ { id, old_text, new_text } ]      // normalised text differs
  removed:  [ { id, old_text, referencing_tests: int, test_ids: [string] } ]
  metadata: [ { id, field, old, new } ]         // field ∈ priority | rfc2119 | source_section | tags | technique_hint
  is_empty: bool
}
```

- **Ordering**: each list is sorted by ID (ordinal); `metadata` is sorted by ID, then by field order.
- **`referencing_tests`**: counted from every `{tests_dir}/*/_index.json` entry whose `criteria` contains the ID
  (case-insensitive).
- **No-op rule**: an empty changeset with the same `doc_hash` → nothing is written. An empty changeset with a new
  `doc_hash` → only the index entry's `doc_hash` / `last_extracted` / `source_doc` are updated, and the criteria file is not
  rewritten.

## Doc change entry (`docs changed`)

```text
{ path, component, status, current_hash, indexed_hash, from? , criteria_count? }
status ∈ new | changed | unchanged | deleted | moved
```

- **deleted**: `path` is the index `source_doc` whose file is gone. Only `source_type: document` entries qualify.
  `current_hash` is null.
- **moved**: `path` is the new location and `from` the old one. Reported only when exactly one `new` doc and exactly one
  `deleted` source share the hash.
- **Result-level additions**: `duplicate_ids: [{ id, files: [..] }]`, `migration_needed: bool`, and `warnings: [string]`.

## State: document criteria source lifecycle

```text
(absent) --ingest--> indexed --doc edited + ingest--> indexed (changeset)
indexed --file deleted--> [reported deleted] --retire--> (absent; criteria reported removed)
indexed --file moved unchanged--> [reported moved] --apply-move--> indexed (new source_doc/file, same IDs)
old-layout --migrate (explicit or before a write)--> new-layout
```
