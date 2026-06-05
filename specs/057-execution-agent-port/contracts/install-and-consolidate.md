# Contract: Install Relocation + Duplicate Consolidation

**Surface**: `SkillInstallLayout`, `InitHandler`, `AgentResourceLoader` (deleted), the two
`Agent/Resources/` files (deleted), the csproj.

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | a clean workspace | `spectra init` | the execution agent is written to `.claude/skills/spectra-execution/SKILL.md` (not `.github/`) |
| 2 | a clean workspace | `spectra init` / `update-skills` | there is exactly **one** execution-agent artifact installed (no `.github/agents/spectra-execution.agent.md`, no `.github/skills/spectra-execution/SKILL.md`) |
| 3 | `SkillInstallLayout.AgentPath` | called with `spectra-execution.agent.md` | returns `.claude/skills/spectra-execution/SKILL.md` |
| 4 | the codebase | inspect | `Agent/Resources/spectra-execution.agent.md`, `Agent/Resources/SKILL.md`, and `AgentResourceLoader` no longer exist; the two csproj `EmbeddedResource` lines are gone |
| 5 | `init` | run | succeeds (exit 0) and registers the execution skill in the skills manifest |
| 6 | a user-modified installed execution skill | `update-skills` | it is skipped (preserved) — hashing unchanged |

## Invariants

- The reused `SkillResourceLoader` / `SkillsManifest` install+hash mechanism is unchanged in mechanism
  — only the execution agent's target path changes and the redundant `.github/` install is removed.
- The other artifacts placed by the preceding spec (authoring skills under `.claude/skills/`, the
  critic under `.claude/agents/`) are unaffected.
- The legacy `test-generation` skill install (`.github/skills/test-generation/SKILL.md`, from a
  separate template) is out of this port's scope and untouched.
