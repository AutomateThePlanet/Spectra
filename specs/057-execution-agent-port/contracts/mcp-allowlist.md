# Contract: MCP Allowlist (`.claude/settings.json`)

**Surface**: `ClaudeSettingsInstaller` (new, pure + idempotent) and `InitHandler` (calls it).

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | no `.claude/settings.json` | `spectra init` | the file is created with `permissions.allow` containing `mcp__spectra__*` |
| 2 | an existing `.claude/settings.json` with other `permissions.allow` entries | `spectra init` | `mcp__spectra__*` is added and the existing entries are preserved |
| 3 | `.claude/settings.json` already containing `mcp__spectra__*` | `spectra init` | no duplicate is added (idempotent no-op) |
| 4 | the installed settings | inspect | `mcp__spectra__*` is present and is a **distinct** entry from `Bash(spectra-mcp:*)` |
| 5 | the Spectra repo | inspect | the repo's own `.claude/settings.json` carries `mcp__spectra__*` (dogfood) |

## Invariants

- The allowlist is **client-side only**; no server-side enforcement is added (`ToolRegistry` is
  unchanged).
- `Bash(spectra-mcp:*)` in `.claude/settings.local.json` is left as-is — the new entry is added
  alongside it, never conflated with it.
- The wildcard `mcp__spectra__*` covers all 25 MCP tool method names; no per-tool enumeration.
- Writing the allowlist removes per-call permission prompts but does **not** suppress the intentional
  human-verdict pause (that pause is agent behavior, not a permission prompt).
