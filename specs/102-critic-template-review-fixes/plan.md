# Implementation Plan: Review Fixes for 101 and the Critic Prompt Template

**Branch**: `102-critic-template-review-fixes` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/102-critic-template-review-fixes/spec.md`

## Summary

Nine review fixes. Two correctness fixes change what the critic sees:
- **F1**: `compile-critic-prompt` builds the system part from the `critic-verification` template (workspace copy, else
  bundled) in every form and mode. The hard-coded prompt in `CriticPromptBuilder` becomes unreachable once the loader is
  required, so it is removed. The `--full-docs` user part stays byte-identical to 101.
- **F2**: `CriticSourceLoader` groups refs by file with the resolver's comparer, so refs that differ only in case reach
  the critic together.

Two fixes change agent text:
- **F3**: the repairer runs the critic only for `written` repairs. An `unchanged` repair is routed by the verdict it
  already has. This is a deliberate change to 101 FR-026, for unchanged repairs only.
- **F4**: the generator's title line becomes `<id> | created: <title>`.

The five smaller fixes:
- **F5**: the ambiguous-anchor warning only for given sections.
- **F6**: the dead criteria parameter is removed.
- **F7**: `CriticSourceLoader` matches each anchor once and slices once.
- **F8**: a guard keeps the three copies of the critic step identical.
- **F9**: `Agent` is the subagent tool's one name: the canonical name per Claude Code's docs, with `Task` an alias
  since v2.1.63.

## Technical Context

**Language/Version**: C# 12, .NET 8.

**Primary Dependencies** (all existing):
- `CompileCriticPromptCommand`, `CriticPromptCompiler`, `CriticPromptBuilder`, `PromptTemplateLoader`,
  `BuiltInTemplates`, `PlaceholderResolver`;
- `CriticSourceLoader`, `SourceRefResolver`, `SectionExtractor`;
- `SkillResourceLoader`, the bundled skills (`spectra-generate`, `spectra-update`, `spectra-criteria`), the generate
  reference file, and the five work agents.

No new packages.

**Storage**: no format change. Installed skills and agents change text; the 096 manifest rules apply.

**Testing**:
- xUnit. Critic rows run over `BaselineTestWorkspace` with `CriticSourceWorkspace` text built in code with LF (099
  lesson), compared through `Lf`.
- Skill and agent content rows run against the embedded text.
- The comparer seam (R3) makes the case-insensitive rows run on every OS. One end-to-end row needs a case-insensitive
  file system and is skipped on Linux.

**Target Platform**: Windows, macOS, Linux. Claude Code with the `Agent` tool (renamed from `Task` in v2.1.63; the
work agents already list `Agent` since 101).

**Project Type**: CLI plus bundled Claude Code skills and agents.

**Performance Goals**: `CriticSourceLoader` runs the matcher n times per document with n cited refs (was ≈ n² + n), and
scans line starts once (was once per kept section).

**Constraints**:
- `--full-docs` user part byte-identical to `full-docs-6a4c41a.txt`.
- Same refusals, exit codes, freshness records and JSON shapes; JSON-mode stdout is only the JSON document.
- No file-format change and no required config section (083).
- 096: edited installed files are never overwritten.
- 0 warnings (TreatWarningsAsErrors); suite green in a fresh `git worktree` with `core.autocrlf=true`.

**Scale/Scope**:
- ~7 source files: `CriticPromptBuilder`, `CriticPromptCompiler`, `CompileCriticPromptCommand`, `CriticSourceLoader`,
  `SourceRefResolver`, `SkillResourceLoader`, plus `SectionExtractor.LineStarts` made internal.
- 3 skills, 1 reference file, 5 agents.
- 1 new golden; ~4 new test classes; ~8 retargeted rows.
- 3 docs, CHANGELOG, CLAUDE.md.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Prompt templates stay in `.spectra/prompts/` (workspace) and the assembly (bundled); no new state | ✅ |
| II. Deterministic | The prompt is still a pure function of test, documents and template: same inputs → same bytes. F7 keeps the output identical (rows + golden) | ✅ |
| III. Orchestrator-agnostic, minimal output, no in-process model | No new output; JSON-mode stdout stays the JSON document; the critic stays a session subagent | ✅ |
| IV. CLI-first | No new command or option. The agents still write only staging inputs and verdict files; `ingest-verdict` is the same read-only call | ✅ |
| V. Simplicity | Removes a second prompt, a dead parameter and repeated matching. One seam added for counting (R7: two optional delegates on an internal method); one comparer exposed instead of a new abstraction | ✅ |
| Test-required discipline | F1–F5 and F7–F9 have rows that fail on `26bfe88` (F7's count row and F8's guard included); F6 is checked by the compiler; the 6a4c41a golden pins the user part | ✅ |

**Post-design re-check**: no violations. Deliberate behaviour changes:
- the critic's system prompt is the template, so verdicts may change through the 037/075 checks; this is the point of F1;
- a customised workspace template now takes effect;
- an `unchanged` repair is not re-judged (101 FR-026, unchanged repairs only);
- the generator's title line form changes;
- installed skills and agents change text, so `update-skills` refreshes unedited copies and keeps edited ones.

## Project Structure

### Documentation (this feature)

```text
specs/102-critic-template-review-fixes/
├── plan.md              # This file
├── research.md          # Phase 0 (R1–R10)
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1 (manual checks)
├── contracts/contracts.md   # Phase 1 (prompt shape, agent lines, loader seam)
├── checklists/requirements.md
└── tasks.md             # /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs              # R1: required loader; hard-coded prompt removed; BuildSystemPrompt()
src/Spectra.CLI/Verification/CriticPromptCompiler.cs             # R1/R6: Compile/Assemble(test, docs, loader); no criteriaContext
src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs  # R1: one PromptTemplateLoader per run, both forms; R6 using; R9 doc comment
src/Spectra.Core/Coverage/SourceRefResolver.cs                   # R3: public static PathComparer
src/Spectra.Core/Baseline/SectionExtractor.cs                    # R7: LineStarts private → internal
src/Spectra.CLI/Source/CriticSourceLoader.cs                     # R3 grouping; R7 match once, slice once, counting seam; F5 warning
src/Spectra.CLI/Skills/SkillResourceLoader.cs                    # R9: GenerateToolsList → Agent; comment
src/Spectra.CLI/Skills/Content/Agents/spectra-repairer.agent.md  # R4 critic for written only; R8; R9
src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md # R5 created:; R8; R9
src/Spectra.CLI/Skills/Content/Agents/spectra-updater.agent.md   # R8 (unchanged text); R9
src/Spectra.CLI/Skills/Content/Agents/spectra-analyst.agent.md   # R9
src/Spectra.CLI/Skills/Content/Agents/spectra-extractor.agent.md # R9
src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md        # R4 8b/fallback/--repaired; R5 Step 7; R9
src/Spectra.CLI/Skills/Content/Skills/spectra-update.md          # R4 repair section/fallback; R9
src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md        # R9
src/Spectra.CLI/Skills/Content/References/spectra-generate.md    # only if SKILL.md is over the cap: the Key rule's explanation (Test Creation Intent Routing); R4 check

tests/Spectra.CLI.Tests/TestFixtures/CriticGolden/full-docs-102.txt     # NEW (R2): new golden; full-docs-6a4c41a.txt stays as the pre-101 record
tests/Spectra.CLI.Tests/TestFixtures/CaseInsensitiveFsFactAttribute.cs  # NEW (R3)
tests/Spectra.CLI.Tests/Commands/Generate/CriticTemplatePromptTests.cs  # NEW (R2): template in every form/mode, workspace edit, JSON, user part
tests/Spectra.CLI.Tests/Source/CriticSourceLoaderReviewTests.cs         # NEW (R3, R7, F5): comparer seam, counts, warning
tests/Spectra.CLI.Tests/Skills/CriticStepConsistencyTests.cs            # NEW (R8)
tests/Spectra.CLI.Tests/Skills/ReviewFixLineAndToolTests.cs             # NEW (R4 repairer/skills, R5 created:, R9 tool name)
(+ retargeted: CriticSectionPromptTests golden row, CriticPromptBuilderTests, VerificationIntegrationTests,
   CriticPromptCompilerTests, GenerateAgentTests, GenerationSkillContractTests, GenerateOrchestratorTests,
   OrchestratorSkillTests, SkillsManifestTests and any NestedCriticTests row on the repairer's ids)
```

Docs: `docs/customization.md`, `docs/cli-reference.md`, `docs/grounding-verification.md`, `CHANGELOG.md`, the CLAUDE.md
Recent Changes row.

**Structure Decision**: The existing layout. Each change stays in the file that has the defect. New tests go beside
the 101 rows they extend.

## Implementation Order

1. **Baseline** on this branch: `dotnet build --no-incremental`, `dotnet test`; record totals and warnings in Notes.
   Write the new rows first and prove each fails on the untouched `src`. Rows that need new members are staged.
2. **F1 + F6** (R1, R2, R6): required loader, hard-coded prompt removed, the command's loader, the criteria parameter
   and import removed. Record `full-docs-102.txt`. Retarget the builder, compiler and integration rows.
3. **F2** (R3), then **F7 + F5** (R7). These are both in `CriticSourceLoader`, F2 first so the count rows use the final
   grouping. Existing `CriticSourceLoaderTests` and both golden rows stay green.
4. **F3** (R4): repairer step 5; generate 8b, fallback and `--repaired`; update repair section and fallback.
5. **F4** (R5): generator contract and example; generate Step 7.
6. **F8** (R8): the consistency guard, run against the final F3/F4 text.
7. **F9** (R9): tools list, comment, skill and agent wording, retargeted rows, the no-"Task tool" guard; run the
   update-skills rows.
8. **Docs** (R10) and the CLAUDE.md row.
9. **Verification**: full suite; 0 warnings; the same in a fresh `git worktree` with `core.autocrlf=true`; quickstart
   checks. Ask before committing and merging into `fix/doc-change-update-workflow`; never push.

## Risks

| Risk | Mitigation |
|---|---|
| The template's added checks change verdicts in real runs | Intended (F1). The 101 quality gate runs after 102 on the new prompt (spec Out of Scope) |
| A user's customised template has broken frontmatter | The loader falls back to the bundled template, as for every other compile command (logged warning); a row covers a valid custom template |
| The system part's line endings differ by checkout | Same as today's hard-coded raw string; rows compare through `Lf`; the fresh-worktree run proves it |
| Removing the hard-coded prompt breaks a test that relied on its exact text | The template contains every line of it (R1 diff); retargeted rows build through a loader |
| `Agent` in `tools:` is unknown to a Claude Code older than 2.1.63 | The six agents already use `Agent` since 101, so 102 sets no new minimum; the docs state `Agent` is current |
| An unchanged repair's verdict file is missing or stale | `ingest-verdict` gives no entry → the existing retry runs the critic once → else `unverified` |
| The F8 guard is brittle to harmless wording edits | The differences live in one table in the test; the guard message names the agent and the first differing line |
| `spectra-generate` SKILL.md exceeds its 16,000-character cap (~150 characters of headroom on the base) | The F3/F4 routing stays in SKILL.md (reference.md is never read during Steps 7–9). T018 frees space first, outside the instructions: the Key rule becomes one line, with its explanation moved to reference.md (−128), and the five "(Spec …)" annotations go (−86): 15,850 → 15,636. Then the exact edits follow: T018 +140, T022 +9, T029 +6 → ≈ 15,791, against a target of ≤ 15,900 (≈ 209 characters of headroom). "Analyze first", the NON-STOP contract, the 075 guard, questions and STOPs are never touched, and the cap is never raised. T020, T023 and T031 record the size; over 15,900 at T031 means stop and report |
| Counting seam leaks into production | Internal method with optional delegates defaulting to the real functions; public signature unchanged |

## Complexity Tracking

No violations to justify.
