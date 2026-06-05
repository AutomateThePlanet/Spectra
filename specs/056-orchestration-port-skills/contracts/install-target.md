# Contract: Install Target (`init` / `update-skills` → `.claude/`)

**Surface**: `InitHandler.CreateBundledSkillFilesAsync` / `InstallAgentFilesAsync` and
`UpdateSkillsHandler.ExecuteAsync` (the install/update loops). Mechanism reused; target path changed.

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | a clean workspace | `spectra init` (skills not skipped) | each authoring skill is written to `.claude/skills/<name>/SKILL.md`; the critic subagent to `.claude/agents/spectra-critic.agent.md`; all recorded in the `.spectra/` manifest by content hash |
| 2 | a clean workspace | `spectra update-skills` after init | same `.claude/` targets are written/updated through the unchanged manifest+hash pipeline |
| 3 | the install completed | inspect the authoring set's locations | **0** authoring skills land under `.github/skills/`; **0** ported agents under `.github/agents/` |
| 4 | the execution agent | after init/update | it is **still** installed at its current `.github/` location — untouched, not relocated (scope boundary) |
| 5 | a user-modified installed skill | `update-skills` | it is **skipped** (preserved) — update-detection hashing unchanged |
| 6 | an unmodified installed skill whose bundled content changed | `update-skills` | it is **rewritten** and the manifest hash updated — unchanged behavior |

## Invariants

- The `SkillsManifestStore`, `FileHasher`, and embedded-resource reflection are **unchanged in
  mechanism** — only the target path string changes (`.github/…` → `.claude/…`).
- `init` / `update-skills` exit-code contract is unchanged (success/error).
- No new CLI flag; no change to which artifacts are bundled (the content of those artifacts changes,
  the bundling does not).
