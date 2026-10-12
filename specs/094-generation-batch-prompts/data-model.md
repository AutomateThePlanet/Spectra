# Data Model: Section-Scoped Generation Batches (Phase 5c)

## Generation plan — `.spectra/generation-plan/<suite>.json`

Scratch, gitignored. Written atomically by `ingest-analysis` on success; replaced by the next successful analysis of the
suite. UTF-8 without BOM, LF, 2-space indent, keys in this order, no timestamps — identical inputs give identical bytes.

```json
{
  "version": 1,
  "suite": "storage",
  "focus": null,
  "batch_size": 8,
  "recommended": 11,
  "planned_total": 11,
  "count_rule": "proportional",
  "behaviors": [
    {"id": "B1", "title": "Upload is refused above the 30 GB quota", "category": "boundary", "technique": "BVA",
     "source": "docs/storage.md#Upload quota", "ref": "docs/storage.md#upload-quota", "resolved": true},
    {"id": "B7", "title": "Audit log keeps 90 days", "category": "happy_path", "technique": "EP",
     "source": "docs/audit.md", "ref": null, "resolved": false, "reason": "not found"}
  ],
  "sections": {
    "docs/storage.md#upload-quota": "sha256:…",
    "docs/storage.md#quota-settings": "sha256:…"
  },
  "batches": [
    {"batch_id": "1", "behavior_ids": ["B1", "B2"], "source_refs": ["docs/storage.md#upload-quota", "docs/storage.md#quota-settings"], "count": 3},
    {"batch_id": "unsourced", "behavior_ids": ["B7"], "source_refs": [], "count": 1}
  ],
  "notes": []
}
```

| Field | Rule |
|---|---|
| `version` | `1`; any other value → `PLAN_MISSING` ("unreadable plan") on load |
| `focus` | the `--focus` given to the ingest, or null |
| `batch_size` | the effective `generation.batch_size` used |
| `recommended` | as in the ingest result (behaviours − already covered, ≥ 0) |
| `planned_total` | Σ batch counts |
| `count_rule` | `proportional` (Σ = recommended) or `one_per_batch` (recommended < number of batches) |
| `behaviors[]` | input order after the focus filter; `id` = `B<n>`, 1-based; `source` verbatim (may be `""`); `ref` = canonical section ref or null; `reason` only when `resolved` is false: `empty ref`, `not found`, `outside allowed roots`, `unreadable`, `anchor not found` |
| `sections` | canonical ref → `SourceFingerprint.OfSection(raw)`, sorted by key (ordinal) |
| `batches[]` | numbered batches `1…n` in document/section order, then `unsourced` (only when there are unresolved behaviours); `count ≥ 1` |
| `notes` | e.g. `docs/storage.md#limits: more than one heading shares the anchor key 'limits'; the first was used` |

Strict load: invalid JSON, a missing key or a wrong type → the plan is unreadable (`PLAN_MISSING`, message "the plan …
is unreadable: …; re-run the analysis").

## Canonical section ref

```
whole document := <repo-relative path, "/" separators>
section        := <path> "#" <anchor>
anchor         := first explicit id of the heading
                | slug(CleanText) [ "-" n ]     (n-th duplicate of that slug, n ≥ 1)
slug           := lowercase; whitespace → "-"; keep letters, digits, "-", "_"
```

Invariant: `SectionExtractor.Match(headings, anchor).Index` = the heading's index. Otherwise the source as given is kept.

## Batch building

```
resolved behaviours ordered by (path ordinal, position: whole doc = -1 else heading index, input order)
group by (path, position) = section groups
for each section group g:
    if g's path ≠ current batch's path → close the batch
    if current has k behaviours and k + |g| > batch_size and k > 0 → close the batch
    while |g remaining| > 0:
        take min(batch_size − k, |g remaining|) into the current batch; close when full
unsourced batch: all unresolved behaviours, input order
```

## Count rule

```
n = number of batches, b_i = behaviours in batch i, R = recommended
R ≥ n : count_i = 1 + floor((R−n)·b_i / Σb) + (1 for the largest remainders, ties → lower i, until Σ = R)
R < n : count_i = 1                       (planned_total = n, count_rule = one_per_batch)
```

## Batch prompt (compile-prompt --batch)

| Template value | Batch mode | Count mode |
|---|---|---|
| `behaviors` | one line per behaviour: `- B3 [boundary · BVA] <title> (<ref>)` | `--focus` text or the default request (unchanged) |
| `source_sections` | per non-absorbed ref: `### <ref>` + `~~~~markdown` fence + raw text + fence | empty |
| `read_docs` | empty (`yes` in `count_fallback`) | `yes` |
| `acceptance_criteria` | the selected criteria (R6), `CriteriaContextLoader.Format` | unchanged (suite match) |
| `count` | `--count` or the batch's count | `--count` or 5 (unchanged) |
| `focus_areas` | `--focus` or empty | empty (unchanged) |
| `profile_format`, `testimize_*` | unchanged | unchanged |

A ref is absorbed when another ref of the same batch and document is a whole-document ref, or covers its heading
(`CoveredHeadings`).

### Compile mode (`--batch`)

| Project `test-generation.md` | Mode | Template used | Warning |
|---|---|---|---|
| absent, or has `{{source_sections}}` | `batch` | the loaded one (bundled when absent) | — |
| predates 094, hash = manifest record (absolute or relative key) | `batch` | bundled 094 | "using the bundled test-generation template; run spectra update-skills to refresh your copy" |
| predates 094, hash differs or no record | `count_fallback` | the user's, count mode; focus `<title> (<ref>); …` | "custom test-generation template predates batch prompts — this batch is compiled in count mode (the generator reads the docs); run spectra prompts reset test-generation to use section batches" |
| (`--batch unsourced`) | `count` | the loaded one, count mode | — |

## Criteria selection (batch)

| Criterion | Included when |
|---|---|
| `source_doc` not a batch document | never |
| no `source_section` | the batch has that document's whole-document ref |
| `source_section` matches heading h | h is covered by a batch ref of that document, or the batch has its whole-document ref |
| `source_section` matches no heading | as "no `source_section`", plus a warning |
| none selected | all criteria of the batch's documents + warning; if still none → `MISSING_INPUT` |

## Config

| Key | Type | Default | Valid | Error |
|---|---|---|---|---|
| `generation.batch_size` | int, optional | 8 | 1–50 | `CONFIG_INVALID` ("generation.batch_size must be an integer from 1 to 50") |

`"generation": null` or a missing `generation` gets the defaults silently.

## Agent lines

```
analyst  := … (093 lines unchanged) …
            ( "batch " <id> " | count " <k> " | " <refs ", "-joined> " | " <titles "; "-joined> )*   (verbatim from the CLI;
                                                                                    "|" in a title shown as "/")
            ( "unresolved: " <B-id> " " <title> " — " <reason> )*
generator input := suite: <suite>, batch: <id> [, count: <k>]
generator lines := unchanged (093): <id> " | " <title> / baseline: / warning: / failed <code>: <error> / stop:
                   (a count_fallback compile's warning is relayed as a warning: line)
```

The `unsourced` batch's line has an empty refs field: `batch unsourced | count 1 |  | Audit log keeps 90 days`.
