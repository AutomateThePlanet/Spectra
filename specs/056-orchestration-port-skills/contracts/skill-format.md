# Contract: Ported SKILL.md Format (no Copilot-isms)

**Surface**: every ported authoring artifact (`Content/Skills/*.md` + `Content/Agents/spectra-generation.agent.md`)
and the `SkillResourceLoader` tool-list constants.

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | any ported skill | inspect frontmatter | contains `name` and `description`; contains a `tools` list of Claude Code tool names |
| 2 | any ported skill | inspect for `model: GPT-4o` | **0** occurrences |
| 3 | any ported skill | inspect for `disable-model-invocation` | **0** occurrences |
| 4 | any ported skill | inspect for `{{` … `}}` | **0** unexpanded placeholders (the loader resolves `{{…TOOLS}}` to Claude Code tools) |
| 5 | any ported skill | inspect for Copilot tool names (`execute/`, `read/`, `browser/`, `search/…` Copilot forms) | **0** occurrences in the resolved `tools` list |
| 6 | any ported skill body | inspect for `runInTerminal` / `awaitTerminal` / `show preview` | **0** occurrences; the terminal/confirmation discipline is present in Claude Code terms |
| 7 | the resolved `{{GENERATION_TOOLS}}` / `{{READONLY_TOOLS}}` constants | inspect | Claude Code tool names (`Read`, `Grep`, `Glob`, `Bash`, and `Task` for the generation set) |

## Invariants

- The reused CLI-verb command lines and their flags (`--no-interaction`, `--output-format json`,
  `--verbosity quiet`, `.spectra-result.json` reads) are **preserved** — they are the regression-net
  surface, not a Copilot-ism.
- The critic subagent's own `model: claude-sonnet-4-6` and `disable-model-invocation: true` are
  **kept** (a subagent legitimately pins its model and is explicit-only) — rules 2/3 above apply to
  the **authoring** set, not to the subagent.
- The execution agent is **not** in this set (untouched).
