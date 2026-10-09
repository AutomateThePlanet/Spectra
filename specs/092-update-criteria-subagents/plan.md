# Implementation Plan: Update and Criteria Skills as Orchestrators (Phase 5)

**Branch**: `092-update-criteria-subagents` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/092-update-criteria-subagents/spec.md`

## Summary

The update and criteria skills keep their user-facing flow but hand the heavy per-group, per-repair and per-document
work to three new forked agents — `spectra-updater`, `spectra-repairer`, `spectra-extractor` — that return a few lines
each (data-model.md). The critic gains a one-line final answer so the update skill routes without reading verdict
files. Every bundled agent installs to `.claude/agents/` by one rule. No CLI, config, prompt-template or file-format
change: the only C# change is the install routing (research R1).

## Technical Context

**Language/Version**: C# 12, .NET 8 (one routing method); Markdown skill and agent content (embedded resources).
**Primary Dependencies**: `SkillInstallLayout`, `AgentContent` / `SkillResourceLoader` (embedded `Agents/*.md` glob,
placeholders), `InitHandler` and `UpdateSkillsHandler` (already iterate every agent), the bundled skills
`spectra-update` and `spectra-criteria`, the critic agent. No new packages.
**Storage**: none new. Agents write only the staging files and `.spectra/criteria.json` they write today, via Write.
**Testing**: xUnit 2.9.3; text-contract tests over `SkillContent` / `AgentContent`; install tests through the real
`InitHandler` and `spectra update-skills` (`BaselineTestWorkspace` / `UpdatePromptWorkspace.InitWithRealTemplateAsync`).
Baseline at `309174b`: Core 1219, CLI 2046 (one known failure), Execution 228, Integration 8; 4 known warnings.
**Target Platform**: Windows and Linux; Claude Code as the assistant runtime.
**Project Type**: CLI with bundled assistant skills and agents.
**Performance Goals**: main-session context growth ≤ 300 tokens per updated test; no document text or compiled prompt in
the main transcript during extraction (manual acceptance, quickstart.md).
**Constraints**:
- no CLI / config / template / format change — stop and report if one turns out necessary;
- sequential subagents only;
- every 084–091 contract holds (triage STOP, mandatory critic, bounded retries, NON-STOP, staging, exit 8,
  `unchanged_pending`, `--ids`);
- nothing the user saw before disappears (FR-018/019);
- each new guard fails on `309174b`.

**Scale/Scope**: 1 C# method; 3 new agent files; 3 edited content files (critic, update skill, criteria skill); 4 docs;
3 new test classes; ~25 retargeted assertions and 1 changed count (research R8).

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | No file format changes; tests, criteria and baselines are still written only by the ingest commands | ✅ |
| II. Deterministic execution | Engine untouched; the CLI seam stays deterministic and model-free | ✅ |
| III. Orchestrator-agnostic, minimal output | The CLI surface is unchanged and works from any orchestrator or terminal. The agents are Claude Code packaging of the same seam, like the existing skills (Spec 056). No in-process model | ✅ |
| IV. CLI-first; the agent never writes to the filesystem directly except through validated handlers | Agents write only staging / extraction files that the ingest commands validate, exactly as the in-session flow does today | ✅ |
| V. Simplicity | One routing rule instead of a name switch; agents reuse the existing recipes; no new commands | ✅ |
| Test-required discipline | T1–T6 plus retargeted rows; each guard proven against `309174b` | ✅ |

**Post-design re-check**: no violations. The manual acceptance (SC-001–SC-003) needs a live Claude Code session and is
the user's run (quickstart.md); automated tests cover install, frontmatter and the text contracts.

## Project Structure

### Documentation

```text
specs/092-update-criteria-subagents/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/agent-contracts.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.CLI/Skills/
├── SkillInstallLayout.cs                         # AgentPath: every agent → .claude/agents/<file>
└── Content/
    ├── Agents/
    │   ├── spectra-critic.agent.md               # + final one-line rule
    │   ├── spectra-updater.agent.md              # NEW
    │   ├── spectra-repairer.agent.md             # NEW
    │   └── spectra-extractor.agent.md            # NEW
    └── Skills/
        ├── spectra-update.md                     # context budget contract; Step 5/5a orchestrate; relay
        └── spectra-criteria.md                   # Step 2 orchestrates; Step 3 from returned lines

tests/Spectra.CLI.Tests/Skills/
├── AgentInstallLayoutTests.cs                    # T1, T2
├── OrchestratorAgentTests.cs                     # T3, T4
├── OrchestratorSkillTests.cs                     # T5, T6
└── SkillsManifestTests.cs                        # retargeted rows (R8)
```

## Implementation Order

1. **Baseline** on `309174b`; write T1–T6 first and run them on the untouched sources (proof, as in 089–091).
2. **Install routing** (R1).
3. **Agents**: updater, repairer, extractor (contracts/agent-contracts.md); the critic's final line.
4. **Skills**: spectra-update (contract, Step 5, Step 5a, Step 7 relay), spectra-criteria (Step 2, Step 3).
5. **Retarget** the 088–091 rows (R8); update `AgentContent_HasAllAgents`.
6. **Docs**: usage.md (update and criteria flows), cli-reference.md where it describes these skills, CHANGELOG, CLAUDE.md.
7. **Finish**: full build and test run, the install quickstart with the built CLI, the scratch workspace script for the
   user's manual acceptance, Notes. Ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| A subagent paraphrases instead of returning the exact lines | Return contracts are stated as the agent's whole final message, with examples; the skills report missing/malformed lines as unknown instead of filling gaps |
| Main still reads a file "to check" | The context budget contract names each forbidden read; T5/T6 assert no `Read` of verdict/test/staging files and no compile/ingest commands in the orchestrated steps |
| Moving text out of the skills loses a 088–091 rule | R8: every moved assertion is retargeted to the agent with the same string; the inventory is checked off in tasks.md |
| A customised update/criteria skill keeps the in-session flow | Existing update-skills rule; documented in CHANGELOG |
| The 300-tokens-per-test target isn't met | Manual acceptance; if missed, the report names which returned lines dominate (likely warnings) — no CLI change in this phase |

## Complexity Tracking

No constitution violations.
