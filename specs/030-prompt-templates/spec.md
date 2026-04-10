# Feature Specification: Customizable Root Prompt Templates

**Feature Branch**: `030-prompt-templates`  
**Created**: 2026-04-10  
**Status**: Draft  
**Depends on**: 023 (Criteria Extraction Overhaul), 022 (Bundled Skills), 009 (Copilot SDK Consolidation)  
**Affects**: Spectra.CLI, Spectra.Core, SKILLs, Agents, Init, Docs  
**Input**: User description: "Customizable Root Prompt Templates - markdown files in .spectra/prompts/ that control every core AI operation with placeholders, hash-tracked updates, and CLI management"

## Problem Statement

All AI prompts in SPECTRA are hardcoded inside C# classes. Users have zero control over the reasoning that produces test cases, analyzes documents, extracts criteria, or verifies grounding. The only customization point is the generation profile, which controls test output format - not the analytical reasoning, domain focus, quality heuristics, or verification rigor.

This causes three problems:

1. **Generic output quality** - A payments team and a UI team get identical analysis. Prompts cannot focus on domain-specific concerns like compliance edge cases vs. accessibility patterns.
2. **No prompt engineering leverage** - Users cannot apply structured prompt patterns (chain-of-thought, persona, few-shot) that improve LLM output quality.
3. **Black box reasoning** - When output quality is poor, users cannot diagnose whether it is an AI limitation or a prompt limitation. No iteration is possible.

## User Scenarios & Testing

### User Story 1 - Use Default Prompt Templates Out of the Box (Priority: P1)

A QA engineer initializes a new SPECTRA project and immediately gets well-structured AI prompts for all operations without any configuration. The default templates use proven prompt engineering patterns (persona, boundaries, chain-of-thought, few-shot examples) that produce high-quality output for general use cases.

**Why this priority**: This is the foundation. Every user benefits from better defaults, and no customization knowledge is required. The system must work seamlessly with built-in templates before custom ones matter.

**Independent Test**: Run `spectra init`, then `spectra ai generate` against a documentation file. Verify that the analysis and generation phases use the built-in templates and produce structured, well-categorized output.

**Acceptance Scenarios**:

1. **Given** a new project, **When** user runs `spectra init`, **Then** the system creates `.spectra/prompts/` directory with 5 default template files (behavior-analysis.md, test-generation.md, criteria-extraction.md, critic-verification.md, test-update.md)
2. **Given** a project without `.spectra/prompts/` directory, **When** user runs `spectra ai generate`, **Then** the system falls back to built-in embedded templates and operates normally
3. **Given** a project with default templates, **When** user runs `spectra ai generate`, **Then** behavior analysis produces categorized behaviors using the 6 default categories (positive-path, negative-path, edge-case, boundary, error-handling, security)
4. **Given** a project with default templates, **When** user runs `spectra ai analyze --extract-criteria`, **Then** criteria extraction uses the criteria-extraction template and produces properly formatted acceptance criteria

---

### User Story 2 - Customize Prompt Templates for Domain-Specific Quality (Priority: P1)

A domain expert (e.g., fintech QA lead) edits prompt templates to focus AI reasoning on their specific concerns. They modify the behavior-analysis template to emphasize compliance and reconciliation patterns, and the test-generation template to require specific data types in test steps.

**Why this priority**: This is the core value proposition. Without customization, the feature is just a refactor of hardcoded prompts.

**Independent Test**: Edit `.spectra/prompts/behavior-analysis.md` to add domain-specific instructions, run `spectra ai generate`, and verify the AI output reflects the customizations.

**Acceptance Scenarios**:

1. **Given** a customized behavior-analysis template, **When** user runs `spectra ai generate`, **Then** the system uses the user's template instead of the built-in default
2. **Given** a customized template with `{{placeholder}}` syntax, **When** the system resolves the template, **Then** all placeholders are replaced with dynamic runtime values (document text, suite name, existing tests, etc.)
3. **Given** a customized template with `{{#if focus_areas}}...{{/if}}` blocks, **When** the `--focus` flag is provided, **Then** the conditional block is included in the resolved prompt
4. **Given** a customized template with `{{#if focus_areas}}...{{/if}}` blocks, **When** the `--focus` flag is NOT provided, **Then** the conditional block is excluded from the resolved prompt
5. **Given** a customized template with `{{#each categories}}...{{/each}}` blocks, **When** categories are provided from config, **Then** the block is expanded once per category item

---

### User Story 3 - Manage Templates via CLI (Priority: P2)

A QA team lead uses the `spectra prompts` command to view template status, validate customized templates for syntax errors, and reset templates that have issues back to defaults.

**Why this priority**: CLI management enables team workflows (checking template status in CI, validating before commit) but is not required for basic customization.

**Independent Test**: Run `spectra prompts list` to see all templates with their customization status, then `spectra prompts validate` to check a template, then `spectra prompts reset` to restore defaults.

**Acceptance Scenarios**:

1. **Given** a mix of customized and default templates, **When** user runs `spectra prompts list`, **Then** each template is shown with status: "customized" (user-modified), "default" (matches built-in), or "missing" (file absent, using built-in)
2. **Given** a template with valid syntax, **When** user runs `spectra prompts validate behavior-analysis`, **Then** the command exits with code 0 and reports success
3. **Given** a template with unknown placeholder names, **When** user runs `spectra prompts validate`, **Then** the command exits with code 2 and lists the unknown placeholders as warnings
4. **Given** a template with unclosed `{{#if}}` blocks, **When** user runs `spectra prompts validate`, **Then** the command exits with code 2 and reports the syntax error
5. **Given** a customized template, **When** user runs `spectra prompts reset behavior-analysis`, **Then** the file is overwritten with the built-in default
6. **Given** any command, **When** user adds `--output-format json`, **Then** the output is structured JSON suitable for SKILL/CI consumption
7. **Given** any template, **When** user runs `spectra prompts show behavior-analysis`, **Then** the template content is displayed
8. **Given** any template, **When** user runs `spectra prompts show behavior-analysis --raw`, **Then** the template content is displayed with `{{placeholders}}` unresolved

---

### User Story 4 - Configure Domain-Specific Behavior Categories (Priority: P2)

A team configures custom behavior categories in `spectra.config.json` to replace the default 6 categories with domain-specific ones (e.g., compliance, reconciliation, idempotency for fintech). These categories flow through analysis, generation, test frontmatter, dashboard, and validation.

**Why this priority**: Categories are the most impactful customization after templates themselves. They affect behavior analysis output, test categorization, `--focus` filtering, and dashboard breakdowns.

**Independent Test**: Add custom categories to config, run `spectra ai generate`, and verify behaviors are categorized using the custom categories.

**Acceptance Scenarios**:

1. **Given** custom categories in `spectra.config.json` under `analysis.categories`, **When** user runs `spectra ai generate`, **Then** the behavior analyzer uses the custom category list in its prompt
2. **Given** no `analysis.categories` in config, **When** user runs `spectra ai generate`, **Then** the system uses the 6 default categories (positive-path, negative-path, edge-case, boundary, error-handling, security)
3. **Given** custom categories, **When** user runs `spectra ai generate --focus "compliance, boundary"`, **Then** only behaviors matching those category IDs are prioritized
4. **Given** a test file with a `category` value not in the configured list, **When** user runs `spectra validate`, **Then** a warning (not error) is reported for the unknown category
5. **Given** custom categories, **When** user runs `spectra init`, **Then** the default 6 categories are written to `analysis.categories` in config

---

### User Story 5 - Safe Template Updates Across Versions (Priority: P2)

When SPECTRA releases new versions with improved default templates, `spectra update-skills` updates unmodified templates to the latest version while preserving user customizations.

**Why this priority**: Without safe updates, users either lose customizations or miss improvements. This enables the same hash-tracking pattern already used for SKILLs.

**Independent Test**: Run `spectra update-skills` with one modified and one unmodified template. Verify the unmodified template is updated and the modified one is preserved.

**Acceptance Scenarios**:

1. **Given** an unmodified template (SHA-256 matches built-in), **When** user runs `spectra update-skills`, **Then** the template is updated to the latest built-in version
2. **Given** a user-modified template (SHA-256 differs from built-in), **When** user runs `spectra update-skills`, **Then** the template is preserved unchanged
3. **Given** a new template introduced in a newer version, **When** user runs `spectra update-skills`, **Then** the new template file is created automatically

---

### User Story 6 - Invalid Template Graceful Fallback (Priority: P3)

A user edits a template and introduces a syntax error. Instead of failing the entire operation, the system logs a warning and falls back to the built-in default for that template.

**Why this priority**: Important for robustness but a rare scenario. Most users will either use defaults or validate before use.

**Independent Test**: Create a template with invalid YAML frontmatter, run `spectra ai generate`, and verify it completes successfully using the built-in fallback.

**Acceptance Scenarios**:

1. **Given** a template with invalid YAML frontmatter, **When** the system loads the template, **Then** it logs a warning and falls back to the built-in default
2. **Given** a template with valid frontmatter but malformed placeholder syntax, **When** the system resolves the template, **Then** unrecognized placeholders are replaced with empty strings

---

### User Story 7 - SKILL Integration for Copilot Chat (Priority: P3)

A VS Code Copilot Chat user invokes the `spectra-prompts` SKILL to view and manage prompt templates through the chat interface, using the same structured tool-call-sequence pattern as other SKILLs.

**Why this priority**: Extends the existing SKILL pattern. Lower priority because CLI access covers the same functionality.

**Independent Test**: Verify the `spectra-prompts` SKILL file is bundled and contains correct step-by-step commands for list, show, validate, and reset operations.

**Acceptance Scenarios**:

1. **Given** the SKILL is installed, **When** user asks Copilot Chat to "list prompt templates", **Then** the agent runs `spectra prompts list --output-format json --no-interaction`
2. **Given** the SKILL is installed, **When** user asks to "show the behavior analysis template", **Then** the agent runs `spectra prompts show behavior-analysis --no-interaction`

---

### Edge Cases

- What happens when a template file exists but is empty? System falls back to built-in default with a warning.
- What happens when a placeholder value contains `{{` characters (template-like syntax in document text)? The resolver only processes known placeholder patterns and does not recursively resolve; literal `{{` in values are preserved as-is.
- What happens when `.spectra/prompts/` directory exists but a specific template file is missing? System uses built-in default for that specific template; other templates in the directory are still loaded normally.
- What happens when a user adds extra placeholders not in the declared list? `spectra prompts validate` reports warnings for unknown placeholders; at runtime, unknown placeholders resolve to empty strings.
- What happens when categories config contains duplicate IDs? The last entry wins; no error is raised.
- What happens with templates during `spectra update-skills --force`? Force mode overwrites all templates regardless of customization status (same behavior as SKILLs).

## Requirements

### Functional Requirements

- **FR-001**: System MUST load prompt templates from `.spectra/prompts/{templateId}.md` when present, falling back to built-in embedded defaults when absent or invalid
- **FR-002**: System MUST support three placeholder syntaxes: `{{variable}}` for simple substitution, `{{#if variable}}...{{/if}}` for conditional blocks, and `{{#each variable}}...{{/each}}` for list iteration
- **FR-003**: System MUST NOT support nesting of control blocks (e.g., `{{#if}}` inside `{{#each}}`)
- **FR-004**: System MUST strip HTML comments (`<!-- -->`) from template bodies before sending to the AI provider
- **FR-005**: System MUST embed 5 default templates as built-in resources: behavior-analysis, test-generation, criteria-extraction, critic-verification, test-update
- **FR-006**: System MUST replace hardcoded prompts in BehaviorAnalyzer, CopilotGenerationAgent, CriteriaExtractor, CopilotCritic, and the test update flow with template-driven prompts
- **FR-007**: System MUST provide a `spectra prompts` CLI command with subcommands: list, show, reset, validate
- **FR-008**: System MUST track template file hashes (SHA-256) to distinguish user-customized templates from unmodified defaults during `spectra update-skills`
- **FR-009**: System MUST create `.spectra/prompts/` with all 5 default templates during `spectra init`
- **FR-010**: System MUST support `analysis.categories` configuration in `spectra.config.json` with `id` and `description` fields per category
- **FR-011**: System MUST use 6 default categories (positive-path, negative-path, edge-case, boundary, error-handling, security) when `analysis.categories` is absent from config
- **FR-012**: System MUST inject configured categories into the behavior-analysis template via the `{{categories}}` placeholder
- **FR-013**: System MUST report unknown category values as warnings (not errors) during `spectra validate`
- **FR-014**: System MUST resolve missing placeholder values to empty strings (not throw errors)
- **FR-015**: System MUST log a warning and fall back to built-in defaults when a user template fails to parse
- **FR-016**: System MUST support `--output-format json` and `--no-interaction` flags on all `spectra prompts` subcommands
- **FR-017**: System MUST bundle a `spectra-prompts` SKILL file (11th SKILL) for Copilot Chat integration
- **FR-018**: System MUST include prompt template hashes in the `SkillsManifest` for update tracking
- **FR-019**: Each template MUST declare its placeholders in YAML frontmatter with `name` and optional `description` fields
- **FR-020**: The `spectra prompts show` command MUST support a `--raw` flag to display templates with unresolved placeholders

### Key Entities

- **PromptTemplate**: A loaded template with metadata (spectra_version, template_id, description, placeholders list) and body text containing placeholder syntax. Has an `IsUserCustomized` flag indicating whether it was loaded from `.spectra/prompts/` or from built-in defaults.
- **PlaceholderSpec**: Declares a single placeholder with name and optional description. Used in template frontmatter for documentation and validation.
- **CategoryDefinition**: A behavior category with id and description, configured in `spectra.config.json` under `analysis.categories`. Flows through analysis, generation, test frontmatter, dashboard, and validation.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Users can customize any AI prompt by editing a markdown file, with changes reflected in the next generation run without code changes or recompilation
- **SC-002**: All 5 AI operations (behavior analysis, test generation, criteria extraction, critic verification, test update) use template-driven prompts instead of hardcoded strings
- **SC-003**: Template validation catches syntax errors and unknown placeholders before they cause runtime issues, with clear error messages guiding the user to fix the problem
- **SC-004**: Existing users experience no behavior change after upgrade - built-in defaults produce equivalent output to the previous hardcoded prompts
- **SC-005**: Users can configure domain-specific behavior categories and see them reflected in analysis output, test frontmatter, focus filtering, and dashboard breakdowns
- **SC-006**: Template updates via `spectra update-skills` preserve 100% of user customizations while updating unmodified templates to latest versions
- **SC-007**: All `spectra prompts` CLI commands support JSON output for CI/SKILL integration, following the same patterns as existing commands

## Assumptions

- The 5 AI-calling classes (BehaviorAnalyzer, CopilotGenerationAgent, CriteriaExtractor, CopilotCritic, test update flow) each have a single primary prompt that can be replaced with a template. If any class uses multiple prompt strings, they will be consolidated into one template.
- The `{{#each}}` block iterates over items that have `id` and `description` properties (used for categories). If other list shapes are needed in the future, the resolver can be extended.
- Template resolution is synchronous and happens in-memory. Templates are small (40-80 lines) and do not require streaming or async loading.
- The existing SKILLs count in documentation is 10 (after spec 029). This feature adds the 11th SKILL (`spectra-prompts`).
- Users will use git for template version history. No built-in versioning or history is needed.
