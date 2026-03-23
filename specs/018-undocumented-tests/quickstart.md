# Quickstart: Undocumented Behavior Test Cases

## What This Feature Does

Enables creating properly structured test cases from undocumented behavior descriptions via the generation agent. Tests are created with special metadata (`grounding.verdict: "manual"`) that marks them as user-described and skips critic verification.

## Implementation Order

### Phase 1: Core Schema (no dependencies)

1. **Add `Manual` to `VerificationVerdict` enum** — `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs`
2. **Extend `GroundingFrontmatter`** with `source`, `created_by`, `note` fields — `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`
3. **Update `ToMetadata()` conversion** — handle manual verdict with placeholder values
4. **Update `TryParseVerdict()`** — accept "manual" string

### Phase 2: Critic Bypass (depends on Phase 1)

5. **Skip verification for manual tests** — `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` in `VerifyTestsAsync()` loop
6. **Ensure `CreateTestWithGrounding()` handles manual verdict** — preserve existing grounding metadata without overwriting

### Phase 3: Coverage Analysis (depends on Phase 1)

7. **Add undocumented fields to `DocumentationCoverage`** — `src/Spectra.Core/Models/Coverage/`
8. **Count empty source_refs in `DocumentationCoverageAnalyzer`** — `src/Spectra.Core/Coverage/DocumentationCoverageAnalyzer.cs`
9. **Surface in `UnifiedCoverageBuilder`** — `src/Spectra.Core/Coverage/UnifiedCoverageBuilder.cs`
10. **Update CLI report output** — `src/Spectra.CLI/Coverage/CoverageReportWriter.cs`

### Phase 4: Dashboard (depends on Phase 3)

11. **Add undocumented fields to `DocumentationSectionData`** — `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`
12. **Populate in `DataCollector.BuildCoverageSummaryAsync()`** — `src/Spectra.CLI/Dashboard/DataCollector.cs`
13. **Add orange CSS variables** — `src/Spectra.CLI/Dashboard/Templates/styles/main.css`
14. **Render orange category in coverage view** — `src/Spectra.CLI/Dashboard/Templates/scripts/app.js`
15. **Add filter toggle** — `src/Spectra.CLI/Dashboard/Templates/scripts/app.js`

### Phase 5: Agent Prompt (depends on Phase 1)

16. **Extend generation agent prompt** — `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`
17. **Add undocumented behavior flow** — clarifying questions, duplicate check, draft review
18. **Add "when NOT to use" guidance** — differentiate from doc-based generation

### Phase 6: Tests

19. Unit tests for `VerificationVerdict.Manual` and `GroundingFrontmatter` extensions
20. Unit tests for `DocumentationCoverageAnalyzer` undocumented metric
21. Integration tests for critic bypass with manual verdict
22. Dashboard rendering tests for orange category

## Key Files to Modify

| File | Change |
|------|--------|
| `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs` | Add `Manual` value |
| `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs` | Add `source`, `created_by`, `note` |
| `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | Skip critic for manual verdict |
| `src/Spectra.Core/Models/Coverage/DocumentationCoverage.cs` | Add undocumented count fields |
| `src/Spectra.Core/Coverage/DocumentationCoverageAnalyzer.cs` | Count empty source_refs |
| `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs` | Add undocumented to dashboard model |
| `src/Spectra.CLI/Dashboard/DataCollector.cs` | Populate undocumented metrics |
| `src/Spectra.CLI/Dashboard/Templates/styles/main.css` | Orange CSS variables |
| `src/Spectra.CLI/Dashboard/Templates/scripts/app.js` | Orange category rendering + filter |
| `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs` | Undocumented behavior flow |
| `src/Spectra.CLI/Coverage/CoverageReportWriter.cs` | Surface undocumented in reports |
