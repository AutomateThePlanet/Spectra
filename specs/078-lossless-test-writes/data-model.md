# Data Model: Spec 078

## TestCase (`Spectra.Core/Models/TestCase.cs`), changed

`sealed class` becomes **`sealed record`** (research R4). Existing properties are unchanged, apart from how they are
parsed and written. The record adds one new property:

| Property | Type | Notes |
|---|---|---|
| `Layout` | `TestCaseLayout?` | Source layout captured by the parser. `null` for tests built in memory (generation). Every `with` copy carries it forward. |

These properties are now **read** by the parser (they were written but never read):

| Property | Frontmatter key | Parse rule |
|---|---|---|
| `Status` | `status` | String, as-is. |
| `OrphanedReason` | `orphaned_reason` | String, as-is. |
| `OrphanedDate` | `orphaned_date` | `yyyy-MM-dd`, invariant culture, midnight UTC. `null` if it doesn't parse; the raw text is still reproduced while unchanged. |

**Equality**

- Production code relies only on `with`.
- Round-trip tests use a test-only deep comparer, `TestCaseEquivalence`. It compares:
  - every property except `FilePath`
  - lists by sequence
  - `Custom` by deep structure
  - `Grounding` field by field
  - `Layout` by segments and raw text

## TestCaseLayout (new, `Spectra.Core/Models/TestCaseLayout.cs`)

This is an immutable record the parser produces. The writer is its only consumer.

| Field | Type | Meaning |
|---|---|---|
| `Baseline` | `TestCase` | The values the parser produced, with `Layout = null`. The writer compares against it to decide raw vs canonical. |
| `FrontmatterEntries` | `IReadOnlyList<FrontmatterEntry>` | Every top-level frontmatter entry, in original order. |
| `BodySegments` | `IReadOnlyList<BodySegment>` | Every body segment, in original order. |
| `Newline` | `string` | `"\r\n"` or `"\n"`, detected from the original file. |

`FrontmatterEntry(string Key, string RawText, bool IsKnown)`
- `RawText` is the exact slice from the key to the next top-level key, including its trailing newline.
- `IsKnown` is `true` for the keys in the Key order table below.

`BodySegment(BodySegmentKind Kind, string RawText)`
- `BodySegmentKind` is one of `Leading`, `Title`, `Intro`, `Preconditions`, `Steps`, `ExpectedResult`, `TestData`,
  `Unknown`.
- `RawText` holds the header line plus content, up to (not including) the next `##` header or the end of the file.
- Only the first section of each known kind is classified as known; later duplicates are `Unknown`.

**Validation**
- The segments concatenated in order reproduce the original body exactly.
- The frontmatter entries concatenated in order reproduce the original frontmatter text exactly.

Unit tests assert both invariants.

## GroundingMetadata (`Spectra.Core/Models/Grounding/`), changed

Adds `string? Source`, `string? CreatedBy` and `string? Note`, mapped from the existing `GroundingFrontmatter` DTO fields
`source`, `created_by` and `note`, and written back.

The manual-verdict mapping changes:
- It uses the file's `verified_at` when it parses, and `DateTimeOffset.MinValue` otherwise. It no longer uses `UtcNow`.
- It maps `flagged_for_review`, `repair_attempts`, `repaired` and `condensed_findings` like the other verdicts.

## TestCaseFrontmatter (DTO), changed

Adds `status`, `orphaned_reason` and `orphaned_date`, all strings.

## Frontmatter key order (fixed, emitted by the writer)

Known keys are emitted in this order. Existing SPECTRA-written files already use a subsequence of it, so they are not
reordered.

1. `id`
2. `priority`
3. `tags`
4. `component`
5. `description`
6. `preconditions`: only when the original had this frontmatter key and the value is unchanged. Otherwise preconditions
   live in the body.
7. `environment`
8. `depends_on`
9. `source_refs`
10. `scenario_from_doc`
11. `estimated_duration`
12. `criteria`: emitted as `criteria: []` when empty, but only for tests with no layout, or when the key was present in the
    original.
13. `requirements`
14. `related_work_items`
15. `bugs`
16. `automated_by`
17. `custom`
18. `status`
19. `orphaned_reason`
20. `orphaned_date`
21. `grounding`

After the known keys come the **unknown keys**, verbatim, in original relative order.

**Per-key rule**
- **Unchanged vs `Layout.Baseline`**:
  - If the key was present in the original, emit its `RawText`.
  - If it was absent, emit nothing.
- **Changed, or no layout**:
  - Render canonically. Empty lists and null values are omitted, except `criteria` (rule 12).

**Canonical scalar rendering**
- Rendering goes through YamlDotNet's emitter: plain style where that is safe, quoted otherwise.
- Free-text fields are always double-quoted, matching today's look: `description`, `scenario_from_doc`,
  `orphaned_reason`, `unverified_claims`, `condensed_findings.*` and `note`.
- `custom` is rendered from its object graph as block mappings and sequences, with plain scalars where safe.

## Body section order (canonical)

`# {Title}` → `## Preconditions` → `## Steps` → `## Expected Result` → `## Test Data`.

Known sections are handled per the R3 rules:
- unchanged: raw text
- changed: canonical, in place
- emptied: removed
- new: inserted after the nearest preceding known section

`Leading`, `Intro` and `Unknown` segments are verbatim, in place. Canonical `Steps` are numbered `1.`…`n.`.

## SourceRefResolver (new, `Spectra.Core/Coverage/SourceRefResolver.cs`)

```text
SourceRefResolver(string repoRoot, string sourceDir)
  SourceRefResolution Resolve(IEnumerable<string> refs)

SourceRefResolution
  IReadOnlyList<ResolvedSourceRef> Resolved   // original order
  IReadOnlyList<MissingSourceRef>  Missing    // original order
  IReadOnlyList<string> DistinctFullPaths     // Resolved, deduplicated by full path (see research R9)
  bool AnyResolved

ResolvedSourceRef(string Ref, string RelativePath /* repo-relative, '/' */, string FullPath)
MissingSourceRef(string Ref, MissingReason Reason)
MissingReason = Empty | NotFound | OutsideRepository
```

Resolution order and rules are in research R9 and `contracts/source-ref-resolution.md`.

## UpdateResult (`Spectra.CLI/Results`), changed

Adds `applied: bool`:
- `false` for dry runs and classify-only `--no-interaction` runs
- `true` when the review or auto-review path ran the apply step

It is serialised in `.spectra-result.json` like the existing fields.

## State: orphan marking

```text
(any status) --classified ORPHANED + apply--> status: orphaned
                                               orphaned_reason: <reason>
                                               orphaned_date: <today UTC>   (kept if it was already orphaned)
```

No other field changes. The transition is `original with { Status, OrphanedReason, OrphanedDate }`.
