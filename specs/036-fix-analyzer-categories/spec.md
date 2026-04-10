# Feature Specification: Fix BehaviorAnalyzer Category Injection

**Feature Branch**: `036-fix-analyzer-categories`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: "Fix BehaviorAnalyzer category injection — custom categories from `spectra.config.json` and edits to `.spectra/prompts/behavior-analysis.md` are silently ignored because the analyzer never receives the config or the prompt template loader."

> **Note on numbering**: The user-supplied draft was titled "034-behavior-analyzer-categories-fix", but feature numbers 034 and 035 already exist in this repo. The branch script auto-assigned **036**. Behaviorally identical to the user's draft.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Custom categories from config drive the analysis (Priority: P1)

A QA lead opens `spectra.config.json`, edits `analysis.categories` to replace the defaults with their domain's categories — say `keyboard_interaction`, `screen_reader_support`, `color_contrast`, `focus_management`. They run `spectra ai generate checkout`. The behavior analysis identifies behaviors using those exact categories, and the per-category breakdown shown to the user reflects the configured set, not the generic defaults.

**Why this priority**: This is the entire value of Spec 030 (Prompt Templates). Today the analyzer hardcodes 5 categories regardless of configuration, which silently breaks the customization promise. Until this is fixed, every user customizing categories sees their changes thrown away with no error, which is the worst kind of bug — invisible.

**Independent Test**: With a `spectra.config.json` declaring 4 custom accessibility categories and zero of the legacy 5, run `spectra ai generate <suite> --analyze-only --output-format json` and inspect the breakdown. The breakdown must contain the 4 custom category IDs and none of the legacy ones.

**Acceptance Scenarios**:

1. **Given** `spectra.config.json` declares custom categories `[keyboard_interaction, screen_reader_support, color_contrast, focus_management]`, **When** the user runs an analysis, **Then** the analysis prompt sent to the AI lists exactly those 4 categories (not the legacy hardcoded set), and the returned breakdown groups behaviors by those same 4 IDs.
2. **Given** the user has not configured `analysis.categories` at all, **When** the user runs an analysis, **Then** the system uses the 6 documented defaults from Spec 030 (happy_path, negative, edge_case, boundary, error_handling, security) — not the legacy 5 hardcoded set inside the analyzer.
3. **Given** the user edits `.spectra/prompts/behavior-analysis.md` to change the prompt wording, **When** the user runs an analysis, **Then** the AI receives the user's edited prompt text — not the legacy hardcoded prompt.
4. **Given** an analysis run, **When** the user inspects the per-category breakdown in the analyzer's output (terminal display, JSON output, and downstream count selector), **Then** every behavior the AI returned with a custom category ID appears under that ID — not collapsed into a generic bucket.

---

### User Story 2 - Focus filter matches custom category names (Priority: P2)

The user runs `spectra ai generate checkout --focus "keyboard"`. The analyzer narrows the identified behaviors to those whose category contains "keyboard" — including custom categories like `keyboard_interaction` — not just the small set of legacy enum-mapped keywords (happy/negative/edge/security/performance).

**Why this priority**: Without this, the `--focus` flag is half-broken: it works for the 5 legacy categories but silently returns *all* behaviors (or worse, none) when the user names a custom category. This is a discoverability and trust issue; once US1 ships, users will start naming custom categories in `--focus` and expect them to filter.

**Independent Test**: With custom categories configured and a behavior list containing both `keyboard_interaction` and `color_contrast`, call the analysis with `--focus "keyboard"` and verify only the `keyboard_interaction` behaviors come through. Repeat with `--focus "happy path"` and verify the legacy keyword path still works.

**Acceptance Scenarios**:

1. **Given** custom category `keyboard_interaction` is in use, **When** the user runs analysis with `--focus "keyboard"`, **Then** only behaviors in `keyboard_interaction` (and any other category whose ID contains "keyboard") are returned.
2. **Given** the legacy categories are in use, **When** the user runs analysis with `--focus "happy path"`, **Then** the existing behavior of returning happy_path matches still works (no regression).
3. **Given** a focus term that matches no category at all, **When** the user runs analysis, **Then** the system returns all identified behaviors (current fallback behavior preserved) and applies the focus to the downstream generation prompt instead.

---

### User Story 3 - Existing tests and existing users see no regression (Priority: P1)

A user who has not changed any defaults runs `spectra ai generate checkout` and gets exactly the same analysis output, the same per-category breakdown shape, the same count recommendation, and the same downstream interactive count selector as they did before this fix. The full existing test suite continues to pass.

**Why this priority**: This is a bug fix, not a redesign. Anything that breaks the default user's experience is a step backwards. Equally critical: the existing test suite is the safety net for the larger refactor this fix requires (the per-category breakdown type changes), so all existing tests must remain meaningful and green.

**Independent Test**: Without changing any config, run the full existing test suite (`dotnet test`) — all green. Run `spectra ai generate <suite>` end-to-end interactively and confirm the count selector and analysis presenter still render correctly with default categories.

**Acceptance Scenarios**:

1. **Given** no `analysis.categories` configuration is set, **When** the analyzer runs, **Then** the breakdown contains the 6 default category IDs from Spec 030 with the same behaviors-per-category counts the AI returns.
2. **Given** the existing test suite, **When** `dotnet test` runs after the fix, **Then** all previously-passing tests still pass.
3. **Given** an interactive `spectra ai generate` session, **When** the AI returns a breakdown, **Then** the count selector still presents the breakdown to the user in a recognizable format.

### Edge Cases

- What happens when a behavior comes back with an empty or null category? → Treat it as a single bucket (e.g., "uncategorized"), do not crash, do not silently merge into a default category.
- What happens when the AI returns a category that does not appear in the configured list? → Include it in the breakdown anyway (the AI may invent a category that the prompt allowed). Do not drop the behavior.
- What happens when `analysis.categories` is configured but is an empty list? → Fall back to the documented defaults from Spec 030. Do not crash.
- What happens when `.spectra/prompts/behavior-analysis.md` does not exist? → Use the bundled built-in template (existing Spec 030 behavior). Do not silently revert to the legacy hardcoded prompt.
- What happens when the user mixes legacy and custom categories in a single config? → Both work side-by-side in the breakdown. No "must be one or the other" rule.
- What happens when the focus term matches **multiple** custom categories? → Return behaviors from all matching categories, not just the first.
- What happens to downstream consumers (count selector, JSON output, suggestion builder) that previously assumed a fixed set of category identifiers? → They must adapt to render arbitrary category IDs — not constrain the set.

## Requirements *(mandatory)*

### Functional Requirements

#### Analyzer wiring

- **FR-001**: The behavior analyzer MUST receive both the project configuration and the prompt template loader at the time it is constructed.
- **FR-002**: When a prompt template loader is available, the behavior analyzer MUST use the template-driven prompt path. The legacy hardcoded prompt is reserved for pure unit tests and the no-loader path only.
- **FR-003**: When configuration is available, the behavior analyzer MUST inject the configured analysis categories into the prompt sent to the AI — not the legacy hardcoded list.
- **FR-004**: When configuration is available but `analysis.categories` is empty or absent, the behavior analyzer MUST use the documented default categories from Spec 030 (six categories), not the five legacy hardcoded categories.
- **FR-005**: All call sites that construct a behavior analyzer MUST pass the project configuration and the prompt template loader. This includes every caller in the generate command path. (Internal/test-only callers MAY pass `null` to exercise the no-loader path.)

#### Category identity

- **FR-006**: The category attached to each identified behavior MUST be preserved end-to-end as the raw string returned by the AI (e.g., `keyboard_interaction`). It MUST NOT be silently collapsed into a fixed enum value when the AI returns a category not in the legacy set.
- **FR-007**: The per-category breakdown surfaced from the analyzer MUST be keyed by the same raw category strings the AI returned, so custom categories are visible distinctly in the breakdown.
- **FR-008**: A behavior whose AI-returned category is empty, null, or whitespace MUST be assigned a single "uncategorized" bucket and counted normally — not dropped, not crashed on, not silently merged into a default category.
- **FR-009**: Downstream consumers of the breakdown (analysis presenter, count selector, suggestion builder, JSON output) MUST display arbitrary category identifiers, not only a fixed set.

#### Focus filtering

- **FR-010**: When the user supplies a `--focus` term, the analyzer MUST narrow the identified behaviors to those whose category identifier (after normalizing case and substituting underscores/hyphens for spaces) substring-matches at least one space-separated word from the focus term.
- **FR-011**: When the focus term matches no category, the analyzer MUST return all identified behaviors unchanged, preserving today's fallback semantics so that focus terms continue to feed the downstream generation prompt.
- **FR-012**: The legacy keyword expansions for the well-known categories (e.g., "happy" → happy_path, "negative" → negative, "edge"/"boundary" → edge_case, "sec"/"auth" → security, "perf"/"load" → performance) MUST continue to match — including matching custom IDs that contain the same substring.

#### Backward compatibility

- **FR-013**: With no configuration changes, default users MUST see the same analysis behavior, the same per-category breakdown shape, the same count recommendation, and the same interactive count selector experience as before.
- **FR-014**: All previously-passing tests in the repository MUST continue to pass after this fix.

### Key Entities

- **Identified behavior**: A single testable behavior the AI extracted from documentation. Carries a title, source document reference, and a free-form **category identifier** (string). The category identifier MUST be preserved as-is end-to-end.
- **Analysis breakdown**: A map from category identifier (string) to count. Reflects the categories the AI actually returned, which may be the configured set, the defaults, or anything the AI invented within the prompt-allowed space.
- **Analysis category set**: The list of category identifiers the user configured under `analysis.categories` (or the defaults from Spec 030 when unconfigured). Injected into the analysis prompt; not enforced as a closed set on the AI's response.
- **Focus term**: A free-text string the user passes via `--focus`. Used to (a) substring-match category identifiers and narrow the analyzed behavior list, and (b) feed the downstream generation prompt regardless of category match.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user who configures 4 custom categories sees those exact 4 IDs in the analyzer's per-category breakdown and in the JSON output of an `--analyze-only` run. None of the legacy hardcoded category names appear unless the user kept them.
- **SC-002**: A user who edits `.spectra/prompts/behavior-analysis.md` sees evidence of that edit in the prompt the AI receives (verifiable by inspecting the prompt-builder output in a unit test).
- **SC-003**: A behavior the AI returns under category `keyboard_interaction` is counted under `keyboard_interaction` in the breakdown — not under `happy_path`, not dropped, not crashed on.
- **SC-004**: `--focus "keyboard"` returns only behaviors whose category identifier contains the substring "keyboard"; `--focus "happy path"` continues to return only happy-path behaviors (no legacy regression); `--focus "<term that matches nothing>"` returns all behaviors (no false-empty result).
- **SC-005**: A user who has not configured `analysis.categories` sees the 6 documented Spec 030 defaults in the breakdown — not the 5 legacy hardcoded values.
- **SC-006**: After the fix, `dotnet test` reports the same number of passing tests as before plus the new tests for this feature, with zero failures.
- **SC-007**: An empty/null AI category does not crash any code path; instead the behavior appears under a single "uncategorized" bucket.
- **SC-008**: The interactive count selector still renders the breakdown for default-configured users in the same form they see today.

## Assumptions

- The `analysis.categories` configuration block already exists in `spectra.config.json` per Spec 030. This feature consumes it; it does not redesign it.
- The `.spectra/prompts/behavior-analysis.md` template already exists and has the right shape (placeholders for `categories`, `document_text`, etc.) per Spec 030.
- The 6 default categories from Spec 030 are correct and out of scope for renumbering.
- The behavior analyzer is only invoked from the `spectra ai generate` command path. (Verified during planning: 3 call sites in `GenerateHandler`, no other callers.)
- The `--focus` flag is a free-text user input; no closed enumeration is enforced or needed.
- "Uncategorized" is an acceptable label for empty/null AI categories. This is a degenerate edge case that should never happen with a well-formed prompt; it exists only to keep the system robust against AI misbehavior.
- The legacy 5-value `BehaviorCategory` enum is still used by downstream consumers (the count selector, the analysis presenter, the breakdown dictionary type, the suggestion builder, and several test files). This feature MUST adapt those consumers to a string-keyed model rather than continuing to constrain category identity to that enum, but the work is mechanical (rename + retype), not architectural.
