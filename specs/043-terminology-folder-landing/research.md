# Research: Terminology, Folder Rename & Landing Page

**Feature**: 043-terminology-folder-landing  
**Date**: 2026-04-12

## R-001: Default Output Directory — Scope of "tests/" References

**Decision**: Change default from `"tests/"` to `"test-cases/"` in all source code, config templates, and test fixtures.

**Findings**:

| File | Line(s) | Current Value | Change |
|------|---------|---------------|--------|
| `src/Spectra.Core/Models/Config/TestsConfig.cs` | 11 | `Dir = "tests/"` | `"test-cases/"` |
| `src/Spectra.CLI/Commands/Init/InitHandler.cs` | 31 | `TestsDir = "tests"` | `"test-cases"` |
| `src/Spectra.CLI/Templates/spectra.config.json` | 10 | `"dir": "tests/"` | `"test-cases/"` |
| `src/Spectra.CLI/Commands/Config/ConfigHandler.cs` | 197 | fallback `"tests/"` | `"test-cases/"` |
| `tests/Spectra.Core.Tests/Config/ConfigLoaderTests.cs` | 20,45,58,101,125,148,195,210 | `"dir": "tests/"` in test JSON | `"test-cases/"` |
| `tests/Spectra.CLI.Tests/Prompts/PromptTemplateLoaderTests.cs` | 146,169 | uses `new TestsConfig()` default | automatic via TestsConfig change |
| `.github/workflows/dashboard.yml.template` | 24,26 | `tests/**` path trigger | `test-cases/**` |
| `.github/workflows/deploy-dashboard.yml.template` | 24,26 | `tests/**` path trigger | `test-cases/**` |

**Alternatives considered**:
- `test-cases/` (chosen) — clearly distinguishes from `tests/` (xUnit), obvious content type
- `specs/` — collides with speckit specs directory
- `cases/` — too generic
- `manual-tests/` — wrong connotation, SPECTRA tests aren't exclusively manual

---

## R-002: SKILL and Agent File Locations

**Decision**: SKILLs and agents are bundled as embedded resources in the CLI, not in `.github/`.

**Findings**:
- `.github/skills/` and `.github/agents/` do NOT exist in the repo
- SKILLs: `src/Spectra.CLI/Skills/Content/Skills/*.md` (11 files)
- Agents: `src/Spectra.CLI/Skills/Content/Agents/*.agent.md` (2 files)
- These are embedded resources deployed via `spectra update-skills` to user repos

**Implication**: Terminology changes target the embedded .md files in `src/Spectra.CLI/Skills/Content/`, not `.github/` paths.

---

## R-003: Documentation Scope for Terminology Sweep

**Decision**: 28 docs files + README + PROJECT-KNOWLEDGE.md + CLAUDE.md need review.

**Findings**:
- Landing page: `docs/index.md`
- cli-vs-chat: `docs/analysis/cli-vs-chat-generation.md` (~4,800 words, 437 lines)
- `PROJECT-KNOWLEDGE.md` at repo root
- `README.md` at repo root
- 28 files in `docs/` directory

**Note**: Not all 28 docs files will need changes. Only files where bare "test" or "tests" refers to SPECTRA output need updating. Compound terms ("test run", "test suite", "test ID") stay unchanged.

---

## R-004: Demo Repository Locations

**Decision**: Update both demo repos directly (no migration logic).

**Findings**:
- Demo 1: `C:\SourceCode\Spectra_Demo\test_app_documentation`
- Demo 2: `C:\SourceCode\AutomateThePlanet_SystemTests`
- Both verified on disk
- After changes: run `spectra update-skills` in both to deploy updated SKILL/agent content

---

## R-005: Constitution Impact

**Decision**: Constitution Principle I must be updated — it explicitly references `tests/{suite}/`.

**Finding**: Line 30 of `.specify/memory/constitution.md`:
> Test cases as Markdown files with YAML frontmatter in `tests/{suite}/`

**Change**: Update to `test-cases/{suite}/`. This is a MINOR version bump (1.0.0 → 1.1.0) since it materially changes guidance.
