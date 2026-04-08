# Research: Acceptance Criteria Import & Extraction Overhaul

**Feature**: 023-criteria-extraction-overhaul  
**Date**: 2026-04-07

## R1: Current Extraction Architecture & Truncation Problem

**Decision**: Replace single-prompt all-documents extraction with per-document iterative extraction.

**Rationale**: The current `RequirementsExtractor.ExtractAsync()` concatenates all documents into a single AI prompt via `BuildExtractionPromptAsync()`. For large doc sets, the AI response exceeds output token limits, producing truncated JSON. The current mitigations are fragile:
- `ParseResponse()` attempts partial JSON recovery by finding the last complete `}` and appending `]`
- The prompt includes "Keep responses under 50 requirements" which artificially limits results
- Two layers of `Task.WhenAny` hard timeouts (2 minutes each) in both `RequirementsExtractor` and `AnalyzeHandler`

Per-document extraction eliminates all three problems: each document produces a small, bounded response (typically 5-30 criteria), no artificial limits needed, and standard per-call cancellation tokens suffice.

**Alternatives considered**:
- Batching documents in groups of 5-10: Still risks truncation for very large documents; adds batch management complexity
- Streaming AI responses: Copilot SDK doesn't support streaming JSON; would require custom parser
- Post-processing split: Still sends too much context; doesn't solve the root cause

## R2: CSV Import Library Selection

**Decision**: Use CsvHelper (NuGet package `CsvHelper`) for CSV parsing.

**Rationale**: CsvHelper is the de facto standard for CSV parsing in .NET. It handles quoted fields, custom delimiters, header mapping, and type conversion. The column auto-detection requirement (mapping Jira/ADO column names to SPECTRA fields) is easily implemented via CsvHelper's `ClassMap` with fallback header matching.

**Alternatives considered**:
- Manual CSV parsing with `string.Split()`: Fragile for quoted fields containing commas, newlines
- `Microsoft.VisualBasic.FileIO.TextFieldParser`: Legacy API, limited mapping capabilities
- `Sylvan.Data.Csv`: Faster but less ecosystem adoption; CsvHelper's mapping features better suit our column auto-detection needs

## R3: Per-Document File Organization

**Decision**: Store criteria in `docs/requirements/` with `{doc-name}.criteria.yaml` naming and `imported/` subdirectory for external imports.

**Rationale**: Mirrors the existing `docs/requirements/` directory already created by `spectra init`. Using the source document's filename as the criteria filename creates a natural 1:1 mapping. The `imported/` subdirectory separates external criteria from extracted ones, enabling clear source-type attribution.

**Alternatives considered**:
- Flat directory with all criteria files: Mixes extracted and imported, harder to identify source type
- Separate `docs/criteria/` directory: Breaking change from existing `docs/requirements/` path without benefit
- Criteria embedded in doc frontmatter: Would require modifying user documentation files

## R4: Master Index Design

**Decision**: YAML master index (`_criteria_index.yaml`) with per-source entries containing SHA-256 hashes.

**Rationale**: Follows the same pattern as `docs/_index.md` (spec 010) for document indexing. SHA-256 hashing enables incremental extraction by comparing stored hashes against current file hashes. The index structure uses `sources[]` array with each entry pointing to a criteria file, its source document, hash, and metadata.

**Alternatives considered**:
- Rebuilding from criteria files on every run: Slower for large projects, no incremental support
- SQLite database: Over-engineered for a file-based tool; breaks principle I (GitHub as source of truth)
- JSON index: YAML is consistent with criteria files and existing SPECTRA patterns

## R5: ID Generation Strategy

**Decision**: Three-tier ID generation based on source:
1. **Extracted from docs**: `AC-{COMPONENT}-{NNN}` (e.g., `AC-CHECKOUT-001`)
2. **Imported with source key**: `AC-{SOURCE_KEY}-{N}` (e.g., `AC-PROJ-1234-1`)
3. **Imported without ID/source**: `AC-{NNN}` sequential using configured prefix

**Rationale**: Extracted criteria use component-prefixed IDs for natural grouping. Imported criteria with source keys (Jira/ADO) preserve traceability to the external system. Fallback sequential IDs handle manual imports. The `AC-` prefix distinguishes from the old `REQ-` prefix, making the rename visible.

**Alternatives considered**:
- Global sequential IDs for all sources: Loses traceability to external systems
- UUIDs: Not human-readable; poor for frontmatter references
- Keep `REQ-` prefix: Inconsistent with terminology rename

## R6: Terminology Rename Scope

**Decision**: Full rename across codebase with `--extract-requirements` hidden alias. No file/config migration.

**Rationale**: Per clarification, `_requirements.yaml` is only used in demo projects. No production users need migration. The hidden alias provides a safety net for scripts/muscle memory. All class names, model properties, JSON property names, dashboard labels, SKILL content, and agent prompts are renamed.

**Files requiring rename** (from codebase research):
- **Models**: `RequirementDefinition.cs`, `RequirementsCoverage.cs`, `UnifiedCoverageReport.cs`, `CoverageSummaryData.cs`
- **Services**: `RequirementsParser.cs`, `RequirementsWriter.cs`, `RequirementsCoverageAnalyzer.cs`, `RequirementsExtractor.cs`
- **CLI**: `AnalyzeHandler.cs`, `AnalyzeCommand.cs`, `CoverageReportWriter.cs`, `AnalysisPresenter.cs`, `InitHandler.cs`
- **Config**: `CoverageConfig.cs` (`requirements_file` → `criteria_file`)
- **Dashboard**: `app.js` (lines 927, 954, 1381-1387, 1444-1458)
- **SKILLs**: `spectra-coverage.md`, `spectra-help.md`, both agent prompts
- **Tests**: All test files referencing old class/property names
- **Results**: `ExtractRequirementsResult.cs` → `ExtractCriteriaResult.cs`

## R7: AI Prompt Design for Per-Document Extraction

**Decision**: Single-document prompt with explicit RFC 2119 normalization instructions.

**Rationale**: Each prompt receives one document's text and returns a bounded JSON response with criteria. The prompt instructs the AI to:
1. Extract testable acceptance criteria as individual statements
2. Normalize informal language to RFC 2119 keywords (MUST, SHOULD, MAY)
3. Identify the source section heading for each criterion
4. Assign component name based on document context
5. Set priority from RFC 2119 keyword strength
6. Return a JSON array of criteria objects

No "keep under N" constraints needed. No partial JSON recovery needed.

## R8: Import AI Splitting Prompt

**Decision**: Separate AI prompt for splitting compound criteria during import.

**Rationale**: When importing from CSV/Jira, a single "Acceptance Criteria" field often contains a bullet list of multiple criteria. The AI splits this into individual entries AND normalizes to RFC 2119 in one pass. The `--skip-splitting` flag bypasses AI entirely for pre-formatted imports.

The prompt receives: one raw text field + source metadata. Returns: array of split, normalized criteria objects.

## R9: CsvHelper Column Auto-Detection

**Decision**: Use a priority-ordered column name mapping with case-insensitive matching.

**Rationale**: Different tools export with different column names. The mapping:
- `text` field: tries `text`, `summary`, `title`, `acceptance_criteria`, `acceptance criteria`, `description` (in order)
- `source` field: tries `source`, `key`, `id`, `work_item_id`, `work item id`
- `component` field: tries `component`, `area_path`, `area path`
- `priority` field: tries `priority`
- `tags` field: tries `tags`, `labels`

First match wins. If no `text`-equivalent column is found, the import fails with a clear error listing expected column names.

## R10: Test Frontmatter `criteria` Field

**Decision**: Add `criteria` as a new list field alongside existing `requirements` field. The `requirements` field remains for backward compatibility but is deprecated.

**Rationale**: Adding a new `criteria` field (rather than renaming `requirements` in frontmatter) avoids breaking existing test files. The generation prompt will produce `criteria` in new tests. Coverage analysis reads both fields. The `requirements` field is not removed — it just stops being populated in new tests.

This is the one place where we DON'T rename, because test files are user-owned artifacts that shouldn't be forcefully modified.
