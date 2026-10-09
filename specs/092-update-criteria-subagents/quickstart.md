# Quickstart: verifying Spec 092

## Automated

```bash
dotnet test --filter "FullyQualifiedName~AgentInstallLayoutTests"      # T1, T2 (install, update-skills, pre-092)
dotnet test --filter "FullyQualifiedName~OrchestratorAgentTests"       # T3 (frontmatter, return contracts), T4 (critic line)
dotnet test --filter "FullyQualifiedName~OrchestratorSkillTests"       # T5, T6 (update / criteria skill guards)
dotnet test --filter "FullyQualifiedName~SkillsManifestTests"          # retargeted 088–091 rows
dotnet test                                                             # everything; only ProfileFormatLoaderTests fails
```

## Install check with the built CLI

1. **New workspace:** `spectra init` → `.claude/agents/` holds `spectra-critic.agent.md`, `spectra-updater.agent.md`,
   `spectra-repairer.agent.md`, `spectra-extractor.agent.md`; `.github/agents/` doesn't exist.
2. **Pre-092 workspace:** in a fresh init, delete the three new agent files and their entries in
   `.spectra/skills-manifest.json`, append a line to `.claude/skills/spectra-update/SKILL.md`, run `spectra
   update-skills` → the three agents are back, "Skipped (customized by user)" lists the update skill, and its content
   still ends with the appended line.

## Manual acceptance (a live Claude Code session; the user runs it)

Set up a scratch workspace with `acceptance/setup-workspace.sh <empty-dir> [spectra command]` (one doc with three sections, 33 tests linked to
them, a baseline, then a doc change that makes all 33 tests LOAD in 6 groups; and 5 new docs for extraction).

1. **Update (SC-001):** run `/context` and note the total. Ask the assistant to update the suite, choose LOAD, accept
   re-verification. When it finishes, run `/context` again.
   - no auto-compaction happened;
   - (after − before) / number of LOAD tests ≤ 300 tokens;
   - the report lists every test's outcome, each group's baseline line, every warning, and the re-verification counts.
2. **Criteria (SC-002):** change 5+ docs, ask the assistant to extract criteria.
   - the main transcript contains no document text and no compiled prompt (only Task calls and returned lines);
   - the summary lists every document's changeset lines (`+`, `~`, `-` with referencing tests, `*`) and warnings.
3. **Relay (SC-003):** compare both reports with the same runs on the 091 build: no outcome, baseline line, warning or
   changeset line is missing.
