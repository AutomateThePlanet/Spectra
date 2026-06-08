# Contract: Configuration Schema

**Feature**: 060-coverage-doc-exclusions

## New key

```jsonc
{
  "coverage": {
    // ... existing keys (criteria_file, criteria_dir, analysis_exclude_patterns, ...) ...

    // NEW — coverage-scoped exclusion. Documents matching any of these globs are
    // dropped from the documentation-coverage DENOMINATOR only. They remain fully
    // present in the document map for generation, analysis, and indexing.
    // Empty/absent = no exclusions (output identical to today).
    "coverage_exclude_patterns": [
      "docs/release-notes/**",
      "**/SUMMARY.md"
    ]
  }
}
```

## Semantics

- **Type**: array of strings (globs). Repo-relative, forward-slash, full glob (`**`, `*`, brace).
- **Default**: `[]` (empty). No implicit/default patterns — explicit opt-in only.
- **Matcher**: the existing `ExclusionPatternMatcher` (FileSystemGlobbing), relocated to `Spectra.Core`.
- **Blank entries**: ignored.

## The three exclusion mechanisms (disambiguation — must appear in docs, FR-008)

| Config key | Scope of effect | What it does | Default |
|------------|-----------------|--------------|---------|
| `source.exclude_patterns` | **Everything** | Total removal at discovery — doc never enters the document map; invisible to generation, analysis, indexing, **and** coverage. | `["**/CHANGELOG.md"]` |
| `coverage.analysis_exclude_patterns` | **Index/analysis** | Doc is still indexed, but its suite is flagged `skip_analysis: true`. Consumed only by the index/migration path. **Does NOT affect the coverage percentage.** | `["**/Old/**", "**/old/**", "**/legacy/**", "**/archive/**", "**/release-notes/**", "**/CHANGELOG*", "**/SUMMARY.md"]` |
| `coverage.coverage_exclude_patterns` *(new)* | **Coverage % only** | Doc dropped from the documentation-coverage denominator and reported with `excluded` status. Remains in the map for generation/analysis/indexing. | `[]` |

**Independence (FR-006)**: A doc matched by `analysis_exclude_patterns` but not by
`coverage_exclude_patterns` **still counts** in the coverage denominator. The three lists are evaluated
independently.

## Backward compatibility

- Absent key → empty list. No deserialization error.
- Existing configs unchanged → coverage output unchanged (FR-005).
