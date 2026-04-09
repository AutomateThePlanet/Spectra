# Quickstart: SPECTRA Update SKILL + Documentation Sync

**Date**: 2026-04-10 | **Branch**: `029-update-skill-docs-sync`

## Implementation Steps

### Step 1: Create the SKILL embedded resource

Create `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` following the pattern of `spectra-docs.md` (long-running SKILL with 5-step flow). Use `tools: [{{READONLY_TOOLS}}]` frontmatter.

### Step 2: Add property to SkillContent.cs

Add `public static string Update => All["spectra-update"];` to `SkillContent.cs`. The resource loader auto-discovers the `.md` file.

### Step 3: Update agent delegation files

- `spectra-generation.agent.md`: Remove the inline "Update tests" section (lines 56-67). Add a delegation row: `| Update tests | spectra-update | spectra ai update --suite {suite} --no-interaction --output-format json --verbosity quiet |`
- `spectra-execution.agent.md`: Add delegation row: `| Update tests | spectra-update | spectra ai update --suite {suite} --no-interaction --output-format json --verbosity quiet |`

### Step 4: Extend UpdateResult model

Add `Success`, `TotalTests`, `TestsFlagged`, `FlaggedTests`, `Duration` fields. Add `FlaggedTestEntry` class.

### Step 5: Update UpdateHandler to populate new fields

Set the new fields when constructing the `UpdateResult` at completion.

### Step 6: Update tests

- Update SKILL count assertions from 9 to 10
- Add spectra-update content assertions
- Add agent delegation assertions

### Step 7: Update documentation

- PROJECT-KNOWLEDGE.md: SKILL count + table + completed specs
- CLAUDE.md: Recent Changes entry
- README.md: SKILL count
- CHANGELOG.md: New entry

## Verification

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Verify SKILL count
# Tests should assert 10 SKILLs in SkillContent.All
```

## Key Files

| File | Action |
|------|--------|
| `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` | CREATE |
| `src/Spectra.CLI/Skills/SkillContent.cs` | MODIFY (add property) |
| `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md` | MODIFY (delegation) |
| `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` | MODIFY (delegation) |
| `src/Spectra.CLI/Results/UpdateResult.cs` | MODIFY (add fields) |
| `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` | MODIFY (populate fields) |
| `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs` | MODIFY (update + new tests) |
| `PROJECT-KNOWLEDGE.md` | MODIFY (SKILL count, table) |
| `CLAUDE.md` | MODIFY (recent changes) |
| `README.md` | MODIFY (SKILL count) |
| `CHANGELOG.md` | MODIFY (new entry) |
