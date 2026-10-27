# Implementation Plan: Performance and Cleanup (Phase 6, Part 4)

**Branch**: `099-performance-cleanup` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/099-performance-cleanup/spec.md`

## Summary

No behaviour change; every command's files, JSON and exit codes stay byte-identical, proven by a golden-output check
captured on `c24d23e`:
- F1 an ingest parses the suite once: the load becomes the baseline's suite scan (with the call's writes applied);
- F2 the baseline context scans each document's headings once;
- F3 read-only callers parse without the 078 layout; the writer refuses a test parsed without it;
- F4 `ingest-update --all` parses each staged file's JSON once;
- F5 dead code from 058/059/069/070 (and 095's leftovers) is deleted, only at 0 production references;
- F6 the 4 build warnings are fixed and the CLI and Core projects treat warnings as errors;
- F7 the CLI test suite gets faster without losing a test: classes leave serial collections when they touch no global
  state, real-init and git fixtures are built once and copied, git fixture helpers start fewer processes.
Every performance change is proven by a deterministic count; timings go to the Notes only.

## Technical Context

**Language/Version**: C# 12, .NET 8.

**Primary Dependencies** (all existing):
- F1 `IngestTestsCommand`, `IngestUpdateCommand` (`LoadExistingTestsAsync` → shared loader), `BaselineRecorder`,
  `BaselineStore` (`ScanSuiteAsync`, `SuiteScan`, `SaveSuiteAsync`);
- F2 `BaselineContext`, Core `SectionExtractor`;
- F3 Core `TestCaseParser`, `TestCase`, `TestCaseLayout`; `TestFileWriter`; every parse caller (research R3 table);
- F4 `GeneratedTestIngestor`, `UpdatedTestIngestor`, `IngestUpdateCommand`;
- F5 the R5 table (≈ 50 types/members; `Microsoft.Extensions.AI.Abstractions` package reference removed);
- F6 `DocsApplyMoveCommand`, `DocsRetireCommand`, `CriteriaLayoutMigrator`, `Spectra.CLI.csproj`, `Spectra.Core.csproj`;
- F7 test fixtures `UpdatePromptWorkspace`, `ManifestWorkspace`, `GitFixture`, `GitWorkspace`, collection definitions.

No new packages; one package reference removed (F5).

**Storage**: none changed (no file format, config key or template change).

**Testing**: xUnit. New: `ParseCounterTests` (Core), `GoldenOutputTests` (+ `Golden/099/` files), `IngestParseCountTests`, `HeadingScanCountTests`,
`LayoutModeTests` (writer guard, read-only mode, writer-path matrix), `StagedJsonParseCountTests`,
`WarningsAsErrorsGuardTests`, `TemplateWorkspaceTests` (copied template == fresh init). Baseline `c24d23e`: Core 1253 /
CLI 2326 / Execution 228 / Integration 8; CLI run **597 s idle**, all green (552 s of it in serial collections, 318 s
in git classes — research R7). A loaded run took 833 s and failed one wall-clock assertion (passes idle).

**Target Platform**: Windows and Linux. **Project Type**: CLI.

**Performance Goals** (counts, asserted): ingest N parses for N tests (was 2N); D heading scans for D documents per
context; K JSON parses for K staged files (was 3K); 0 layout captures in read-only commands. (Time, recorded only): CLI
test suite under ≈ 7 min on this machine.

**Constraints**: byte-identical outputs (golden check, documented masks proven to vary between two `c24d23e` runs); no
CLI/config/template/format change; 0 warnings; no test deleted or weakened except tests of deleted code (listed); no
timing threshold relaxed; 083 rule (real init + pre-099 workspace).

**Scale/Scope**: ≈ 15 edited source files for F1–F4/F6; ≈ 50 deleted types/members (F5) with ≈ 15 test files or rows
removed; ≈ 10 test fixture files edited (F7); 8 new test classes (incl. `ParseCounterTests` in Core); CHANGELOG +
CLAUDE.md.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | No storage change | ✅ |
| II. Deterministic | Outputs byte-identical (golden check); the counts are deterministic by construction | ✅ |
| III. Orchestrator-agnostic, minimal output | No output change | ✅ |
| IV. CLI-first; exit codes | No command, option or exit code changes | ✅ |
| V. Simplicity | Removes dead code and a package reference; each perf change is local (one cache, one shared loader, one parser flag, one extracted-JSON record). "Profile first": every change targets a finding measured or counted in a review (085/078/089) | ✅ |
| Test-required discipline | Each change has a count guard or a lossless-write guard; the golden check covers the whole surface | ✅ |

**Post-design re-check**: no violations. Two internal-only additions are justified in research: the `LayoutOmitted`
flag on `TestCase` (needed to tell a read-only parse from a new test — `Layout == null` already means "new") and the
internal `ParseCounter` (an `AsyncLocal` scope in Core: production code only increments the current scope, if any; no
scope = no cost beyond a null check). Core grants internal access to `Spectra.CLI.Tests` for it.

## Project Structure

### Documentation (this feature)

```text
specs/099-performance-cleanup/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md            # /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.Core/Diagnostics/ParseCounter.cs (new)    # AsyncLocal scope: FullParses, LayoutCaptures, DocumentHeadingScans, JsonParses
src/Spectra.Core/Spectra.Core.csproj                   # InternalsVisibleTo Spectra.CLI.Tests
src/Spectra.Core/Parsing/TestCaseParser.cs            # F3 captureLayout ctor arg; counts into ParseCounter
src/Spectra.Core/Models/TestCase.cs                    # F3 LayoutOmitted (internal, ignored by serializers)
src/Spectra.Core/Baseline/SectionExtractor.cs          # F2 Extract(normalised, headings, anchor) overload
src/Spectra.CLI/IO/TestFileWriter.cs                   # F3 refuse LayoutOmitted
src/Spectra.CLI/Baseline/BaselineContext.cs            # F2 heading cache; counts document scans
src/Spectra.CLI/Baseline/BaselineStore.cs              # F1 scan from files; F3 captureLayout param
src/Spectra.CLI/Baseline/BaselineRecorder.cs           # F1 optional SuiteScan
src/Spectra.CLI/Baseline/SuiteScanBuilder.cs (new)     # F1 FromFiles / WithWritten
src/Spectra.CLI/Commands/Generate/IngestSuiteLoader.cs (new)  # F1 shared loader (was 2 copies)
src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs, IngestUpdateCommand.cs  # F1, F4
src/Spectra.CLI/Generation/GeneratedTestIngestor.cs, UpdatedTestIngestor.cs      # F4 ExtractedContent
src/Spectra.CLI/** read-only parse callers             # F3 captureLayout: false (R3 table)
src/Spectra.CLI/Generation/AnalysisRecommendationBuilder.cs, IngestAnalysisCommand.cs  # F5
src/Spectra.CLI/{Agent,Analysis,Interactive,Session,Review,Coverage,Output,Provider,Services,Source}/…  # F5 deletions
src/Spectra.Core/{Index,Models}/…                      # F5 deletions (TestIdAllocator, Testimize models, McpToolResponse, SuiteInfo, RequirementsWriter)
src/Spectra.CLI/Commands/Docs/DocsApplyMoveCommand.cs, DocsRetireCommand.cs  # F6 CommandName
src/Spectra.CLI/Criteria/CriteriaLayoutMigrator.cs     # F6 explicit type args
src/Spectra.CLI/Spectra.CLI.csproj, src/Spectra.Core/Spectra.Core.csproj  # F6 TreatWarningsAsErrors; F5 package removal
tests/Spectra.CLI.Tests/TestFixtures/{UpdatePromptWorkspace,ManifestWorkspace,GitFixture,GitWorkspace,TemplateWorkspaces}.cs  # F7
tests/Spectra.CLI.Tests/Golden/099/                    # golden files (captured on c24d23e)
```

## Implementation Order

Same order as tasks.md:
1. **Foundational (Phase 2):** F6 (warnings, then `TreatWarningsAsErrors`) so every later step builds with warnings as
   errors; Core internal access for `Spectra.CLI.Tests`; the `ParseCounter` scope wired into the existing parse, scan
   and JSON sites (counting only — so each guard first shows today's number).
2. **Golden capture (US1):** write `GoldenOutputTests`; capture twice in a `c24d23e` worktree (record mode) plus the
   post-init tree for the pre-099 row; derive the masks from the diff of the two captures; commit; green on the branch.
3. F1 → F2 → F3 → F4 (F1 creates the shared loader whose `captureLayout` F3 sets; F2 and F4 are independent and may
   swap); after each: count guard + golden check + full run.
4. F5 dead code (re-count each row; delete chains leaf-first; build + golden after each group).
5. F7 test-suite work (measure, change, re-measure per class).
6. CHANGELOG.md, CLAUDE.md entry. Full run (Core, CLI, Execution, Integration), 0 CLI/Core warnings (Debug); built-CLI
   quickstart; Notes (timings, masks, deleted-test list). Ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| A masked field hides a real change | Masks only for fields shown to vary between two `c24d23e` runs; masks replace values, never drop lines |
| F1 derived scan differs from a re-scan (case-insensitive overwrite, unparseable file overwritten, `foo.md` updated as `{id}.md`) | `SuiteScanBuilder` unit rows for each case comparing with `ScanSuiteAsync` on the same disk state |
| F3 marks a writer path read-only → canonical rewrite of a hand-edited file | Capture stays the parser default; only listed read-only callers opt out; the writer throws on `LayoutOmitted` (loud, never silent); writer-path matrix test |
| F3 misses a read-only caller | Only costs time; `LayoutCaptures` = 0 asserted per read-only command family |
| F3 changes record equality (no layout → equal) | No production value comparison (research R3); tests comparing layouts use `TestCaseEquivalence` |
| F5 deletes something reached by reflection or a template | Re-count at implement time incl. embedded resources and type-name strings; clean build; golden check; full suite |
| Warnings as errors breaks builds on a new NuGet advisory or analyzer release | `WarningsNotAsErrors` NU1901–NU1904 (already real: ImageSharp 3.1.12 in Execution has NU1902/NU1903 and blocks `-c Release` restore today — separate task); the floating `NetAnalyzers 10.*` is a known remaining risk; pinning it is out of scope |
| Copied init template embeds its own path | `TemplateWorkspaceTests` compares a copy with a fresh init byte for byte |
| Copied git repo breaks (absolute paths in `.git`) | Copy only where the class's tests pass unchanged on the copy; per-class before/after counts in Notes |
| Moving a class out of a serial collection introduces flakiness | Move only after reading the class for every global-state use listed in R7; run the CLI suite 3× after the moves |
| Wall-clock perf tests flake under load (seen on the `c24d23e` loaded run) | Not relaxed (spec); measure on an idle machine; noted |
| The AsyncLocal-scoped counter misses parses on threads that don't flow the async context | Before trusting a guard, grep `src/` for `new Thread`, `ThreadPool.UnsafeQueueUserWorkItem` and `SuppressFlow`; none are expected. If any appear on a counted path, count there explicitly. Grep result in tasks.md Notes |

## Complexity Tracking

No violations.
