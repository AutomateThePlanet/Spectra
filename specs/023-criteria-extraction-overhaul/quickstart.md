# Quickstart: Acceptance Criteria Import & Extraction Overhaul

**Feature**: 023-criteria-extraction-overhaul

## Prerequisites

- .NET 8+ SDK
- Existing SPECTRA project with `spectra.config.json`
- Documentation files in `docs/` (for extraction)

## Build & Test

```bash
dotnet build
dotnet test
```

## Key Workflows

### 1. Extract criteria from documentation

```bash
# Incremental extraction (skips unchanged docs)
spectra ai analyze --extract-criteria

# Force full re-extraction
spectra ai analyze --extract-criteria --force

# Preview without writing
spectra ai analyze --extract-criteria --dry-run
```

Output: per-document `.criteria.yaml` files + `_criteria_index.yaml` in `docs/requirements/`.

### 2. Import from Jira/ADO CSV

```bash
# Import with AI splitting and RFC 2119 normalization
spectra ai analyze --import-criteria ./jira-export.csv

# Import without AI processing
spectra ai analyze --import-criteria ./jira-export.csv --skip-splitting
```

Output: criteria file in `docs/requirements/imported/`.

### 3. List and filter criteria

```bash
spectra ai analyze --list-criteria
spectra ai analyze --list-criteria --component checkout --priority high
```

### 4. Coverage analysis (uses all criteria sources)

```bash
spectra ai analyze --coverage
```

### 5. Generate tests (auto-loads related criteria)

```bash
spectra ai generate checkout
```

## Implementation Entry Points

| Area | Start File | What to do |
|------|-----------|------------|
| Core model | `src/Spectra.Core/Models/Coverage/RequirementDefinition.cs` | Rename to `AcceptanceCriterion`, add fields |
| Parser | `src/Spectra.Core/Parsing/RequirementsParser.cs` | Rename to `AcceptanceCriteriaParser` |
| Extractor | `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` | Rewrite as per-document `CriteriaExtractor` |
| CLI handler | `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` | Add new flags, rewrite extraction flow |
| Config | `src/Spectra.Core/Models/Config/CoverageConfig.cs` | Add `criteria_file`, `criteria_dir`, `criteria_import` |
| Coverage | `src/Spectra.Core/Coverage/RequirementsCoverageAnalyzer.cs` | Rename, read from criteria index |
| Dashboard | `dashboard-site/scripts/app.js` | Rename "Requirements" labels |
| Init | `src/Spectra.CLI/Commands/Init/InitHandler.cs` | Create criteria template files |
