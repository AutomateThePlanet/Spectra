# Research: Plan Only Uncovered Behaviours (094 Review Fix)

All facts verified on `db20415`. No open questions.

## R1 — Where the rule lives, and keeping today's measure exactly

**Decision:** add `Spectra.Core.Validation.TitleCoverage`, next to `DuplicateDetector`:

```csharp
public static IReadOnlyList<CoverageMatch?> Match(
    IReadOnlyList<string> behaviourTitles,
    IReadOnlyList<(string Id, string Title)> tests,
    double threshold);
// CoverageMatch(string TestId, double Similarity)
```

- It gives the same similarity as `DuplicateDetector.CalculateTitleSimilarity`: the same `NormalizeText` (exposed as
  `public static NormalizeTitle`, which the private helper now calls) and `1 − distance / maxLength`.
- Covered when similarity ≥ threshold. `covered_by` = the most similar test; among equal similarities the lowest id
  (ordinal).

`AnalysisRecommendationBuilder.Build` gains an optional `double duplicateThreshold = 0.6` and computes coverage with
`TitleCoverage`:
- `AlreadyCovered` = covered count; `RecommendedCount` = uncovered count. The result exposes per-behaviour `CoveredBy`.
- The `snapshot` parameter stays in the signature for its callers but no longer affects any count (FR-002).
- `CountCoveredBehaviors` (pinned by two `BehaviorAnalyzerTests` rows) delegates to the same rule at 0.6, so those
  rows stay green.

**Rationale:** one rule for counts and plan; Core holds the measure; no duplicated normalisation.

## R2 — Performance (FR-005, SC-004)

The existing `LevenshteinDistance` allocates an `(n+1)×(m+1)` matrix per call; 250,000 pairs of ~50-character titles
would be ~625 M cells plus 250,000 allocations.

**Decision:** in `TitleCoverage`:
1. Normalise every title once.
2. Exact normalised equality → 1.0.
3. Skip a pair when its length bound `min(n,m) / max(n,m)` is below the threshold, or below the behaviour's current
   best (it can't reach or beat it). Equal bounds are still computed, so ties are decided correctly.
4. Compute the distance with two reused rows, stopping as soon as a whole row exceeds the largest distance that could
   still reach `max(threshold, best)`. A stopped pair is "not covered by this test" — never a wrong similarity.

A Core row asserts that `TitleCoverage` similarities equal `CalculateTitleSimilarity` on 2,000 random pairs. A
performance row (2,500 tests × 100 behaviours of realistic, word-based titles) asserts < 2 s and writes the time to
the test output.

**Alternatives:** n-gram prefiltering — more code and a second measure to keep in sync; parallel loops —
non-deterministic tie order unless re-sorted, and unnecessary at this size.

## R3 — Where the threshold comes from (FR-004)

**Decision:**
- `ingest-analysis` passes `config.Generation.DuplicateThreshold`, loaded through the checked loader since 094.
  `"generation": null` or a missing key gives 0.6.
- No new validation: the existing config rules don't check `duplicate_threshold`. A value outside 0–1 is used as
  given and documented.

## R4 — The snapshot in the ingest (FR-002)

**Decision:** `ingest-analysis` stops building the `CoverageSnapshot` (it was built only for the count) and passes
`null`. The analysis *prompt* still gets its coverage context from `compile-analysis-prompt`, which is unchanged.

**Rationale:** same counts with and without criteria, and less work per ingest.

## R5 — The plan (FR-006, FR-010)

**Decision:**
- `PlannedBehavior` gains `CoveredBy`. The plan gains a plan-level `duplicate_threshold`, which also marks it as
  written by 095.
- Serialisation writes `duplicate_threshold` after `batch_size`, and `covered_by` (null when uncovered) after
  `reason`/`resolved` for every behaviour. Byte-identical as before.
- Parsing: both keys are optional. A plan without `duplicate_threshold` is a 094 plan, so `CoverageKnown = false`, and
  `covered_by` without a value reads as null. Version stays 1.
- `ResolvedBehavior` gains an optional trailing `CoveredBy` and `Build` an optional `duplicateThreshold`, so the 094
  builder rows compile unchanged.
- `GenerationPlanBuilder` assigns ids to all behaviours, but batches, the `unsourced` batch and the counts use only
  behaviours with `CoveredBy == null`. Sections of covered behaviours are still resolved, but are not fingerprinted
  unless an uncovered behaviour uses them; the plan's `sections` lists only refs that batches use.
- **Counts (analyze M1):** `recommended` = uncovered behaviours and each batch holds ≥ 1 of them, so `recommended` ≥
  the number of batches. *Correction found during implementation:* the 094 share rule (1 + a share of `recommended − n`)
  does **not** give each batch its own behaviour count for unequal batches (weights 4 and 1 → counts 3 and 2). So
  `GenerationPlanBuilder.Counts` gains one explicit case: when `recommended` equals the number of batched behaviours —
  always, from the ingest — each batch's count is its number of behaviours (one test per behaviour, as guidance; sum =
  `recommended`). Every 094 builder row keeps its expected counts. The `one_per_batch` branch stays in `GenerationPlanBuilder` (and its
  094 unit tests) but is unreachable from `ingest-analysis`. The skill's split of a user's explicit total is unchanged.

**Rationale:** a covered behaviour's section is never compiled, so it must not be able to make a batch stale.

## R6 — Output (FR-007, FR-009)

**Decision:**
- `ingest-analysis` JSON adds `covered: [{id, title, covered_by}]` in behaviour order.
- The human summary adds one `covered: <title> (<covered_by>)` line per covered behaviour, after the batch lines and
  before the `unresolved:` lines. The analyst copies them verbatim.
- `unresolved` (JSON and lines) lists only **uncovered** behaviours whose source didn't resolve; a covered behaviour is
  listed only under `covered`, whatever its source (analyze M2), so the analyst relays exactly one line for it.
- **Threshold warning (analyze L1):** `TitleCoverage.ThresholdWarning(double)` returns "generation.duplicate_threshold = <v> is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered." for a value
  outside (0, 1] (null otherwise); `ingest-analysis` prints it to stderr as a `Warning:` line (its JSON has no
  warnings array, so nothing is added there). No exit-code change.
- The skill's Step 4 shows them under "Already covered — not generated"; Step 9 lists them the same way.

## R7 — A 094 plan (FR-010)

**Decision:**
- When `CoverageKnown` is false, `compile-prompt --batch` (every mode) adds the warning "the generation plan predates
  per-behaviour coverage; re-run the analysis for corrected counts" and compiles as 094 did.
- The generator relays it like any warning. The skill reports it once per run and offers to re-run the analysis.

## R8 — Tests and proof

**Decision:**
- Rows: T1 `TitleCoverageTests` (Core: equality with the existing measure, threshold, ties, empty titles) and a
  performance row; T2 `AnalysisRecommendationBuilderTests` (replace the 044 row; threshold; snapshot ignored; 50 tests
  / 12 gaps); T3 `PlanCoverageIngestTests` (real init: suite tests, with and without criteria, `covered` JSON and
  lines, config threshold 0.9, uncovered-only batches, covered-unresolved not `unsourced`, all covered → no batches);
  T4 `PlanCoverageCompileTests` (no covered title in any batch prompt; the 094 plan: the compat guard and the warning
  row); T5 text guards (analyst, skill); builder rows in `GenerationPlanBuilderTests`.
- Baseline on `db20415`: Core 1228 / CLI 2167 (one known failure) / Execution 228 / Integration 8; 4 warnings.
- Rows that use new APIs (T1, the builder and T2 threshold rows) can't compile on the base: staged out for the proof
  run, as in 094.
