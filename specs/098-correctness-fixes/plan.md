# Implementation Plan: Correctness Fixes (Phase 6, Part 3)

**Branch**: `098-correctness-fixes` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/098-correctness-fixes/spec.md`

## Summary

Nine correctness fixes deferred from 085–097, no features:
- F1 a legacy or invalid `ai.critic` block never fails a command; its model is ignored with a notice (`sonnet` used);
- F2 the audit's action comes from the verdict (`ground` / `repair` / `review`); the generate resume routes each;
- F3 suite delete reports verdict files once;
- F4 the manifest saves the newest record (or every candidate when it can't tell);
- F5 a keep-baseline ingest never creates a baseline entry;
- F6 `id=` only as a whole attribute; image headings match GitHub anchors; 084 `{#id}` snapshots compare as cosmetic;
- F7 each test block in a group prompt shows its own criterion state when the group disagrees;
- F8 `count_fallback` batches warn instead of `PLAN_STALE`;
- F9 `generation.coverage_threshold` separates coverage from duplicate detection.

## Technical Context

**Language/Version**: C# 12, .NET 8.

**Primary Dependencies** (all existing):
- F1 `CriticConfig`, `AiConfig`, `ConfigLoader` (Validate, `DetectDeprecatedKeys`), `ValidateHandler`, `UpdateSkillsHandler`;
- F2 `AuditGroundingHandler` / `AuditGroundingCommand` / `AuditGroundingResult`, `spectra-generate.md`;
- F3 `SuiteDeleteHandler`, `SuiteDeleteResult`;
- F4 `SkillsManifest`, `SkillsManifestStore`;
- F5 `BaselineRecorder`, `BaselineWriteSummary`;
- F6 `SectionExtractor`, `MarkdownHeading`, `SectionRef`, `ChangeClassifier`;
- F7 `UpdateContextBuilder` (`TestBlock`), `UpdatePromptRenderer`;
- F8 `CompileBatchPrompt`;
- F9 `GenerationConfig`, `ConfigLoader.Validate`, `IngestAnalysisCommand`, `TitleCoverage`, `GenerationPlan`, init template.

No new packages.

**Storage**: additive only — skills manifest `candidates` (only when ambiguous), plan `coverage_threshold` (095 plans
still read), config `generation.coverage_threshold` (optional), ingest baseline JSON `left_unbaselined`, suite delete
`verdict_files_removed`, audit `pending_ground` / `pending_review`.

**Testing**: xUnit; real-init workspaces (`BaselineTestWorkspace`, `InitTemplateWorkspaceTests` pattern) and pre-098
variants of the embedded template; skill-text guards. Baseline `986da01`: Core 1230 / CLI 2258 / Execution 228 /
Integration 8, 0 failures.

**Target Platform**: Windows and Linux. **Project Type**: CLI. **Performance Goals**: none new.

**Constraints**: no config section becomes required (083); every fix verified on real-init and pre-098 workspaces;
human summaries on stderr in JSON mode; every fix fails on `986da01`; suite fully green.

**Scale/Scope**: ~20 edited source files, 2 skill texts, 1 template; ~9 new test classes; 5 docs.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Config keys and manifest stay in the repo; no new storage | ✅ |
| II. Deterministic | Audit actions, manifest choice, anchor keys, prompt annotations are pure functions of their inputs; byte-identical prompt output kept | ✅ |
| III. Orchestrator-agnostic, minimal output | The audit lets the resume path act without reading files; no new output beyond small additive fields | ✅ |
| IV. CLI-first; `--dry-run` | Suite delete dry run reports the same split as the real run; no new writing command | ✅ |
| V. Simplicity | Each fix is local to the component that has the bug; the critic rule moves into one converter instead of loader special cases | ✅ |
| Test-required discipline | Each fix has a row failing on `986da01` | ✅ |

**Post-design re-check**: no violations. Deliberate behaviour changes (each with a Notes entry in tasks.md):
- `ai.critic.model` invalid values no longer `CONFIG_INVALID` (097 rows retargeted);
- the audit's `repair` splits into `ground` / `repair` / `review`;
- suite delete's `leftovers*` drop verdict files;
- keep-baseline ingests stop creating entries;
- `data-id` stops being a heading id;
- coverage reads `coverage_threshold`.

## Project Structure

### Documentation (this feature)

```text
specs/098-correctness-fixes/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md            # /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.Core/Models/Config/CriticConfig.cs        # F1 converter, retired keys, invalid model
src/Spectra.Core/Models/Config/AiConfig.cs            # F1 CriticModel (model, warning); EffectiveCriticModel
src/Spectra.Core/Config/ConfigLoader.cs               # F1 drop INVALID_CRITIC_MODEL, retired keys list; F9 coverage range
src/Spectra.CLI/Commands/Validate/ValidateHandler.cs  # F1 notice text
src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs  # F1 warning
src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs, AuditGroundingCommand.cs  # F2
src/Spectra.CLI/Results/AuditGroundingResult.cs       # F2 summary counts
src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md   # F2 resume routing
src/Spectra.CLI/Commands/Suite/SuiteDeleteHandler.cs, Results/SuiteResults.cs  # F3
src/Spectra.CLI/Skills/SkillsManifest.cs              # F4 ranks, candidates on save/load
src/Spectra.CLI/Baseline/BaselineRecorder.cs          # F5
src/Spectra.Core/Baseline/SectionExtractor.cs, SectionRef.cs, ChangeClassifier.cs  # F6
src/Spectra.CLI/Generation/UpdateContext.cs, UpdatePromptRenderer.cs  # F7
src/Spectra.CLI/Commands/Generate/CompileBatchPrompt.cs  # F8
src/Spectra.Core/Models/Config/GenerationConfig.cs, Validation/TitleCoverage.cs  # F9
src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs, Generation/Plan/GenerationPlan*.cs  # F9
src/Spectra.CLI/Templates/spectra.config.json         # F9 coverage_threshold
```

Tests (new): `LegacyCriticConfigTests`, `AuditActionTests`, `SuiteDeleteReportTests`, `ManifestNewestTests`,
`KeepBaselineNoEntryTests`, `HeadingIdAnchorTests` (Core), `GroupCriterionStateTests`, `CountFallbackStaleTests`,
`CoverageThresholdTests`; rows added to `ConfigRuleMatrixTests`.

## Implementation Order

1. Baseline on `986da01`; write the guard rows; prove they fail on the untouched `src`.
2. F1 (top priority) → F2 → F3 → F4 → F5 → F6 → F7 → F8 → F9.
3. Docs, CLAUDE.md, CHANGELOG.
4. Full run; built-CLI quickstart on real-init and pre-098 workspaces; Notes. Ask before committing and merging; never
   push.

## Risks

| Risk | Mitigation |
|---|---|
| A custom converter on `CriticConfig` changes how other config parsing behaves | It only handles the `ai.critic` value; round-trip and case-insensitive key tests |
| 097 rows pin `CONFIG_INVALID` for invalid models | Retarget deliberately with a Notes entry |
| Image-free key creates new ambiguities | It's an extra key per heading; ambiguity is counted over both; 085 rows stay green |
| Snapshot stripping hides a real change | Only the equality test widens, only for a block after a heading marker's text; the diff is unchanged |
| Coverage fallback noise in every workspace | Fallback only when `duplicate_threshold` ≠ 0.6 (checked against the init template) |
| Manifest `candidates` growing forever | Written only for ambiguous keys; `Record` clears them |

## Complexity Tracking

No violations.
