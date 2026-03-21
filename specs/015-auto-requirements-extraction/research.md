# Research: Automatic Requirements Extraction

**Feature**: 015-auto-requirements-extraction
**Date**: 2026-03-21

## Decision 1: AI Extraction Approach

**Decision**: Use the existing CopilotGenerationAgent pattern with a dedicated extraction prompt. The AI receives full document content via `SourceDocumentLoader` and returns structured requirement objects.

**Rationale**: The generation workflow already loads documents, invokes the Copilot SDK, and parses structured output. Requirements extraction follows the same pattern — send document content to the AI with instructions to identify testable behaviors and return structured data. No new AI infrastructure needed.

**Alternatives considered**:
- Rule-based extraction (regex/NLP): Too brittle for natural language documents; would miss implicit requirements and produce noisy results.
- Separate extraction service: Violates YAGNI — the Copilot SDK already handles all AI operations.
- Extraction as a Copilot SDK tool: Adds unnecessary indirection; a single prompt with document content is sufficient.

## Decision 2: Duplicate Detection Strategy

**Decision**: Use case-insensitive normalized string comparison on requirement titles. Normalize by lowercasing, trimming whitespace, and removing punctuation. Consider two titles duplicates if they match after normalization or if one is a substring of the other (with a minimum length threshold to avoid false positives on short common phrases).

**Rationale**: Requirements titles from the same or similar documents will use consistent terminology. Full fuzzy matching (Levenshtein, cosine similarity) adds complexity without proportional benefit. The AI model itself can be prompted to avoid extracting requirements that overlap with existing ones, providing a first-pass filter before the string comparison.

**Alternatives considered**:
- Fuzzy string matching (Levenshtein distance): Adds a dependency and tuning complexity. Substring + normalization covers the primary case (same behavior described slightly differently).
- AI-based dedup (send existing + new to model): Expensive — doubles API calls. Better to use prompt context to prevent duplicates at extraction time.
- Semantic embedding similarity: Requires embedding infrastructure not present in the project.

## Decision 3: Integration Point in Generation Flow

**Decision**: Insert requirements extraction in `GenerateHandler` after document loading (step 4 in current flow) and before AI test generation (step 10). Extracted requirements are passed to the generation prompt so the AI can reference them when populating `requirements` fields in generated tests.

**Rationale**: Requirements must exist before test generation so that generated tests can reference requirement IDs. Loading documents first is a prerequisite for extraction. The extraction step produces `RequirementDefinition[]` which is merged into the existing file, then the full requirements list (existing + new) is passed to the generation prompt.

**Alternatives considered**:
- Post-generation extraction: Would require a second pass to link tests to requirements. More complex, slower.
- Parallel extraction and generation: The generation prompt needs requirement IDs to populate test frontmatter, so extraction must complete first.

## Decision 4: Requirements Writer Implementation

**Decision**: Create `RequirementsWriter` in `Spectra.Core/Parsing/` alongside `RequirementsParser`. Uses YamlDotNet serializer with the same naming convention (`UnderscoreNamingConvention`). Reads existing file first, merges new entries, writes atomically.

**Rationale**: Keeps read/write logic co-located. The existing `RequirementsParser` uses YamlDotNet with `RequirementsDocument` model, so the writer uses the same serialization pipeline. Atomic write (write to temp file, then rename) prevents corruption on failure.

**Alternatives considered**:
- Append-only YAML: YAML doesn't support clean appending (need to maintain the `requirements:` root key). Full read-merge-write is required.
- JSON instead of YAML: Spec explicitly requires YAML for compatibility with existing `RequirementsParser`.

## Decision 5: Standalone Command Placement

**Decision**: Add `--extract-requirements` flag to `spectra ai analyze` command rather than creating a new top-level command. This keeps the analyze command as the home for all coverage-related analysis operations.

**Rationale**: The spec says "spectra ai analyze --extract-requirements". This aligns with the existing `--coverage` and `--auto-link` flags on the same command. The analyze handler already loads config and has access to document paths. Adding a new handler method keeps the code organized without a new command registration.

**Alternatives considered**:
- New `spectra ai extract` command: Creates a new command tree entry. The analyze command already handles coverage operations, so this fragments related functionality.
- New `spectra requirements extract` command: Too many command levels. The `ai` subcommand tree is the right home for AI-powered operations.

## Decision 6: Interactive Review UX

**Decision**: Create `RequirementsReviewer` following the same pattern as `TestReviewer`. Display extracted requirements in a table using Spectre.Console, allowing bulk accept/reject and individual editing. Reuse `ReviewPresenter` patterns for consistent UX.

**Rationale**: The existing test review flow (accept/reject/edit per item, bulk operations) maps directly to requirements review. Users already know this interaction pattern. The reviewer produces a filtered list of approved requirements for writing.

**Alternatives considered**:
- Simple yes/no confirmation: Doesn't allow editing individual requirements or rejecting specific ones. Too coarse.
- Full editor experience (vim-like): Over-engineered for reviewing 5-20 requirements at a time.

## Decision 7: Priority Inference

**Decision**: Priority inference is performed by the AI model as part of the extraction prompt. The prompt instructs the model to assign priorities based on RFC 2119 keywords (MUST/SHALL = high, SHOULD = medium, MAY = low) and context. This is simpler than post-processing with keyword scanning.

**Rationale**: The AI model already reads the full document content. It can identify priority signals in context (e.g., "critical path", "optional feature") better than keyword regex. The prompt defines the mapping explicitly so output is consistent.

**Alternatives considered**:
- Post-extraction keyword scan: Loses context — a "must" in a code example isn't the same as "MUST" in a requirement statement. AI handles this naturally.
- User-assigned priorities: Adds friction to the extraction workflow. Users can edit priorities during interactive review if needed.

## Decision 8: Document Scope for Standalone Extraction

**Decision**: Standalone extraction (`--extract-requirements`) scans all documents matching the existing `config.Source` include/exclude patterns — the same document set used by `spectra ai generate`. Uses `SourceDocumentLoader.LoadAllAsync()`.

**Rationale**: Reuses existing document discovery logic. The `Source` config already defines which files are documentation vs. other content. No new configuration needed.

**Alternatives considered**:
- Separate `extraction_patterns` config: Adds config complexity. Documents relevant for test generation are the same ones containing requirements.
- Scan only docs/ directory: Too restrictive — some projects have docs in other locations configured via Source settings.
