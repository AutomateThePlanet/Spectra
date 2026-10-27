# Implementation Plan: Update Prompt Context (Phase 3)

**Branch**: `088-update-prompt-context` | **Date**: 2026-10-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/088-update-prompt-context/spec.md`

## Summary

The update edit prompt gets what the edit needs, and one prompt covers a whole triage group.

- **`UpdateContextBuilder`** describes each distinct ref of the group's tests, reusing the 084–087 machinery
  (resolver, 085 matcher, snapshots, `ChangeClassifier`, relink candidates): the current raw section, the previous
  normalised version, and what changed — or why it doesn't resolve. It also lists the linked criteria with their state.
  Nothing else.
- **`UpdatePromptRenderer`** renders `source_diff`, `acceptance_criteria` and `tests` deterministically.
- The rewritten `test-update` template consumes them. Outdated custom templates keep working for `--test-id` with a
  warning, and are refused for `--group`.
- **`compile-update-prompt`** gains `--group` (validated by a `TriageGroupResolver` extracted from `update-ack`), the
  083 config rule, the shared failure shape, and refusals for nothing-to-reconcile and over-budget prompts.
- **`ingest-update`** reports an identical edit as `unchanged`: it isn't written, and its baseline is rebuilt (or kept
  with `--keep-baseline`). `--all` deletes staged files it consumed and lists failures.
- **`spectra-update`** edits per group.

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318.
**Primary Dependencies**: the baseline layer (`BaselineContext`, `BaselineStore`, `BaselineRecorder`), Core
`ChangeClassifier` / `SectionExtractor` / `WordDiff`, `RenameResolver` (via `BaselineContext.RelinkCandidateAsync`), the
prompt template system (`PromptTemplateLoader`, `PlaceholderResolver`, `BuiltInTemplates`), `UpdatedTestIngestor`,
`TestPersistenceService`, `TokenEstimator`. No new packages.
**Storage**: no format change. Staging files `.spectra/updates/{suite}/updated-{id}.json` are now deleted when
consumed.
**Testing**: xUnit 2.9.3; `BaselineTestWorkspace` (`CriteriaCommands` collection); `[GitFact]` for RELINK/submodule
rows; the real init template for T8. Baseline at `eb1151c`: Core 1219 / CLI 1812 (1 known failure) / Execution 228 /
Integration 8, re-measured in T001.
**Target Platform**: Windows and Linux.
**Project Type**: CLI and shared library.
**Performance Goals**: a group prompt for 10 tests sharing one section is ≤ 1.3 × its raw content (SC-001). Building
the context reads each doc and snapshot once (the `BaselineContext` caches).
**Constraints**:
- Deterministic, byte-identical prompts.
- Model-free.
- The compile step writes nothing.
- No new config.
- The critic is untouched.

**Scale/Scope**:
- about 4 new source files (`UpdateContext` + builder, `UpdatePromptRenderer`, `TriageGroupResolver`);
- edits to about 8 files (`CompileUpdatePromptCommand`, `UpdatePromptCompiler`, `test-update.md`, `BaselineContext`,
  `UpdateAckHandler`, `UpdatedTestIngestor`, `IngestUpdateCommand`, the skill);
- 4 docs;
- about 6 new test classes.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Edited tests and baselines are committed files. Deleting consumed staging files touches only gitignored scratch (`.spectra/updates/`) | ✅ |
| II. Deterministic execution | The engine is untouched. Prompts are byte-identical for identical inputs (T7) | ✅ |
| III. Orchestrator-agnostic, minimal output | Model-free compile. The prompt carries only the test's sections and linked criteria — far less than today's repo-wide criteria — and one prompt per group | ✅ |
| IV. CLI-first, deterministic exit codes, `--dry-run` | New refusals are exit 4 with named codes in the shared shape. The compile step writes nothing; `ingest-update` (no `--dry-run`, as before) keeps its fail-loud boundary | ✅ |
| V. Simplicity | Reuses the 084–087 classifier, matcher and group validation; no new storage or config | ✅ |
| Test-required discipline | T1–T9, an end to end on the real init template, and the SC-001 size check | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/088-update-prompt-context/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.CLI/
├── Baseline/
│   ├── BaselineContext.cs          # + GetRawSectionAsync(ref), GetCriterionText(id)
│   └── TriageGroupResolver.cs      # NEW: group → tests via .spectra/update-triage.json (extracted from update-ack)
├── Generation/
│   ├── UpdateContext.cs            # NEW: SectionBlock, CriterionBlock, TestBlock, UpdateContext; UpdateContextBuilder
│   ├── UpdatePromptRenderer.cs     # NEW: source_diff / acceptance_criteria / tests strings
│   ├── UpdatePromptCompiler.cs     # + CompileContext(UpdateContext, template, profile) → prompt; legacy Compile kept
│   └── UpdatedTestIngestor.cs      # + IsUnchanged; IngestAsync returns Unchanged without persisting
├── Commands/
│   ├── Generate/CompileUpdatePromptCommand.cs   # --group; 083 config; shared failure shape; refusals; size check
│   ├── Generate/IngestUpdateCommand.cs          # unchanged outcome; per-file results; delete consumed staging files
│   └── Baseline/UpdateAckHandler.cs             # uses TriageGroupResolver
├── Prompts/Content/test-update.md               # source_diff / tests placeholders, per-test output, unchanged rule
└── Skills/Content/Skills/spectra-update.md      # per-group edit loop

tests/Spectra.CLI.Tests/
├── Generation/UpdateContextBuilderTests.cs       # T1, T2
├── Commands/Generate/CompileUpdatePromptGroupTests.cs   # T3, T4, T6, T7, SC-001
├── Commands/Generate/IngestUpdateStagingTests.cs        # T5
├── Commands/Generate/UpdatePromptEndToEndTests.cs       # T8
└── Skills/SkillsManifestTests.cs                         # T9
```

## Implementation Order

1. **T001** baseline measurement.
2. **The context:** `BaselineContext` additions, then `UpdateContextBuilder` and the T1/T2 tests.
3. **Rendering and template:** `UpdatePromptRenderer`, `UpdatePromptCompiler.CompileContext`, the new `test-update.md`,
   outdated-template detection (T4).
4. **The command:** `TriageGroupResolver` (with `update-ack` switched to it), then `compile-update-prompt` with
   `--group`, the 083 config rule, refusals and the size check (T3, T6, T7, SC-001).
5. **Ingest:** `IsUnchanged`, the unchanged outcome, per-file results and deletion (T5).
6. **Skill** (T9), then **end to end** on the real init template (T8).
7. **Docs:** CHANGELOG, CLAUDE.md, cli-reference, usage.
8. **Finish:** a full test run, the quickstart, Notes. Ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Doc text breaks the prompt's fences | Sections are fenced with `~~~~`, which an inner ```` ``` ```` fence can't close |
| A shared ref with different baseline fingerprints across tests | Described once per (ref, fingerprint), labelled with its tests (research R1) |
| Deleting staging files loses an edit | Deleted only after persist and baseline recording; failures and baseline-write errors keep every file |
| The critic-repair flow depends on staged files staying | It reads verdict files, not staging files; `--keep-baseline` keeps its baseline semantics (085) |
| `compile-update-prompt` now runs on defaults without a config | The 083 read-command rule, consistent with `compile-critic-prompt`; noted in docs |
| Existing `UpdatePromptCompilerTests` | The legacy `Compile`/`Assemble` API is kept for outdated custom templates |

## Complexity Tracking

No constitution violations.
