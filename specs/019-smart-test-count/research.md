# Research: Smart Test Count Recommendation

**Feature**: 019-smart-test-count
**Date**: 2026-03-23

## R1: Where to Insert the Analysis Step

**Decision**: Insert analysis in `GenerateHandler` between document loading and agent creation, when `count` is null.

**Rationale**: The handler already loads source documents via `SourceDocumentLoader.LoadAllAsync()` and builds a document map via `DocumentMapBuilder.BuildAsync()`. The analysis step needs the same documents. Placing it after document loading and before `agent.GenerateTestsAsync()` means no changes to the agent interface — the handler simply resolves the count before passing it downstream.

**Alternatives considered**:
- Inside `CopilotGenerationAgent.GenerateTestsAsync()`: Rejected — would require changing the agent interface and mixing concerns (analysis is a pre-step, not part of generation).
- As a separate CLI command (`spectra ai analyze-behaviors`): Rejected — YAGNI; the analysis is only useful as a pre-step to generation.

## R2: AI Call for Behavior Analysis

**Decision**: Create `BehaviorAnalyzer` service that sends a single structured prompt to the same Copilot SDK provider used for generation, requesting a JSON response with categorized behaviors.

**Rationale**: Reusing the same provider avoids new configuration. The analysis prompt is lightweight (~500-1000 input tokens using document summaries, not full content). The structured JSON response format matches existing patterns in the codebase (e.g., `RequirementsExtractor` in feature 015).

**Alternatives considered**:
- Heuristic analysis (no AI): Rejected — counting headings/sections doesn't identify testable behaviors accurately. AI understanding is needed for semantic categorization.
- Full document content in analysis: Rejected — wasteful. Document summaries (title + section headings + first 200 chars per section) provide sufficient signal for behavior identification.

## R3: Dedup Against Existing Tests

**Decision**: Reuse existing `DuplicateDetector` with Jaccard similarity (0.6 threshold) to match identified behaviors against existing test titles.

**Rationale**: The `DuplicateDetector` already implements word-overlap similarity and is battle-tested. Behavior titles from the analysis are short descriptions comparable to test titles. No new dedup algorithm needed.

**Alternatives considered**:
- Semantic embedding similarity: Rejected — requires additional infrastructure and API calls. Word overlap is sufficient for title-level matching.
- Pass existing tests into the analysis prompt: Rejected — increases token count significantly. Better to let the AI identify all behaviors, then filter locally.

## R4: Interactive Menu Design

**Decision**: New `CountSelector` class using Spectre.Console `SelectionPrompt<T>`, dynamically building options from the analysis breakdown.

**Rationale**: Follows the existing pattern used by `SuiteSelector`, `TestTypeSelector`, and `GapSelector`. Spectre.Console selection prompts are already the established UX pattern.

**Alternatives considered**:
- Raw console input: Rejected — inconsistent with existing interactive UX.
- Integrate into existing `TestTypeSelector`: Rejected — different concern; test type selection is about focus, count selection is about quantity.

## R5: Post-Generation Gap Notification

**Decision**: After generation, compare generated test count against analysis breakdown. If fewer were generated, compute remaining by category and display via existing `NextStepHints` pattern.

**Rationale**: The `NextStepHints` helper already prints context-aware suggestions in dimmed text. Gap notification is a natural extension — display remaining categories and the command to generate more.

**Alternatives considered**:
- Separate gap command: Rejected — the information is ephemeral (session-scoped) and only useful immediately after generation.

## R6: Focus Flag Integration

**Decision**: When `--focus` is provided, filter the analysis breakdown to matching categories before presenting/generating.

**Rationale**: The focus flag already narrows generation scope. Filtering the analysis result by category name (fuzzy match) or letting the AI prompt include the focus constraint naturally scopes the behavior list.

**Alternatives considered**:
- Re-run analysis with focus in prompt: Rejected — wasteful extra API call. Filter locally instead.

## R7: Error Handling / Fallback

**Decision**: If the analysis AI call fails (timeout, parse error, empty response), warn the user and fall back to the existing behavior: prompt for count in interactive mode, or use default 20 in direct mode.

**Rationale**: The analysis is a convenience feature, not a blocking requirement. Graceful degradation ensures the generate command always works even if analysis fails.
