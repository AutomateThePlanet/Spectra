# Implementation Plan: Token Economy Follow-up

**Branch**: `101-token-economy-followup` | **Date**: 2026-10-19 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/101-token-economy-followup/spec.md`

## Summary

Four changes that cut the tokens the authoring flows spend. Nothing changes except what the critic sees and how the
skills and agents are packaged:
- **F1**: the critic gets the sections a test cites instead of the first 8,000 characters of each whole document.
  `--full-docs` keeps today's input. The dead criteria load goes. A measured gate decides whether F2 and F3 go ahead.
- **F2**: the generate and update SKILL.md bodies shrink to ≤ 16,000 characters each (token count measured under
  5,000), so all of each comes back after compaction. Rarely needed material moves to a `reference.md` beside each, installed and tracked under the 096
  rules.
- **F3**: the work subagents (generator, updater, repairer) run the critic for their own tests and return one verdict
  line per test. The main session routes by those lines and runs the critic itself only after `critic: not run`.
- **F4**: the six agents carry only fields Claude Code reads. Their descriptions say they are only for use by their skill.

Planning corrected two background facts: the critic has never received criteria, and each document is cut at 8,000
characters. The user chose to keep the critic criteria-free and to split the gate (spec Clarifications; research R0).

## Technical Context

**Language/Version**: C# 12, .NET 8.

**Primary Dependencies** (all existing):
- `CompileCriticPromptCommand`, `SourceRefDocumentLoader`, `SourceRefResolver`, `SectionExtractor`,
  `CriticPromptBuilder` (unchanged), `CriteriaContextLoader` (call removed from the critic command only);
- `SkillResourceLoader`, `SkillContent`, `AgentContent`, `SkillInstallLayout`, `SkillsManifest`, `ManifestKey`,
  `InitHandler`, `UpdateSkillsHandler`;
- bundled `spectra-generate.md`, `spectra-update.md`, `spectra-review-flagged.md`, and the six `*.agent.md`.

No new packages.

**Storage**: no format change. New installed files `.claude/skills/<skill>/reference.md`, recorded in
`.spectra/skills-manifest.json` like any other bundled file.

**Testing**:
- xUnit; real-init workspaces (`BaselineTestWorkspace`, `ManifestWorkspace`, the non-parallel `CriteriaCommands`
  collection) for install rows;
- critic-prompt golden rows built from fixture text with LF endings (099 lesson: independent of checkout line endings);
- skill and agent text guards against the embedded content, and the copies of the NON-STOP and 075 text that already
  exist.

Baseline `6a4c41a` (plan-time run, 2026-10-19): Core 1228 / CLI 2345 / Execution 228 / Integration 8, 0 failures.

**Target Platform**: Windows and Linux; Claude Code with nested subagents (default depth 3) for F3, with a fallback when
nesting is off.

**Project Type**: CLI plus bundled Claude Code skills and agents.

**Performance Goals**:
- critic prompt median no larger than `--full-docs` on the gate's tests;
- main-session growth ≤ 300 tokens per test (092/093 target) and below `6a4c41a`;
- SKILL.md bodies ≤ 16,000 characters each, with a measured token count under 5,000 each.

**Constraints**:
- `--full-docs` byte-identical to `6a4c41a`;
- same refusals, exit codes, freshness and JSON shapes;
- no file-format change; no required config section (083); human summaries on stderr (constitution III);
- NON-STOP and 075 guard text verbatim in SKILL.md;
- 096: edited files are never overwritten;
- each change fails on `6a4c41a`;
- verified in a fresh checkout, on a real `spectra init` workspace and on a pre-101 workspace with edited files.

**Scale/Scope**:
- ~8 edited source files and 1 new (`CriticSourceLoader`);
- 2 skills slimmed, 2 new reference files, 1 skill touched (`spectra-review-flagged`: retarget only), 6 agents;
- ~6 new test classes, ~10 retargeted (research R8);
- 4 docs.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Reference files are bundled content, installed into the repo and tracked by the committed manifest | ✅ |
| II. Deterministic | Section assembly is pure: same refs, same files → same bytes (FR-005); first-reference order; no timestamps | ✅ |
| III. Orchestrator-agnostic, minimal output, no in-process model | Smaller critic input; `compile-critic-prompt` output shape unchanged; stderr notes only; the critic stays a session subagent | ✅ |
| IV. CLI-first | One option on an existing command (`--full-docs`), no new command. The work agents only add a read-only CLI call (`ingest-verdict`); every write (`ingest-grounding`, `record-drop`, `delete`) stays a main-session CLI call | ✅ |
| V. Simplicity | Reuses `SectionExtractor`, `SourceRefResolver`, the builder and the 096 manifest rules; one reference file per skill; no shared helper extracted (R1). `--full-docs` keeps the pre-101 document loading (see Complexity Tracking) | ⚠ justified, tracked |
| Test-required discipline | Each change has a row failing on `6a4c41a`; manual gate and measurements recorded | ✅ |

**Post-design re-check**: one tracked item (V, `--full-docs`; Complexity Tracking). Deliberate behaviour changes:
- the default critic input is section-scoped (`--full-docs` restores it);
- a malformed criteria file no longer affects `compile-critic-prompt`;
- the update skill asks its re-verify question before the first updater call;
- the per-test critic runs move from the main session to the work subagents;
- agent descriptions and frontmatter change, so `update-skills` refreshes unedited agents and skips edited ones (096).

## Project Structure

### Documentation (this feature)

```text
specs/101-token-economy-followup/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1 (gate, budgets, workspaces)
├── contracts/contracts.md   # Phase 1 (CLI option, prompt shape, agent result lines, frontmatter)
├── checklists/requirements.md
└── tasks.md             # /speckit.tasks
```

### Source Code (repository root)

```text
src/Spectra.CLI/Source/CriticSourceLoader.cs                     # NEW (R1): cited sections per document
src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs  # R2: --full-docs, loader choice; criteria load removed
src/Spectra.CLI/Spectra.CLI.csproj                               # R6: embed Skills\Content\References\*.md
src/Spectra.CLI/Skills/SkillResourceLoader.cs                    # R6: GetAllReferences (new prefix)
src/Spectra.CLI/Skills/SkillReferenceContent.cs                  # NEW (R6): skill name → reference content
src/Spectra.CLI/Skills/SkillInstallLayout.cs                     # R6: ReferencePath
src/Spectra.CLI/Skills/ManifestKey.cs                            # R6: InstallTail knows .claude/skills/<n>/reference.md
src/Spectra.CLI/Commands/Init/InitHandler.cs                     # R6: install + record references (not with --skip-skills)
src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs     # R6: process references with the 096 rules
src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md        # F2 slim; F3 routing by verdict lines; F4 wording
src/Spectra.CLI/Skills/Content/Skills/spectra-update.md          # F2 slim; F3 verify question + routing; F4 wording
src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md  # retarget "same procedure as Step 8" to the reference
src/Spectra.CLI/Skills/Content/References/spectra-generate.md    # NEW (F2): installed as .claude/skills/spectra-generate/reference.md
src/Spectra.CLI/Skills/Content/References/spectra-update.md      # NEW (F2)
src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md # F3 critic loop + verdict lines; F4 frontmatter
src/Spectra.CLI/Skills/Content/Agents/spectra-updater.agent.md   # F3 (verify: yes|no); F4
src/Spectra.CLI/Skills/Content/Agents/spectra-repairer.agent.md  # F3; F4
src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md    # R3 full_docs input; F4
src/Spectra.CLI/Skills/Content/Agents/spectra-analyst.agent.md   # F4
src/Spectra.CLI/Skills/Content/Agents/spectra-extractor.agent.md # F4

tests/Spectra.CLI.Tests/Source/CriticSourceLoaderTests.cs              # F1 unit: sections, absorption, fallback note, whole file
tests/Spectra.CLI.Tests/Commands/Generate/CriticSectionPromptTests.cs  # F1 CLI: section mode, --full-docs golden, determinism, refusals, no criteria
tests/Spectra.CLI.Tests/Skills/LeanSkillTests.cs                       # F2: size caps, verbatim NON-STOP/075, decision points, reference link
tests/Spectra.CLI.Tests/Skills/SkillReferenceInstallTests.cs           # F2: init, update-skills, edited files, moved workspace, --skip-skills
tests/Spectra.CLI.Tests/Skills/NestedCriticTests.cs                    # F3: agents' tools, critic loop, verdict lines, fallback; skills' routing
tests/Spectra.CLI.Tests/Skills/AgentFrontmatterTests.cs                # F4: allowed fields, description prefix, no context: fork wording
(+ the retargeted rows of research R8)
```

Docs: `docs/usage.md`, `docs/cli-reference.md` (`compile-critic-prompt --full-docs`, section-scoped input),
`docs/grounding-verification.md` (what the critic sees), `CHANGELOG.md`, the CLAUDE.md Recent Changes row.

**Structure Decision**: The existing CLI project layout. New code sits beside the code it mirrors
(`Source/` for the loader, `Skills/` for the reference content).

## Implementation Order

The same order as tasks.md: F1 first, then F2, F3, F4. F4 doesn't depend on the gate and may run earlier (right after
the proof run), but the default is last.

1. **Baseline** on `6a4c41a`: test totals in Notes. Write the guard rows; prove each fails on the untouched `src`
   (stage rows that need new members).
2. **F1**: `CriticSourceLoader`, `--full-docs`, the criteria load removed, the critic agent's `full_docs` input; the
   golden and determinism rows.
3. **F1 gate (manual, quickstart §1)**: record the results in Notes. **If it fails, stop and report; F2 and F3 don't
   start.**
4. **F2 packaging**: the reference resource, loader, layout, manifest key, init and update-skills; install rows.
5. **F3**: the work agents' critic loop and verdict lines; the skills' routing and fallback; the update skill's
   question moved.
6. **F2 slimming**: move text to the reference files (after F3, so Step 8 / 5a are slimmed in their final form);
   retarget the R8 rows; list each retargeted guard in Notes; measure both SKILL.md token counts.
7. **F4**: the agent frontmatter and descriptions, the allowed-field guard, the skill wording, and the retargeted
   frontmatter rows.
8. **Docs** and the CLAUDE.md row.
9. **Verification**: full run in a fresh checkout; quickstart §2–§5 (real init, pre-101 workspace with edits,
   generation of ≥ 24 tests and update of ≥ 3 groups with `/context`, a compaction run, `claude --debug`). Ask before
   committing and merging into `fix/doc-change-update-workflow`; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Section mode judges worse (missing context outside the cited section) | The gate (US2) before F2/F3; `--full-docs` / `full_docs: true` as an escape hatch |
| `--full-docs` drifts from `6a4c41a` | A golden row compares it with a prompt captured from the `6a4c41a` code path on the same fixture |
| A reference file is picked up as a skill | Separate resource prefix (R6); `SkillsManifestTests` keeps `Assert.Equal(16, SkillContent.All.Count)` |
| A moved workspace drops `reference.md` keys | `ManifestKey.InstallTail` learns the shape; `UpdateSkillsReportTests.AMovedWorkspace_ReportsNoWarning` covers it |
| 16,000 characters still exceeds 5,000 tokens for a command-heavy skill | Token count measured by hand (quickstart §3); if over, more reference material moves out |
| Slimming removes a decision point or STOP | `LeanSkillTests` pins each question, STOP and the verbatim NON-STOP / 075 text, listed from the `6a4c41a` text |
| Nesting is off in a user's setup | `critic: not run` fallback: the main session runs today's per-test procedure (kept in the reference file, named from SKILL.md) |
| A work agent misreports a verdict | `ingest-grounding` reads the verdict files and writes only fresh grounded ones; drops still need the critic's line to agree |
| The 5,000-token re-attach budget is per skill but 25,000 in total | Only one authoring skill runs at a time; both fit alone |
| `claude --debug` message needs Claude Code ≥ 2.1.296 | The manual check notes the version used; the automated allowed-field guard doesn't depend on it |

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| V (no backwards-compatibility shims): `--full-docs` keeps the pre-101 document loading | Needed as the F1 quality gate's baseline and as an escape hatch if section mode misjudges a test | Deleting the old path would leave no baseline to measure the gate against and no fallback. Revisit after the gate passes in real use |
