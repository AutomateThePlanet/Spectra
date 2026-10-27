# Feature Specification: `spectra init --agent` — install SPECTRA for the other AI agents (additive only)

**Feature Branch**: `104-multi-agent-init`
**Created**: 2026-10-27
**Status**: In progress
**Investigation**: [`FINDINGS-multi-client-gap-v2.md`](../../FINDINGS-multi-client-gap-v2.md) (provider table, decisions)

## Overview

`spectra init` installs SPECTRA's skills and subagents for Claude Code only. This feature adds `--agent <list>` so the
same skills, subagents and an always-on instructions fragment are also installed for GitHub Copilot, Cursor, Devin
(alias `windsurf`), Codex CLI, Gemini CLI (enterprise), Antigravity CLI (alias `agy`), Kiro CLI and OpenCode — with
the agent names, aliases and auto-detect of BELLATRIX Agent, so both tools can be installed side by side in one repo.

**Golden rule: add, don't change.** `spectra init` without `--agent` installs exactly what it installs today, byte for
byte. No workflow logic (generation, critic, verdicts, repair, review, execution) and no installed Claude content changes.

## User Scenarios & Testing

### User Story 1 — A Copilot / Cursor / … user installs SPECTRA for their agent (P1)

`spectra init --agent cursor` installs the skills, the six subagents and an always-on rule in Cursor's own layout, and
writes no `.claude/` file. The user asks Cursor to generate tests and the skill drives the same `spectra` commands.

**Acceptance**: each agent's emitted files equal its golden file; the skills are readable by that agent (Agent Skills
format, `name` = folder); the subagents use that agent's format and documented tool names; no Claude-only wording or
frontmatter key remains in the emitted content.

### User Story 2 — A team using several agents installs them together (P1)

`spectra init --agent claude,copilot,codex,gemini` installs each agent once; a skill directory that several agents read is
written once (Claude's `.claude/skills` is reused where the agent reads it); the Claude files are byte-identical to a
plain `spectra init`.

**Acceptance**: all-agents coexistence test; per-agent re-run (`--regenerate`) is byte-stable; the Claude golden passes.

### User Story 3 — An existing Claude workspace adds another agent (P2)

In a repo already initialised for Claude, `spectra init --agent copilot` adds Copilot without `--force`, without touching
`spectra.config.json`, the Claude files or `.spectra/skills-manifest.json`.

### User Story 4 — SPECTRA and BELLATRIX Agent in one repo (P2)

`bellatrix init` and `spectra init` for the same agents, in either order, leave each other's files untouched: distinct
marker blocks in `AGENTS.md` / `GEMINI.md`, distinct fragments, unique skill and subagent names.

### Edge cases

- `--agent` omitted → Claude, as today, plus one notice line; `--agent auto` detects (manifests, else exactly one agent's
  markers; none or several → usage error listing the choices).
- Unknown agent name → usage error listing the valid names; `windsurf` → `devin` with a notice; `agy` → `antigravity`.
- A file the user edited since SPECTRA wrote it is skipped and reported, unless `--regenerate`.
- Human-owned `AGENTS.md` / `GEMINI.md`: one marker block only, bytes outside it untouched (R1).
- `--dry-run` with `--agent` writes nothing and lists every planned file, decision and skip.

## Requirements

- **FR-001** `spectra init --agent <list>` accepts `claude, copilot, cursor, devin, codex, gemini, antigravity, kiro,
  opencode`, aliases `windsurf`→`devin`, `agy`→`antigravity`, and `auto`; comma-separated; canonical order; de-duplicated.
- **FR-002** Without `--agent`, init's output is byte-identical to 61ea574 (`ClaudeInstallGoldenTests`).
- **FR-003** Per agent: the native instructions fragment (or one marker block in `AGENTS.md` / `GEMINI.md` pointing at
  `.spectra/spectra.instructions.md`), the 16 skills (+ reference files) in the agent's skills directory, the 6 subagents
  in the agent's native format — see the provider table in the findings.
- **FR-004** Content for other agents is the embedded Claude content passed through a transform at emit time: neutral
  wording, Claude-only frontmatter removed or mapped, provider tool names in subagent frontmatter. Source files unchanged.
- **FR-005** Skill directories are de-duplicated by set cover (`.claude/skills` first when Claude is present).
- **FR-006** `.spectra/manifest.<agent>.json` per non-Claude agent (CRLF-normalised SHA-256); user-edited files skipped
  unless `--regenerate`; files an agent no longer plans are retired only if unedited.
- **FR-007** Project-level command allowlists only where documented (Cursor, Devin, OpenCode), merged by key.
- **FR-008** Namespace contract R1–R8: only `spectra-*` / `spectra.*` / `.spectra/manifest.*` are written or removed.
- **FR-009** The critic keeps today's instructions; where delegation is not platform-enforced the skill text still tells
  the agent to run the critic subagent. No new gate, file or workflow step.

## Success Criteria

- **SC-001** The Claude golden test stays green through the whole feature.
- **SC-002** A golden file per agent; all-agents coexistence and per-agent `--regenerate` byte-stability tests pass.
- **SC-003** Coexistence with BELLATRIX (recorded fixture of `bellatrix init`, both orders) leaves both sides intact.
- **SC-004** Full suite green (baseline 4,073 + new tests).

## Out of Scope

ImageSharp, System.CommandLine migration (the new option uses the current API), Dependabot, `update-skills` for the new
agents, a CLI-enforced critic gate (planned later), MCP registration.
