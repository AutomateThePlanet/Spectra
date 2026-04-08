# Implementation Plan: Docs Index Progress, SKILL Integration & Coverage Dashboard Fix

**Branch**: `024-docs-skill-coverage-fix` | **Date**: 2026-04-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/024-docs-skill-coverage-fix/spec.md`

## Summary

Three related issues surfaced when running SPECTRA through VS Code Copilot Chat SKILLs: (1) `spectra docs index` blocks in SKILL flows because it has no result/progress files and auto-triggers interactive criteria extraction, (2) remaining "requirements" terminology that should be "acceptance criteria", and (3) a broken dashboard Coverage tab. The fix adds result/progress file writing to `DocsIndexHandler` (replicating the `GenerateHandler` pattern), creates a dedicated `spectra-docs` SKILL, completes the terminology rename, and fixes dashboard JavaScript field references.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json, GitHub Copilot SDK
**Storage**: File system (`.spectra-result.json`, `.spectra-progress.html`, `_criteria_index.yaml`)
**Testing**: xUnit (1280+ existing tests across 3 test projects)
**Target Platform**: Windows, macOS, Linux (cross-platform CLI)
**Project Type**: CLI tool + MCP server
**Performance Goals**: N/A (no performance-critical changes)
**Constraints**: Must maintain backward compatibility for `--extract-requirements` alias, `requirements` YAML field, `requirements_file` config key
**Scale/Scope**: ~15 files modified, 1 new embedded resource file, ~15 new tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All changes are file-based (result JSON, progress HTML, SKILL markdown, config) |
| II. Deterministic Execution | PASS | No state machine changes; result files are deterministic outputs |
| III. Orchestrator-Agnostic Design | PASS | SKILL files work with any Copilot Chat agent; result files are plain JSON |
| IV. CLI-First Interface | PASS | All functionality exposed via CLI flags (`--skip-criteria`, `--no-interaction`); SKILL is a wrapper |
| V. Simplicity (YAGNI) | PASS | Reusing existing `ProgressPageWriter` and `FlushWriteFile` patterns; no new abstractions |

No violations. All gates pass.

## Project Structure

### Documentation (this feature)

```text
specs/024-docs-skill-coverage-fix/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── Docs/
│   │   │   ├── DocsIndexHandler.cs          # MODIFY: Add result/progress writing, --skip-criteria, fix strings
│   │   │   └── DocsIndexCommand.cs          # MODIFY: Add --skip-criteria option
│   │   ├── Analyze/
│   │   │   └── AnalyzeCommand.cs            # VERIFY: --extract-requirements alias preserved
│   │   └── Init/
│   │       └── InitHandler.cs               # MODIFY: Add spectra-docs SKILL to init flow
│   ├── Agent/
│   │   └── Copilot/
│   │       └── RequirementsExtractor.cs     # MODIFY: Fix user-facing strings
│   ├── Skills/
│   │   ├── Content/
│   │   │   ├── Skills/
│   │   │   │   └── spectra-docs.md          # CREATE: New SKILL file (embedded resource)
│   │   │   └── Agents/
│   │   │       └── spectra-generation.agent.md  # MODIFY: Update docs index flow
│   │   ├── SkillContent.cs                  # MODIFY: Add DocsIndex property
│   │   └── SkillsManifest.cs                # MODIFY: Bump count 8→9
│   ├── Results/
│   │   └── DocsIndexResult.cs               # MODIFY: Add new fields
│   ├── Progress/
│   │   └── ProgressPageWriter.cs            # MODIFY: Support docs-index status rendering
│   └── Dashboard/
│       └── DataCollector.cs                 # MODIFY: Null-safe coverage defaults
├── Spectra.Core/
│   ├── Parsing/
│   │   └── AcceptanceCriteriaParser.cs      # MODIFY: Add _requirements.yaml → .bak rename
│   └── Models/
│       └── Config/
│           └── CoverageConfig.cs            # VERIFY: requirements_file fallback works
dashboard-site/
├── scripts/
│   └── app.js                               # MODIFY: Fix "requirements" help text
└── index.html                               # VERIFY: Labels already correct

tests/
├── Spectra.CLI.Tests/
│   └── Commands/
│       └── DocsIndexHandlerTests.cs         # MODIFY: Add result/progress/skip-criteria tests
└── Spectra.Core.Tests/
    └── Parsing/
        └── AcceptanceCriteriaParserTests.cs # MODIFY: Add migration tests
```

**Structure Decision**: All changes fit within the existing project structure. No new projects or directories needed (except the embedded resource file for the new SKILL).

## Implementation Approach

### Part C: Terminology Rename (do first — unblocks everything)

**C1: Fix user-facing strings in CLI**

Files with remaining "requirement" strings to fix:
- `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs`: "Extracting requirements from documentation..." → "Extracting acceptance criteria from documentation..."
- `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs`: "Extracted {n} requirement(s)" → "Extracted {n} acceptance criteria"
- `dashboard-site/scripts/app.js` line ~1386: Help text references "requirements field" → "criteria field"

**Exclude from rename** (intentional backward compat):
- `--extract-requirements` hidden alias in `AnalyzeCommand.cs`
- `requirements` YAML key fallback in `AcceptanceCriteriaParser.cs`
- `RequirementsFile` config property (read fallback)
- Class names (`RequirementsExtractor`, `RequirementsWriter`, `RequirementsParser`) — renaming these would be a larger refactor; the spec says only user-facing strings

**C2: Auto-rename `_requirements.yaml` in criteria reader**

In `AcceptanceCriteriaParser.cs` (or the `CriteriaIndexReader`), when attempting to read criteria:
1. Check if `_criteria_index.yaml` exists → use it
2. If not, check if `_requirements.yaml` exists → rename to `_requirements.yaml.bak`, log message
3. Return empty/null criteria (fresh `_criteria_index.yaml` will be created by next extraction run)

### Part D: Dashboard Coverage Fix

**D1: Fix help text in app.js**

The dashboard JS already uses `acceptance_criteria` field names correctly (confirmed by research). Only the empty-state help text at line ~1386 references "requirements". Fix that one string.

**D2: Null-safe coverage defaults in DataCollector**

Ensure `DataCollector.CollectAsync()` never produces null sections. If criteria data is missing, provide zero-state:
```csharp
AcceptanceCriteria = criteriaCoverage ?? new AcceptanceCriteriaSectionData { Covered = 0, Total = 0, Percentage = 0, HasCriteriaFile = false, Details = [] }
```

### Part A: DocsIndexHandler Result & Progress

**A1: Extend `DocsIndexResult` model**

Add fields to match the spec's result schema:
- `DocumentsNew` (int)
- `DocumentsChanged` (int)  
- `DocumentsUnchanged` (int)
- `CriteriaExtracted` (int, nullable)
- `CriteriaFile` (string, nullable)

**A2: Add result/progress file writing to DocsIndexHandler**

Replicate the `GenerateHandler` pattern:
1. Delete stale `.spectra-result.json` and `.spectra-progress.html` at start
2. Write in-progress result with status `"scanning"` → `"indexing"` → `"extracting-criteria"`
3. Call `ProgressPageWriter.WriteProgressPage()` at each phase transition
4. Write final result with status `"completed"` or `"failed"`
5. Use `FlushWriteFile` for Windows NTFS reliability

**A3: Extend `ProgressPageWriter` for docs-index**

The existing `ProgressPageWriter.WriteProgressPage()` parses JSON `status` field and renders phase-appropriate HTML. Current phases: `analyzing`, `analyzed`, `generating`, `completed`, `failed`.

Add handling for docs-index statuses: `scanning`, `indexing`, `extracting-criteria`. The writer will render:
- Phase stepper: Scanning → Indexing → Extracting Criteria → Completed
- Summary cards: documents scanned, indexed, skipped, criteria extracted
- Same auto-refresh and `vscode://file/` link patterns

**A4: Add `--skip-criteria` flag**

In `DocsIndexCommand.cs`, add `--skip-criteria` option. Pass through to `DocsIndexHandler`. When set, skip the `TryExtractRequirementsAsync()` call.

**A5: Pass `--no-interaction` to criteria extraction**

The `TryExtractRequirementsAsync()` method doesn't currently receive the `noInteraction` flag. Thread it through so criteria extraction runs non-interactively when the parent command is non-interactive.

### Part B: New SKILL — `spectra-docs`

**B1: Create embedded SKILL file**

Create `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md` with the structured tool-call-sequence format matching existing SKILL patterns. Include:
- `show preview .spectra-progress.html` step
- `runInTerminal: spectra docs index --no-interaction --output-format json --verbosity quiet`
- `awaitTerminal`
- `readFile .spectra-result.json`
- Result presentation instructions
- Next step suggestions

**B2: Update SkillContent.cs**

Add `DocsIndex` static property pointing to the new embedded resource.

**B3: Update SkillsManifest**

The manifest auto-discovers embedded resources, so no manual count bump needed — just ensure the `.md` file is set as an embedded resource in the `.csproj`.

**B4: Update InitHandler**

The `CreateBundledSkillFilesAsync` method iterates `SkillContent.All` which auto-includes new SKILLs. Verify it works.

**B5: Update generation agent prompt**

In `spectra-generation.agent.md`, update the docs index section to reference progress page and `--no-interaction`.
