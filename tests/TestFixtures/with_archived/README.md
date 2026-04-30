# Fixture: with_archived

**Purpose**: Drives `ExclusionPatternMatcherTests`, `IncludeArchivedFlagTests`, and the various `*_SkipsArchivedSuitesTests` (Phase 5 of branch 045 — exclusion patterns + frontmatter overrides).

**Spec**: see `specs/045-doc-index-restructure/spec.md` §5.1 ("small project containing `Old/`, `legacy/`, `archive/`, `release-notes/` directories").

## Shape

A small synthetic project with documents distributed across the four default-excluded directories plus an active suite for contrast:

- `active/foo.md` — should be analyzed.
- `Old/old1.md`, `Old/old2.md` — matched by `**/Old/**`.
- `legacy/x.md` — matched by `**/legacy/**`.
- `archive/y.md` — matched by `**/archive/**`.
- `release-notes/3.0.md`, `release-notes/2.0.md` — matched by `**/release-notes/**`.

## Generation

Like `large_single_suite/`, the actual content is built on demand by the test helper `tests/Spectra.CLI.Tests/TestHelpers/ArchivedFixtureBuilder.cs` (added in Phase 5 task T065). The helper writes a temp directory and yields its root path to the test.

Phase 1+2 work does not exercise this fixture.
