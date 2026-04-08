# Research: Criteria Folder Rename, Index Exclusion & Coverage Fix

## Decision 1: Folder Rename Strategy

**Decision**: Change default paths in `CoverageConfig.cs` and add one-time auto-migration in `AnalyzeHandler.cs`.

**Rationale**: Most code already reads paths from config, so changing defaults propagates automatically. Only 4 files have hardcoded "docs/requirements" strings. Auto-migration ensures existing users don't lose data.

**Alternatives considered**:
- Manual migration (user runs a command) — rejected because it adds friction and users won't know to do it
- Symlink from old to new — rejected because Windows symlinks require elevated permissions

## Decision 2: Index Exclusion Placement

**Decision**: Add `ShouldSkipDocument()` filter in the document enumeration loop within `AnalyzeHandler.cs`, before documents are passed to `CriteriaExtractor`.

**Rationale**: `CriteriaExtractor` is a stateless service that processes individual documents. The filtering responsibility belongs to the orchestrator (handler) that decides which documents to process.

**Alternatives considered**:
- Filter inside `CriteriaExtractor` — rejected because the extractor should remain a pure document processor
- Filter in `DocumentIndexService` — rejected because the index service has a different purpose (building `_index.md`)

## Decision 3: Coverage Fix Approach

**Decision**: Fix the `AcceptanceCriteriaCoverageAnalyzer` to properly enumerate per-document `.criteria.yaml` files and match against test frontmatter `criteria:` fields. Fix `DataCollector` to call the analyzer with correct parameters.

**Rationale**: The analyzer has the right structure but likely has a path resolution issue or isn't being called with the directory-based entry point. The `DataCollector` may be calling `AnalyzeAsync(filePath)` which tries to derive the directory — if the master index doesn't exist, it may return empty results.

**Alternatives considered**:
- Restructure to use only master index — rejected because per-document files are the source of truth
- Add a new analyzer class — rejected because the existing one just needs fixing (YAGNI)

## Decision 4: Dashboard Unit Label

**Decision**: Change the unit parameter from `'acceptance_criteria'` to `'criteria'` in `app.js` line 927.

**Rationale**: The current display reads "X of Y acceptance_criteria covered" which is grammatically incorrect. The section header already says "Acceptance Criteria Coverage", so the detail text should read "X of Y criteria covered".
