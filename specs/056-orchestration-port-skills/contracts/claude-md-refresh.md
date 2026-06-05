# Contract: CLAUDE.md Runtime-Declaration Refresh

**Surface**: `CLAUDE.md` (repo root) and the affected documentation pages.

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | `CLAUDE.md` | read line 6 (and the file) | **0** occurrences of "GitHub Copilot SDK (sole AI runtime)" / "sole AI runtime" |
| 2 | `CLAUDE.md` | read the tech-stack/runtime statement | describes the Claude-Code-only runtime model |
| 3 | the authoring docs (skills-integration, getting-started/quickstart) | read | describe the `.claude/skills/` surface as the authoring path of record and the `.claude/settings.json` allowlist setup step (allowlist **content** is the next spec) |
| 4 | the Copilot chat/CLI/spaces-setup + customization pages | read | superseded content is marked deprecated or replaced with the Claude Code equivalent |

## Invariants

- `CLAUDE.md` stays under the 40K-char compactness budget (existing repo constraint).
- No documentation describes the `mcp__spectra__*` allowlist **content** (that is the next,
  execution-side spec) — only the setup **step** referencing it is added.
- The change is documentation-only for this contract (no behavior depends on `CLAUDE.md`).
