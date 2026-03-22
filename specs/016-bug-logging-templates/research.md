# Research: 016 Bug Logging, Templates, and Execution Agent Integration

**Date**: 2026-03-22
**Branch**: `016-bug-logging-templates`

## R1: Config Extension Pattern

**Decision**: Add `BugTrackingConfig` class following the `CoverageConfig` pattern
**Rationale**: All config sections in SPECTRA use the same pattern — a dedicated C# record/class with `[JsonPropertyName]` attributes, added as a property to `SpectraConfig`. The config template JSON at `src/Spectra.CLI/Templates/spectra.config.json` includes defaults.
**Alternatives considered**:
- Inline in existing config section → rejected, `bug_tracking` is a distinct concern
- External config file → rejected, contradicts single-config-file pattern

## R2: Frontmatter Field Addition Pattern

**Decision**: Add `bugs` field to `TestCaseFrontmatter`, `TestCase`, and `TestIndexEntry` following the `automated_by`/`requirements` pattern
**Rationale**: Both `automated_by` and `requirements` are `List<string>` in frontmatter and `IReadOnlyList<string>` in models. `FrontmatterUpdater` already handles YAML list writeback with regex-based insertion. Adding a `UpdateBugs()` method mirrors `UpdateAutomatedBy()`.
**Alternatives considered**:
- Store bugs only in execution notes → rejected, loses traceability across runs
- Use `related_work_items` field → rejected, that field exists but has different semantics (manual links vs. auto-populated bug references)

## R3: Template Variable Substitution

**Decision**: Simple `{{variable}}` string replacement in `BugReportTemplateEngine` service
**Rationale**: The template is a Markdown file with named placeholders. No conditionals, loops, or nesting needed — simple `string.Replace()` for each known variable. Unrecognized variables are left as-is (spec requirement).
**Alternatives considered**:
- Handlebars/Mustache library → rejected, over-engineered for flat variable replacement (Constitution Principle V: YAGNI)
- Razor templates → rejected, heavy dependency for a simple use case

## R4: Local Bug File Storage

**Decision**: Save to `reports/{run_id}/bugs/BUG-{test_id}.md` with attachments copied to `reports/{run_id}/bugs/attachments/`
**Rationale**: Follows existing report output conventions. `ReportWriter` already creates `reports/{run_id}/` directories. Bug files are standalone Markdown that can be committed to git.
**Alternatives considered**:
- Separate `bugs/` top-level directory → rejected, breaks run-scoped organization
- JSON format → rejected, Markdown is human-readable and matches the template format

## R5: Agent Prompt Bug Logging Section

**Decision**: Update existing bug logging section in `spectra-execution.agent.md` with template-aware flow, tracker auto-detection, duplicate checking, and `bugs` frontmatter writeback
**Rationale**: The agent prompt already has a basic bug logging section (lines 101-113). This feature expands it with the full workflow specified in the feature spec.
**Alternatives considered**:
- Separate agent file → rejected, the execution agent is one cohesive prompt
- MCP tool for bug creation → rejected for now, bug creation goes through external tracker MCPs; the agent orchestrates

## R6: Init Command Extension

**Decision**: Add template file creation and `bug_tracking` config section in `InitHandler`
**Rationale**: `InitHandler.HandleAsync()` follows a sequential flow — create dirs, create config, install files. Adding template creation fits naturally after directory creation. The config template JSON already includes all sections.
**Alternatives considered**:
- Separate `spectra bug init` command → rejected, init is the single setup entry point
- Post-init migration command → rejected, existing projects can re-run `spectra init` or manually add the section

## R7: Duplicate Bug Detection Strategy

**Decision**: Check `bugs` frontmatter field first (local), then query tracker if available
**Rationale**: The `bugs` field in test case frontmatter provides instant local duplicate detection without network calls. For tracker-connected setups, the agent can additionally search for open issues matching the test ID. Two-tier approach: local-first, then remote.
**Alternatives considered**:
- Tracker-only detection → rejected, doesn't work in local-only mode
- Local-only detection → rejected, misses bugs created outside Spectra
