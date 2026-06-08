# Contract: Coverage Report Output

**Feature**: 060-coverage-doc-exclusions

Two report writers render documentation coverage and both must surface the `excluded` status:
- **Unified path** → `CoverageReportWriter` (consumes Core `DocumentationCoverage`).
- **Legacy path** → `ReportWriter` (consumes `LegacyModels.CoverageReport`).

## JSON (unified `DocumentationCoverage`)

When exclusions ARE configured:

```jsonc
{
  "total_docs": 12,            // denominator — non-excluded only
  "covered_docs": 9,
  "percentage": 75.00,
  "excluded_docs": 3,          // NEW — count dropped from denominator
  "details": [
    { "doc": "docs/login.md", "test_count": 4, "covered": true },
    { "doc": "docs/release-notes/v1.md", "test_count": 0, "covered": false,
      "excluded": true, "excluded_by_pattern": "docs/release-notes/**" }   // NEW fields
  ]
}
```

When NO exclusions are configured (FR-005 — byte-for-byte unchanged):

```jsonc
{
  "total_docs": 15,
  "covered_docs": 9,
  "percentage": 60.00,
  "details": [
    { "doc": "docs/login.md", "test_count": 4, "covered": true }
    // no "excluded"/"excluded_by_pattern" keys (omitted via WhenWritingDefault)
    // no "excluded_docs" key (default 0, omitted)
  ]
}
```

**Rule**: `excluded`, `excluded_by_pattern`, and `excluded_docs` are serialized only when non-default,
so unconfigured output is identical to the current schema.

## Human-readable (markdown — `CoverageReportWriter`)

Current table has columns `| Document | Tests | Covered |` with `Covered ∈ {Yes, No}`.

- Excluded docs render a distinct third status in the `Covered` column (e.g. `Excluded`), not `Yes`/`No`.
- The summary line gains an excluded count when `excluded_docs > 0`, e.g.
  `**75.0%** — 9 of 12 documents covered (3 excluded)`.
- When `excluded_docs == 0`, summary and table are unchanged.

## Human-readable (terminal — `CoverageReportWriter` compact)

Current marks: `+` covered, `-` uncovered. Add a distinct mark for excluded (e.g. `~`) so excluded docs
are visibly distinct from uncovered. Only appears when docs are excluded.

## Legacy report (`ReportWriter` / `LegacyModels.CoverageReport`)

- `TotalDocuments` denominator excludes excluded docs; `excluded_documents` count added.
- Excluded docs are NOT listed under uncovered and do NOT generate a `CoverageGap`.
- Unchanged when no patterns configured.

## Status precedence

`excluded` overrides `covered`/uncovered. A doc that matches an exclude pattern **and** has linked tests
is reported `excluded` (and still listed with its `test_count`).
