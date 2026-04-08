# Research: 024-docs-skill-coverage-fix

## R1: DocsIndexHandler Current State

**Decision**: Extend existing `DocsIndexHandler` with result/progress file writing rather than creating a new handler.

**Rationale**: The handler already has the correct structure (loads config, runs indexing, triggers criteria extraction, produces `DocsIndexResult`). It writes JSON to stdout via `JsonResultWriter.Write()` but not to files. Adding file writes follows the same `GenerateHandler` pattern — minimal code, proven approach.

**Alternatives considered**:
- Create a separate `DocsIndexSkillHandler` — rejected, unnecessary complexity
- Use middleware/decorator pattern for file writing — rejected, YAGNI

**Key findings**:
- `DocsIndexHandler` constructor takes `(VerbosityLevel, bool dryRun, OutputFormat)` — no `noInteraction` parameter currently
- `TryExtractRequirementsAsync()` auto-triggers after indexing; has 60s hard timeout
- `DocsIndexResult` model exists with fields: `DocumentsIndexed`, `DocumentsUpdated`, `IndexPath`
- No `.spectra-result.json` or `.spectra-progress.html` writing exists

## R2: GenerateHandler File Writing Pattern

**Decision**: Replicate the `GenerateHandler` pattern exactly for consistency.

**Rationale**: The pattern is proven (used in production since spec 023), handles Windows NTFS flush issues, and integrates with `ProgressPageWriter`.

**Pattern summary**:
1. `DeleteResultFile()` at start — removes stale `.spectra-result.json` and `.spectra-progress.html`
2. `WriteInProgressResultFile()` — writes JSON + calls `ProgressPageWriter.WriteProgressPage(path, json, isTerminal: false)`
3. `WriteResultFile()` — final write with `isTerminal: true`
4. `FlushWriteFile()` — uses `FileStream.Flush(true)` for OS-level sync
5. `ResultFileOptions` — camelCase, null-ignore, enum-as-string, indented

## R3: ProgressPageWriter Capabilities

**Decision**: Extend `ProgressPageWriter` to handle docs-index-specific status values.

**Rationale**: The writer already parses JSON `status` field to render phase-appropriate HTML. Adding new status values (`scanning`, `indexing`, `extracting-criteria`) is a straightforward addition to the existing switch/if-else logic.

**Key findings**:
- `WriteProgressPage(htmlPath, jsonData, isTerminal)` — single public method
- Renders phase stepper, status cards, summary cards, error section, file links
- Currently handles: `analyzing`, `analyzed`, `generating`, `completed`, `failed`
- Auto-refresh via `<meta http-equiv="refresh" content="2">` when not terminal
- Embeds JSON inline (no external fetch)

## R4: Dashboard Coverage Field Names

**Decision**: The dashboard JS already uses correct `acceptance_criteria` field names. Only one help text string needs fixing.

**Rationale**: Research confirmed the C# `CoverageSummaryData` model and JS both use `acceptance_criteria` as the section key. The field name mismatch theory from the spec was wrong — the rename was already done in spec 023.

**Key findings**:
- `CoverageSummaryData.AcceptanceCriteriaSectionData` serializes as `acceptance_criteria` ✓
- `app.js` reads `summary.acceptance_criteria` with `.covered`, `.total`, `.percentage`, `.has_criteria_file`, `.details` ✓
- Only issue: empty-state help text at line ~1386 says "Add a `requirements` field" — should say "criteria"
- Dashboard rendering likely breaks due to null/missing data, not field name mismatch

**Root cause update**: The dashboard coverage breakage is likely caused by `DataCollector` returning null for the criteria section when no `_criteria_index.yaml` exists, which causes a JS null-reference error when the dashboard tries to read `.covered` on a null object.

## R5: SKILL Infrastructure

**Decision**: Add new embedded resource file; the existing infrastructure auto-discovers it.

**Rationale**: `SkillResourceLoader.GetAllSkills()` scans embedded resources with prefix `Spectra.CLI.Skills.Content.Skills.`. Adding a new `.md` file with the correct build action is sufficient.

**Key findings**:
- 8 existing SKILLs loaded from embedded resources
- `SkillContent.All` dictionary auto-populated by `SkillResourceLoader`
- `InitHandler.CreateBundledSkillFilesAsync()` iterates `SkillContent.All` — new SKILL auto-included
- `SkillsManifest` tracks file hashes for update detection
- Need to add `DocsIndex` static property to `SkillContent.cs`
- Need to set build action to `EmbeddedResource` in `.csproj`

## R6: Terminology Rename Scope

**Decision**: Only fix user-facing strings. Do not rename classes/files (that's a larger refactor out of scope).

**Rationale**: The spec says "All user-facing CLI messages MUST use 'acceptance criteria'" — class names like `RequirementsExtractor` are internal and don't affect users.

**Strings to fix**:
- `RequirementsExtractor.cs`: "Extracting requirements from documentation..." → "Extracting acceptance criteria from documentation..."
- `RequirementsExtractor.cs`: "Extracted {n} requirement(s)" → "Extracted {n} acceptance criteria"
- `RequirementsExtractor.cs`: prompt text "Extract all testable behavioral requirements" → "Extract all testable acceptance criteria"
- `app.js` ~line 1386: "requirements field" → "criteria field"

**Intentionally preserved** (backward compat):
- `--extract-requirements` CLI alias
- `requirements` YAML key in `AcceptanceCriteriaParser`
- `RequirementsFile` config property
- Class/file names

## R7: Migration Strategy for `_requirements.yaml`

**Decision**: Simple rename to `.bak` in the criteria reader, no format conversion.

**Rationale**: The old `_requirements.yaml` format is not used anywhere beyond the demo project. A rename is simpler and safer than format conversion.

**Implementation point**: Centralize in the criteria reader (likely `CriteriaIndexReader` or `AcceptanceCriteriaParser`) so any command that reads criteria triggers the rename automatically.
