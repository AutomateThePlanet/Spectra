# Quickstart: Terminology, Folder Rename & Landing Page

**Feature**: 043-terminology-folder-landing  
**Date**: 2026-04-12

## Implementation Order

### Phase 1: Folder Rename (code changes + tests)

1. Update `TestsConfig.cs` default from `"tests/"` to `"test-cases/"`
2. Update `InitHandler.cs` constant from `"tests"` to `"test-cases"`
3. Update `Templates/spectra.config.json` dir value
4. Update `ConfigHandler.cs` fallback display string
5. Update test fixtures in `ConfigLoaderTests.cs`
6. Update GitHub workflow templates (`tests/**` → `test-cases/**`)
7. Run all tests — must be green before proceeding

### Phase 2: Terminology Sweep (docs + SKILLs + agents)

1. Update constitution (`tests/{suite}/` → `test-cases/{suite}/`)
2. Update all SKILL .md files in `src/Spectra.CLI/Skills/Content/Skills/`
3. Update both agent .md files in `src/Spectra.CLI/Skills/Content/Agents/`
4. Sweep all `docs/*.md` files for "test" → "test case" where referring to SPECTRA output
5. Update `PROJECT-KNOWLEDGE.md`, `README.md`, `CLAUDE.md`

### Phase 3+4: Landing Page + CLI-vs-Chat (content only)

1. Rewrite `docs/index.md` with value proposition content
2. Replace `docs/analysis/cli-vs-chat-generation.md` with 150-word version

### Post-Implementation

1. Update both demo repos (rename folder + config)
2. Run `spectra update-skills` in both demo repos

## Verification

```bash
# Phase 1 verification
dotnet test

# Phase 2 verification — no bare "test generation" without "case"
grep -r "test generation" docs/ --include="*.md" | grep -v "test case generation"

# Phase 4 verification — word count
wc -w docs/analysis/cli-vs-chat-generation.md
# Expected: < 200
```
