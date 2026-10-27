# SPECTRA

This repository uses SPECTRA (the `spectra` CLI) to generate, verify, update and execute manual test cases from its
documentation. SPECTRA makes no model calls: you do the reasoning, and `spectra` commands do the deterministic work.

- **Use the `spectra-*` skills** for SPECTRA work: generating tests (`spectra-generate`), updating tests after a
  documentation change (`spectra-update`), acceptance criteria (`spectra-criteria`), coverage, the dashboard, reviewing
  flagged tests, running tests (`spectra-execute`). If unsure, start with `spectra-help` or `spectra-quickstart`.
- **Run `spectra` commands in the shell exactly as a skill says** and read their JSON output. Do not invent commands or
  flags, and do not edit SPECTRA's files (`test-cases/**`, `_index.json`, `docs/criteria/**`, `.spectra/**`) by hand
  unless a skill tells you to.
- **Subagents.** When a skill says to start a subagent (`spectra-analyst`, `spectra-generator`, `spectra-updater`,
  `spectra-repairer`, `spectra-extractor`, `spectra-critic`), delegate to it with your agent's subagent mechanism and pass
  only what the skill lists:

  | Agent | How to start a SPECTRA subagent |
  |---|---|
  | GitHub Copilot | the `agent` tool (`runSubagent`), by the subagent's name |
  | Cursor | delegate by the subagent's name (or `/name`) |
  | Devin | delegate to the custom subagent by its name |
  | Codex CLI | ask for the subagent by its name |
  | Gemini CLI | `@name` |
  | Antigravity CLI | invoke the subagent by its name |
  | Kiro CLI | name the subagent in plain language |
  | OpenCode | `@name` |

- **The critic is part of every flow.** Each test that is generated, edited or repaired is verified by the
  `spectra-critic` subagent against the documentation it cites, as the skills describe. Never write, edit or guess a
  verdict yourself.
