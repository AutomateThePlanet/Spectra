---
title: Live Verification (AI Agents)
parent: User Guide
nav_order: 12
---

# Live verification checklist: SPECTRA in each AI agent

The automated tests check every file `spectra init --agent` writes, byte for byte. They cannot check that an agent
actually loads those files and follows them; that takes a person and a real session. Run this checklist once per agent
after a release, and record the results in the table at the end.

Related: [Skills Integration](skills-integration.md#other-ai-agents) | [CLI Reference](cli-reference.md#spectra-init)

## Setup (once)

1. Install the CLI under test: `dotnet tool update -g Spectra.CLI --version <version>`.
2. Create an empty folder with `git init` and one small document, e.g. `docs/login.md` with three or four testable rules
   (a password length limit, a lockout after N failures, an error message).
3. Have the agent installed and signed in.

## Per agent

### A. Scaffold

- [ ] `spectra init --agent <agent> --dry-run` lists the files from the table in
      [Skills Integration](skills-integration.md#other-ai-agents) and writes nothing (`git status` is clean).
- [ ] `spectra init --agent <agent>` exits 0 and prints the agent's line, its instructions file and its decisions.
- [ ] The instructions file, 16 `spectra-*` skill folders (or the reused folder named in the decisions) and six
      `spectra-*` subagents exist; `.spectra/manifest.<agent>.json` lists them.
- [ ] Running the same command again reports `0 written` and changes nothing (`git status` shows no new change).

### B. Skills and instructions are visible

- [ ] The agent lists the `spectra-*` skills (skills picker, `/skills`, or ask: "Which SPECTRA skills do you have?").
- [ ] Ask: "What do the SPECTRA instructions say about the critic?" The answer mentions `spectra-critic` and that it
      verifies every generated test. This shows the always-on instructions file is loaded.
- [ ] The agent lists the SPECTRA subagents (subagent list, `/agents`, or ask).

### C. One generate run

- [ ] Ask: "Generate test cases for the login documentation." The agent uses the `spectra-generate` skill: it runs
      `spectra ai compile-analysis-prompt`, starts `spectra-analyst`, shows a recommendation and asks before generating.
- [ ] After you confirm, it starts `spectra-generator` for the batch and `spectra ai ingest-tests` writes the tests:
      `spectra list --suite <suite>` shows them.
- [ ] The run made no direct edits to `test-cases/**` or `.spectra/**` other than through `spectra` commands and the
      staging files the skill names.

### D. The critic was invoked

- [ ] `.spectra/verdicts/critic-verdict-<id>.json` exists for every new test.
- [ ] `spectra ai audit-grounding --suite <suite>` shows a verdict (`grounded`, `partial` or `hallucinated`) for every new
      test and no `critic` in `action_needed`.
- [ ] If any test was `partial`, the repair ran (`spectra-repairer`) and the test was verified again.

Where the agent did not start the critic on its own (section D fails), note how it behaved. This is the known
limitation: outside Claude Code, delegation is the agent's choice. Do not fix it by hand-writing verdict files.

## Notes per agent

| Agent | How to see skills | How to see subagents | Delegation to watch for |
|---|---|---|---|
| Claude Code | `/skills` | `/agents` | `spectra-analyst`, `spectra-generator`, `spectra-critic` subagent turns |
| GitHub Copilot | skills list in chat / `copilot` CLI | agent picker (`.github/agents`, or `.claude/agents` when reused) | `runSubagent` / `agent` tool calls |
| Cursor | Agent chat → skills | `.cursor/agents` in settings | subagent (Task) calls by name |
| Devin | skills list | custom subagents list | delegation to the named profile |
| Codex CLI | `/skills` | `.codex/agents` | ask it explicitly to run `spectra-critic` if it does not |
| Gemini CLI | `/skills` | `/agents` | `@spectra-critic` |
| Antigravity CLI | skills panel | `/agents` panel | `invoke_subagent` by the main agent |
| Kiro CLI | `/skills` (or the agent's `resources`) | `/agent list` | subagent named in plain language |
| OpenCode | skills list | `@` completion | `@spectra-critic` |

## Results

| `--agent` | Agent version | A Scaffold | B Visible | C Generate | D Critic | Notes | Date / who |
|---|---|---|---|---|---|---|---|
| `--agent claude` | | | | | | | |
| `--agent copilot` | | | | | | | |
| `--agent cursor` | | | | | | | |
| `--agent devin` | | | | | | | |
| `--agent codex` | | | | | | | |
| `--agent gemini` | | | | | | | |
| `--agent antigravity` | | | | | | | |
| `--agent kiro` | | | | | | | |
| `--agent opencode` | | | | | | | |
