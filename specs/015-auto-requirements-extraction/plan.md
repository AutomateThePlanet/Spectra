# Implementation Plan: Automatic Requirements Extraction

**Branch**: `015-auto-requirements-extraction` | **Date**: 2026-03-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/015-auto-requirements-extraction/spec.md`

## Summary

Add AI-powered requirements extraction that identifies testable behaviors from project documentation, writes them to `_requirements.yaml` with unique IDs and priorities, and integrates into the test generation workflow so generated tests automatically link to extracted requirement IDs. Available as both a standalone analysis command (`--extract-requirements`) and as an automatic step during `spectra ai generate`.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK (AI extraction), YamlDotNet (YAML read/write), Spectre.Console (interactive review UX), System.CommandLine (CLI)
**Storage**: File-based YAML (`docs/requirements/_requirements.yaml`)
**Testing**: xUnit with structured results
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI tool
**Performance Goals**: Extraction adds <20% overhead to test generation time
**Constraints**: Must use existing Copilot SDK; no new AI dependencies
**Scale/Scope**: Handles projects with 10-100 documentation files, producing 10-500 requirements

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Requirements stored in `_requirements.yaml` in Git. No external storage. |
| II. Deterministic Execution | PASS | Same documents + same model = same requirements. ID allocation is deterministic (max existing + 1). Merge logic is deterministic (append-only, no reorder). |
| III. Orchestrator-Agnostic Design | PASS | Uses Copilot SDK with configurable providers. No orchestrator-specific code. |
| IV. CLI-First Interface | PASS | `--extract-requirements` flag on existing CLI command. Supports `--dry-run`. Non-interactive mode for CI. |
| V. Simplicity (YAGNI) | PASS | Reuses existing patterns (SourceDocumentLoader, RequirementsParser model, Copilot agent, ReviewPresenter). No new abstractions beyond what's needed. New code: RequirementsWriter, RequirementsExtractor, RequirementsReviewer. |

No violations. No complexity tracking needed.

## Project Structure

### Documentation (this feature)

```text
specs/015-auto-requirements-extraction/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research decisions
├── data-model.md        # Entity definitions
├── quickstart.md        # Usage guide
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── Analyze/
│   │   │   └── AnalyzeHandler.cs          # ADD: --extract-requirements flag + handler method
│   │   └── Generate/
│   │       └── GenerateHandler.cs         # MODIFY: insert extraction step before generation
│   ├── Agent/
│   │   └── Copilot/
│   │       └── RequirementsExtractor.cs   # NEW: AI extraction via Copilot SDK
│   └── Review/
│       └── RequirementsReviewer.cs        # NEW: interactive review for extracted requirements
├── Spectra.Core/
│   └── Parsing/
│       └── RequirementsWriter.cs          # NEW: YAML write + merge logic

tests/
├── Spectra.Core.Tests/
│   └── Parsing/
│       └── RequirementsWriterTests.cs     # NEW: merge, dedup, ID allocation tests
├── Spectra.CLI.Tests/
│   ├── Commands/
│   │   └── AnalyzeExtractRequirementsTests.cs  # NEW: standalone extraction tests
│   └── Agent/
│       └── RequirementsExtractorTests.cs  # NEW: AI extraction prompt/response tests
```

**Structure Decision**: Follows existing project structure. New files added to established directories (Parsing, Agent/Copilot, Review, Commands). No new projects or top-level directories.

## Design Decisions

### 1. RequirementsWriter (Spectra.Core/Parsing/)

Co-located with `RequirementsParser`. Responsibilities:
- Read existing YAML file (delegates to `RequirementsParser.ParseAsync()`)
- Merge new requirements: append to list, skip duplicates
- Duplicate detection: case-insensitive normalized title comparison + substring matching
- ID allocation: find max existing ID number, assign sequentially from max+1
- Write atomically: serialize to temp file, then rename
- Create parent directories if missing

### 2. RequirementsExtractor (Spectra.CLI/Agent/Copilot/)

Follows `CopilotGenerationAgent` pattern:
- Takes `SourceDocument[]` (full content) as input
- Builds extraction prompt with:
  - Instructions to identify testable behavioral requirements
  - Existing requirement titles (to avoid duplicates at AI level)
  - Priority assignment rules (RFC 2119 keywords)
  - Output format (structured JSON array of {title, source, priority})
- Invokes Copilot SDK via existing `AgentFactory`/`CopilotService`
- Parses AI response into `RequirementDefinition[]` (IDs assigned later by writer)
- Returns `ExtractionResult` with extracted, merged, and duplicate lists

### 3. RequirementsReviewer (Spectra.CLI/Review/)

Follows `TestReviewer` pattern for interactive mode:
- Displays extracted requirements in a Spectre.Console table
- Per-requirement actions: Accept / Reject / Edit title / Edit priority
- Bulk actions: Accept All / Reject All
- Returns filtered list of approved requirements
- Skipped entirely in non-interactive mode

### 4. GenerateHandler Integration

Insert after document loading (line ~110), before agent creation (line ~193):

```
1. Load documents (existing)
2. NEW: Load existing requirements from file
3. NEW: Extract requirements from loaded documents via RequirementsExtractor
4. NEW: Merge with existing requirements via RequirementsWriter
5. NEW: Interactive review (if interactive mode)
6. NEW: Write merged requirements (unless dry-run)
7. NEW: Pass full requirements list to generation prompt
8. Build prompt (existing — enhanced with requirements context)
9. Generate tests (existing — AI populates requirements field)
10. Write tests (existing)
```

### 5. AnalyzeHandler Integration

Add `--extract-requirements` option to `AnalyzeCommand`:
- When flag is set, load all documents via `SourceDocumentLoader`
- Run `RequirementsExtractor` against all documents
- Merge + write via `RequirementsWriter`
- Report results (new count, duplicate count, total)
- Supports `--dry-run` (show what would be extracted without writing)

### 6. Generation Prompt Enhancement

Update the generation prompt (in `GenerateHandler.BuildPrompt()` or equivalent) to:
- Include the full requirements list as context
- Instruct the AI to populate `requirements: [REQ-xxx]` in test frontmatter
- Map each generated test to the requirements it validates

## Post-Design Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All output goes to `_requirements.yaml` in repo. |
| II. Deterministic Execution | PASS | Same doc content + same model config = consistent extraction. ID allocation is deterministic. |
| III. Orchestrator-Agnostic | PASS | Uses Copilot SDK multi-provider. No orchestrator assumptions. |
| IV. CLI-First | PASS | `--extract-requirements` flag, `--dry-run` support, CI-friendly exit codes. |
| V. Simplicity | PASS | 3 new files (writer, extractor, reviewer), 2 modified handlers. No new abstractions, patterns, or dependencies. |

No violations. Design is clean.
