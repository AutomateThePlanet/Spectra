# Research: Undocumented Behavior Test Cases

**Feature**: 018-undocumented-tests
**Date**: 2026-03-23

## R1: Grounding Metadata Extension for "manual" Verdict

**Decision**: Add `"manual"` as a fourth verdict value alongside `Grounded`, `Partial`, `Hallucinated`. Extend `GroundingFrontmatter` with `source` and `created_by` fields.

**Rationale**: The existing `VerificationVerdict` enum has three values. Adding `Manual` keeps it in the same type system. The `GroundingMetadata` record currently requires `Generator`, `Critic`, `Score`, and `VerifiedAt` — all of which are meaningless for manual tests. Rather than making these nullable, we create the metadata only at the frontmatter DTO level (`GroundingFrontmatter`) and skip `ToMetadata()` conversion for manual verdicts.

**Current architecture**:
- `VerificationVerdict.cs` — enum with `Grounded`, `Partial`, `Hallucinated`
- `GroundingFrontmatter.cs` — YAML DTO with `verdict`, `score`, `generator`, `critic`, `verified_at`, `unverified_claims`
- `GroundingMetadata.cs` — sealed record with `IsValid()` requiring score 0-1, non-null generator/critic
- `GroundingFrontmatter.ToMetadata()` converts DTO → record; returns null if required fields missing

**Approach**:
1. Add `Manual` to `VerificationVerdict` enum
2. Add `source` (string?) and `created_by` (string?) fields to `GroundingFrontmatter`
3. For manual verdicts, `ToMetadata()` returns a special `GroundingMetadata` with placeholder values (generator="user", critic="none", score=1.0) or returns null and let the frontmatter handle serialization directly
4. Add `note` (string?) field to `GroundingFrontmatter`

**Alternatives considered**:
- Separate `ManualGroundingMetadata` type — rejected for complexity; one type with optional fields is simpler
- Store manual metadata outside grounding section — rejected; keeping it in `grounding:` maintains consistency

## R2: Critic Verification Bypass

**Decision**: Skip critic verification for tests with `grounding.verdict: "manual"` at the `GenerateHandler.VerifyTestsAsync()` level.

**Rationale**: The critic flow in `GenerateHandler.cs` (lines 917-987) loops through generated tests and calls `critic.VerifyTestAsync()`. For manual tests, there's no documentation to verify against, so the critic call would always fail or produce meaningless results.

**Current flow**:
1. `ShouldVerify()` checks `--skip-critic` flag and config
2. `VerifyTestsAsync()` creates critic via `CriticFactory.TryCreate()`
3. Loops through tests, calls `critic.VerifyTestAsync(test, sourceDocs, ct)`
4. `CreateTestWithGrounding()` attaches metadata
5. Filter: `Verdict != Hallucinated` determines write-to-disk

**Approach**: In the verification loop, check if the test already has `grounding.verdict: "manual"` (set during generation). If so, skip verification and include the test in the write list directly.

**Alternatives considered**:
- Add a `--skip-critic-for-manual` flag — rejected; unnecessary complexity, should be automatic
- Handle in CriticPromptBuilder — rejected; the check should happen before calling the critic at all

## R3: Schema Validation for Empty source_refs

**Decision**: No changes needed. Empty `source_refs: []` is already valid.

**Rationale**: `TestValidator.cs` does NOT validate `source_refs` content. The field defaults to an empty list in both `TestCaseFrontmatter` and `TestCase`. Validation only checks: ID format, title, priority, expected_result, step count. Empty `source_refs` passes validation today.

**Verification**: Confirmed by reviewing `TestValidator.Validate()` — no check references `SourceRefs`.

## R4: Coverage Analysis — Undocumented Test Metric

**Decision**: Add undocumented test count to `DocumentationCoverage` model and surface in the unified report.

**Rationale**: `DocumentationCoverageAnalyzer.Analyze()` iterates tests and matches `source_refs` against document paths. Tests with empty `source_refs` are currently uncounted — they don't appear in any document's coverage. Adding a separate metric for "tests with no source_refs" provides the visibility the spec requires.

**Current architecture**:
- `DocumentationCoverage` has `TotalDocs`, `CoveredDocs`, `Percentage`, `Details[]`
- `DocumentationCoverageAnalyzer.Analyze()` groups tests by their source_refs
- `UnifiedCoverageBuilder.Build()` assembles all three sections

**Approach**:
1. Add `UndocumentedTests` (int) and `UndocumentedTestIds` (List<string>) to `DocumentationCoverage`
2. In `DocumentationCoverageAnalyzer.Analyze()`, count tests where `SourceRefs` is empty
3. Surface in CLI report output and dashboard data

**Alternatives considered**:
- Create a fourth coverage section — rejected; undocumented tests are a documentation coverage concern, not a separate dimension
- Use `grounding.verdict == "manual"` as the filter — rejected; empty `source_refs` is the objective criterion (a test could have empty source_refs without being manually created)

## R5: Dashboard Orange Category

**Decision**: Add orange CSS variable (`--cov-orange`) and use it for undocumented tests in coverage visualization.

**Rationale**: Current colors: green (≥80%), amber (50-79%), red (<50%), blue (labels), slate (manual). Orange is visually distinct from all existing categories and semantically conveys "attention needed but not critical" — matching the spec's intent.

**Current CSS variables**:
- `--cov-green: #10B981` — covered
- `--cov-amber: #F59E0B` — partial
- `--cov-red: #EF4444` — uncovered
- `--cov-blue: #3B82F6` — labels
- `--cov-slate: #64748b` — manual/unlinked

**Approach**:
1. Add `--cov-orange: #F97316` and `--cov-orange-bg: #fff7ed` to CSS variables
2. Add undocumented test count to `CoverageSummaryData` (new fields on `DocumentationSectionData`)
3. Render as a segment in coverage KPI cards and donut chart
4. Add filter toggle in coverage view JavaScript

**Alternatives considered**:
- Reuse amber — rejected; amber is used for 50-79% coverage thresholds, undocumented is a different semantic
- Use purple — rejected; purple is used for feature hierarchy in coverage tree

## R6: Generation Agent Prompt for Undocumented Flow

**Decision**: Extend `GroundedPromptBuilder` system prompt with an "undocumented behavior" section.

**Rationale**: The generation agent prompt in `GroundedPromptBuilder.cs` (lines 17-84) currently enforces document-grounded test generation. Adding a section that recognizes user-described behaviors and provides the alternative flow enables the agent to choose the right path.

**Current prompt structure**:
- System prompt defines "DOCUMENT-GROUNDED" approach
- Two-step: Scenario Discovery → Test Generation
- Requires `scenario_from_doc` and non-empty `source_refs`

**Approach**:
1. Add a conditional section: "If the user explicitly describes a behavior not in documentation..."
2. Define the alternative output format with `source_refs: []` and manual grounding metadata
3. Add guidance on when to use this vs normal generation
4. Include duplicate check instructions (use `find_test_cases` tool)

## R7: Duplicate Detection Strategy

**Decision**: Use the existing `find_test_cases` MCP tool for similarity search during the conversational flow.

**Rationale**: The `find_test_cases` tool (from 010-smart-test-selection) already supports free-text query, priority/tag/component/automation filters. The generation agent can call this tool as part of the conversational flow before creating a new test.

**Approach**: The agent prompt instructs the agent to:
1. Extract keywords from the user's description
2. Call `find_test_cases` with the relevant query and component filter
3. Review results for duplicates
4. Present findings to user before proceeding

No new code needed — this is a prompt-level integration with existing MCP tools.

## R8: User Identity for created_by

**Decision**: Use the existing `UserIdentityResolver` from Spectra.MCP for MCP-based flows. For CLI-based generation, use git config user.name or environment variables.

**Rationale**: `UserIdentityResolver` in `src/Spectra.MCP/Identity/` already resolves the current user. The generation agent running via MCP has access to this. For direct CLI usage, falling back to git config is standard practice.

**Alternatives considered**:
- Always require explicit `--user` flag — rejected; friction for no benefit
- Skip created_by if unknown — rejected; "unknown" fallback is acceptable
