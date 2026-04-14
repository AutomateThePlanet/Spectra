# Research: Coverage-Aware Behavior Analysis

**Date**: 2026-04-13 | **Feature**: 044-coverage-aware-analysis

## R1: How does BehaviorAnalyzer currently estimate coverage?

**Decision**: The current `CountCoveredBehaviors` method uses `DuplicateDetector.CalculateTitleSimilarity()` with a 0.6 threshold — comparing AI-generated behavior titles against existing test titles. This catches almost nothing when test titles don't exactly match behavior descriptions.

**Rationale**: Title similarity is inherently weak because test titles are author-written while behavior titles are AI-generated. Even semantically identical items rarely exceed 0.6 similarity. The new approach replaces this heuristic with structured data: criteria IDs and source_refs that have deterministic matching.

**Alternatives considered**:
- Embedding-based semantic similarity: More accurate than string matching but adds a dependency, latency, and complexity. Rejected per YAGNI.
- Sending all existing test content to the AI: Too many tokens. A suite with 231 tests could be 50k+ tokens of test content.
- Criteria-only approach (no title dedup): Misses coverage from tests that aren't linked to criteria. Keeping titles as supplementary context is low-cost.

## R2: Where to inject coverage context in the prompt template?

**Decision**: Add a new `{{coverage_context}}` placeholder in `behavior-analysis.md` between the technique instructions and the output format section. The placeholder resolves to either a full or summary coverage block depending on suite size.

**Rationale**: The placeholder system (`PlaceholderResolver`) already supports `{{#if}}` conditionals and `{{#each}}` loops. A single `{{coverage_context}}` placeholder keeps the template clean. The formatter builds the full markdown block before injection.

**Alternatives considered**:
- Multiple separate placeholders (`{{covered_criteria}}`, `{{uncovered_criteria}}`, etc.): More flexible but clutters the template with 8+ new placeholders.
- Injecting as a system message in the Copilot SDK call: Would bypass the template system and not be customizable by users.

## R3: How to read all three data sources efficiently?

**Decision**: Read all three sources concurrently in `CoverageSnapshotBuilder.BuildAsync`:
1. `_index.json` via `IndexWriter.ReadAsync()` (existing)
2. `.criteria.yaml` files via `CriteriaFileReader.ReadAsync()` + `CriteriaIndexReader.ReadAsync()` for the index (existing)
3. `docs/_index.md` via `DocumentIndexReader` (existing)

Each source is independent. If any fails or is missing, the snapshot has partial data — the formatter skips empty sections.

**Rationale**: All readers already exist in `Spectra.Core`. No new I/O code needed. Concurrent reads minimize latency.

**Alternatives considered**:
- Using the `AcceptanceCriteriaCoverageAnalyzer` (existing in Core): It does more work than needed (builds full coverage model with test linking). The snapshot builder only needs ID sets. Using the analyzer would add unnecessary coupling.
- Caching the snapshot across runs: YAGNI — snapshot build is sub-second for 500 tests.

## R4: How to handle the >500 test threshold (summary mode)?

**Decision**: When `ExistingTestCount > 500`, the formatter omits the full title list and covered criteria list. It includes:
- Coverage statistics (counts and percentages)
- Uncovered criteria (full details — always included)
- Uncovered source refs (always included)
- A note: "This suite has {N} existing tests — full title list omitted to conserve tokens"

**Rationale**: The uncovered items are the actionable part. The AI doesn't need to see 600 covered titles to know what NOT to generate — it just needs to know what's missing.

**Alternatives considered**:
- Configurable threshold via `spectra.config.json`: YAGNI. 500 is a safe default. Can be made configurable later if needed.
- Chunked title list (send in batches): Overcomplicated for marginal benefit.

## R5: How does `GenerateHandler` currently wire criteria into the analysis?

**Decision**: `GenerateHandler.LoadCriteriaContextAsync()` already loads criteria from `.criteria.yaml` files and formats them as a markdown string for the `{{acceptance_criteria}}` placeholder. The new coverage snapshot reuses the same data path but cross-references criteria IDs against test criteria fields.

**Rationale**: The existing criteria loading is relevant-filtered (by suite name, component, source doc). The snapshot builder should use the same filtering logic to ensure consistency.

**Alternatives considered**:
- Loading all criteria globally: Would be noisy for multi-suite projects. Keeping the suite-scoped filtering is correct.

## R6: How to keep BehaviorAnalyzer's signature backward-compatible?

**Decision**: Add an optional `CoverageSnapshot? snapshot = null` parameter to `AnalyzeAsync`. When null, behavior is identical to today. When provided, the prompt includes coverage context and `AlreadyCovered` is computed from the snapshot instead of title similarity.

**Rationale**: Optional parameter means all existing callers (3 call sites in GenerateHandler) continue working. Only the call sites that build a snapshot pass it in.

**Alternatives considered**:
- New method `AnalyzeWithCoverageAsync`: Duplicates the retry/timeout/error logic. Rejected.
- Overload: More code for no benefit over an optional parameter.
