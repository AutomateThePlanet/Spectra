# Implementation Plan: Review Fixes for Spec 082 — One Config Rule

**Branch**: `083-review-fixes-082` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/083-review-fixes-082/spec.md`

## Summary

082 made the criteria write commands require `coverage`, but the config `spectra init` writes has none. So the
criteria workflow fails in every init-created and every pre-existing workspace.

This spec replaces the per-section rules with one table-driven rule in which no section is ever required. It also:
- adds `coverage` to the init template;
- rejects case-variant (and exact) duplicate top-level keys;
- narrows what counts as HTML to five unambiguous forms;
- adds guard tests that run against the real embedded template and a pre-083 workspace.

Every fix has a test that fails on `3594d62`.

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318
**Primary Dependencies**: System.Text.Json (`JsonNode` pre-scan), `GeneratedRegex` with a match evaluator. No new
packages.
**Storage**: Files (`spectra.config.json`, the embedded init template, criteria YAML).
**Testing**: xUnit. Baseline at `3594d62` is expected to be Core 812 / CLI 1568 (1 known failure:
`ProfileFormatLoaderTests`) / Execution 228 / Integration 8. Re-measure in T001.
**Target Platform**: Cross-platform CLI; CI on ubuntu.
**Project Type**: CLI and shared library.
**Performance Goals**: No change.
**Constraints**:
- Model-free and deterministic.
- `SpectraConfig` untouched.
- Exit codes unchanged except where the rule table changes them (`coverage`).
- Human-mode wording unchanged.
**Scale/Scope**: About 14 edited source files (removals mostly), 1 template, 1 skill, 3 docs, 2 new test classes, and
3 test classes edited.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Criteria land where the config says, or in the documented default. No workspace is rewritten. | ✅ |
| II. Deterministic execution | Engine untouched. | ✅ N/A |
| III. Minimal structured output | Same failure document; one fewer error code. | ✅ |
| IV. CLI-first, deterministic exit codes | One rule table; the exit codes follow it. | ✅ |
| V. Simplicity | Removes the `requires` concept, a flag and an error code. Adds no new abstraction. | ✅ |
| Test-required discipline | Adds the missing class of test: real template and pre-existing workspace, end to end. | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/083-review-fixes-082/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-behaviour-changes.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Config/ConfigLoader.cs            # one rule: no `requires`, no Coverage flag, no SECTION_MISSING; duplicates → INVALID
└── Text/TextNormalizer.cs            # five HTML forms via match evaluator

src/Spectra.CLI/
├── Templates/spectra.config.json     # + coverage section (F1)
├── Criteria/CriteriaCommandIO.cs     # LoadConfigAsync(repoRoot, reads, ct)
├── Commands/Generate/IngestCriteriaCommand.cs, CompileExtractionPromptCommand.cs, CompileCriticPromptCommand.cs
├── Commands/Docs/DocsRetireCommand.cs, DocsApplyMoveCommand.cs, DocsMigrateCriteriaCommand.cs, DocsChangedHandler.cs
├── Commands/Analyze/AnalyzeHandler.cs          # declarations per FR-003
└── Skills/Content/Skills/spectra-criteria.md   # drop CONFIG_SECTION_MISSING

tests/
├── Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs          # NEW (T1, T5)
├── Spectra.CLI.Tests/Commands/Criteria/InitTemplateWorkspaceTests.cs  # NEW (T2, T3)
├── Spectra.CLI.Tests/Commands/InitCommandTests.cs               # template has coverage (F1)
├── Spectra.CLI.Tests/Commands/ConfigLoadingConsistencyTests.cs  # replace the 082 coverage/section-missing tests
├── Spectra.Core.Tests/Config/ConfigLoaderEntryPointTests.cs     # replace the `requires` tests; exact-duplicate test
├── Spectra.Core.Tests/Text/TextNormalizerTests.cs               # T4 decision table
└── Spectra.CLI.Tests/Skills/SkillsManifestTests.cs              # skill has no CONFIG_SECTION_MISSING
```

## Implementation Order

1. **Baseline** (T001).
2. **Guard tests first.** Write T2, T3 and the F1 template test, then T1/T5 and T4. Confirm they fail on
   `3594d62`: T2 and T3 fail at `ingest-criteria` exit 1.
3. **F1**: the template gets `coverage`.
4. **The rule in `ConfigLoader`:**
   - remove `requires`, `Coverage` and `SECTION_MISSING`;
   - add the duplicate checks;
   - catch `ArgumentException`.
5. **Command declarations and `CriteriaCommandIO`**, then remove the dead skill text. Replace the 082 tests that
   assert the removed rule.
6. **F4 normaliser.**
7. **Docs:**
   - `CHANGELOG.md` gets a Spec 083 subsection under the unreleased `[2.6.0]`;
   - `CLAUDE.md` "Recent Changes";
   - `docs/cli-reference.md`: the rule table, the codes, the template's `coverage`, the HTML forms and residual, and
     the guard rule from FR-021.
8. **Full `dotnet test`**, the fails-before worktree check at `3594d62`, and the quickstart with the real
   `spectra init`. Then commit and merge on approval; never push.

## Risks

| Risk | Mitigation |
|---|---|
| The T2 end-to-end test is slow or flaky (init writes skills, prompts, etc.) | Uses the same `InitHandler` path as the existing init tests, in the non-parallel `CriteriaCommands` collection. No network. |
| Someone later adds a config rule and checks it only against fixtures again | T2 and T3 run against the embedded template. FR-021 and the loader XML docs name them as the required check. A memory note records the lesson. |
| Removing `requires` misses a call site | The compiler flags every removed parameter use; the Phase-0 grep lists the 14 files. |
| F4 changes an existing expectation | Modelled: 0 of 71 existing inputs change. T4 keeps all of them. |

## Complexity Tracking

No constitution violations.
