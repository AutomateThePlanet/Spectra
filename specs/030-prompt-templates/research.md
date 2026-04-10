# Research: Customizable Root Prompt Templates

**Feature**: 030-prompt-templates | **Date**: 2026-04-10

## R1: Placeholder Template Engine Design

**Decision**: Custom lightweight resolver (~100 lines) supporting `{{var}}`, `{{#if var}}...{{/if}}`, `{{#each var}}...{{/each}}`.

**Rationale**: The template syntax is intentionally simple. A full Handlebars/Mustache library (e.g., Stubble, Handlebars.Net) would add a NuGet dependency for <100 lines of logic. The supported constructs are:
- Simple substitution: `{{name}}` → value
- Conditional block: `{{#if name}}...{{/if}}` → included if value is non-empty
- Iteration block: `{{#each name}}...{{/each}}` → expanded per item (items have `id` and `description` properties)
- No nesting of control blocks (explicit design constraint)
- Missing values resolve to empty string (no errors)

**Alternatives considered**:
- Stubble (Mustache for .NET): Adds dependency, more features than needed, heavier parsing
- Handlebars.Net: Even heavier, supports helpers/partials we don't need
- String.Replace only: Too simple, no conditionals or iteration
- Scriban: Powerful but complex, Liquid-compatible, overkill for 5 templates

## R2: Template Frontmatter Parsing

**Decision**: Reuse the existing YAML frontmatter parsing pattern from test case files, adapted for prompt template metadata.

**Rationale**: Spectra already parses YAML frontmatter from markdown files (test cases use `---` delimiters with YAML between them). The `PromptTemplateParser` will:
1. Split on first `---` and second `---` to extract YAML block
2. Deserialize YAML frontmatter to `PromptTemplate` metadata using `System.Text.Json` (with a simple key-value parser, not pulling in YamlDotNet just for frontmatter)
3. Everything after the second `---` is the template body

**Note**: The frontmatter is simple enough (spectra_version, template_id, description, placeholders list) that a lightweight parser suffices. The existing `FrontmatterExtractor` pattern in Spectra.Core can be referenced.

## R3: Built-in Template Embedding Strategy

**Decision**: Embed default templates as `.md` files in the assembly using the same embedded resource pattern as SKILLs.

**Rationale**: `SkillResourceLoader` loads files from `Spectra.CLI.Skills.Content.Skills.*` namespace. Prompt templates will be at `Spectra.CLI.Prompts.Content.*`. This keeps the pattern consistent:
- Files in `src/Spectra.CLI/Prompts/Content/*.md` with `<EmbeddedResource>` in csproj
- `PromptResourceLoader` (or extension of `SkillResourceLoader`) loads them at startup
- `BuiltInTemplates` static class provides typed access (like `SkillContent`)

## R4: BehaviorAnalyzer Integration

**Decision**: Replace `BuildAnalysisPrompt()` (lines 140-190) with template loading. The method currently builds the prompt imperatively (StringBuilder), appending categories, documents, and focus area.

**Rationale**: Current flow:
1. Hardcoded category list (5 categories as text)
2. Optional focus area appended
3. Documents appended with title + content (truncated to 2000 chars)

Template integration:
- Categories come from config (`analysis.categories`) via `{{#each categories}}`
- Focus area via `{{#if focus_areas}}`
- Document text via `{{document_text}}` (the method will pre-format documents into a string before passing as placeholder)
- The `AnalyzeAsync()` method signature doesn't change; only the internal prompt construction does

## R5: GenerationAgent Integration

**Decision**: Replace `BuildFullPrompt()` (lines 247-316) with template loading. This is the most complex prompt with workflow instructions and JSON schema.

**Rationale**: Current prompt includes:
1. Persona ("test case generation expert")
2. CRITICAL RULES (3 rules)
3. WORKFLOW (6-step tool-driven workflow)
4. OUTPUT FORMAT (JSON schema example)
5. YOUR TASK section with count and criteria

The template needs placeholders for: `behaviors` (analyzed behaviors), `suite_name`, `existing_tests`, `acceptance_criteria`, `profile_format` (JSON schema), `count`, `focus_areas`.

**Important**: The current prompt includes tool-usage instructions (ListDocumentationFiles, ReadTestIndex, etc.) that are specific to the Copilot SDK tool-calling pattern. These MUST be preserved in the default template.

## R6: CriticPromptBuilder Integration

**Decision**: Replace `BuildSystemPrompt()` (lines 18-51) with template loading. Keep `BuildUserPrompt()` code-driven.

**Rationale**: The system prompt is a static ~30-line string defining verdict rules and output format. Perfect for a template. The user prompt (`BuildUserPrompt()`) dynamically selects relevant documents and formats test case fields - this logic doesn't benefit from templating because it's structural assembly, not reasoning instructions.

Template: `critic-verification.md` with placeholders `{{test_case}}`, `{{source_document}}`, `{{acceptance_criteria}}`. At resolve time, the loader merges the system prompt template with the code-built user prompt.

## R7: Category Config Design

**Decision**: Add `analysis.categories` as an array of `{id, description}` objects in `spectra.config.json`. String-based IDs (not enum).

**Rationale**: 
- Current `BehaviorCategory` enum has 5 values (HappyPath, Negative, EdgeCase, Security, Performance)
- Config categories are string-based to support user-defined values without code changes
- The enum remains for backward compatibility with existing test file deserialization
- `IdentifiedBehavior.ParseCategory()` will be extended to check config categories first, falling back to enum mapping
- Default 6 categories created by `spectra init`: positive-path, negative-path, edge-case, boundary, error-handling, security

## R8: Test Update Template Mapping

**Decision**: The `test-update.md` template maps to the AI-powered rewrite step in `UpdateHandler`, not to `TestClassifier`.

**Rationale**: `TestClassifier.cs` is a local algorithm (Jaccard similarity) with zero AI prompts. The update flow in `UpdateHandler` calls CopilotGenerationAgent for proposed rewrites when tests are classified as OUTDATED. The test-update template provides the reasoning framework for that AI call.

**Current update flow**:
1. `TestClassifier.Classify()` → local algorithm → classification enum
2. For OUTDATED tests → AI call to propose changes (this is where the template applies)
3. User reviews proposed changes in interactive mode

## R9: HTML Comment Stripping

**Decision**: Strip `<!-- ... -->` HTML comments from template bodies before sending to AI. Single regex: `<!--[\s\S]*?-->`.

**Rationale**: Templates use HTML comments for user-facing documentation (explaining patterns, customization tips). These should not be sent to the AI as they waste tokens and may confuse the model. Stripping is done in `PlaceholderResolver.Resolve()` after placeholder substitution.
