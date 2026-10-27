# Implementation Plan: Stable Acceptance-Criteria Identity (Update Workflow Phase 1)

**Branch**: `079-stable-criteria-ids` | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/079-stable-criteria-ids/spec.md`

## Summary

This phase makes criterion IDs trustworthy, so that later phases can tell which criteria changed and which tests are
affected.

- **Extraction prompt.** It now lists the doc's existing criteria (FR1). The model returns the existing `id` for the same
  requirement.
- **Ingest.**
  - Validates returned IDs strictly: an unknown, foreign or duplicated ID exits 7 (FR2).
  - Falls back to a one-to-one match on a shared, culture-invariant normalised-text key (FR5).
  - Allocates new IDs from a committed, per-prefix high-water mark kept in `_criteria_index.yaml`, under one cross-process
    criteria lock (FR3).
  - Reports a changeset (added / modified / removed with referencing-test counts / metadata) in the JSON result. The human
    summary goes to stderr in JSON mode (FR4).
- **Storage.** Each doc's criteria file mirrors the doc's repo-relative path. An explicit, idempotent
  `docs migrate-criteria` command moves existing files; write commands run it automatically, read commands only warn (FR6).
  `source_doc` is normalised (FR7).
- **Deleted and moved docs.** `docs changed` reports `deleted` and `moved` docs. New explicit commands `docs retire` and
  `docs apply-move` handle them (FR8).
- **Skill.** `spectra-criteria` drives moves, retires deleted docs only after the user confirms, and summarises all
  changesets (FR9).

## Technical Context

**Language/Version**: C# 12, .NET 8 (`net8.0`); SDK 9.0.318 via `global.json`
**Primary Dependencies**: YamlDotNet 16.3.0, System.Text.Json, System.CommandLine, Spectre.Console. No new packages.
**Storage**: Files only:
- `docs/criteria/_criteria_index.yaml` (committed), which gains `id_high_water_marks`
- per-doc `*.criteria.yaml` under `docs/criteria/<repo-relative path>`
- `.spectra/criteria.lock` (gitignored)

**Testing**: xUnit.
- Baseline: Core 611 / CLI 1355 (1 known failure) / Execution 228 / Integration 8.
- New tests:
  - normaliser
  - allocator: no reuse, fresh clone, concurrency with parallel tasks, and a second process via the lock
  - matcher and ID validation
  - changeset and referencing counts
  - path derivation and normalisation
  - migration: idempotent, collisions
  - `docs changed` deleted/moved
  - retire and apply-move
  - prompt determinism
  - skill content
  - `ai update` tolerating duplicates

**Target Platform**: Cross-platform CLI. Path comparison is case-insensitive where the index already was.
**Project Type**: CLI plus shared library.
**Performance Goals**: The duplicate and high-water-mark scans read every criteria file once per write command. The files
are small, and there are at most hundreds of docs.
**Constraints**:
- model-free and deterministic (III)
- deterministic exit codes, `--dry-run` on every destructive or batch command (IV)
- no shims beyond the one-time migration (V)

**Scale/Scope**:
- ~6 new source files: `TextNormalizer`, `CriteriaIdAllocator`, `CriteriaPaths`, `CriteriaChangeset(Builder)`,
  `CriteriaStore`, `CriteriaLayoutMigrator`
- 3 new commands: `docs retire`, `docs apply-move`, `docs migrate-criteria`
- ~8 edited files: ingestor, ingest command, compile command and compiler, template, classifier, `docs changed`, import
  path, `TestClassifier`/`UpdateHandler`, init gitignore
- 1 skill
- 4 docs

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. GitHub as Source of Truth | The high-water marks are committed with the index, so ID history survives clones and is visible in review. The migration moves files, and Git is the undo path. | ✅ |
| II. Deterministic Execution | The engine is untouched. | ✅ N/A |
| III. Orchestrator-Agnostic / model-free | Matching, normalising, allocation, the changeset, migration, retire and move are all pure CLI; the model only returns an optional `id`. **Recorded exception on output:** the human changeset summary is always printed, per the user's decision. To keep JSON output "minimal and structured", it goes to **stderr** in JSON mode, and stdout stays exactly the JSON document. | ✅ (exception documented) |
| IV. CLI-First | New operations are named commands with explicit parameters, deterministic exit codes (ingest adds 7; retire and apply-move use 4 for refusals) and `--dry-run`. `docs changed` stays read-only. | ✅ |
| V. Simplicity | One Core `Criteria/` folder with small pure helpers. A single `CriteriaStore` owns the lock and read-modify-write for ingest, import, retire, move and migrate: five callers, which replaces the duplicated unlocked read-modify-write code. The only compatibility code is the one-time migration. No flags added to existing commands. | ✅ |
| Quality gates | `spectra validate` is unaffected. | ✅ |

**Post-design re-check**: no violations. Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/079-stable-criteria-ids/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/
│   ├── ingest-criteria.md      # input id, processing order, result + changeset, exit 7, stderr summary
│   ├── docs-commands.md        # docs changed deleted/moved + retire + apply-move + migrate-criteria + auto-migration + lock
│   └── extraction-prompt.md    # existing_criteria format, template block, custom-template warning
├── checklists/requirements.md
└── tasks.md                    # /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.Core/
├── Text/TextNormalizer.cs                    # NEW  comparison key (R4)
├── Criteria/
│   ├── CriteriaPaths.cs                      # NEW  NormalizeSourceDoc, DerivedFile, IsOldLayout (R2, FR-017)
│   ├── CriteriaIdAllocator.cs                # NEW  ParseId, effective HWM, Allocate (R1)
│   ├── CriteriaMatcher.cs                    # NEW  explicit-id validation + one-to-one text fallback (R3)
│   ├── CriteriaChangeset.cs                  # NEW  records + Build(previous, next) (data-model)
│   └── CriteriaDuplicateScanner.cs           # NEW  id → files (FR-010)
├── Models/Coverage/CriteriaIndex.cs          # + IdHighWaterMarks (sorted)
├── Parsing/CriteriaFileReader.cs             # + ReadStrictAsync (throws on unreadable; FR edge case)
├── Parsing/CriteriaFileWriter.cs, CriteriaIndexWriter.cs   # unique temp names
└── Update/TestClassifier.cs                  # first-wins lookup (FR-011)

src/Spectra.CLI/
├── Criteria/
│   ├── CriteriaStore.cs                      # NEW  lock + load index/files + auto-migrate + persist (+HWM)
│   ├── CriteriaLayoutMigrator.cs             # NEW  migrate (R7)
│   ├── CriteriaReferenceCounter.cs           # NEW  tests referencing ids from _index.json (R12)
│   └── CriteriaChangesetPresenter.cs         # NEW  human summary (stderr in JSON mode)
├── Extraction/CriteriaIngestor.cs            # rewritten on CriteriaStore/Matcher/Allocator/Changeset
├── Extraction/CriteriaResponseClassifier.cs  # + id, [JsonPropertyName("source_section")]
├── Extraction/ExtractionPromptCompiler.cs    # existing_criteria JSON
├── Prompts/Content/criteria-extraction.md    # {{#if existing_criteria}} block + id in schema
├── Commands/Generate/IngestCriteriaCommand.cs        # result JSON + exit 7 + summary
├── Commands/Generate/CompileExtractionPromptCommand.cs # read existing via index, old-layout + custom-template warnings
├── Commands/Docs/DocsChangedHandler.cs       # normalised compare, deleted, moved, duplicates, migration warning
├── Commands/Docs/DocsRetireCommand.cs        # NEW
├── Commands/Docs/DocsApplyMoveCommand.cs     # NEW
├── Commands/Docs/DocsMigrateCriteriaCommand.cs  # NEW
├── Commands/Docs/DocsCommand.cs              # register the three
├── Commands/Analyze/AnalyzeHandler.cs        # import: merge first, allocate via CriteriaStore (FR-008)
├── Commands/Update/UpdateHandler.cs          # duplicate-id warning
├── Commands/Init/InitHandler.cs              # gitignore .spectra/criteria.lock
└── Skills/Content/Skills/spectra-criteria.md # moved/deleted handling + changeset summary

tests/Spectra.Core.Tests/{Text,Criteria}/...           # normaliser, paths, allocator, matcher, changeset, duplicates
tests/Spectra.CLI.Tests/Criteria/...                   # store/lock concurrency, migrator, reference counter
tests/Spectra.CLI.Tests/Commands/Criteria*/Docs*/...   # ingest contract, docs changed, retire, apply-move, migrate
tests/Spectra.CLI.Tests/Extraction/...                 # prompt determinism, classifier id/source_section
tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs  # skill assertions (keep the 078 criteria assertions)
```

**Structure decision**: Pure logic goes in `Spectra.Core/Criteria` and `Spectra.Core/Text`, which is unit-testable with no
I/O. The locked, filesystem-orchestrating `CriteriaStore` and the command handlers go in `Spectra.CLI/Criteria`.

## Implementation Order

1. **Core pure pieces**: `TextNormalizer`, `CriteriaPaths`, `CriteriaIdAllocator`, `CriteriaMatcher`, `CriteriaChangeset`,
   `CriteriaDuplicateScanner`, `CriteriaIndex.IdHighWaterMarks`, the strict reader, unique temp names. Unit tests for each.
2. **Classifier fix**: add `id` and `source_section` binding.
3. **`CriteriaStore` and `CriteriaLayoutMigrator`**: lock, load, auto-migrate, persist. Tests for concurrency and
   migration idempotence and collisions.
4. **Ingest**: rewrite the ingestor on top of the store; the command's result JSON, exit 7, and the stderr/stdout summary
   with the reference counter.
5. **Prompt**: compiler, template, and the compile command reading through the index; determinism and warnings.
6. **`docs changed`**: normalised comparison, deleted, moved, duplicates, migration warning.
7. **New commands**: `docs retire`, `docs apply-move`, `docs migrate-criteria`, and registering them.
8. **Import** through the store and allocator: merge first.
9. **Duplicate tolerance**: `TestClassifier` / `UpdateHandler`; init gitignore.
10. **Skill** and skill tests.
11. **Docs**: CHANGELOG `[2.6.0]`, CLAUDE.md, `cli-reference.md`, `usage.md` / coverage docs, version bump.
12. Full `dotnet test`, then the quickstart in a scratch git repo.

## Risks

| Risk | Mitigation |
|---|---|
| Existing tests assert the flat file name or `file` = base name (`CriteriaIngestorTests`, `ExtractionCommandsTests`, `DocsChangedTests`) | Update them to the derived paths. Each one encoded the collision bug, so list each in the PR. |
| Fixing `source_section` causes a one-time metadata churn | Documented in the CHANGELOG. The changeset reports it as metadata changes, never as text changes. |
| A user's custom prompt override lacks `existing_criteria` | stderr warning plus the `prompts reset` hint. The text fallback still keeps unchanged criteria. |
| Lock contention on large batch runs | The lock is held only for read-compute-write of one doc; 10 s timeout → `LOCK_TIMEOUT` (exit 1). |
| Recursive scanners double-count if a migration copies instead of moving | The migration always moves, and a test asserts no old files remain. |

## Complexity Tracking

No constitution violations; nothing to justify.
