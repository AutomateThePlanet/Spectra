# Implementation Plan: Customizable Root Prompt Templates

**Branch**: `030-prompt-templates` | **Date**: 2026-04-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/030-prompt-templates/spec.md`

## Summary

Replace hardcoded AI prompts in 4 C# classes (BehaviorAnalyzer, CopilotGenerationAgent, CriteriaExtractor, CopilotCritic/CriticPromptBuilder) with a template-driven system. Templates are markdown files with YAML frontmatter and `{{placeholder}}` syntax, stored in `.spectra/prompts/`, with built-in defaults as embedded resources. Add configurable behavior categories in `spectra.config.json`. New `spectra prompts` CLI command for template management. New `spectra-prompts` SKILL for Copilot Chat.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (terminal UX), System.Text.Json (serialization), GitHub Copilot SDK (AI runtime)
**Storage**: File-based (`.spectra/prompts/*.md`, `spectra.config.json`, `.spectra/skills-manifest.json`)
**Testing**: xUnit (462 Core + 466 CLI + 351 MCP = 1279+ tests)
**Target Platform**: Cross-platform .NET CLI tool
**Project Type**: CLI tool with MCP server
**Performance Goals**: Template loading <10ms (file read + parse). No impact on AI call latency.
**Constraints**: Templates must be backward-compatible; missing templates fall back to built-in defaults silently.
**Scale/Scope**: 5 templates, 6 default categories, 4 CLI subcommands, 1 SKILL

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Templates stored in `.spectra/prompts/` (git-tracked). Categories in `spectra.config.json` (git-tracked). |
| II. Deterministic Execution | PASS | Same template + same inputs = same resolved prompt. No randomness. |
| III. Orchestrator-Agnostic Design | PASS | Templates are provider-agnostic markdown. Any LLM can process the resolved prompt. |
| IV. CLI-First Interface | PASS | `spectra prompts list/show/reset/validate` commands. JSON output support. CI-friendly exit codes. |
| V. Simplicity (YAGNI) | PASS | Simple `{{var}}`/`{{#if}}`/`{{#each}}` syntax. No inheritance, no nesting, no expression language. Custom PlaceholderResolver (~100 lines) vs. pulling in a Handlebars/Mustache NuGet dependency. |

**Quality Gates**: All existing gates remain. New `spectra validate` warning for unknown category values. Template validation via `spectra prompts validate`.

## Project Structure

### Documentation (this feature)

```text
specs/030-prompt-templates/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── cli-commands.md  # CLI contract for spectra prompts
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/
│       ├── PromptTemplate.cs              # NEW: PromptTemplate, PlaceholderSpec records
│       └── Config/
│           ├── AnalysisConfig.cs           # NEW: AnalysisConfig with Categories list
│           ├── CategoryDefinition.cs       # NEW: CategoryDefinition record (Id, Description)
│           └── SpectraConfig.cs            # MOD: Add Analysis property
├── Spectra.CLI/
│   ├── Prompts/                           # NEW directory
│   │   ├── PromptTemplateLoader.cs        # Load, parse, resolve templates
│   │   ├── PromptTemplateParser.cs        # Parse markdown + YAML frontmatter
│   │   ├── PlaceholderResolver.cs         # {{var}}, {{#if}}, {{#each}} resolution
│   │   └── BuiltInTemplates.cs            # Embedded default templates (static class)
│   ├── Commands/Prompts/                  # NEW directory
│   │   ├── PromptsCommand.cs              # spectra prompts root command
│   │   ├── PromptsListHandler.cs          # list subcommand
│   │   ├── PromptsShowHandler.cs          # show subcommand
│   │   ├── PromptsResetHandler.cs         # reset subcommand
│   │   └── PromptsValidateHandler.cs      # validate subcommand
│   ├── Results/
│   │   └── PromptsListResult.cs           # NEW: JSON result model
│   ├── Skills/
│   │   ├── SkillContent.cs                # MOD: Add Prompts property
│   │   └── Content/Skills/
│   │       └── spectra-prompts.md         # NEW: 11th SKILL file
│   ├── Agent/
│   │   ├── Copilot/
│   │   │   ├── BehaviorAnalyzer.cs        # MOD: Use PromptTemplateLoader
│   │   │   ├── GenerationAgent.cs         # MOD: Use PromptTemplateLoader
│   │   │   └── CriteriaExtractor.cs       # MOD: Use PromptTemplateLoader
│   │   └── Critic/
│   │       └── CriticPromptBuilder.cs     # MOD: Use PromptTemplateLoader
│   └── Commands/
│       ├── Init/InitHandler.cs            # MOD: Create .spectra/prompts/ + categories
│       ├── UpdateSkills/UpdateSkillsHandler.cs  # MOD: Include templates in update
│       └── Validate/ValidateCommand.cs    # MOD: Warn on unknown categories

tests/
├── Spectra.Core.Tests/
│   └── Models/
│       └── PromptTemplateTests.cs         # NEW: Frontmatter parsing tests
├── Spectra.CLI.Tests/
│   ├── Prompts/                           # NEW directory
│   │   ├── PlaceholderResolverTests.cs    # Resolver unit tests
│   │   ├── PromptTemplateLoaderTests.cs   # Loader unit tests
│   │   └── BuiltInTemplatesTests.cs       # Built-in template validation
│   └── Commands/Prompts/                  # NEW directory
│       └── PromptsCommandTests.cs         # CLI command tests
```

**Structure Decision**: Follows existing Spectra patterns exactly. New `Prompts/` directory under CLI parallels `Session/`, `Skills/`, `Progress/`. New `Commands/Prompts/` parallels `Commands/Dashboard/`, `Commands/Docs/`. Built-in templates embedded as resources using the same `SkillResourceLoader` pattern.

## Complexity Tracking

No constitution violations. All designs follow existing patterns.

## Key Research Findings

### Finding 1: TestClassifier is NOT AI-powered

**Decision**: The `test-update.md` template maps to the **UpdateHandler's AI-powered flow** (which calls CopilotGenerationAgent for rewriting), not to `TestClassifier` which uses local Jaccard similarity. The template will be used when the update flow sends prompts to the AI for proposed changes, not for the local classification step.

**Rationale**: TestClassifier.cs (319 lines) uses Jaccard word-overlap similarity with thresholds (0.7 outdated, 0.3 orphaned). It has zero AI prompts. The update flow in `UpdateHandler` does call AI for proposed rewrites. The test-update template targets that AI call.

**Alternatives considered**: Remove the test-update template entirely. Rejected because the AI-powered rewrite step in the update flow still benefits from customization.

### Finding 2: CriticPromptBuilder is separate from CopilotCritic

**Decision**: The `critic-verification.md` template replaces prompts in `CriticPromptBuilder.cs`, not directly in `CopilotCritic` (which is the orchestrator calling the builder).

**Rationale**: `CriticPromptBuilder` has `BuildSystemPrompt()` (30-line system prompt) and `BuildUserPrompt()` (structured user prompt with test case + docs). The template replaces `BuildSystemPrompt()`. `BuildUserPrompt()` remains code-driven since it dynamically selects relevant documents and formats test case fields.

### Finding 3: Existing category enum needs expansion, not replacement

**Decision**: Keep the `BehaviorCategory` enum for backward compatibility but add config-driven categories that override the hardcoded list. The enum serves as a fallback when config is absent.

**Rationale**: `BehaviorCategory.cs` has 5 values (HappyPath, Negative, EdgeCase, Security, Performance). `IdentifiedBehavior.cs` maps string aliases to enum values. Config categories will be string-based (not enum) to allow user-defined values. The enum remains for deserialization of existing test files.

### Finding 4: Prompt signatures vary across classes

**Decision**: Each template maps to a specific method signature. The `PromptTemplateLoader` provides a generic `Resolve()` method; each AI class builds its own placeholder dictionary.

| Class | Method | Signature |
|-------|--------|-----------|
| BehaviorAnalyzer | `BuildAnalysisPrompt` | `(IReadOnlyList<SourceDocument>, string? focusArea)` → single prompt string |
| GenerationAgent | `BuildFullPrompt` | `(string userPrompt, int requestedCount, string? criteriaContext)` → single prompt string |
| CriteriaExtractor | `BuildExtractionPrompt` | `(string docPath, string content, string? component)` → single prompt string |
| CriteriaExtractor | `BuildSplitPrompt` | `(string rawText, string? sourceKey, string? component)` → single prompt string |
| CriticPromptBuilder | `BuildSystemPrompt` | `()` → system prompt string |

**Note**: CriteriaExtractor has TWO prompts (extraction + splitting). The `criteria-extraction.md` template covers the main extraction prompt. The split prompt is a minor utility (~19 lines) that can be added as a 6th template (`criteria-splitting.md`) or kept hardcoded. Decision: keep it hardcoded for v1 (YAGNI - it's 19 lines and rarely needs customization).

### Finding 5: Resource embedding follows SkillResourceLoader pattern

**Decision**: Embed default templates as `.md` resources in the `Spectra.CLI` assembly, loaded via a new `PromptResourceLoader` that follows the same pattern as `SkillResourceLoader`.

**Rationale**: `SkillResourceLoader.cs` (94 lines) loads embedded resources with prefix `Spectra.CLI.Skills.Content.Skills.` and caches them in a static dictionary. The same pattern works for prompt templates with prefix `Spectra.CLI.Prompts.Content.`.

### Finding 6: Template hash tracking extends SkillsManifest

**Decision**: Add prompt template paths to the existing `SkillsManifest` (`.spectra/skills-manifest.json`). No separate manifest file.

**Rationale**: `SkillsManifest` already stores `Files: Dictionary<string, string>` mapping paths to SHA-256 hashes. Prompt templates are just more entries. `UpdateSkillsHandler` already iterates over file dictionaries and calls `FileHasher.ComputeHash()`.

## Post-Design Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All templates and config in git-tracked files. No external storage. |
| II. Deterministic Execution | PASS | Template resolution is pure string substitution. No side effects, no randomness. |
| III. Orchestrator-Agnostic Design | PASS | Templates produce plain-text prompts consumable by any LLM provider. No provider-specific syntax. |
| IV. CLI-First Interface | PASS | Full CLI commands with JSON output, exit codes, --no-interaction. SKILL built on top of CLI. |
| V. Simplicity (YAGNI) | PASS | No inheritance, no expression language, no nesting. PlaceholderResolver ~100 lines. Reuses existing SkillsManifest/FileHasher. CriteriaExtractor split prompt kept hardcoded (19 lines, YAGNI). |

**No violations. Design is constitution-compliant.**
