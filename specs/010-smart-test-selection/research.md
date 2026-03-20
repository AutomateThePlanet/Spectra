# Research: Smart Test Selection

## R1: MCP Tool Implementation Pattern

**Decision**: Follow existing tool pattern — implement `IMcpTool` interface with constructor DI, JSON parameter deserialization via `McpProtocol.DeserializeParams<T>`, and `McpToolResponse<T>` for responses.

**Rationale**: All 19 existing tools follow this exact pattern. Consistency enables easier code review, testing, and maintenance. No new abstractions needed.

**Alternatives considered**:
- Generic tool base class — rejected because existing tools don't use one and Constitution Principle V (YAGNI) discourages premature abstraction.

## R2: Cross-Suite Index Loading

**Decision**: Reuse existing `Func<string, IEnumerable<TestIndexEntry>>` indexLoader pattern from Program.cs. For cross-suite search, add a `Func<IEnumerable<string>>` suiteListLoader to enumerate all suites, then call indexLoader per suite.

**Rationale**: The indexLoader already handles file I/O, JSON deserialization, and missing file cases. Composing it with a suite enumerator avoids duplicating index reading logic.

**Alternatives considered**:
- Single function that loads all indexes at once — rejected because it would require a new loader signature and break the existing DI pattern.
- Direct file system access in the tool — rejected because it couples tool to file structure.

## R3: Execution History Querying

**Decision**: Add new methods to `ResultRepository` for history aggregation. Query `test_results` table grouped by `test_id`, ordered by `completed_at DESC`, computing pass rate from status counts.

**Rationale**: The existing `ResultRepository` already handles all test result queries. Adding aggregation methods keeps data access centralized. SQLite handles the aggregation efficiently.

**Alternatives considered**:
- New HistoryRepository class — rejected as unnecessary given ResultRepository already owns the test_results table.
- In-memory aggregation after loading all results — rejected for performance reasons at scale.

## R4: Saved Selections Config Model

**Decision**: Add `SelectionsConfig` as a new dictionary property on `SpectraConfig`. Each entry is a `SavedSelectionConfig` with description, tags, priorities, components, and has_automation fields. Reuse `SelectionFilters` for runtime filter application.

**Rationale**: Follows existing config pattern (e.g., `Suites` is already `IReadOnlyDictionary<string, SuiteConfig>`). Dictionary keyed by selection name matches the JSON structure from spec.

**Alternatives considered**:
- Separate selections.json file — rejected because config consolidation in spectra.config.json is the established pattern.

## R5: Test Description Field

**Decision**: Add optional `description` field to `TestCaseFrontmatter` (YamlDotNet), `TestCase` (business model), and `TestIndexEntry` (JSON index). Use `WhenWritingNull` ignore condition for backward compatibility.

**Rationale**: Follows the exact pattern used for `automated_by` and `requirements` fields added in 011-coverage-overhaul. Minimal change, fully backward compatible.

**Alternatives considered**:
- Store description separately from frontmatter — rejected because it breaks the single-source-of-truth pattern.

## R6: Keyword Search Implementation

**Decision**: Case-insensitive OR matching. Split query on whitespace, match each keyword against title + description + tags (joined). Rank by hit count. Pure string operations — no regex, no stemming, no fuzzy matching.

**Rationale**: Simple, predictable, fast. Matches spec clarification (OR logic, ranked by hits). Advanced search features (stemming, fuzzy) are out of scope per Constitution Principle V.

**Alternatives considered**:
- Full-text search library (Lucene.NET) — rejected as massive dependency for simple keyword matching.
- Regex-based matching — rejected as unnecessarily complex for simple keyword containment.

## R7: Start Execution Run Extension

**Decision**: Extend `StartExecutionRunTool` to accept `test_ids` and `selection` parameters alongside existing `suite`. Validate mutual exclusivity in the tool. For `test_ids`, resolve each ID across all suite indexes. For `selection`, load from config, apply filters via the same logic as `find_test_cases`, then start run with resolved IDs.

**Rationale**: Keeps the single tool entry point for starting runs. Mutual exclusivity validation is simple and explicit. Reusing filter logic from `find_test_cases` avoids duplication.

**Alternatives considered**:
- Separate `start_custom_run` tool — rejected because it fragments the run-start interface and complicates agent prompts.

## R8: Agent Prompt Updates

**Decision**: Add a "Smart Test Selection" section to `spectra-execution.agent.md` with step-by-step workflow, risk-based recommendations, and example conversations.

**Rationale**: Agent prompts are the intelligence layer per the spec's architecture note. Prompt changes are zero-cost deploys (Markdown only). The existing agent prompt already follows a structured step-by-step format.

**Alternatives considered**:
- Separate agent prompt file for selection — rejected because the execution agent is the natural home for selection-to-execution workflows.
