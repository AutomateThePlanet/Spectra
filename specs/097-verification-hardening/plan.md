# Implementation Plan: Verification Hardening (Phase 6, Part 2)

**Branch**: `097-verification-hardening` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/097-verification-hardening/spec.md`

## Summary

Seven fixes, no features beyond the named options:
- the generate and update skills name the tests they ground (`--ids`);
- `audit-grounding` lists one suite only, filters by action and id, and flags stale verdicts;
- the repair commands refuse stale verdicts;
- freshness compares two file-system times (the record's file time instead of the process clock);
- an empty `--ids` means "not given";
- `--dry-run` works on the three batch ingests;
- the critic model is configurable (`ai.critic.model`, default `sonnet`) and the dead in-process critic code goes.

## Technical Context

**Language/Version**: C# 12, .NET 8.

**Primary Dependencies**:
- `VerdictFreshness` (+ `FreshnessResult`), `GroundingWriteBackService`, `IdList`;
- `AuditGroundingCommand` / `AuditGroundingHandler` (+ `AuditGroundingResult` / entry / summary);
- `CompileRepairPromptCommand`, `CompileRepairBatchCommand`;
- `IngestGroundingCommand`, `IngestVerdictCommand`, `IngestAnalysisCommand`;
- `ConfigLoader`, `AiConfig`, `CriticConfig`, the init config template;
- `AgentContent`, `SkillResourceLoader`, `InitHandler`, `UpdateSkillsHandler`;
- the bundled `spectra-generate.md`, `spectra-update.md`, `spectra-critic.agent.md`.

No new packages.

**Storage**: no format change. The record's file time becomes the freshness reference. The config gains optional
`ai.critic.model`.

**Testing**:
- xUnit 2.9.3, with `BaselineTestWorkspace` real-init rows in the non-parallel `CriteriaCommands` collection;
- file times set explicitly (`File.SetLastWriteTimeUtc`) for F4;
- skill-text guards against copies of the `807047c` text.

Baseline `807047c`: Core 1245 / CLI 2228 / Execution 228 / Integration 8, 0 failures.

**Target Platform**: Windows and Linux (file-time resolution differs; the 2 s allowance covers both).

**Project Type**: CLI.

**Performance Goals**: none new. The audit reads fewer files (one suite's verdicts).

**Constraints**:
- no config section becomes required (083);
- `--dry-run` writes nothing;
- human summaries on stderr in JSON mode;
- additive JSON;
- every fix fails on `807047c`;
- verified on real-init and pre-097 workspaces;
- suite fully green.

**Scale/Scope**:
- ~14 edited source files, 2 deleted (`CriticFactory`, `CriticModelResolver`) and their 2 test classes;
- 3 bundled content files;
- ~7 new test classes;
- 5 docs, plus 2 old plan.md rows.

## Constitution Check

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | `ai.critic.model` lives in the committed config; the agent file is rendered from it | ✅ |
| II. Deterministic | Freshness compares two file-system times; the audit and filters are deterministic | ✅ |
| III. Orchestrator-agnostic, minimal output, no in-process model | The audit `--action` / `--ids` filters shrink output; dead in-process critic code removed | ✅ |
| IV. CLI-first; `--dry-run` / `--no-review` | `--dry-run` added (F6) to `ingest-grounding --all`, `ingest-verdict`, `ingest-analysis`. `--no-review` is accepted (global option); none of the three commands prompts, so it is a no-op. Refusals keep named codes (exit 4) | ✅ (closes the 091/094 tracked gap) |
| V. Simplicity | Reuses the 090/091 freshness check, `IdList`, the 096 manifest rule; removes ~2 dead classes | ✅ |
| Test-required discipline | Each fix has a row failing on `807047c` | ✅ |

**Post-design re-check**: no violations. Deliberate behaviour changes:
- the audit drops stray verdicts and other suites;
- a stale verdict becomes `critic`;
- `compile-repair-*` refuse stale verdicts;
- `--ids ""` no longer means "nothing";
- `--dry-run` stops writing;
- `update-skills` now reads the config for the critic model; an invalid config only warns and leaves the critic's
  model line unchanged (exit 0);
- a pinned critic agent moves to `sonnet`.

## Project Structure

```text
src/Spectra.CLI/Verification/VerdictFreshness.cs              # R4: record file time as reference (ReferenceAt)
src/Spectra.CLI/Commands/Review/AuditGroundingCommand.cs      # R2: --action, --ids
src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs      # R2: suite scope, stale, filters, not_found
src/Spectra.CLI/Results/AuditGrounding*.cs (where defined)    # stale, summary.stale, not_found
src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs   # R3: VERDICT_STALE exit 4
src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs    # R3: skip stale, stderr line
src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs   # R5 empty ids; R6 dry run, --test refusal
src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs     # R5 empty ids; R6 dry_run
src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs    # R6 dry run (no plan save)
src/Spectra.CLI/IO/GroundingWriteBackService.cs               # R6 dryRun parameter
src/Spectra.Core/Config/ConfigLoader.cs                       # R7 ai.critic.model validation
src/Spectra.Core/Models/Config/AiConfig.cs                    # R7 Critic, effective model
src/Spectra.Core/Models/Config/CriticConfig.cs                # R7 trimmed to Model
src/Spectra.CLI/Templates/spectra.config.json                 # R7 ai.critic.model: sonnet
src/Spectra.CLI/Skills/AgentContent.cs                        # R7 Render(criticModel)
src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md # model: {{CRITIC_MODEL}}
src/Spectra.CLI/Commands/Init/InitHandler.cs                  # R7 render critic from the written config
src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs  # R7 load config; render critic
src/Spectra.CLI/Agent/Critic/CriticFactory.cs                 # DELETED
src/Spectra.CLI/Agent/Critic/CriticModelResolver.cs           # DELETED
src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md     # R1 8a --ids; resume --action repair
src/Spectra.CLI/Skills/Content/Skills/spectra-update.md       # R1 5a --ids

tests/Spectra.CLI.Tests/Skills/GroundingIdsSkillTests.cs              # F1 (skill text + guard verbatim)
tests/Spectra.CLI.Tests/Commands/Generate/GroundingIdsBatchTests.cs   # F1 (CLI: only named tests get blocks)
tests/Spectra.CLI.Tests/Commands/Review/AuditTruthTests.cs            # F2
tests/Spectra.CLI.Tests/Commands/Generate/RepairFreshnessTests.cs     # F3
tests/Spectra.CLI.Tests/Commands/Generate/OneClockTests.cs            # F4
tests/Spectra.CLI.Tests/Commands/Generate/EmptyIdsTests.cs            # F5
tests/Spectra.CLI.Tests/Commands/Generate/BatchDryRunTests.cs         # F6
tests/Spectra.CLI.Tests/Skills/CriticModelTests.cs                    # F7 (config, init, update-skills, pre-097)
tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs, CriticAuthTests.cs   # DELETED (dead code's tests)
```

Docs:
- `docs/cli-reference.md`, `docs/usage.md`, `docs/configuration.md`, `CHANGELOG.md`, `CLAUDE.md`;
- `specs/091-review-fixes-090/plan.md` and `specs/094-generation-batch-prompts/plan.md`: the IV rows resolved and the
  Complexity Tracking rows removed.

## Implementation Order

1. Baseline on `807047c`. Write the guard rows; run the proof on the untouched `src` (stage rows that need new
   members).
2. F4 (`VerdictFreshness` reference clock): F2 and F3 call it.
3. F5 (`IdList`-based "given"), shared by F2 `--ids`.
4. F2 (audit), F3 (repair commands).
5. F6 (`--dry-run`: write-back service flag, handlers, refusal).
6. F1 (skill text: 8a, 5a, resume).
7. F7 (config key, template, agent placeholder, init/update-skills rendering, removals).
8. Docs (+ the 091/094 plan rows), CLAUDE.md sentence.
9. Full run, built-CLI quickstart on real init and pre-097, Notes. Ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Existing rows rely on stray verdicts in the audit or on `compiled_at` being the reference | Inventory task; retarget deliberately with Notes |
| File-time resolution (e.g. 2 s on FAT, 1 s on some mounts) | The 2 s allowance stays; tests set file times explicitly |
| `update-skills` could start failing on a broken config | It never fails on the config (FR-022a): one warning, the critic's model line kept, everything else refreshed |
| A model value read as YAML non-string (e.g. starting with `[`) | The value must start with a letter or digit (FR-020) |
| The critic agent text changes (placeholder), so 096's hash rule sees a new bundled content | Rendered content is what is compared; unedited copies update, edited ones are skipped (tested) |
| Removing `CriticConfig` members breaks other code | Grep shows only the dead classes and their tests use them; build confirms |

## Complexity Tracking

No violations. This spec closes the constitution IV gap tracked in 091 and 094.
