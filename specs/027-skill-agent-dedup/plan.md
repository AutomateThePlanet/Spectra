# Implementation Plan: SKILL/Agent Prompt Deduplication

**Branch**: `027-skill-agent-dedup` | **Date**: 2026-04-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/027-skill-agent-dedup/spec.md`

## Summary

Eliminate ~324 lines of duplicated CLI command blocks across 2 agent prompts and 8 SKILL files. Agents become routers that delegate CLI tasks to authoritative SKILL files. Fix SKILL inconsistencies (step format, result reading, wait instructions). Update help SKILL with missing command categories. Regenerate SHA-256 hashes in SkillsManifest.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (manifest serialization), embedded resources (SKILL/agent content)
**Storage**: File-based (.md content files as embedded resources, `.spectra/skills-manifest.json` for hashes)
**Testing**: xUnit (Spectra.CLI.Tests)
**Target Platform**: Windows/Linux/macOS CLI
**Project Type**: CLI tool
**Performance Goals**: N/A (text content refactoring only)
**Constraints**: Agent prompts must stay under line count limits (execution ≤200, generation ≤100)
**Scale/Scope**: 10 content files (2 agents + 8 SKILLs), 3 C# files (SkillContent, AgentContent, SkillsManifest)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ Pass | SKILL/agent files remain in Git, embedded as resources |
| II. Deterministic Execution | ✅ Pass | No MCP or state machine changes |
| III. Orchestrator-Agnostic | ✅ Pass | SKILLs work with any Copilot Chat model |
| IV. CLI-First Interface | ✅ Pass | All commands remain CLI-first; agents delegate to CLI SKILLs |
| V. Simplicity (YAGNI) | ✅ Pass | Reducing duplication is simplification, not new abstraction |

All gates pass. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/027-skill-agent-dedup/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (files to modify)

```text
src/Spectra.CLI/Skills/
├── Content/
│   ├── Agents/
│   │   ├── spectra-execution.agent.md    # Refactor: ~400→~180 lines
│   │   └── spectra-generation.agent.md   # Refactor: ~219→~90 lines
│   └── Skills/
│       ├── spectra-list.md               # Fix: Tool call N → Step N, terminalLastCommand → readFile
│       ├── spectra-init-profile.md       # Fix: Tool call N → Step N, terminalLastCommand → readFile, add flags
│       ├── spectra-validate.md           # Fix: Tool call N → Step N
│       ├── spectra-help.md               # Add: criteria and docs index sections
│       ├── spectra-docs.md               # Add: incremental vs force note
│       ├── spectra-coverage.md           # Add: "do NOTHING" instruction
│       ├── spectra-dashboard.md          # Add: "do NOTHING" instruction
│       ├── spectra-criteria.md           # Already has "do NOTHING" — no change needed
│       └── spectra-generate.md           # Already correct — no change needed
├── SkillsManifest.cs                     # No code changes (runtime hash computation)
├── SkillContent.cs                       # No code changes
├── AgentContent.cs                       # No code changes
└── SkillResourceLoader.cs                # No code changes
```

**Structure Decision**: No new files or directories. All changes are to existing embedded content files. The C# classes (SkillContent, AgentContent, SkillResourceLoader, SkillsManifest) require no code changes — they load content dynamically from embedded resources. Hash updates happen automatically at runtime since `spectra update-skills` computes hashes from the loaded content.

## Phase 0: Research

### No unknowns to resolve

All technologies and patterns are already established in the codebase:
- Embedded resource loading via `SkillResourceLoader`
- SHA-256 hash-based update detection via `SkillsManifestStore`
- SKILL step format conventions (already used by 5 of 8 SKILLs)
- Agent delegation pattern (new but straightforward — just text replacement)

No `research.md` needed.

## Phase 1: Design

### Data Model

No new entities, fields, or state transitions. The change is purely to content files. Existing entities remain unchanged:
- `SkillsManifest` (JSON: version + files hash dictionary) — no schema changes
- `SkillContent` / `AgentContent` (static string dictionaries) — no API changes

No `data-model.md` needed.

### Contracts

No external interface changes. The SKILL/agent files are consumed by Copilot Chat's SKILL resolution mechanism, which reads them from `.github/skills/` and `.github/agents/` directories. The file format (YAML frontmatter + markdown body) is unchanged.

No `contracts/` needed.

## Implementation Details

### Phase 1: SKILL Consistency Fixes

Fix the 5 SKILL files with format/pattern inconsistencies.

#### 1.1 `spectra-list.md`

**Changes**:
- Replace "Tool call N" with "Step N" format
- Replace `terminalLastCommand` with `readFile .spectra-result.json`
- Add `--no-interaction --output-format json --verbosity quiet` flags to CLI commands

#### 1.2 `spectra-init-profile.md`

**Changes**:
- Replace "Tool call N" with "Step N" format
- Replace `terminalLastCommand` with `readFile .spectra-result.json`
- Add `--no-interaction --output-format json --verbosity quiet` to CLI command

#### 1.3 `spectra-validate.md`

**Changes**:
- Replace "Tool call N" with "Step N" format (already uses readFile — just fix numbering)

#### 1.4 `spectra-help.md`

**Changes**:
- Add "Acceptance Criteria" section with extract, import, list, filter examples
- Add "Documentation Index" section with index and force-reindex examples

#### 1.5 `spectra-docs.md`

**Changes**:
- Add note after incremental command: "Incremental mode skips unchanged files (SHA-256 hash check). Use `--force` for a complete rebuild."

#### 1.6 `spectra-coverage.md`

**Changes**:
- Add "do NOTHING" instruction between runInTerminal and awaitTerminal steps

#### 1.7 `spectra-dashboard.md`

**Changes**:
- Add "do NOTHING" instruction between runInTerminal and awaitTerminal steps

### Phase 2: Refactor Agents

#### 2.1 Execution Agent (`spectra-execution.agent.md`)

**Target**: ~180 lines (from ~400)

**Content to KEEP** (execution-specific):
- Lines 1-15: YAML frontmatter
- Lines 17-56: Core execution workflow (steps 1-8) — rewritten concisely
- Lines 57-92: Presentation rules and test presentation format
- Lines 93-119: Result collection with status interpretation table and shortcuts
- Lines 120-131: Proactive tool usage (screenshots after FAIL, progress summaries)
- Lines 132-145: Bug logging (Azure DevOps MCP)
- Lines 146-167: Screenshot handling section
- Lines 169-176: Error handling
- Lines 177-195: Copilot Spaces documentation assistance
- Lines 197-267: Smart test selection (intent → saved selections → search → start)
- Lines 268-278: Ending a run

**Content to REMOVE** (replaced by delegation):
- Lines 282-297: Help table → "Follow `spectra-help` SKILL"
- Lines 300-314: Coverage CLI block → delegation
- Lines 318-328: Criteria CLI block → delegation
- Lines 332-355: Dashboard CLI block → delegation
- Lines 359-369: Document index CLI block → delegation
- Lines 373-382: Validate CLI block → delegation
- Lines 386-399: List/Show CLI block → delegation

**Content to REMOVE** (redundant warnings):
- Line 22: Long CRITICAL paragraph about CLI commands (covered by delegation table)
- Line 26: "NEVER use MCP tools" for CLI tasks (unnecessary with shorter prompt)
- Line 29: "NEVER use createFile, editFiles..." for reports (unnecessary)
- Line 302: "NEVER create files manually..." (unnecessary)
- Line 334: "NEVER use MCP tools for dashboard" (unnecessary)

**Content to KEEP** (essential warnings):
- Line 27: "NEVER use askQuestion" — real Copilot Chat limitation
- Line 28: "NEVER fabricate failure notes" — prevents hallucinated user comments

**New content**: Delegation table (~15 lines) at the end

#### 2.2 Generation Agent (`spectra-generation.agent.md`)

**Target**: ~90 lines (from ~219)

**Content to KEEP** (generation-specific):
- Lines 1-7: YAML frontmatter
- Lines 9-15: Introduction with CRITICAL and ALWAYS instructions
- Lines 35-97: Generate test cases (CLI flags table, analyze steps 1-4, generate steps 5-7)
- Lines 185-196: Update tests section

**Content to REMOVE** (replaced by delegation):
- Lines 17-33: Help table → "Follow `spectra-help` SKILL"
- Lines 100-110: Coverage analysis → delegation
- Lines 114-124: Extract acceptance criteria → delegation
- Lines 128-153: Dashboard → delegation
- Lines 155-164: Validate tests → delegation
- Lines 168-182: List tests / Show test → delegation
- Lines 199-218: Document index → delegation

**New content**: Delegation table (~15 lines) at the end

### Phase 3: Update Tests

No C# code changes needed — only embedded content changes. The existing hash-based update mechanism works automatically since hashes are computed from loaded content at runtime.

**New tests to add**:
1. Execution agent line count ≤ 200
2. Generation agent line count ≤ 100
3. Agent content does NOT contain duplicated CLI command blocks (specific command strings)
4. All SKILLs use "Step N" format (not "Tool call N")
5. All JSON-output SKILLs use `readFile .spectra-result.json` (not `terminalLastCommand`)
6. Help SKILL contains entries for all 8 command categories

### Phase 4: Documentation

Update `CLAUDE.md` and `PROJECT-KNOWLEDGE.md` with:
- Reduced agent line counts
- Delegation pattern description
- SKILL format conventions

## Implementation Order

1. **Phase 1**: Fix SKILL consistency (7 files) — safe, doesn't affect agent behavior
2. **Phase 2**: Refactor agents (2 files) — the core change
3. **Phase 3**: Add new tests — validate the refactoring
4. **Phase 4**: Update documentation — reflect the changes

## Post-Design Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ Pass | All content stays in Git as embedded resources |
| II. Deterministic Execution | ✅ Pass | No execution logic changes |
| III. Orchestrator-Agnostic | ✅ Pass | SKILLs work with any model/orchestrator |
| IV. CLI-First Interface | ✅ Pass | Agents delegate to CLI SKILLs |
| V. Simplicity (YAGNI) | ✅ Pass | Net reduction of ~330 lines, no new abstractions |

All gates pass.
