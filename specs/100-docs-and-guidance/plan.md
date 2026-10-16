# Implementation Plan: Docs and Guidance (Phase 6, Part 5)

**Branch**: `100-docs-and-guidance` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/100-docs-and-guidance/spec.md`

## Summary

Text only; no condition, exit code, JSON key or written-file set changes. The docs, the CLI's user-visible strings, the
bundled skills, the agent guidance and the constitution stop naming removed commands and describe the product as it is:
- F1 README and docs/*.md rewritten where they present a removed command/key as current (inventory: research R3); a
  shared removed-concept list (R1) and a paragraph-level removal-marker rule (R2) drive a doc guard test;
- F2 usage.md "When documentation changes" replaces "Updating Test Cases After Doc Changes": 10 steps, one worked
  example, a committed-vs-scratch table checked against `SpectraGitIgnore.Patterns`; command lines checked against the
  real command tree (R4, R5);
- F3 test-format.md `depends_on` = one id (R13);
- F4 help strings (`config`, `run`, `ai update`) and, by the user's decision, the runtime strings and bundled skill text
  that name removed commands (R6, R7); the 099 golden folder re-captured with a byte copy of the old file tree kept for
  the pre-099 run (R8);
- F5 CLAUDE.md ≤ 150 lines (Recent Changes as a table so the agent-context script cannot truncate it), AGENTS.md
  rewritten, PROJECT-KNOWLEDGE.md / architecture-v5.md bannered as historical (R9);
- F6 constitution 2.1.0 (R11); CHANGELOG 100 entry + 2.6.0 Highlights (R12).

## Technical Context

**Language/Version**: C# 12, .NET 8 (test code only, plus string literals).

**Primary Dependencies** (all existing): `Program.CreateRootCommand()` + System.CommandLine (guard walks the parser's
tree); `SpectraGitIgnore.Patterns` (committed-vs-scratch table check); `ProfileFormatLoader` (embedded USAGE/
CUSTOMIZATION); `GoldenOutputTests` (+ `Golden/099/`). Changed literals: `NextStepHints`, `InitHandler`,
`CoverageReportWriter`, `PromptCompiler`, `UpdatePromptCompiler`, `UpdateHandler`, `AnalyzerInputBuilder`,
`PreFlightTokenChecker`, `ReportWriter` (Execution), `ConfigCommand`, `RunCommand`, `UpdateCommand`, both `app.js`
copies; bundled skills `spectra-docs`, `spectra-help`, `spectra-generate`, `spectra-quickstart`.

No new packages, projects, commands, options, JSON fields or config keys.

**Storage**: none changed.

**Testing**: xUnit. New: `RemovedConcepts` (shared list) + `DocsRemovedConceptsGuardTests`, `SourceStringsGuardTests`
(user-visible literals + bundled content), `CliReference_CoversEveryCommand` (FR-004a), `CommandLineCheck` helper + `CommandLineCheckTests` (self-test: a known-bad
line fails), `ClaudeMdGuardTests` (commands, folders, ≤ 150 lines, both script headings), `DocChangeGuideTests`
(section present, step order, structural + parse checks, skill/agent names vs the bundled content, the refresh
line, committed/scratch table vs `SpectraGitIgnore.Patterns`, README and index.md links), `DependsOnDocTests` (row text; no list example). Changed: `GoldenOutputTests` (pre-099 run reads
`files-pre099.txt`; the record mode no longer rewrites `pre099-workspace/`), `ProfileFormatLoaderTests` heading row, pre-flight message rows, any test pinning a replaced
string (listed in tasks Notes). Baseline `71b1bea`: Core 1228 / CLI 2280 / Execution 228 / Integration 8.

**Target Platform**: Windows dev + any CI (guards are platform-neutral; the golden comparison stays Windows-only as in
099).

**Project Type**: CLI + libraries.

**Performance Goals**: guards run in < 2 s total (file reads + one parser tree).

**Constraints**: golden check passes; re-capture diff limited to the R8 allowed lines; full suite green; 0 build
warnings (`TreatWarningsAsErrors` on CLI/Core since 099).

**Scale/Scope**: ~20 doc files, ~15 string literals, 4 bundled skills, CLAUDE.md/AGENTS.md, constitution, CHANGELOG.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status |
|---|---|
| I. GitHub as Source of Truth | Pass — all content is files in the repo. |
| II. Deterministic Execution | Pass — engine untouched; only the report footer string changes. |
| III. Orchestrator-Agnostic Design | Pass — no output-shape change; F6 records the JSON-mode stderr-summary rule the code already follows. |
| IV. CLI-First Interface | Pass — no command/option added or removed; help text matches behaviour. |
| V. Simplicity (YAGNI) | Pass — guards reuse the parser's own tree instead of a hand-kept command list. |
| Quality gates (tests, exit codes) | Pass — no exit code changes; new guards keep docs honest. |

Post-design re-check: unchanged (Pass). The constitution itself is amended in F6 (MINOR, R11) — the amendment follows
the Governance procedure (version, date, Sync Impact Report, templates checked).

## Project Structure

### Documentation (this feature)

```text
specs/100-docs-and-guidance/
├── plan.md
├── research.md       # R1–R13: removed-concept list, allow rule, inventory, guards, guide, strings, golden, CLAUDE.md
├── data-model.md     # RemovedConcept, allow rule, CommandLineCheck, guide/table shapes
├── quickstart.md     # how to run the guards and the golden re-capture review
├── contracts/
│   └── contracts.md  # text-change contract (old → new), guard contracts, golden diff contract
├── checklists/requirements.md
└── tasks.md          # /speckit.tasks
```

### Source Code (repository root)

```text
README.md, AGENTS.md, CLAUDE.md, CONTRIBUTING.md, CHANGELOG.md, PROJECT-KNOWLEDGE.md, architecture-v5.md
docs/                         # R3 inventory; usage.md + customization.md are embedded (USAGE.md / CUSTOMIZATION.md)
.specify/memory/constitution.md
dashboard-site/scripts/app.js
src/Spectra.CLI/
  Output/NextStepHints.cs, Commands/Init/InitHandler.cs, Coverage/CoverageReportWriter.cs,
  Generation/PromptCompiler.cs, Generation/UpdatePromptCompiler.cs, Commands/Update/UpdateHandler.cs,
  Commands/Update/UpdateCommand.cs, Analysis/AnalyzerInputBuilder.cs, Index/PreFlightTokenChecker.cs,
  Commands/Config/ConfigCommand.cs, Commands/Run/RunCommand.cs, Dashboard/Templates/scripts/app.js,
  Skills/Content/Skills/{spectra-docs,spectra-help,spectra-generate,spectra-quickstart}.md
src/Spectra.Execution/Reports/ReportWriter.cs
tests/Spectra.CLI.Tests/
  Docs/ (new): RemovedConcepts.cs, CommandLineCheck.cs, DocsRemovedConceptsGuardTests.cs, SourceStringsGuardTests.cs,
               CommandLineCheckTests.cs, ClaudeMdGuardTests.cs, DocChangeGuideTests.cs, DependsOnDocTests.cs
  Golden/GoldenOutputTests.cs, Golden/099/ (re-captured; files-pre099.txt added)
  Profile/ProfileFormatLoaderTests.cs, Index/PreFlightTokenCheckerTests.cs, Agent/Copilot/AnalyzerInputBuilderTests.cs
```

**Structure Decision**: guard tests live in a new `tests/Spectra.CLI.Tests/Docs/` folder (CLI tests can reach both
`Program.CreateRootCommand()` and the repo files via the existing repo-root helper pattern of
`RepositoryAttributesTests`).

## Implementation Order

1. **Guards first, red** — `RemovedConcepts`, `CommandLineCheck` (+ self-test), the doc / source-string / CLAUDE.md /
   guide / depends_on guards. They fail on `71b1bea`; the failure lists are the work lists.
2. **Golden prep** — byte-copy `files.txt` → `files-pre099.txt`; point the pre-099 run at it; stop the record mode
   from rewriting `pre099-workspace/`; both runs still green (nothing changed yet).
3. **F4 strings** (R6, R7) → build, fix the pinned tests (deliberate list), re-capture golden, review the diff (R8).
4. **F1 docs** (R3), F3 test-format.md, then **F2 guide** (R5) — usage.md/customization.md edits re-enter the golden
   file tree; re-capture again and review.
5. **F5** CLAUDE.md, AGENTS.md, historical banners.
6. **F6** constitution; CHANGELOG (R12).
7. Full suite (incl. a fresh `git worktree` checkout run — 099 lesson), 0 warnings, guards green.

## Risks

- **Paragraph marker rule too lenient** (R2): a "removed" paragraph that still instructs a removed command. Mitigated by
  the command-tree checks on the quick start, guide and CLAUDE.md, and by review of every R3 "mark" row.
- **Golden re-capture hides an unintended change**: the diff is reviewed line by line against contracts.md; the
  pre-099 run's tree must equal the old capture byte for byte.
- **Embedded USAGE.md is read by `ProfileFormatLoader`**: heading-pinning tests retargeted; content only, the loader is
  unchanged.
- **Skill text pinned by SkillsManifestTests rows**: rows that assert a replaced phrase are updated deliberately and
  listed; rows asserting the 075/093 NON-STOP text are untouched (no edit there).
- **CRLF in raw string literals / golden bytes** (099 lesson): verify in a fresh worktree checkout.
- **Agent-context script** truncates `- ` lists under Recent Changes (R9): table format + guard.

## Complexity Tracking

None.
